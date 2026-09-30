using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
namespace SwapHunter
{
 public sealed class BossPerformanceQA:MonoBehaviour
 {
  [Serializable] public class Report{public string version,kind="Chapter 12 sustained phase-three radial projectile stress; explicit boss phase, stationary camera and restored player health are performance fixtures, not normal play or video footage",gpu;public int width,height,frames,peakProjectiles,casts;public float seconds,averageFps,p50Ms,p95Ms,p99Ms,worstMs,focusFraction,renderScale;public long peakAllocated;public long peakWorkingSet=-1;public bool workingSetAvailable;public bool passed;public List<string> errors=new List<string>();}
  Report report=new Report();string output;bool done;float began;DemoGame G=>DemoGame.I;
  void Awake(){began=Time.realtimeSinceStartup;Application.logMessageReceived+=Error;}
  void Error(string m,string s,LogType t){if(t==LogType.Error||t==LogType.Exception||t==LogType.Assert)report.errors.Add(m);}
  void Update(){if(!done&&Time.realtimeSinceStartup-began>90){report.errors.Add("90s watchdog");Finish();}}
  IEnumerator Start()
  {
   var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"-qaOutput");output=i>=0?args[i+1]:RunStorage.Root;Directory.CreateDirectory(output);while(!UnityEngine.Rendering.SplashScreen.isFinished)yield return null;
   for(i=0;i<11;i++){string run=G.expeditionStore.Begin(i);G.expeditionStore.Settle(run,true,false,30);}G.BeginExpedition(11);G.qaSuppressAI=true;yield return new WaitForSeconds(3.5f);G.ClearDeployments();foreach(var e in G.enemies)if(e)e.suppressAI=true;
   var boss=EnemyActor.Spawn(EnemyKind.Elite,new Vector3(0,.03f,55),0);boss.Damage(721);G.player.SetFeet(new Vector3(0,.03f,47));G.player.SetLook(0,0);G.qaSuppressAI=false;
   QualitySettings.vSyncCount=0;Application.targetFrameRate=120;var pipeline=GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;if(pipeline)pipeline.renderScale=.85f;
   report.version=Application.version;report.gpu=SystemInfo.graphicsDeviceName;report.width=Screen.width;report.height=Screen.height;report.renderScale=pipeline?pipeline.renderScale:1;
   double warm=Time.realtimeSinceStartupAsDouble;
   while(Time.realtimeSinceStartupAsDouble-warm<5){G.player.health=100;if(boss.boss.SkillStep!=BossSkillStep.Execute&&boss.boss.SkillStep!=BossSkillStep.Telegraph)boss.boss.TryBeginSkill(BossSkill.OmniFire);yield return null;}
   var values=new List<double>();int focus=0;double start=Time.realtimeSinceStartupAsDouble,previous=start;float nextSample=0;int projectiles=0;
   using(var writer=new StreamWriter(Path.Combine(output,"boss-frames.csv"))){writer.WriteLine("frame,elapsedSeconds,wallFrameMs,focused,projectiles,skillStep");
   while(Time.realtimeSinceStartupAsDouble-start<30)
   {
    G.player.health=100;if(boss.boss.SkillStep!=BossSkillStep.Execute&&boss.boss.SkillStep!=BossSkillStep.Telegraph)boss.boss.TryBeginSkill(BossSkill.OmniFire);
    if(Time.unscaledTime>=nextSample){nextSample=Time.unscaledTime+.5f;projectiles=FindObjectsByType<Bolt>(FindObjectsSortMode.None).Length;report.peakProjectiles=Math.Max(report.peakProjectiles,projectiles);report.peakAllocated=Math.Max(report.peakAllocated,Profiler.GetTotalAllocatedMemoryLong());long working=RuntimeMemory.WorkingSet();if(working>0){report.workingSetAvailable=true;report.peakWorkingSet=Math.Max(report.peakWorkingSet,working);}}
    yield return null;double now=Time.realtimeSinceStartupAsDouble,ms=(now-previous)*1000;previous=now;values.Add(ms);if(Application.isFocused)focus++;
    writer.WriteLine((values.Count-1)+","+(now-start).ToString("F6",CultureInfo.InvariantCulture)+","+ms.ToString("F6",CultureInfo.InvariantCulture)+","+(Application.isFocused?1:0)+","+projectiles+","+boss.boss.SkillStep);
   }}
   report.seconds=(float)(previous-start);report.frames=values.Count;report.averageFps=values.Count/report.seconds;report.focusFraction=focus/(float)values.Count;values.Sort();report.p50Ms=(float)values[(int)(values.Count*.5)];report.p95Ms=(float)values[(int)(values.Count*.95)];report.p99Ms=(float)values[(int)(values.Count*.99)];report.worstMs=(float)values[values.Count-1];report.casts=boss.boss.SpatialCasts;
   report.passed=report.errors.Count==0&&report.seconds>=29.5f&&report.casts>=6&&report.peakProjectiles>40&&report.focusFraction>.95f&&report.averageFps>=60&&report.p95Ms<=16.67f;Finish();
  }
  void Finish(){if(done)return;done=true;if(string.IsNullOrEmpty(output))return;File.WriteAllText(Path.Combine(output,"boss-performance.json"),JsonUtility.ToJson(report,true));Application.Quit(report.passed?0:1);}
  void OnDestroy(){Application.logMessageReceived-=Error;}
 }
}
