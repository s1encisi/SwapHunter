using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
namespace SwapHunter
{
 public sealed class BattlefieldQA:MonoBehaviour
 {
  [Serializable] public class Check{public string name,detail;public bool passed;}
  [Serializable] public class Report{public string kind="Actual chapter 12 geometry and interactables: staged boss phase/counterplay integration, not continuous combat or human playtesting";public bool passed;public List<Check> checks=new List<Check>();}
  Report report=new Report();string output;bool done;float start;EnemyActor enemy;BossEncounter boss;Vector3[] anchors;Keyboard keys;
  DemoGame G=>DemoGame.I;PlayerMotor P=>G.player;
  void Awake(){start=Time.realtimeSinceStartup;Application.logMessageReceived+=Error;}
  void Error(string m,string s,LogType t){if(t==LogType.Error||t==LogType.Exception||t==LogType.Assert)Add("runtime_error",false,m);}
  void Add(string n,bool ok,string d){report.checks.Add(new Check{name=n,passed=ok,detail=d});Debug.Log("BATTLEFIELD_QA "+n+" "+ok+" "+d);}
  void Update(){if(!done&&Time.realtimeSinceStartup-start>150){Add("watchdog",false,"150s");Finish();}}
  void Aim(Vector3 p){var d=p-P.Eye.position;P.SetLook(Mathf.Atan2(d.x,d.z)*Mathf.Rad2Deg,-Mathf.Atan2(d.y,new Vector2(d.x,d.z).magnitude)*Mathf.Rad2Deg);}
  IEnumerator Prepare(BossPhase phase,Vector3 player,Vector3 bossFeet)
  {
   G.qaSuppressAI=true;if(enemy){enemy.gameObject.SetActive(false);Destroy(enemy.gameObject);}
   foreach(var b in FindObjectsByType<Bolt>(FindObjectsSortMode.None)){b.gameObject.SetActive(false);Destroy(b.gameObject);}
   P.ResetAt(new Vector3(0,.03f,3));for(int i=0;i<anchors.Length;i++)G.expeditionMap.anchors[i].MoveTo(anchors[i]);Physics.SyncTransforms();
   enemy=EnemyActor.Spawn(EnemyKind.Elite,bossFeet,0);boss=enemy.boss;
   if(phase!=BossPhase.EnergyMelee)enemy.Damage(BossEncounter.ShieldMaximum);if(phase==BossPhase.Combined)enemy.Damage(361);
   P.SetFeet(player);yield return new WaitForSeconds(.12f);Aim(enemy.AimPoint);
  }
  bool Begin(BossSkill skill){bool ok=boss.TryBeginSkill(skill);Add("chapter12_"+skill+"_warning",ok,boss.Warning);enemy.suppressAI=false;G.qaSuppressAI=false;return ok;}
  IEnumerator EndSkill(){float until=Time.time+8;while(Time.time<until&&(boss.SkillStep==BossSkillStep.Telegraph||boss.SkillStep==BossSkillStep.Execute))yield return null;G.qaSuppressAI=true;Add("chapter12_skill_recovers",boss.SkillStep==BossSkillStep.Recover,boss.Skill.ToString());}
  IEnumerator Capture(string name){yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(output,name+".png"));yield return null;}
  IEnumerator Start()
  {
   var a=Environment.GetCommandLineArgs();int i=Array.IndexOf(a,"-qaOutput");output=i>=0?a[i+1]:RunStorage.Root;Directory.CreateDirectory(output);while(!UnityEngine.Rendering.SplashScreen.isFinished)yield return null;
   for(int l=0;l<11;l++){var run=G.expeditionStore.Begin(l);G.expeditionStore.Settle(run,true,false,30);}
   G.BeginExpedition(11);G.qaSuppressAI=true;yield return new WaitForSeconds(3.6f);foreach(var e in G.enemies)if(e)e.suppressAI=true;
   anchors=new Vector3[G.expeditionMap.anchors.Count];for(i=0;i<anchors.Length;i++)anchors[i]=G.expeditionMap.anchors[i].transform.position;
   keys=InputSystem.AddDevice<Keyboard>();G.qaKeyboard=keys;G.qaInputEnabled=true;var high=G.expeditionMap.anchors.Find(x=>x.transform.position.y>3&&x.transform.position.x<0);
   yield return Prepare(BossPhase.EnergyMelee,new Vector3(-9,3.34f,44),new Vector3(0,.03f,55));Begin(BossSkill.SwapElbow);var chosen=boss.SpatialTarget;Vector3 old=enemy.transform.position,destination=chosen?chosen.transform.position:Vector3.zero;
   yield return Capture("01-real-arena-elbow-warning");yield return EndSkill();Add("real_arena_elbow_exchanges_both_ends",chosen&&boss.SpatialSwaps==1&&Vector3.Distance(enemy.transform.position,destination)<.2f&&Vector3.Distance(chosen.transform.position,old)<.2f,"boss="+enemy.transform.position+";target="+(chosen?chosen.transform.position.ToString():"none"));
   yield return Prepare(BossPhase.Rifle,new Vector3(0,.03f,47),new Vector3(0,.03f,32));Begin(BossSkill.AdvantageSwap);chosen=boss.SpatialTarget;yield return EndSkill();Add("real_arena_boss_chooses_elevated_position",chosen&&enemy.transform.position.y>3&&boss.SpatialSwaps==1,"boss="+enemy.transform.position);
   yield return Prepare(BossPhase.Rifle,new Vector3(0,.03f,47),new Vector3(0,.03f,55));Begin(BossSkill.Attraction);yield return new WaitForSeconds(.5f);Aim(high.AimPoint);bool swap=P.RequestSwap(high);yield return EndSkill();Add("real_arena_swap_escapes_attraction",swap&&P.health==100,"hp="+P.health+";feet="+P.transform.position);
   yield return Prepare(BossPhase.Combined,new Vector3(0,.03f,47),new Vector3(0,.03f,55));Begin(BossSkill.SweepLaser);yield return new WaitForSeconds(.5f);Aim(high.AimPoint);swap=P.RequestSwap(high);yield return EndSkill();Add("real_arena_swap_avoids_laser",swap&&P.health==100,"hp="+P.health);yield return Capture("02-real-arena-laser-counter");
   yield return Prepare(BossPhase.Combined,new Vector3(-7,.03f,43),new Vector3(0,.03f,55));var pad=G.expeditionMap.nodes.Find(n=>n.kind==ExpeditionNodeKind.Launch&&n.transform.position.z>40&&n.transform.position.z<45);Begin(BossSkill.GroundWave);yield return new WaitForSeconds(1.35f);Aim(pad.Point);InputSystem.QueueStateEvent(keys,new KeyboardState(Key.E));yield return null;yield return null;InputSystem.QueueStateEvent(keys,new KeyboardState());yield return new WaitForSeconds(.5f);yield return Capture("03-real-arena-launch-wave");yield return EndSkill();Add("real_arena_one_use_pad_counters_wave",pad.used&&P.health==100,"padUsed="+pad.used+";hp="+P.health);
   yield return Prepare(BossPhase.Combined,new Vector3(0,.03f,47),new Vector3(0,.03f,55));Begin(BossSkill.OmniFire);yield return new WaitForSeconds(.7f);Aim(enemy.AimPoint);bool shield=P.tactics.ActivateShield();yield return EndSkill();yield return new WaitForSeconds(.4f);Add("real_arena_shield_counters_omni",shield&&P.health==100&&P.tactics.ShieldBlocks>0,"hp="+P.health+";blocks="+P.tactics.ShieldBlocks);yield return Capture("04-real-arena-shield-counter");
   yield return Prepare(BossPhase.Combined,new Vector3(0,.03f,35),new Vector3(0,.03f,42));Begin(BossSkill.OmniFire);yield return EndSkill();yield return new WaitForSeconds(.4f);Add("real_arena_existing_cover_counters_omni",P.health==100,"hp="+P.health);
   yield return Prepare(BossPhase.Combined,new Vector3(-9,3.34f,44),new Vector3(0,.03f,55));Begin(BossSkill.GroundWave);yield return EndSkill();Add("already_elevated_player_avoids_ground_wave",P.health==100&&boss.DangerCenter.y<.2f,"hp="+P.health+";ground="+boss.DangerCenter.y);
   yield return Prepare(BossPhase.Combined,new Vector3(-9,3.34f,44),new Vector3(0,.03f,55));Begin(BossSkill.SweepLaser);yield return EndSkill();Add("already_elevated_player_avoids_ground_laser",P.health==100&&boss.DangerCenter.y<.2f,"hp="+P.health+";ground="+boss.DangerCenter.y);
   G.qaInputEnabled=false;G.qaKeyboard=null;InputSystem.RemoveDevice(keys);Finish();
  }
  void Finish(){if(done)return;done=true;report.passed=report.checks.Count>0&&report.checks.TrueForAll(c=>c.passed);File.WriteAllText(Path.Combine(output,"battlefield-report.json"),JsonUtility.ToJson(report,true));Application.Quit(report.passed?0:1);}
  void OnDestroy(){Application.logMessageReceived-=Error;}
 }
}
