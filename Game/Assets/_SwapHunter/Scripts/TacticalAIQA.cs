using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Unity.AI.Navigation;
namespace SwapHunter
{
 public sealed class TacticalAIQA:MonoBehaviour
 {
  [Serializable] public class Check{public string name,detail;public bool passed;}
  [Serializable] public class Report{public string kind="Native cover cycle, melee pursuit, standoff retreat, swap reselection, facing-gated fire, melee presentation and collision sampling in explicitly positioned nav/physics fixtures; not human playtesting";public bool passed;public List<Check> checks=new List<Check>();}
  Report report=new Report();string output;float started;bool done;Transform fixture;EnemyActor enemy;
  DemoGame G=>DemoGame.I;PlayerMotor P=>G.player;
  void Awake(){started=Time.realtimeSinceStartup;Application.logMessageReceived+=Error;}
  void Error(string m,string s,LogType t){if(t==LogType.Error||t==LogType.Exception||t==LogType.Assert)Add("runtime_error",false,m);}
  void Add(string n,bool ok,string d){report.checks.Add(new Check{name=n,passed=ok,detail=d});Debug.Log("TACTICAL_AI_QA "+n+" "+ok+" "+d);}
  void Update(){if(!done&&Time.realtimeSinceStartup-started>130){Add("watchdog",false,"130s");Finish();}}
  GameObject Box(string n,Vector3 p,Vector3 size)=>Shapes.Box(n,fixture,p,size,1,true,Layers.World);
  bool Overlap(EnemyActor e)=>!EnemyMotion.ClearVolume(e.transform.position,e.Radius-.03f,e.Height,e.transform);
  float Facing(EnemyActor e)=>Vector3.Dot(e.transform.forward,Vector3.ProjectOnPlane(P.Eye.position-e.AimPoint,Vector3.up).normalized);
  IEnumerator Observe(float seconds,Action sample){float end=Time.time+seconds;while(Time.time<end){yield return null;P.health=100;sample();}}
  IEnumerator Start()
  {
   var a=Environment.GetCommandLineArgs();int i=Array.IndexOf(a,"-qaOutput");output=i>=0?a[i+1]:RunStorage.Root;Directory.CreateDirectory(output);
   while(!UnityEngine.Rendering.SplashScreen.isFinished)yield return null;
   G.StartRun(false);G.qaSuppressAI=true;yield return new WaitForSeconds(.3f);foreach(var e in G.enemies)e.gameObject.SetActive(false);
   fixture=new GameObject("Tactical AI real collision fixture").transform;Box("Floor",new Vector3(103,-.5f,0),new Vector3(38,1,38));
   var cover=Box("Cover A",new Vector3(100,.8f,0),new Vector3(5,1.6f,1.8f));cover.AddComponent<CoverSite>();
   var other=Box("Cover B",new Vector3(109,.9f,3),new Vector3(4,1.8f,2));other.AddComponent<CoverSite>();
   Box("Offset solid wall",new Vector3(94,1.5f,4),new Vector3(1,3,7));
   var surface=fixture.gameObject.AddComponent<NavMeshSurface>();surface.collectObjects=CollectObjects.Children;surface.layerMask=Layers.WorldMask;surface.useGeometry=UnityEngine.AI.NavMeshCollectGeometry.PhysicsColliders;surface.BuildNavMesh();
   P.ResetAt(new Vector3(100,.03f,-10));P.SetLook(0,0);enemy=EnemyActor.Spawn(EnemyKind.Assault,new Vector3(100,.03f,6),0);int overlaps=0;float nearest=100;G.qaSuppressAI=false;
   yield return Observe(14,()=>{if(Overlap(enemy))overlaps++;nearest=Mathf.Min(nearest,Vector3.Distance(P.transform.position,enemy.transform.position));});
   G.qaSuppressAI=true;int states=enemy.tactical.StatesVisited;
   int expected=(1<<(int)TacticalState.MoveToCover)|(1<<(int)TacticalState.Hide)|(1<<(int)TacticalState.Peek)|(1<<(int)TacticalState.Fire);
   Add("rifle_completes_real_cover_cycle",(states&expected)==expected&&enemy.shotsFired>0,"states="+states+";shots="+enemy.shotsFired+";feet="+enemy.transform.position);
   Add("cover_motion_no_wall_overlap",overlaps==0,"overlapFrames="+overlaps);Add("rifle_keeps_standoff",nearest>6,"minimumDistance="+nearest);
   Add("selected_hide_occluded_peek_clear",enemy.tactical.HasCover&&Physics.Linecast(enemy.tactical.HidePoint+Vector3.up*1.12f,P.Eye.position,Layers.WorldMask)&&!Physics.Linecast(enemy.tactical.PeekPoint+Vector3.up*1.12f,P.Eye.position,Layers.WorldMask),"hide="+enemy.tactical.HidePoint+";peek="+enemy.tactical.PeekPoint);
   yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(output,"rifle-cover-cycle.png"));yield return null;
   int selections=enemy.tactical.CoverSelections;P.SetFeet(new Vector3(112,.03f,-10));G.totalSwaps++;G.qaSuppressAI=false;yield return Observe(1,()=>{});G.qaSuppressAI=true;
   Add("player_swap_invalidates_cover_choice",enemy.tactical.CoverSelections>selections,"before="+selections+";after="+enemy.tactical.CoverSelections);
   cover.SetActive(false);other.SetActive(false);Physics.SyncTransforms();enemy.MoveTo(new Vector3(104,.03f,5));P.SetFeet(new Vector3(104,.03f,2));enemy.tactical.Invalidate();G.qaSuppressAI=false;float before=Vector3.Distance(P.transform.position,enemy.transform.position);yield return Observe(1.4f,()=>{if(Overlap(enemy))overlaps++;});G.qaSuppressAI=true;
   Add("rifle_retreats_when_rushed",Vector3.Distance(P.transform.position,enemy.transform.position)>before+1,"before="+before+";after="+Vector3.Distance(P.transform.position,enemy.transform.position));
   // The former code fired backwards here: clear line of sight, no usable cover and an expired fire timer.
   enemy.MoveTo(new Vector3(114,.03f,-2));P.SetFeet(new Vector3(114,.03f,-14));enemy.transform.rotation=Quaternion.identity;enemy.AfterSwap();
   yield return new WaitForSeconds(1.8f);int beforeTurnShots=enemy.shotsFired,previousShots=beforeTurnShots;float minimumShotFacing=1;G.qaSuppressAI=false;
   Action sampleFacing=()=>{if(enemy.shotsFired>previousShots){minimumShotFacing=Mathf.Min(minimumShotFacing,Facing(enemy));previousShots=enemy.shotsFired;}};
   yield return Observe(.4f,sampleFacing);
   Add("rifle_back_facing_does_not_fire",enemy.shotsFired==beforeTurnShots&&Facing(enemy)<.85f,"shots="+(enemy.shotsFired-beforeTurnShots)+";facing="+Facing(enemy));
   yield return Observe(1.8f,sampleFacing);G.qaSuppressAI=true;
   Add("rifle_resumes_after_visible_turn",enemy.shotsFired>beforeTurnShots,"shots="+(enemy.shotsFired-beforeTurnShots)+";facing="+Facing(enemy));
   Add("rifle_all_observed_shots_face_player",enemy.shotsFired>beforeTurnShots&&minimumShotFacing>=.845f,"minimumShotFacing="+minimumShotFacing);
   enemy.gameObject.SetActive(false);Destroy(enemy.gameObject);cover.SetActive(true);other.SetActive(true);Physics.SyncTransforms();
   P.SetFeet(new Vector3(100,.03f,-8));enemy=EnemyActor.Spawn(EnemyKind.Shield,new Vector3(100,.03f,7),0);G.qaSuppressAI=false;int shieldOverlap=0;float startDistance=Vector3.Distance(P.transform.position,enemy.transform.position);bool playerDamaged=false;
   yield return Observe(13,()=>{if(Overlap(enemy))shieldOverlap++;playerDamaged|=P.hurtFlash>0;});G.qaSuppressAI=true;
   Add("melee_routes_around_cover_and_attacks",enemy.tactical.MeleeAttacks>0&&Vector3.Distance(P.transform.position,enemy.transform.position)<3&&enemy.shotsFired==0,"attacks="+enemy.tactical.MeleeAttacks+";distance="+Vector3.Distance(P.transform.position,enemy.transform.position)+";shots="+enemy.shotsFired);
   Add("melee_pursuit_no_body_wall_overlap",shieldOverlap==0,"overlapFrames="+shieldOverlap);
   // One unchanged melee cycle, observed from a fixed nearby first-person view on an open patch.
   // The player is restored between frames only to isolate presentation from survival balance.
   P.ResetAt(new Vector3(114,.03f,-10.3f));P.SetLook(0,8);enemy.MoveTo(new Vector3(114,.03f,-8));enemy.transform.rotation=Quaternion.Euler(0,180,0);enemy.tactical.Invalidate();
   int attacksBeforePose=enemy.tactical.MeleeAttacks;bool warningPose=false,strikePose=false,recoveredPose=false,visibleBlade=true,hiddenRifle=true;
   float maxGrip=0,bladeTravel=0;Vector3 warningBlade=Vector3.zero;float poseUntil=Time.time+3;G.qaSuppressAI=false;
   while(Time.time<poseUntil&&!recoveredPose)
   {
    yield return new WaitForEndOfFrame();P.health=100;
    visibleBlade&=enemy.ShieldBladeVisible;hiddenRifle&=!enemy.ShieldRifleVisible;maxGrip=Mathf.Max(maxGrip,enemy.ShieldBladeGripError);
    if(!warningPose&&enemy.tactical.State==TacticalState.MeleeWarning&&enemy.ShieldMeleePoseOffset>30)
    {warningPose=true;warningBlade=enemy.transform.InverseTransformPoint(enemy.ShieldBladePosition);ScreenCapture.CaptureScreenshot(Path.Combine(output,"shield-melee-01-windup.png"));}
    if(!strikePose&&enemy.tactical.MeleeAttacks>attacksBeforePose)
    {strikePose=true;bladeTravel=Vector3.Distance(warningBlade,enemy.transform.InverseTransformPoint(enemy.ShieldBladePosition));ScreenCapture.CaptureScreenshot(Path.Combine(output,"shield-melee-02-impact.png"));}
    if(strikePose&&enemy.tactical.State==TacticalState.Recover&&enemy.ShieldMeleePoseOffset<5)
    {recoveredPose=true;ScreenCapture.CaptureScreenshot(Path.Combine(output,"shield-melee-03-recovery.png"));}
   }
   G.qaSuppressAI=true;yield return null;
   Add("shield_has_visible_blade_and_hidden_rifle",visibleBlade&&hiddenRifle,"blade="+visibleBlade+";rifleHidden="+hiddenRifle);
   Add("shield_melee_has_windup_and_swing",warningPose&&strikePose&&bladeTravel>.08f,"warning="+warningPose+";impact="+strikePose+";bladeTravel="+bladeTravel);
   Add("shield_melee_returns_to_ready_pose",recoveredPose,"poseOffset="+enemy.ShieldMeleePoseOffset);
   Add("shield_blade_stays_gripped_during_attack",maxGrip<.001f,"maximumGripError="+maxGrip);
   // A real post-bake wall blocks external pushes and rotation of the attached shield.
   enemy.MoveTo(new Vector3(114,.03f,5));enemy.transform.rotation=Quaternion.identity;var wall=Box("Post-bake shield wall",new Vector3(115,1.3f,5),new Vector3(.2f,2.6f,5));Physics.SyncTransforms();
   enemy.FaceSafely(Quaternion.Euler(0,90,0));var physical=enemy.transform.Find("Shield").GetComponent<BoxCollider>();
   bool shieldPenetrates=Physics.CheckBox(physical.transform.TransformPoint(physical.center),Vector3.Scale(physical.size,physical.transform.lossyScale)*.5f,physical.transform.rotation,Layers.WorldMask);
   enemy.motion.Push(Vector3.right*9);Add("attached_shield_rotation_and_push_respect_wall",!shieldPenetrates&&enemy.transform.position.x<114.5f,"rotation="+enemy.transform.eulerAngles+";feet="+enemy.transform.position);
   var converted=enemy.EnableBoss();enemy.tactical.Invalidate();
   Add("boss_conversion_disables_guard_presentation",!enemy.ShieldBladeVisible&&enemy.BossBladeVisible&&!enemy.BossRifleVisible,"guardBlade="+enemy.ShieldBladeVisible+";bossBlade="+enemy.BossBladeVisible);
   converted.Absorb(BossEncounter.ShieldMaximum);enemy.tactical.Invalidate();
   Add("converted_boss_rifle_is_not_hidden_by_guard",!enemy.ShieldBladeVisible&&!enemy.BossBladeVisible&&enemy.BossRifleVisible,"bossRifle="+enemy.BossRifleVisible);
   Finish();
  }
  void Finish(){if(done)return;done=true;report.passed=report.checks.Count>0&&report.checks.TrueForAll(x=>x.passed);File.WriteAllText(Path.Combine(output,"tactical-ai-report.json"),JsonUtility.ToJson(report,true));Application.Quit(report.passed?0:1);}
  void OnDestroy(){Application.logMessageReceived-=Error;}
 }
}
