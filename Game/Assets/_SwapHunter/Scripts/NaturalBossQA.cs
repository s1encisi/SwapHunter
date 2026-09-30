using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
namespace SwapHunter
{
 public sealed class NaturalBossQA:MonoBehaviour
 {
  [Serializable] public class Run{public string strategy;public bool won;public float seconds,playerHealth,bossHealth,energy,firstSpecial=-1;public int shots,swaps,shieldBlocks,casts;public List<string> skills=new List<string>();}
  [Serializable] public class Report{public string version,kind="Chapter12 solo encounter comparison: normal 100HP, carbine, ammunition, cooldowns and boss layers; continuous actual fire/reload and optional real movement/swap/shield. Entrance and archive are fixtures; no forced phase, special cast, direct damage or health restoration during combat. Matched recoil-compensated automated aiming in both conditions, fixed seed recorded in report; not human playtesting.";public int seed;public string specialization="Default breach rank0; its normal post-swap damage window is part of the counter strategy";public bool passed;public List<Run> runs=new List<Run>();public List<string> errors=new List<string>();}
  int seed=505;Report report=new Report();string output;float started;bool done;Keyboard keys;Mouse mouse;
  DemoGame G=>DemoGame.I;PlayerMotor P=>G.player;
  void Awake(){started=Time.realtimeSinceStartup;Application.logMessageReceived+=Error;}
  void Error(string m,string s,LogType t){if(t==LogType.Error||t==LogType.Exception||t==LogType.Assert)report.errors.Add(m);}
  void Update(){if(!done&&Time.realtimeSinceStartup-started>150){report.errors.Add("150s watchdog");Finish();}}
  void Aim(Vector3 point){var d=point-P.Eye.position;P.SetLook(Mathf.Atan2(d.x,d.z)*Mathf.Rad2Deg-P.MechanicalRecoil.y,-Mathf.Atan2(d.y,new Vector2(d.x,d.z).magnitude)*Mathf.Rad2Deg+P.MechanicalRecoil.x);}
  IEnumerator Fight(bool counter)
  {
   if(G.expeditionActive&&G.expeditionReceipt==null)G.FinishExpedition(false);G.OpenExpeditionBoard();G.BeginExpedition(11);G.qaSuppressAI=true;yield return new WaitForSeconds(3.5f);G.ClearDeployments();
   foreach(var old in G.enemies)if(old)old.gameObject.SetActive(false);
   UnityEngine.Random.InitState(seed);P.ResetAt(new Vector3(0,.03f,47));var enemy=EnemyActor.Spawn(EnemyKind.Elite,new Vector3(0,.03f,55),0);var boss=enemy.boss;
   var run=new Run{strategy=counter?"Move + swap + directional shield":"Stationary body-fire baseline"};G.qaSuppressAI=false;float start=Time.time;int casts=0;
   while(G.IsPlaying&&enemy.Alive&&Time.time-start<35)
   {
    Aim(enemy.AimPoint);InputSystem.QueueStateEvent(keys,new KeyboardState());
    if(counter)
    {
     if(boss.Phase==BossPhase.EnergyMelee&&Vector3.Distance(P.transform.position,enemy.transform.position)<4.5f)InputSystem.QueueStateEvent(keys,new KeyboardState(Key.S));
     if((boss.SkillStep==BossSkillStep.Telegraph||boss.SkillStep==BossSkillStep.Execute)&&boss.Skill==BossSkill.OmniFire)P.tactics.ActivateShield();
     if((boss.SkillStep==BossSkillStep.Telegraph||boss.SkillStep==BossSkillStep.Execute)&&(boss.Skill==BossSkill.Attraction||boss.Skill==BossSkill.SweepLaser||boss.Skill==BossSkill.GroundWave)&&P.cooldown<=0)
     {
      bool exposed=boss.Skill==BossSkill.Attraction?Vector3.Distance(P.transform.position,boss.DangerCenter)<4.8f:P.transform.position.y<1.2f;
      if(exposed){PhaseAnchor best=null;float height=-100;foreach(var anchor in G.expeditionMap.anchors)if(anchor&&anchor.transform.position.y>height&&P.ValidateSwap(anchor)=="可换位"){best=anchor;height=anchor.transform.position.y;}if(best)P.RequestSwap(best);}
     }
    }
    if(P.ammo[P.weapon]<=0)P.Reload();else P.Fire();
    if(boss.SpatialCasts>casts){casts=boss.SpatialCasts;if(run.firstSpecial<0)run.firstSpecial=Time.time-start;if(!run.skills.Contains(boss.Skill.ToString()))run.skills.Add(boss.Skill.ToString());}
    yield return null;
   }
   run.seconds=Time.time-start;InputSystem.QueueStateEvent(keys,new KeyboardState());
   if(G.IsPlaying&&!enemy.Alive)yield return new WaitForSecondsRealtime(.75f);
   run.won=!enemy.Alive&&P.health>0;run.playerHealth=P.health;run.bossHealth=enemy.health;run.energy=boss.Energy;run.shots=G.totalShots;run.swaps=G.totalSwaps;run.shieldBlocks=P.tactics.ShieldBlocks;run.casts=boss.SpatialCasts;
   report.runs.Add(run);Debug.Log("NATURAL_BOSS "+JsonUtility.ToJson(run));
  }
  IEnumerator Start()
  {
   var a=Environment.GetCommandLineArgs();int i=Array.IndexOf(a,"-qaOutput");output=i>=0?a[i+1]:RunStorage.Root;Directory.CreateDirectory(output);while(!UnityEngine.Rendering.SplashScreen.isFinished)yield return null;
   report.version=Application.version;int si=Array.IndexOf(a,"-bossSeed");if(si>=0&&si+1<a.Length)int.TryParse(a[si+1],out seed);report.seed=seed;for(i=0;i<11;i++){var run=G.expeditionStore.Begin(i);G.expeditionStore.Settle(run,true,false,30);}
   keys=InputSystem.AddDevice<Keyboard>();mouse=InputSystem.AddDevice<Mouse>();G.qaKeyboard=keys;G.qaMouse=mouse;G.qaInputEnabled=true;
   yield return Fight(false);yield return Fight(true);
   report.passed=report.errors.Count==0&&report.runs.Count==2&&report.runs[0].casts>=1&&report.runs[1].won&&report.runs[1].swaps>0&&report.runs[1].shieldBlocks>0&&report.runs[1].playerHealth>report.runs[0].playerHealth+20;
   G.qaKeyboard=null;G.qaMouse=null;G.qaInputEnabled=false;InputSystem.RemoveDevice(keys);InputSystem.RemoveDevice(mouse);Finish();
  }
  void Finish(){if(done)return;done=true;Time.timeScale=1;File.WriteAllText(Path.Combine(output,"natural-boss-report.json"),JsonUtility.ToJson(report,true));Application.Quit(report.passed?0:1);}
  void OnDestroy(){Application.logMessageReceived-=Error;}
 }
}
