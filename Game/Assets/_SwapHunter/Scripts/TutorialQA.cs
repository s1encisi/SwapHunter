using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
namespace SwapHunter
{
 public sealed class TutorialQA:MonoBehaviour
 {
  [Serializable] public class Check{public string name,detail;public bool passed;}
  [Serializable] public class Report{public string kind="Nine playable tutorial gates via native keyboard, real fire/swap/launch/projectiles/recall; explicit setup positions between independent gates, not a continuous human playthrough";public bool passed;public List<Check> checks=new List<Check>();}
  Report report=new Report();bool done;float started;string output;Keyboard keys;DemoGame G=>DemoGame.I;PlayerMotor P=>G.player;TutorialCourse T=>G.tutorialCourse;
  void Awake(){started=Time.realtimeSinceStartup;Application.logMessageReceived+=Error;}
  void Error(string m,string s,LogType t){if(t==LogType.Error||t==LogType.Exception||t==LogType.Assert)Add("runtime_error",false,m);}
  void Add(string n,bool ok,string d){report.checks.Add(new Check{name=n,passed=ok,detail=d});Debug.Log("TUTORIAL_QA "+n+" "+ok+" "+d);}
  void Update(){if(!done&&Time.realtimeSinceStartup-started>100){Add("watchdog",false,"100s");Finish();}}
  void Aim(Vector3 p){var d=p-P.Eye.position;P.SetLook(Mathf.Atan2(d.x,d.z)*Mathf.Rad2Deg,-Mathf.Atan2(d.y,new Vector2(d.x,d.z).magnitude)*Mathf.Rad2Deg);}
  IEnumerator Press(Key k,float seconds=.03f){InputSystem.QueueStateEvent(keys,new KeyboardState(k));yield return new WaitForSeconds(seconds);InputSystem.QueueStateEvent(keys,new KeyboardState());yield return null;yield return null;}
  IEnumerator Capture(string n){yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(output,n+".png"));yield return null;}
  IEnumerator Start()
  {
   var a=Environment.GetCommandLineArgs();int i=Array.IndexOf(a,"-qaOutput");output=i>=0?a[i+1]:RunStorage.Root;Directory.CreateDirectory(output);
   while(!UnityEngine.Rendering.SplashScreen.isFinished)yield return null;
   G.StartTutorial();keys=InputSystem.AddDevice<Keyboard>();G.qaKeyboard=keys;G.qaInputEnabled=true;yield return new WaitForSeconds(.3f);
   Add("starts_with_one_movement_goal",T.Step==0&&T.Active,"step="+T.Step);yield return Press(Key.W,1.1f);yield return new WaitForSeconds(.2f);Add("real_movement_advances_to_shooting",T.Step==1,"feet="+P.transform.position);
   Aim(T.TrainingTarget.AimPoint);P.Fire();yield return new WaitForSeconds(.2f);Add("actual_hit_advances_to_swap",T.Step==2,"step="+T.Step);
   Aim(T.TrainingTarget.AimPoint);yield return Press(Key.Q);yield return new WaitForSeconds(.2f);Add("actual_exchange_advances_to_air_lesson",T.Step==3,"step="+T.Step);
   P.cooldown=0;yield return Press(Key.Space);yield return new WaitForSeconds(.12f);Aim(T.TrainingTarget.AimPoint);yield return Press(Key.Q);yield return new WaitForSeconds(.2f);Add("airborne_swap_required_and_observed",T.Step==4&&T.AirSwaps>0,"airSwaps="+T.AirSwaps);
   yield return new WaitForSeconds(.8f);P.SetFeet(T.LaunchPoint);P.verticalSpeed=0;yield return new WaitForSeconds(.15f);yield return Press(Key.E);Add("launch_device_used_once",T.LaunchUsed&&!T.Interact(),"launch used="+T.LaunchUsed);
   yield return new WaitForSeconds(.3f);var high=G.anchors[0];Aim(high.AimPoint);yield return Press(Key.Q);yield return new WaitForSeconds(.2f);Add("launch_air_swap_reaches_high_point",T.Step==5&&T.LaunchedAirSwap,"step="+T.Step+";feet="+P.transform.position);yield return Capture("01-high-point");
   P.SetFeet(new Vector3(0,.03f,8));P.verticalSpeed=0;yield return new WaitForSeconds(.1f);Aim(T.TrainingTarget.AimPoint);yield return Press(Key.C);yield return new WaitForSeconds(2.7f);Add("real_shield_intercept_completes_lesson",T.Step==6,"step="+T.Step+";blocks="+P.tactics.ShieldBlocks);
   P.SetFeet(new Vector3(0,.03f,11));Aim(T.TrainingTarget.AimPoint);yield return Press(Key.V);yield return new WaitForSeconds(.1f);Add("melee_hit_completes_lesson",T.Step==7,"step="+T.Step);
   P.SetFeet(new Vector3(0,.03f,10));Aim(T.TrainingTarget.AimPoint);yield return Press(Key.G);yield return new WaitForSeconds(.35f);Aim(T.RecallAnchor.AimPoint);yield return Press(Key.Q);yield return new WaitForSeconds(.2f);yield return Capture("02-three-point-recall");yield return Press(Key.X);yield return new WaitForSeconds(.8f);Add("three_point_recall_completes_lesson",T.Step==8,"step="+T.Step+";returnHits="+P.combatTools.ReturnHits);
   P.health=0;G.Die();G.Retry();Add("death_retries_current_lesson",T.Step==8&&T.Active&&P.health==G.config.playerHealth&&G.IsPlaying,"step="+T.Step);
   G.qaSuppressAI=true;P.SetFeet(new Vector3(0,.03f,8));Aim(T.TrainingTarget.AimPoint);yield return Press(Key.Q);yield return new WaitForSeconds(.2f);float until=Time.time+6;
   while(T.Active&&Time.time<until){Aim(T.TrainingTarget.AimPoint);P.Fire();yield return new WaitForSecondsRealtime(.16f);}
   Add("spatial_fight_completes_course",T.Completed&&T.Step==9&&G.state==RunState.Victory,"step="+T.Step);yield return Capture("03-course-complete");
   G.qaInputEnabled=false;G.qaKeyboard=null;InputSystem.RemoveDevice(keys);Finish();
  }
  void Finish(){if(done)return;done=true;report.passed=report.checks.Count>0&&report.checks.TrueForAll(x=>x.passed);File.WriteAllText(Path.Combine(output,"tutorial-report.json"),JsonUtility.ToJson(report,true));Application.Quit(report.passed?0:1);}
  void OnDestroy(){Application.logMessageReceived-=Error;}
 }
}
