using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
namespace SwapHunter {
public sealed class BossQA:MonoBehaviour {
 [Serializable] public sealed class Check{public string name,detail;public bool passed;}
 [Serializable] public sealed class Report{public string kind="Boss continuous-life and native phase integration ONLY; six-skill counterplay not yet covered";public bool passed;public List<Check> checks=new List<Check>();}
 Report report=new Report();string output;bool done;float began;DemoGame G=>DemoGame.I;PlayerMotor P=>G.player;
 void Awake(){began=Time.realtimeSinceStartup;Application.logMessageReceived+=Error;}
 void Error(string msg,string stack,LogType t){if(t==LogType.Exception||t==LogType.Error||t==LogType.Assert)Add("runtime_error",false,msg);}
 void Add(string n,bool ok,string d){report.checks.Add(new Check{name=n,passed=ok,detail=d});Debug.Log("BOSS_QA "+n+" "+ok+" "+d);}
 void Update(){if(!done&&Time.realtimeSinceStartup-began>90){Add("watchdog",false,"90 seconds");Finish();}}
 void Aim(Vector3 p){var d=p-P.Eye.position;P.SetLook(Mathf.Atan2(d.x,d.z)*Mathf.Rad2Deg,-Mathf.Atan2(d.y,new Vector2(d.x,d.z).magnitude)*Mathf.Rad2Deg);}
 IEnumerator Capture(string name){yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(output,name+".png"));yield return null;}
 IEnumerator Start(){
 var a=Environment.GetCommandLineArgs();int i=Array.IndexOf(a,"-qaOutput");output=i>=0?a[i+1]:RunStorage.Root;Directory.CreateDirectory(output);
 while(!UnityEngine.Rendering.SplashScreen.isFinished)yield return null;
 G.StartRun(true);G.qaSuppressAI=true;yield return new WaitForSeconds(.3f);
 foreach(var old in G.enemies.ToArray())if(old)old.gameObject.SetActive(false);
 var e=EnemyActor.Spawn(EnemyKind.Elite,new Vector3(0,0,13),0);var b=e.EnableBoss();P.SetFeet(new Vector3(0,.03f,8));P.verticalSpeed=0;Aim(e.AimPoint);yield return new WaitForSeconds(.2f);
 Add("starts_energy_melee",b.Phase==BossPhase.EnergyMelee&&b.Energy==360&&e.health==720,"energy="+b.Energy+";hp="+e.health);
 var oldShield=ImportedModels.Find(e.transform,"shield_visual");
 Add("boss_has_no_physical_shield",!e.HasPhysicalShield&&oldShield&&!oldShield.gameObject.activeInHierarchy,"energy shield is a continuous life layer, not directional armour");
 Add("energy_phase_shows_melee_weapon",e.BossBladeVisible&&!e.BossRifleVisible,"original phase blade in actual right-hand rig");
 float hp=e.health,energy=b.Energy;int ammo=P.ammo[0];P.Fire();yield return new WaitForSeconds(.15f);
 Add("actual_gun_hits_energy_first",P.ammo[0]<ammo&&b.Energy<energy&&e.health==hp,"energy="+b.Energy+";hp="+e.health);
 yield return Capture("01-boss-energy");
 P.SetFeet(new Vector3(0,.03f,10.5f));G.qaSuppressAI=false;float oldHp=P.health;
 yield return new WaitForSeconds(2.6f);
 Add("phase_one_melee_applies",P.health<oldHp&&e.shotsFired==0,"hp="+P.health+";shots="+e.shotsFired);
 G.qaSuppressAI=true;energy=b.Energy;e.Damage(energy+20);
 Add("shield_break_overflow_to_hp",b.Energy==0&&e.health==700&&b.Phase==BossPhase.Rifle,"energy="+b.Energy+";hp="+e.health);
 Add("broken_shield_shows_rifle",!e.BossBladeVisible&&e.BossRifleVisible,"visible weapon changes with phase");
 P.SetFeet(new Vector3(0,.03f,5));Aim(e.AimPoint);int shots=e.shotsFired;G.qaSuppressAI=false;yield return new WaitForSeconds(2.3f);
 Add("phase_two_uses_rifle",e.shotsFired>shots,"shots="+e.shotsFired);yield return Capture("02-boss-rifle");
 G.qaSuppressAI=true;e.Damage(e.health-359);
 Add("half_health_phase_three",b.Phase==BossPhase.Combined&&b.PhaseTransitions==2&&e.maximumHealth==720,"phase="+b.Phase+";hp="+e.health);
 yield return Capture("03-boss-combined");int kills=G.totalKills;e.Damage(1000);
 Add("boss_defeated_once",b.Phase==BossPhase.Defeated&&G.totalKills==kills+1,"kills="+G.totalKills);e.Damage(1000);Add("no_repeat_reward",G.totalKills==kills+1,"idempotent death");
 Finish();
 }
 void Finish(){if(done)return;done=true;report.passed=report.checks.Count>0&&report.checks.TrueForAll(x=>x.passed);File.WriteAllText(Path.Combine(output,"boss-report.json"),JsonUtility.ToJson(report,true));Application.Quit(report.passed?0:1);}
 void OnDestroy(){Application.logMessageReceived-=Error;}
}}
