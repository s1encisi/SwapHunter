using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
namespace SwapHunter
{
 // Short native workload observations, not full-campaign profiling or a human playthrough.
 public static class MapV2PerformanceSample
 {
  [Serializable]sealed class Report
  {public string utc,unity,operatingSystem,cpu,gpu,graphicsApi;public int cpuCores,systemMemoryMB,gpuMemoryMB;public string scope="Native chapter combat, unchanged mission budgets and concurrency, normal AI, TimeScale 1. High player HP prevents early termination. Chapter 12 preparation may auto-aim and use real Fire/Reload for at most 60 seconds; no forced deaths or granted victory. Frame samples exclude preparation and screenshot encoding.";public List<Sample> samples=new List<Sample>();}
  [Serializable]sealed class Sample
  {
   public int chapter,plannedBudget,liveLimit,escortLimit,width,height,qualityIndex,gameQuality,vSyncCount,targetFrameRate,frameCount,aliveMin=int.MaxValue,aliveMax,localMin=int.MaxValue,localMax,visibleMin=int.MaxValue,visibleMax,shots,playerPreparationShots;
   public string scene,preparation,screenshot,qualityName,preparationEndReason;public bool preparationComplete,completed,finite,targetMet,aiSuppressed;public float preparationSeconds,sampleSeconds,renderScale,timeScale,p50Ms,p95Ms,p99Ms,warmupSeconds=2,targetP95Ms=16.7f,targetP99Ms=25,miniHealthAtFirstShot=-1,miniHealthAtPreparationEnd=-1;public Vector3 standingPoint,lookAt;public float[] frameMs;public int deployedAtEnd,cancelledAtEnd,untriggeredAtEnd;public List<PreparationFrame> preparationFrames=new List<PreparationFrame>();
  }
  [Serializable]sealed class PreparationFrame
  {public float seconds,miniHealth,feetAboveFloor,opticalAimErrorDegrees;public int shots,ammo,reserve;public bool floorFound;public Vector3 miniFeet,miniAim,headAim,playerFeet;public Vector2 mechanicalRecoil;public string firstRayCollider;}
  static DemoGame G=>DemoGame.I;static PlayerMotor P=>G.player;
  static bool Feet(Vector3 wanted,out Vector3 feet)
  {
   feet=wanted;if(!NavMesh.SamplePosition(wanted,out var hit,2.5f,NavMesh.AllAreas)||Mathf.Abs(hit.position.y-wanted.y)>2.5f)return false;
   if(!EnemyMotion.ClearVolume(hit.position,.32f,1.8f,P.transform))return false;feet=hit.position+Vector3.up*.035f;return true;
  }
  static bool PlaceRegion(MissionLayout layout,EncounterGroup group)
  {
   if(group==null||!layout.TryRegion(group.region,out var bounds))return false;Vector3 spawn=layout.Spawn(group.id,0).position;var candidates=new List<Vector3>();
   // A compact region may be centred on the actual spawn. Standing on that
   // point correctly defers the live deployment under FairDeployment's 7 m
   // rule; the observer must enter from a legal adjacent position instead.
   foreach(var offset in new[]{Vector3.back*10,Vector3.left*10,Vector3.right*10,Vector3.forward*10,Vector3.back*12})candidates.Add(spawn+offset);
   candidates.Add(new Vector3(bounds.center.x,spawn.y,bounds.center.z));
   foreach(var p in candidates)if(Feet(p,out var feet)&&bounds.Contains(feet+Vector3.up*.2f)&&Vector3.Distance(feet,spawn)>=8){P.SetFeet(feet);P.verticalSpeed=0;Look(spawn+Vector3.up);Physics.SyncTransforms();return true;}return false;
  }
  static EncounterGroup Group(MissionDefinition definition,string id){foreach(var g in definition.groups)if(g.id==id)return g;return null;}
  static EnemyActor Member(string group){foreach(var m in G.mission.Members)if(m.group.id==group&&m.actor&&m.actor.Alive)return m.actor;return null;}
  static void Look(Vector3 point)
  {
   Vector3 d=point-P.Eye.position;float yaw=Mathf.Atan2(d.x,d.z)*Mathf.Rad2Deg,pitch=-Mathf.Atan2(d.y,new Vector2(d.x,d.z).magnitude)*Mathf.Rad2Deg;
   // SetLook takes the player's base input, while the optical ray includes real
   // mechanical recoil. Apply equivalent counter-input; never clear recoil or change Fire.
   Vector2 recoil=P.MechanicalRecoil;P.SetLook(yaw-recoil.y,pitch+recoil.x);
  }
  static void PreparationSnapshot(Sample sample,EnemyActor mini,float started)
  {
   Vector3 aim=mini.HeadHitbox?mini.HeadHitbox.bounds.center:mini.AimPoint;
   var frame=new PreparationFrame{seconds=Time.realtimeSinceStartup-started,miniHealth=mini.health,miniFeet=mini.transform.position,miniAim=mini.AimPoint,headAim=aim,playerFeet=P.transform.position,mechanicalRecoil=P.MechanicalRecoil,shots=P.shots,ammo=P.ammo[P.weapon],reserve=P.reserve[P.weapon],opticalAimErrorDegrees=Vector3.Angle(P.Eye.forward,aim-P.Eye.position)};
   frame.floorFound=Physics.Raycast(mini.transform.position+Vector3.up*3,Vector3.down,out var ground,10,Layers.WorldMask,QueryTriggerInteraction.Ignore);if(frame.floorFound)frame.feetAboveFloor=mini.transform.position.y-ground.point.y;
   if(EnemyActor.CastWeaponRay(P.Eye.position,P.Eye.forward,100,P.transform,out var first))frame.firstRayCollider=first.collider.name;
   sample.preparationFrames.Add(frame);
  }
  static void KeepAlive(){P.health=100000;if(G.StoryModal)G.CloseStoryModal();}
  static IEnumerator WaitPlaying(float seconds){float until=Time.realtimeSinceStartup+seconds;while(Time.realtimeSinceStartup<until){KeepAlive();yield return null;}}
  static bool ChooseCombatView()
  {
   var candidates=new List<Vector3>{P.transform.position};foreach(var m in G.mission.Members)if(m.actor&&m.actor.Alive)foreach(var offset in new[]{Vector3.back*10,Vector3.left*10,Vector3.right*10})candidates.Add(m.actor.transform.position+offset);
   int best=-1;Vector3 choice=P.transform.position,target=P.Eye.position+P.Eye.forward*10;
   foreach(var p in candidates)
   {
    if(!Feet(p,out var feet))continue;int score=0;Vector3 sum=Vector3.zero;
    foreach(var m in G.mission.Members)if(m.actor&&m.actor.Alive)
    {var e=m.actor;Vector3 d=feet+Vector3.up*.9f-e.AimPoint;if(d.sqrMagnitude<40*40&&!Physics.Linecast(feet+Vector3.up*1.62f,e.AimPoint,Layers.WorldMask)){score+=Vector3.Dot(e.transform.forward,d.normalized)>.5f?3:1;sum+=e.AimPoint;}}
    if(score>best&&score>0){best=score;choice=feet;int visible=0;foreach(var m in G.mission.Members)if(m.actor&&m.actor.Alive&&(m.actor.AimPoint-(feet+Vector3.up*1.62f)).sqrMagnitude<40*40&&!Physics.Linecast(feet+Vector3.up*1.62f,m.actor.AimPoint,Layers.WorldMask))visible++;target=sum/Mathf.Max(1,visible);}
   }
   if(best<=0)return false;P.SetFeet(choice);P.verticalSpeed=0;Look(target);Physics.SyncTransforms();return true;
  }
  public static IEnumerator Run(Action<string,bool,string> add,string output,int[] selectedLevels=null)
  {
   if(!RunStorage.IsIsolated){add("performance_isolated",false,"Refusing formal save");yield break;}Directory.CreateDirectory(output);
   var report=new Report{utc=DateTime.UtcNow.ToString("O"),unity=Application.unityVersion,operatingSystem=SystemInfo.operatingSystem,cpu=SystemInfo.processorType,cpuCores=SystemInfo.processorCount,gpu=SystemInfo.graphicsDeviceName,graphicsApi=SystemInfo.graphicsDeviceType.ToString(),systemMemoryMB=SystemInfo.systemMemorySize,gpuMemoryMB=SystemInfo.graphicsMemorySize};
   bool oldAI=G.qaSuppressAI,oldInput=G.qaInputEnabled;float oldScale=Time.timeScale;var originalStore=G.expeditionStore;
   try
   {
    Time.timeScale=1;G.qaSuppressAI=false;G.qaInputEnabled=false;P.enabled=true;P.SetViewActive(true);
    foreach(int level in selectedLevels??new[]{4,11})
    {
     if(G.StoryModal)G.CloseStoryModal();if(G.expeditionActive&&G.expeditionReceipt==null)G.AbandonExpedition();G.OpenExpeditionBoard();
     string storage=Path.Combine(RunStorage.Root,"mapv2-perf-"+level);Directory.CreateDirectory(storage);File.WriteAllText(Path.Combine(storage,"expedition-v6.json"),JsonUtility.ToJson(TacticalDemoProfile.Create(0,true)));G.expeditionStore=new ExpeditionStore(storage,true);
     var sample=new Sample{chapter=level+1,preparation="Legal NavMesh positions selected from actual region and member bindings, at least 8 m away from the primary pending spawn; no budget edits."};report.samples.Add(sample);
     bool began=G.BeginExpedition(level);if(!began){sample.preparation="Deployment failed: "+G.expeditionNotice;add("performance_ch"+(level+1)+"_sample",false,sample.preparation);continue;}
     KeepAlive();yield return null;var layout=G.expeditionMap.root.GetComponent<MissionLayout>();var definition=G.mission.Definition;sample.plannedBudget=definition.TotalBudget;sample.liveLimit=definition.liveLimit;sample.escortLimit=definition.maximumEscorts;
     float prepareStart=Time.realtimeSinceStartup;int preparationShots=P.shots;bool finalReady=false;
     if(level==4)
     {
      bool left=PlaceRegion(layout,Group(definition,"left"));yield return WaitPlaying(3.4f);bool right=PlaceRegion(layout,Group(definition,"right"));yield return WaitPlaying(3.4f);sample.preparationComplete=left&&right;sample.scene="第5章双塔主战区，真实分区激活";sample.preparation+=" Visited left/right by valid feet placements; did not complete objectives.";
     }
     else
     {
      bool entered=PlaceRegion(layout,Group(definition,"mini-boss"));float deadline=prepareStart+60;EnemyActor mini=null;float repositionAt=0,diagnosticAt=0;sample.preparationEndReason=entered?"60-second preparation limit":"No legal entry feet";
      while(entered&&Time.realtimeSinceStartup<deadline)
      {
       KeepAlive();mini=Member("mini-boss");
       bool defeated=false;foreach(var m in G.mission.Members)if(m.group.id=="mini-boss")defeated=m.Defeated;
       if(defeated){sample.preparationEndReason="MiniBoss defeated by real Fire path";sample.miniHealthAtPreparationEnd=0;break;}
       if(mini)
       {
        if(Time.realtimeSinceStartup>=repositionAt){repositionAt=Time.realtimeSinceStartup+3;Vector3 proposed=mini.transform.position-mini.transform.forward*7+mini.transform.right*3;if(Feet(proposed,out var feet)&&!Physics.Linecast(feet+Vector3.up*1.62f,mini.AimPoint,Layers.WorldMask)){P.SetFeet(feet);P.verticalSpeed=0;Physics.SyncTransforms();}}
        Look(mini.HeadHitbox?mini.HeadHitbox.bounds.center:mini.AimPoint);
        if(sample.miniHealthAtFirstShot<0)sample.miniHealthAtFirstShot=mini.health;sample.miniHealthAtPreparationEnd=mini.health;
        if(Time.realtimeSinceStartup>=diagnosticAt){diagnosticAt=Time.realtimeSinceStartup+1;PreparationSnapshot(sample,mini,prepareStart);}
        if(P.ammo[P.weapon]<=0&&P.reserve[P.weapon]<=0){sample.preparationEndReason="Normal ammunition exhausted; preparation stopped without forced death";break;}
        if(P.ammo[P.weapon]<=0)P.Reload();else P.Fire();
       }
       yield return null;
      }
      bool miniDefeated=false;foreach(var m in G.mission.Members)if(m.group.id=="mini-boss")miniDefeated=m.Defeated;
      if(miniDefeated&&Time.realtimeSinceStartup<deadline)
      {
       while(layout.NavigationUpdating&&Time.realtimeSinceStartup<deadline){KeepAlive();yield return null;}
       if(PlaceRegion(layout,Group(definition,"boss")))while(Time.realtimeSinceStartup<deadline){KeepAlive();if(Member("boss")&&G.mission.ActiveEscorts>0){finalReady=true;break;}yield return null;}
      }
      sample.preparationComplete=finalReady;sample.scene=finalReady?"第12章原最终Boss主战区，实际门卫击败后触发":"第12章前置交战，60秒准备内未进入终战";
      sample.preparation+=" Preparation only: auto-aim with counter-input for actual mechanical recoil, real PlayerMotor.Fire/Reload and collision-checked reposition; normal ammunition; player HP 100000. MiniBossDefeated="+miniDefeated+";finalReady="+finalReady+";no direct enemy health writes.";
     }
     sample.preparationSeconds=Time.realtimeSinceStartup-prepareStart;sample.playerPreparationShots=P.shots-preparationShots;
     ChooseCombatView();sample.standingPoint=P.transform.position;sample.lookAt=P.Eye.position+P.Eye.forward*20;
     yield return WaitPlaying(2);
     sample.gameQuality=G.quality;
     sample.width=Screen.width;sample.height=Screen.height;sample.qualityIndex=QualitySettings.GetQualityLevel();sample.qualityName=QualitySettings.names.Length>sample.qualityIndex?QualitySettings.names[sample.qualityIndex]:"unknown";sample.vSyncCount=QualitySettings.vSyncCount;sample.targetFrameRate=Application.targetFrameRate;var pipeline=GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;sample.renderScale=pipeline?pipeline.renderScale:1;sample.timeScale=Time.timeScale;sample.aiSuppressed=G.qaSuppressAI;
     var frames=new List<float>();var previousShots=new Dictionary<int,int>();foreach(var e in G.enemies)if(e)previousShots[e.GetInstanceID()]=e.shotsFired;
     float start=Time.realtimeSinceStartup,observeAt=start;
     while(Time.realtimeSinceStartup-start<10&&G.IsPlaying)
     {
      KeepAlive();float ms=Time.unscaledDeltaTime*1000;if(ms>0&&!float.IsNaN(ms)&&!float.IsInfinity(ms))frames.Add(ms);
      if(Time.realtimeSinceStartup>=observeAt)
      {
       observeAt=Time.realtimeSinceStartup+.2f;int alive=0,local=0,visible=0;var planes=GeometryUtility.CalculateFrustumPlanes(P.cameraView);
       foreach(var e in G.enemies)if(e&&e.Alive)
       {alive++;if(Vector3.Distance(e.transform.position,P.transform.position)<=80)local++;Bounds b=new Bounds(e.transform.position+Vector3.up*e.Height*.5f,new Vector3(e.Radius*2,e.Height,e.Radius*2));if(GeometryUtility.TestPlanesAABB(planes,b)&&!Physics.Linecast(P.Eye.position,e.AimPoint,Layers.WorldMask))visible++;int id=e.GetInstanceID();int before=previousShots.TryGetValue(id,out var value)?value:0;sample.shots+=Mathf.Max(0,e.shotsFired-before);previousShots[id]=e.shotsFired;}
       sample.aliveMin=Mathf.Min(sample.aliveMin,alive);sample.aliveMax=Mathf.Max(sample.aliveMax,alive);sample.localMin=Mathf.Min(sample.localMin,local);sample.localMax=Mathf.Max(sample.localMax,local);sample.visibleMin=Mathf.Min(sample.visibleMin,visible);sample.visibleMax=Mathf.Max(sample.visibleMax,visible);
      }
      yield return null;
     }
     sample.sampleSeconds=Time.realtimeSinceStartup-start;sample.frameCount=frames.Count;sample.frameMs=frames.ToArray();frames.Sort();sample.p50Ms=Percentile(frames,.5f);sample.p95Ms=Percentile(frames,.95f);sample.p99Ms=Percentile(frames,.99f);sample.finite=frames.Count>0&&!float.IsNaN(sample.p99Ms)&&!float.IsInfinity(sample.p99Ms);sample.targetMet=sample.finite&&sample.p95Ms<=sample.targetP95Ms&&sample.p99Ms<=sample.targetP99Ms;sample.completed=sample.sampleSeconds>=10&&sample.finite&&sample.aliveMax>0&&!sample.aiSuppressed&&sample.timeScale==1;sample.deployedAtEnd=G.mission.DeployedBudget;sample.cancelledAtEnd=G.mission.CancelledBudget;sample.untriggeredAtEnd=G.mission.UntriggeredBudget;
     sample.screenshot="performance-chapter-"+(level+1).ToString("00")+".png";yield return new WaitForEndOfFrame();Capture(P.cameraView,Path.Combine(output,sample.screenshot),sample.width,sample.height);
     add("performance_ch"+(level+1)+"_sample",sample.completed,"scene="+sample.scene+";frames="+sample.frameCount+";seconds="+sample.sampleSeconds.ToString("F2")+";alive="+sample.aliveMin+"–"+sample.aliveMax+";visible="+sample.visibleMin+"–"+sample.visibleMax+";shots="+sample.shots+";P50/P95/P99="+sample.p50Ms.ToString("F2")+"/"+sample.p95Ms.ToString("F2")+"/"+sample.p99Ms.ToString("F2")+"ms;targetMet="+sample.targetMet+";target is reported separately from collection success");
    }
   }
   finally{File.WriteAllText(Path.Combine(output,"performance-map-v2.json"),JsonUtility.ToJson(report,true));if(G.expeditionActive&&G.expeditionReceipt==null)G.AbandonExpedition();G.OpenExpeditionBoard();G.expeditionStore=originalStore;Time.timeScale=oldScale;G.qaSuppressAI=oldAI;G.qaInputEnabled=oldInput;}
  }
  static float Percentile(List<float> sorted,float fraction){if(sorted.Count==0)return 0;float index=(sorted.Count-1)*fraction;int low=Mathf.FloorToInt(index),high=Mathf.CeilToInt(index);return Mathf.Lerp(sorted[low],sorted[high],index-low);}
  static void Capture(Camera camera,string path,int width,int height)
  {var target=new RenderTexture(Mathf.Max(1,width),Mathf.Max(1,height),24,RenderTextureFormat.ARGB32);Texture2D pixels=null;var previous=RenderTexture.active;try{target.Create();RenderPipeline.SubmitRenderRequest(camera,new RenderPipeline.StandardRequest{destination=target});RenderTexture.active=target;pixels=new Texture2D(target.width,target.height,TextureFormat.RGBA32,false);pixels.ReadPixels(new Rect(0,0,target.width,target.height),0,0);pixels.Apply();File.WriteAllBytes(path,pixels.EncodeToPNG());}finally{RenderTexture.active=previous;target.Release();UnityEngine.Object.Destroy(target);if(pixels)UnityEngine.Object.Destroy(pixels);}}
 }
}
