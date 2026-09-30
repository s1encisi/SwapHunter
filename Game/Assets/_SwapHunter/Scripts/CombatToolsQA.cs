using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Unity.AI.Navigation;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
namespace SwapHunter
{
 public sealed class CombatToolsQA:MonoBehaviour
 {
  [Serializable] public class Check{public string name,detail;public bool passed;}
  [Serializable] public class Report{public string kind="Native melee, thrown silence, spatial recall, collision and input integration with explicit fixture setup";public bool passed;public List<Check> checks=new List<Check>();}
  Report report=new Report();string output;bool done;float began;Transform fixture;PhaseAnchor anchor;Keyboard keys;EnemyActor target,neighbor;GameObject wall;
  DemoGame G=>DemoGame.I;PlayerMotor P=>G.player;PlayerCombatTools T=>P.combatTools;
  void Awake(){began=Time.realtimeSinceStartup;Application.logMessageReceived+=Error;}
  void Error(string m,string s,LogType t){if(t==LogType.Error||t==LogType.Exception||t==LogType.Assert)Add("runtime_error",false,m);}
  void Add(string n,bool ok,string d){report.checks.Add(new Check{name=n,passed=ok,detail=d});Debug.Log("COMBAT_TOOLS_QA "+n+" "+ok+" "+d);}
  void Update(){if(!done&&Time.realtimeSinceStartup-began>120){Add("watchdog",false,"120s");Finish();}}
  void Aim(Vector3 p){var d=p-P.Eye.position;P.SetLook(Mathf.Atan2(d.x,d.z)*Mathf.Rad2Deg,-Mathf.Atan2(d.y,new Vector2(d.x,d.z).magnitude)*Mathf.Rad2Deg);}
  IEnumerator KeyPress(Key key){InputSystem.QueueStateEvent(keys,new KeyboardState(key));yield return null;yield return null;InputSystem.QueueStateEvent(keys,new KeyboardState());yield return null;}
  IEnumerator Setup(float z=6)
  {
   G.qaSuppressAI=true;if(wall){wall.SetActive(false);Destroy(wall);wall=null;}
   foreach(var e in G.enemies.ToArray())if(e){e.gameObject.SetActive(false);Destroy(e.gameObject);}G.enemies.Clear();
   foreach(var k in FindObjectsByType<ThrownKnife>(FindObjectsSortMode.None))Destroy(k.gameObject);
   anchor.MoveTo(new Vector3(108,.03f,0));P.ResetAt(new Vector3(100,.03f,0));yield return new WaitForSeconds(.1f);
   target=EnemyActor.Spawn(EnemyKind.Training,new Vector3(100,.03f,z),0);neighbor=EnemyActor.Spawn(EnemyKind.Training,new Vector3(102,.03f,6),0);Aim(target.AimPoint);yield return new WaitForSeconds(.1f);
  }
  IEnumerator Capture(string n){yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(output,n+".png"));yield return null;}
  IEnumerator Start()
  {
   var a=Environment.GetCommandLineArgs();int i=Array.IndexOf(a,"-qaOutput");output=i>=0?a[i+1]:RunStorage.Root;Directory.CreateDirectory(output);
   while(!UnityEngine.Rendering.SplashScreen.isFinished)yield return null;
   G.StartRun(false);G.qaSuppressAI=true;yield return new WaitForSeconds(.2f);
   fixture=new GameObject("Combat tools validation arena").transform;
   Shapes.Box("Floor",fixture,new Vector3(103,-.5f,4),new Vector3(28,1,30),1,true,Layers.World);
   var surface=fixture.gameObject.AddComponent<NavMeshSurface>();surface.collectObjects=CollectObjects.Children;surface.layerMask=Layers.WorldMask;surface.useGeometry=UnityEngine.AI.NavMeshCollectGeometry.PhysicsColliders;surface.BuildNavMesh();
   var go=new GameObject("Recall exchange anchor");go.layer=Layers.Actor;go.transform.SetParent(fixture);anchor=go.AddComponent<PhaseAnchor>();var c=go.AddComponent<CapsuleCollider>();c.radius=.32f;c.height=1.4f;c.center=Vector3.up*.7f;ImportedModels.Create(G.config.models.beacon,go.transform,Vector3.zero);G.anchors.Add(anchor);
   keys=InputSystem.AddDevice<Keyboard>();G.qaKeyboard=keys;G.qaInputEnabled=true;
   yield return Setup(1.9f);float hp=target.health;yield return KeyPress(Key.V);Add("keyboard_melee_hits_and_displaces",target.health<hp&&T.MeleeHits==1&&target.transform.position.z>2.4f,"health="+target.health+";feet="+target.transform.position);Add("melee_has_recovery",!T.Melee(),"no repeated instant hit");yield return new WaitForSeconds(.08f);yield return Capture("01-melee-grip");yield return new WaitForEndOfFrame();Add("blade_grip_and_wrist_connected",T.GripError<.005f&&P.ReloadWristError<.005f,"grip="+T.GripError+";wrist="+P.ReloadWristError);
   yield return Setup(1.9f);wall=Shapes.Box("Melee wall",fixture,new Vector3(100,1.2f,1),new Vector3(4,2.4f,.25f),1,true,Layers.World);Physics.SyncTransforms();hp=target.health;T.Melee();Add("melee_cannot_hit_through_wall",target.health==hp,"hp="+target.health);

   for(int rank=0;rank<3;rank++)
   {
    yield return Setup();hp=target.health;var k=ThrownKnife.Launch(T,P,P.Eye.position,P.Eye.forward,rank);yield return new WaitForSeconds(.35f);
    Add("knife_rank_"+rank+"_impact",target.health<hp&&k.OutboundHits==1,"health="+target.health+";hits="+k.OutboundHits);
    Add("knife_rank_"+rank+"_silence_scope",target.Silenced==(rank>=1)&&neighbor.Silenced==(rank>=2),"target="+target.Silenced+";neighbor="+neighbor.Silenced);Destroy(k.gameObject);
   }
   yield return Setup();neighbor.gameObject.SetActive(false);neighbor=EnemyActor.Spawn(EnemyKind.Training,new Vector3(104,.03f,2.8f),0);hp=neighbor.health;
   yield return KeyPress(Key.G);yield return new WaitForSeconds(.35f);var returning=T.Knife;Add("keyboard_throw_places_real_knife",returning&&!returning.Flying&&returning.OutboundHits==1,"throws="+T.Throws);
   Vector3 knifePoint=returning.transform.position;Aim(anchor.AimPoint);bool swap=P.RequestSwap(anchor);yield return new WaitForSeconds(.2f);Add("swap_changes_recall_geometry",swap&&P.transform.position.x>107&&Vector3.Distance(returning.transform.position,knifePoint)<.02f,"player="+P.transform.position+";knife="+returning.transform.position);
   yield return KeyPress(Key.X);yield return new WaitForSeconds(.17f);yield return Capture("knife-recall-after-swap");yield return new WaitForSeconds(.6f);
   Add("return_segment_hits_new_flank_target_once",neighbor.health==hp-22&&T.ReturnHits>=1,"neighbor="+neighbor.health+";returnHits="+T.ReturnHits);Add("recall_returns_to_hand",!T.Knife,"knife recovered");

   yield return Setup();neighbor.gameObject.SetActive(false);neighbor=EnemyActor.Spawn(EnemyKind.Training,new Vector3(106,.03f,1.2f),0);hp=neighbor.health;T.Throw();yield return new WaitForSeconds(.35f);Aim(anchor.AimPoint);P.RequestSwap(anchor);yield return new WaitForSeconds(.2f);
   wall=Shapes.Box("Return obstruction",fixture,new Vector3(104,1.4f,2.8f),new Vector3(3,2.8f,.6f),1,true,Layers.World);Physics.SyncTransforms();T.Recall();yield return new WaitForSeconds(.65f);
   Add("return_stops_at_real_wall",T.Knife&&T.Knife.WallStops>0&&!T.Knife.Returning&&T.Knife.transform.position.x<105,"knife="+(T.Knife?T.Knife.transform.position.ToString():"none"));Add("return_does_not_damage_behind_wall",neighbor.health==hp,"health="+neighbor.health);
   yield return Setup();wall=Shapes.Box("Throw obstruction",fixture,new Vector3(100,1.4f,3),new Vector3(3,2.8f,.4f),1,true,Layers.World);Physics.SyncTransforms();hp=target.health;T.Throw();yield return new WaitForSeconds(.4f);Add("outbound_cannot_cross_wall",T.Knife&&T.Knife.WallStops==1&&target.health==hp,"target="+target.health);

   yield return Setup();target.gameObject.SetActive(false);target=EnemyActor.Spawn(EnemyKind.Assault,new Vector3(100,.03f,9),0);target.ApplySilence(5);int shots=target.shotsFired;G.qaSuppressAI=false;yield return new WaitForSeconds(3.7f);Add("silence_does_not_pause_basic_rifle_ai",target.Silenced&&target.shotsFired>shots,"shots="+target.shotsFired+";silence="+target.silenceLeft);G.qaSuppressAI=true;
   yield return Setup();target.gameObject.SetActive(false);target=EnemyActor.Spawn(EnemyKind.Elite,new Vector3(100,.03f,9),0);var boss=target.EnableBoss();target.Damage(BossEncounter.ShieldMaximum);bool begin=boss.TryBeginSkill(BossSkill.Attraction);target.ApplySilence(3);boss.Tick(.016f);Add("silence_interrupts_boss_special",begin&&boss.SkillStep==BossSkillStep.Ready&&!boss.TryBeginSkill(BossSkill.Attraction),"step="+boss.SkillStep);
   var legacy=new GameOptions();legacy.actions=new ActionBinding[21];Array.Copy(GameOptions.DefaultBindings(),legacy.actions,21);legacy.actions[0].primary="K:G";legacy.Validate();Add("new_tool_bindings_preserve_custom_G",legacy.actions.Length==24&&legacy.actions[0].primary=="K:G"&&legacy.actions[22].primary=="","count="+legacy.actions.Length);
   G.qaInputEnabled=false;G.qaKeyboard=null;InputSystem.RemoveDevice(keys);Finish();
  }
  void Finish(){if(done)return;done=true;report.passed=report.checks.Count>0&&report.checks.TrueForAll(x=>x.passed);File.WriteAllText(Path.Combine(output,"combat-tools-report.json"),JsonUtility.ToJson(report,true));Application.Quit(report.passed?0:1);}
  void OnDestroy(){Application.logMessageReceived-=Error;}
 }
}
