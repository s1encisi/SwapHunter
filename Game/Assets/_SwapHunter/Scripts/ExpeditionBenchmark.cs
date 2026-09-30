using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Profiling;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace SwapHunter
{
    // Explicit CLI-only benchmark. Camera stations, persistent health and reinforcements are fixtures,
    // not player completion evidence. All frame samples remain in order in the CSV.
    public sealed class ExpeditionBenchmark : MonoBehaviour
    {
        [Serializable] public sealed class Run
        {
            public int chapter, frames, focusedFrames, visibleCombatFrames, enabledCameraFrames, shots, enemyShots, maxLiveEnemies, minLiveEnemies;
            public int width, height, frameCap, vSyncCount;
            public string name, rawFrames;
            public double warmupSeconds=5, requestedSeconds=30, seconds;
            public float averageFps, p50Ms, p95Ms, p99Ms, worstMs, focusFraction, visibleCombatFraction, renderScaleMin=10, renderScaleMax, meanLiveEnemies;
            public long allocatedStart, allocatedEnd, allocatedPeak, reservedPeak, monoPeak; public long workingSetPeak=-1; public bool workingSetAvailable;
            public bool averageAtLeast60, p95AtMost16_67, complete;
            public List<string> stations=new List<string>();
        }
        [Serializable] public sealed class Report
        {
            public string version=Application.version;
            public string kind="Native rendered expedition benchmark: chapters 5, 9, 12; five-second warmup then 30-second sample per chapter; no screenshots during measurement.";
            public string fixture="Isolated profile unlocked through domain Begin/Settle calls (not played completion). Live AI; target six enemies; scripted actual firing/reloading. Camera teleports among three collision-checked stations. Player maximum health temporarily 500 and reserve replenished.";
            public string timing="Raw CSV includes every sampled frame in order. Primary wallFrameMs is realtime elapsed between coroutine samples; unityFrameMs is Time.unscaledDeltaTime. Percentiles use nearest-rank. FPS=frames/summed wall seconds. These are rendered application frame intervals, not GPU-only duration.";
            public string visibility="Player cameras enabled in a native window. isFocused is sampled per frame. Desktop occlusion and physical monitor visibility are not verified.";
            public string performanceMeaning="measurementComplete confirms complete error-free evidence only. It does not mean the 60 FPS performance thresholds passed.";
            public string unity, os, cpu, gpu, graphicsApi, output;
            public int cpuCores, systemMemoryMb, graphicsMemoryMb, requestedWidth=1920, requestedHeight=1080;
            public float requestedRenderScale=.85f;
            public bool dynamicResolution=false, measurementComplete, allRunsAverageAtLeast60, allRunsP95AtMost16_67, restored;
            public List<Run> runs=new List<Run>();
            public List<string> errors=new List<string>();
        }
        struct Frame
        {
            public int index, station, focused, enemies, visible, width, height, camera;
            public double elapsed, wallMs;
            public float unityMs, renderScale;
            public long allocated;
        }
        readonly Report report=new Report();
        GameOptions saved;float savedHealth;bool savedSuppress,savedInput,savedAudioPause;int savedCheckpoint;
        float started;bool finishing,restored;string output;int spawnCursor;
        DemoGame G=>DemoGame.I;
        PlayerMotor P=>G.player;
        void Awake(){started=Time.realtimeSinceStartup;Application.logMessageReceived+=OnLog;}
        void OnLog(string msg,string stack,LogType type)
        {if((type==LogType.Error||type==LogType.Exception||type==LogType.Assert)&&report.errors.Count<100)report.errors.Add(type+": "+msg);}
        void Update()
        {
            if(!finishing&&Time.realtimeSinceStartup-started>180)
            {report.errors.Add("180-second watchdog; partial results retained.");StopAllCoroutines();Finish();}
        }
        IEnumerator Start()
        {
            var args=Environment.GetCommandLineArgs();
            if(Array.IndexOf(args,"-swapHunterExpeditionBenchmark")<0){Destroy(this);yield break;}
            int index=Array.IndexOf(args,"-qaOutput");output=index>=0&&index+1<args.Length?args[index+1]:RunStorage.Root;
            Directory.CreateDirectory(output);report.output=Path.GetFullPath(output);
            if(!G.qaMode||!RunStorage.IsValidation||G.expeditionStore==null||G.expeditionActive)
            {report.errors.Add("Benchmark requires isolated validation startup with no active action.");Finish();yield break;}
            if(SystemInfo.graphicsDeviceType==GraphicsDeviceType.Null)
            {report.errors.Add("Null graphics device cannot provide native rendered frame evidence.");Finish();yield break;}
            saved=G.options.Clone();savedHealth=G.config.playerHealth;savedSuppress=G.qaSuppressAI;savedInput=G.qaInputEnabled;
            savedCheckpoint=RunStorage.Checkpoint;savedAudioPause=AudioListener.pause;
            report.unity=Application.unityVersion;report.os=SystemInfo.operatingSystem;report.cpu=SystemInfo.processorType;report.cpuCores=SystemInfo.processorCount;
            report.gpu=SystemInfo.graphicsDeviceName;report.graphicsApi=SystemInfo.graphicsDeviceType.ToString();report.systemMemoryMb=SystemInfo.systemMemorySize;report.graphicsMemoryMb=SystemInfo.graphicsMemorySize;
            G.options=new GameOptions{resolutionWidth=1920,resolutionHeight=1080,windowMode=0,quality=1,renderScale=.85f,dynamicResolution=false,vSync=false,frameLimit=1,muteWhenUnfocused=false};
            G.SyncLegacyBindings();G.ApplySettings(true);AudioListener.pause=false;G.config.playerHealth=500;
            G.qaInputEnabled=false;G.qaSuppressAI=false;UnityEngine.Random.InitState(540912);
            while(!UnityEngine.Rendering.SplashScreen.isFinished)yield return null;
            yield return new WaitForSecondsRealtime(1);
            // Fixture-only unlock path is expressly not completion, route, or reward-balance validation.
            for(int i=G.expeditionStore.Profile.highestUnlocked;i<11;i++)
            {string run=G.expeditionStore.Begin(i);G.expeditionStore.Settle(run,true,false,1);}
            foreach(int level in new[]{4,8,11})
            {
                G.OpenExpeditionBoard();
                if(!G.BeginExpedition(level)){report.errors.Add("Cannot deploy chapter "+(level+1)+": "+G.expeditionNotice);break;}
                G.qaSuppressAI=false;G.qaInputEnabled=false;
                int[] loadout=level==4?new[]{0,1}:level==8?new[]{8,9}:new[]{2,10};
                P.SetLoadout(loadout[0],loadout[1]);
                var run=new Run{chapter=level+1,name=ExpeditionCatalog.Levels[level].name,minLiveEnemies=int.MaxValue};
                Vector3[] stations=Stations(level);spawnCursor=0;
                if(!Place(stations[0],run,0)){G.FinishExpedition(false);report.runs.Add(run);continue;}
                double warmStart=Time.realtimeSinceStartupAsDouble;float nextReinforcement=0;
                while(Time.realtimeSinceStartupAsDouble-warmStart<5)
                {DriveScene(ref nextReinforcement);yield return null;}
                run.warmupSeconds=Time.realtimeSinceStartupAsDouble-warmStart;
                run.width=Screen.width;run.height=Screen.height;run.vSyncCount=QualitySettings.vSyncCount;run.frameCap=Application.targetFrameRate;
                run.allocatedStart=Profiler.GetTotalAllocatedMemoryLong();
                var frames=new List<Frame>(6000);double began=Time.realtimeSinceStartupAsDouble,last=began;
                int station=0,shots=G.totalShots;float nextMemory=0;long allocated=run.allocatedStart;
                int enemiesFiredBefore=EnemyShots();int retiredEnemyShots=0;var seenShots=new Dictionary<int,int>();
                while(Time.realtimeSinceStartupAsDouble-began<30&&frames.Count<100000)
                {
                    double elapsed=Time.realtimeSinceStartupAsDouble-began;
                    int wanted=Mathf.Min(2,(int)(elapsed/10));
                    if(wanted!=station)
                    {
                        if(!Place(stations[wanted],run,wanted))break;
                        station=wanted;P.SwitchWeapon(loadout[station%2]);
                    }
                    if(!G.IsPlaying){report.errors.Add("Chapter "+run.chapter+" left playing state during sample: "+G.state);break;}
                    DriveScene(ref nextReinforcement);
                    foreach(var enemy in G.enemies)if(enemy)seenShots[enemy.GetInstanceID()]=enemy.shotsFired;
                    if(Time.unscaledTime>=nextMemory)
                    {
                        nextMemory=Time.unscaledTime+1;allocated=Profiler.GetTotalAllocatedMemoryLong();
                        run.allocatedPeak=Math.Max(run.allocatedPeak,allocated);run.reservedPeak=Math.Max(run.reservedPeak,Profiler.GetTotalReservedMemoryLong());run.monoPeak=Math.Max(run.monoPeak,Profiler.GetMonoUsedSizeLong());
                        long working=RuntimeMemory.WorkingSet();if(working>0){run.workingSetAvailable=true;run.workingSetPeak=Math.Max(run.workingSetPeak,working);}
                    }
                    yield return null;
                    double now=Time.realtimeSinceStartupAsDouble;
                    float scale=GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset pipe?pipe.renderScale:1;
                    int live=G.AliveEnemies,visible=VisibleEnemies();
                    frames.Add(new Frame{index=frames.Count,station=station,elapsed=now-began,wallMs=(now-last)*1000,unityMs=Time.unscaledDeltaTime*1000,
                        focused=Application.isFocused?1:0,enemies=live,visible=visible,width=Screen.width,height=Screen.height,
                        camera=P.cameraView.enabled&&P.weaponCamera.enabled?1:0,renderScale=scale,allocated=allocated});
                    last=now;
                }
                foreach(var value in seenShots.Values)retiredEnemyShots+=value;
                run.enemyShots=Math.Max(0,retiredEnemyShots-enemiesFiredBefore);run.shots=G.totalShots-shots;
                Summarize(run,frames);run.allocatedEnd=Profiler.GetTotalAllocatedMemoryLong();
                run.rawFrames="chapter-"+run.chapter.ToString("00")+"-frames.csv";WriteFrames(Path.Combine(output,run.rawFrames),frames);
                report.runs.Add(run);WriteReport();
                Debug.Log("EXPEDITION_BENCHMARK chapter="+run.chapter+" frames="+run.frames+" FPS="+run.averageFps.ToString("F2")+" p95="+run.p95Ms.ToString("F2")+" focus="+run.focusFraction.ToString("P1"));
                G.FinishExpedition(false);yield return null;
            }
            Finish();
        }
        void DriveScene(ref float nextReinforcement)
        {
            P.health=G.config.playerHealth;
            foreach(int id in P.EquippedWeapons)P.reserve[id]=WeaponCatalog.Get(id).reserve;
            if(Time.unscaledTime>=nextReinforcement){EnsureEnemies(6);nextReinforcement=Time.unscaledTime+.5f;}
            EnemyActor target=null;float nearest=float.MaxValue;
            foreach(var enemy in G.enemies)
            {
                if(!enemy||!enemy.Alive||Physics.Linecast(P.Eye.position,enemy.AimPoint,Layers.WorldMask))continue;
                float d=Vector3.Distance(P.Eye.position,enemy.AimPoint);if(d<nearest){nearest=d;target=enemy;}
            }
            Vector3 point=target?target.AimPoint:new Vector3(0,2,52);
            Vector3 direction=(point-P.Eye.position).normalized;
            P.SetLook(Mathf.Atan2(direction.x,direction.z)*Mathf.Rad2Deg,-Mathf.Asin(direction.y)*Mathf.Rad2Deg);
            if(target)P.Fire();
            if(P.ammo[P.weapon]<=0)P.Reload();
        }
        void EnsureEnemies(int desired)
        {
            if(G.expeditionMap==null)return;
            int attempts=0;
            while(G.AliveEnemies<desired&&attempts++<24)
            {
                int cursor=spawnCursor++;var candidates=G.expeditionMap.enemySpawns;
                if(candidates.Count==0)return;
                Vector3 candidate=candidates[cursor%candidates.Count]+new Vector3(((cursor/Mathf.Max(1,candidates.Count))%3-1)*1.3f,0,(cursor%2)*1.3f);
                if(!NavMesh.SamplePosition(candidate,out var nav,.8f,NavMesh.AllAreas)||!Standable(nav.position+Vector3.up*.035f))continue;
                if(Vector3.Distance(nav.position,P.transform.position)<5)continue;
                var kind=cursor%4==0?EnemyKind.Sniper:cursor%4==1?EnemyKind.Shield:EnemyKind.Assault;
                EnemyActor.Spawn(kind,nav.position,G.stage);
            }
        }
        int EnemyShots(){int n=0;foreach(var enemy in G.enemies)if(enemy)n+=enemy.shotsFired;return n;}
        int VisibleEnemies()
        {
            int n=0;foreach(var enemy in G.enemies)
            {
                if(!enemy||!enemy.Alive)continue;Vector3 v=P.cameraView.WorldToViewportPoint(enemy.AimPoint);
                if(v.z>0&&v.x>=0&&v.x<=1&&v.y>=0&&v.y<=1&&!Physics.Linecast(P.Eye.position,enemy.AimPoint,Layers.WorldMask))n++;
            }
            return n;
        }
        bool Place(Vector3 wanted,Run run,int station)
        {
            foreach(var offset in new[]{Vector3.zero,Vector3.right*.8f,Vector3.left*.8f,Vector3.forward*.8f,Vector3.back*.8f})
            {
                Vector3 point=wanted+offset;
                if(!NavMesh.SamplePosition(point,out var nav,.5f,NavMesh.AllAreas)||Mathf.Abs(nav.position.y-wanted.y)>.35f)continue;
                point=nav.position+Vector3.up*.035f;
                if(!Standable(point))continue;
                P.SetFeet(point);P.verticalSpeed=0;run.stations.Add(station+": "+point.ToString("F3"));return true;
            }
            report.errors.Add("No safe camera station for chapter "+run.chapter+", station "+station+", requested "+wanted);return false;
        }
        bool Standable(Vector3 feet)
        {
            if(!Physics.Raycast(feet+Vector3.up*.18f,Vector3.down,.4f,Layers.WorldMask))return false;
            foreach(var collider in Physics.OverlapCapsule(feet+Vector3.up*.37f,feet+Vector3.up*1.46f,.30f,Layers.CombatMask,QueryTriggerInteraction.Ignore))
                if(collider.transform!=P.transform&&!collider.transform.IsChildOf(P.transform))return false;
            return true;
        }
        static Vector3[] Stations(int level)
        {
            if(level==4)return new[]{new Vector3(0,.04f,32),new Vector3(-9,4.44f,47),new Vector3(9,3.34f,60)};
            if(level==8)return new[]{new Vector3(3,.04f,32),new Vector3(2,4.44f,58),new Vector3(9,6.64f,65)};
            return new[]{new Vector3(-5,.04f,32),new Vector3(-9,3.34f,45),new Vector3(9,4.44f,57)};
        }
        static void Summarize(Run run,List<Frame> samples)
        {
            run.frames=samples.Count;if(samples.Count==0){run.complete=false;return;}
            var ordered=new double[samples.Count];double sum=0,enemySum=0;
            for(int i=0;i<samples.Count;i++)
            {
                var f=samples[i];ordered[i]=f.wallMs;sum+=f.wallMs;enemySum+=f.enemies;run.focusedFrames+=f.focused;
                if(f.visible>0)run.visibleCombatFrames++;run.enabledCameraFrames+=f.camera;
                run.minLiveEnemies=Math.Min(run.minLiveEnemies,f.enemies);run.maxLiveEnemies=Math.Max(run.maxLiveEnemies,f.enemies);
                run.renderScaleMin=Mathf.Min(run.renderScaleMin,f.renderScale);run.renderScaleMax=Mathf.Max(run.renderScaleMax,f.renderScale);
            }
            Array.Sort(ordered);run.seconds=sum/1000;run.averageFps=(float)(samples.Count/run.seconds);
            run.p50Ms=(float)ordered[Math.Max(0,(int)Math.Ceiling(samples.Count*.50)-1)];
            run.p95Ms=(float)ordered[Math.Max(0,(int)Math.Ceiling(samples.Count*.95)-1)];
            run.p99Ms=(float)ordered[Math.Max(0,(int)Math.Ceiling(samples.Count*.99)-1)];run.worstMs=(float)ordered[ordered.Length-1];
            run.focusFraction=run.focusedFrames/(float)samples.Count;run.visibleCombatFraction=run.visibleCombatFrames/(float)samples.Count;run.meanLiveEnemies=(float)(enemySum/samples.Count);
            run.averageAtLeast60=run.averageFps>=60;run.p95AtMost16_67=run.p95Ms<=1000f/60;
            run.complete=run.seconds>=29.5&&run.frames>0&&run.shots>0&&run.meanLiveEnemies>=3&&run.enabledCameraFrames==run.frames;
        }
        static void WriteFrames(string path,List<Frame> frames)
        {
            var culture=CultureInfo.InvariantCulture;
            using(var writer=new StreamWriter(path,false,new UTF8Encoding(false)))
            {
                writer.WriteLine("frame,elapsedSeconds,wallFrameMs,unityFrameMs,station,focused,aliveEnemies,visibleEnemies,width,height,renderScale,cameraEnabled,allocatedBytes");
                foreach(var f in frames)writer.WriteLine(f.index+","+f.elapsed.ToString("F6",culture)+","+f.wallMs.ToString("F6",culture)+","+f.unityMs.ToString("F6",culture)+","+f.station+","+f.focused+","+f.enemies+","+f.visible+","+f.width+","+f.height+","+f.renderScale.ToString("F4",culture)+","+f.camera+","+f.allocated);
            }
        }
        void Restore()
        {
            if(restored||saved==null||!G)return;restored=true;G.options=saved;G.config.playerHealth=savedHealth;
            G.qaSuppressAI=savedSuppress;G.qaInputEnabled=savedInput;G.SyncLegacyBindings();G.ApplySettings(false);
            AudioListener.pause=savedAudioPause;RunStorage.Checkpoint=savedCheckpoint;report.restored=true;
        }
        void WriteReport(){if(!string.IsNullOrEmpty(output))File.WriteAllText(Path.Combine(output,"expedition-benchmark.json"),JsonUtility.ToJson(report,true));}
        void Finish()
        {
            if(finishing)return;finishing=true;Restore();
            report.measurementComplete=report.runs.Count==3&&report.runs.TrueForAll(r=>r.complete)&&report.errors.Count==0&&report.restored;
            report.allRunsAverageAtLeast60=report.runs.Count==3&&report.runs.TrueForAll(r=>r.averageAtLeast60);
            report.allRunsP95AtMost16_67=report.runs.Count==3&&report.runs.TrueForAll(r=>r.p95AtMost16_67);
            WriteReport();Debug.Log("EXPEDITION_BENCHMARK_RESULT="+report.measurementComplete+"; output="+output);Application.Quit(report.measurementComplete?0:1);
        }
        void OnApplicationQuit(){Restore();}
        void OnDestroy(){Restore();Application.logMessageReceived-=OnLog;}
    }
}
