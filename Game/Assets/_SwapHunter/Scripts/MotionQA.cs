using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
namespace SwapHunter {
public sealed class MotionQA:MonoBehaviour {
 [Serializable] public sealed class Check {public string name,detail;public bool passed;}
 [Serializable] public sealed class Report {public string kind="Native collision and airborne swap; explicitly positioned fixtures, not human playtesting";public bool passed;public List<Check> checks=new List<Check>();}
 Report report=new Report();string output;bool finished;float began;
 DemoGame G=>DemoGame.I;PlayerMotor P=>G.player;
 void Awake(){began=Time.realtimeSinceStartup;Application.logMessageReceived+=Error;}
 void Error(string msg,string stack,LogType t){if(t==LogType.Exception||t==LogType.Error||t==LogType.Assert)CheckThat("runtime_error",false,msg);}
 void CheckThat(string n,bool ok,string detail){report.checks.Add(new Check{name=n,passed=ok,detail=detail});Debug.Log("MOTION_QA "+n+" "+ok+" "+detail);}
 void Update(){if(!finished&&Time.realtimeSinceStartup-began>180){CheckThat("watchdog",false,"180 seconds");Finish();}}
 GameObject Box(Transform parent,string name,Vector3 p,Vector3 size){var o=GameObject.CreatePrimitive(PrimitiveType.Cube);o.name=name;o.layer=Layers.World;o.transform.SetParent(parent);o.transform.position=p;o.transform.localScale=size;o.GetComponent<Renderer>().sharedMaterial=Shapes.Mat(1);return o;}
 void Aim(Vector3 p){Vector3 d=p-P.Eye.position;P.yaw=Mathf.Atan2(d.x,d.z)*Mathf.Rad2Deg;P.pitch=-Mathf.Atan2(d.y,new Vector2(d.x,d.z).magnitude)*Mathf.Rad2Deg;P.transform.rotation=Quaternion.Euler(0,P.yaw,0);P.Eye.localRotation=Quaternion.Euler(P.pitch,0,0);}
 IEnumerator Capture(string file){yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(output,file+".png"));yield return null;}
 IEnumerator Start(){
 var a=Environment.GetCommandLineArgs();int i=Array.IndexOf(a,"-qaOutput");output=i>=0?a[i+1]:RunStorage.Root;Directory.CreateDirectory(output);
 while(!UnityEngine.Rendering.SplashScreen.isFinished)yield return null;
 G.StartRun();G.qaSuppressAI=true;yield return new WaitForSeconds(.3f);
 var target=G.enemies[0];P.SetFeet(new Vector3(0,3,8));P.verticalSpeed=0;P.cooldown=0;Aim(target.AimPoint);
 CheckThat("airborne_swap_valid",P.ValidateSwap(target)=="可换位",P.ValidateSwap(target));
 bool begin=P.RequestSwap(target);yield return new WaitForSeconds(.15f);
 CheckThat("airborne_exchange_actual",begin&&P.swapCount>0&&target.transform.position.y>1,"enemy="+target.transform.position+";player="+P.transform.position);
 Aim(target.AimPoint);yield return Capture("01-airborne-exchange");yield return new WaitForSeconds(1.1f);
 CheckThat("airborne_enemy_lands_navigation",target.Grounded&&target.agent.enabled&&target.agent.isOnNavMesh,"feet="+target.transform.position);
 // Real keyboard jump, then real swap input while still moving vertically.
 target.MoveTo(new Vector3(0,.03f,13));P.SetFeet(new Vector3(0,.03f,8));P.cooldown=0;P.verticalSpeed=0;yield return new WaitForSeconds(.2f);
 var keys=InputSystem.AddDevice<Keyboard>();G.qaKeyboard=keys;G.qaInputEnabled=true;
 InputSystem.QueueStateEvent(keys,new KeyboardState(Key.Space));yield return null;yield return null;
 InputSystem.QueueStateEvent(keys,new KeyboardState());yield return new WaitForSeconds(.15f);Aim(target.AimPoint);
 bool jumped=!P.Grounded&&P.transform.position.y>.4f;int swaps=P.swapCount;
 InputSystem.QueueStateEvent(keys,new KeyboardState(Key.Q));yield return null;yield return null;
 InputSystem.QueueStateEvent(keys,new KeyboardState());yield return new WaitForSeconds(.14f);
 CheckThat("keyboard_jump_then_swap",jumped&&P.swapCount==swaps+1,"jumped="+jumped+"; swaps="+P.swapCount);
 yield return new WaitForSeconds(.9f);
 target.MoveTo(new Vector3(0,.03f,13));P.SetFeet(new Vector3(0,.03f,8));P.cooldown=0;P.verticalSpeed=0;yield return new WaitForSeconds(.2f);
 bool launched=P.Launch();yield return new WaitForSeconds(.35f);Aim(target.AimPoint);swaps=P.swapCount;
 InputSystem.QueueStateEvent(keys,new KeyboardState(Key.Q));yield return null;yield return null;
 InputSystem.QueueStateEvent(keys,new KeyboardState());yield return new WaitForSeconds(.14f);
 CheckThat("launch_then_air_swap",launched&&P.swapCount==swaps+1&&target.transform.position.y>3,"targetHeight="+target.transform.position.y);
 yield return new WaitForSeconds(1.2f);
 G.qaInputEnabled=false;G.qaKeyboard=null;InputSystem.RemoveDevice(keys);
 target.gameObject.SetActive(false);
 var root=new GameObject("Motion validation fixture").transform;
 Box(root,"floor",new Vector3(100,-.5f,0),new Vector3(32,1,30));Physics.SyncTransforms();
 var surface=root.gameObject.AddComponent<NavMeshSurface>();surface.collectObjects=CollectObjects.Children;surface.layerMask=Layers.WorldMask;surface.useGeometry=NavMeshCollectGeometry.PhysicsColliders;surface.BuildNavMesh();
 var e=EnemyActor.Spawn(EnemyKind.Assault,new Vector3(100,0,-5),0);e.enabled=false;yield return null;
 Box(root,"post-bake solid wall",new Vector3(100,1.5f,0),new Vector3(8,3,.25f));Physics.SyncTransforms();
 e.agent.speed=20;e.agent.acceleration=200;e.agent.stoppingDistance=0;e.agent.isStopped=false;e.agent.SetDestination(new Vector3(100,0,5));
 float until=Time.time+1,maxZ=e.transform.position.z;int overlap=0;
 while(Time.time<until){yield return null;maxZ=Mathf.Max(maxZ,e.transform.position.z);if(Physics.CheckCapsule(e.transform.position+Vector3.up*.42f,e.transform.position+Vector3.up*1.38f,.30f,Layers.WorldMask,QueryTriggerInteraction.Ignore))overlap++;}
 CheckThat("physical_wall_blocks_nav_motion",maxZ<-.35f&&overlap==0,"maxZ="+maxZ+";penetratingFrames="+overlap);
 e.motion.Push(new Vector3(0,0,12));
 CheckThat("external_push_swept_collision",e.transform.position.z<-.35f,"z="+e.transform.position.z);
 e.agent.SetDestination(new Vector3(100,0,-8));yield return new WaitForSeconds(.65f);
 CheckThat("retreat_from_wall",e.transform.position.z<-6,"z="+e.transform.position.z);
 e.agent.SetDestination(new Vector3(100,0,5));yield return new WaitForSeconds(.8f);
 CheckThat("reapproach_still_blocked",e.transform.position.z<-.35f,"z="+e.transform.position.z);
 P.SetFeet(new Vector3(97,.03f,-6));Aim(e.AimPoint);yield return Capture("02-wall-movement");e.gameObject.SetActive(false);
 Box(root,"spawn blocking model",new Vector3(110,1.5f,0),new Vector3(1.5f,3,1.5f));Physics.SyncTransforms();
 var spawned=EnemyActor.Spawn(EnemyKind.Assault,new Vector3(110,0,0),0);
 bool clear=!spawned||!Physics.CheckCapsule(spawned.transform.position+Vector3.up*.42f,spawned.transform.position+Vector3.up*1.38f,.30f,Layers.WorldMask,QueryTriggerInteraction.Ignore);
 CheckThat("spawn_rejects_or_resolves_occupied_volume",clear,spawned?spawned.transform.position.ToString():"safely rejected");
 if(spawned)spawned.gameObject.SetActive(false);root.gameObject.SetActive(false);
 // Normal chapter installation and keyboard interaction exercise the actual one-use pad.
 for(int level=0;level<2;level++){var id=G.expeditionStore.Begin(level);G.expeditionStore.Settle(id,true,false,30);}
 G.OpenExpeditionBoard();bool deployed=G.BeginExpedition(2);yield return new WaitForSeconds(.25f);
 CheckThat("launch_chapter_deployed",deployed,G.expeditionNotice);
 var pad=G.expeditionMap.nodes.Find(n=>n.kind==ExpeditionNodeKind.Launch);
 CheckThat("launch_pad_present",pad!=null,"Chapter 3 launch tutorial device");
 if(pad)
 {
  P.SetFeet(pad.transform.position+Vector3.back*.9f);P.verticalSpeed=0;P.cooldown=0;yield return new WaitForSeconds(.2f);Aim(pad.Point);
  keys=InputSystem.AddDevice<Keyboard>();G.qaKeyboard=keys;G.qaInputEnabled=true;
  InputSystem.QueueStateEvent(keys,new KeyboardState(Key.E));yield return null;yield return null;
  InputSystem.QueueStateEvent(keys,new KeyboardState());yield return new WaitForSeconds(.25f);
  CheckThat("pad_keyboard_activation",pad.used&&!P.Grounded&&P.transform.position.y>2,"height="+P.transform.position.y);
  var beacon=G.expeditionMap.anchors[0];Aim(beacon.AimPoint);swaps=P.swapCount;
  InputSystem.QueueStateEvent(keys,new KeyboardState(Key.Q));yield return null;yield return null;
  InputSystem.QueueStateEvent(keys,new KeyboardState());yield return new WaitForSeconds(.15f);
  CheckThat("actual_pad_air_exchange",P.swapCount==swaps+1&&P.transform.position.z>27,"player="+P.transform.position+"; reason="+P.swapReason);
  CheckThat("pad_single_use",pad.used&&!G.UseExpeditionNode(pad),"Activated pad cannot be reused");
  P.SetLook(180,0);yield return Capture("03-pad-air-swap");
  G.qaInputEnabled=false;G.qaKeyboard=null;InputSystem.RemoveDevice(keys);
 }
 G.FinishExpedition(false);G.OpenExpeditionBoard();
 // Void is an intentional spatial tactic, not a reason to require the player to land first.
 G.StartRun(true);G.qaSuppressAI=true;root.gameObject.SetActive(true);P.SetFeet(new Vector3(119,3,0));P.verticalSpeed=0;P.cooldown=0;
 var voidTarget=EnemyActor.Spawn(EnemyKind.Assault,new Vector3(114,0,0),0);Aim(voidTarget.AimPoint);swaps=P.swapCount;
 bool voidSwap=P.RequestSwap(voidTarget);yield return new WaitForSeconds(.15f);
 CheckThat("over_void_exchange",voidSwap&&P.swapCount==swaps+1,"player="+P.transform.position+"; target="+voidTarget.transform.position);
 yield return new WaitForSecondsRealtime(1.8f);
 CheckThat("displaced_enemy_falls_out",!voidTarget||!voidTarget.Alive,"No rescue teleport on void fall");
 Finish();
 }
 void Finish(){if(finished)return;finished=true;report.passed=report.checks.Count>0&&report.checks.TrueForAll(x=>x.passed);File.WriteAllText(Path.Combine(output,"motion-report.json"),JsonUtility.ToJson(report,true));Application.Quit(report.passed?0:1);}
 void OnDestroy(){Application.logMessageReceived-=Error;}
}}
