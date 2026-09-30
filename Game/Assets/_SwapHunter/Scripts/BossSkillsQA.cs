using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Unity.AI.Navigation;
namespace SwapHunter
{
 public sealed class BossSkillsQA:MonoBehaviour
 {
  [Serializable] public class Check {public string name,detail;public bool passed;}
  [Serializable] public class Report {public string kind="Native boss six-skill counterplay; explicit fixture placement and phase setup, real time movement/projectiles/swaps, not human playtesting";public bool passed;public List<Check> checks=new List<Check>();}
  Report report=new Report();string output;bool done;float started;Transform fixture;EnemyActor enemy;BossEncounter boss;PhaseAnchor high,near;
  DemoGame G=>DemoGame.I;PlayerMotor P=>G.player;
  void Awake(){started=Time.realtimeSinceStartup;Application.logMessageReceived+=Error;}
  void Error(string m,string trace,LogType t){if(t==LogType.Error||t==LogType.Exception||t==LogType.Assert)Add("runtime_error",false,m);}
  void Add(string name,bool ok,string detail){report.checks.Add(new Check{name=name,passed=ok,detail=detail});Debug.Log("BOSS_SKILLS_QA "+name+" "+ok+" "+detail);}
  void Update(){if(!done&&Time.realtimeSinceStartup-started>210){Add("watchdog",false,"210s");Finish();}}
  GameObject Box(string name,Vector3 p,Vector3 size)=>Shapes.Box(name,fixture,p,size,1,true,Layers.World);
  PhaseAnchor Anchor(Vector3 p)
  {
   var go=new GameObject("QA exchange anchor");go.transform.SetParent(fixture);go.transform.position=p;go.layer=Layers.Actor;
   var a=go.AddComponent<PhaseAnchor>();var c=go.AddComponent<CapsuleCollider>();c.radius=.32f;c.height=1.4f;c.center=Vector3.up*.7f;
   ImportedModels.Create(G.config.models.beacon,go.transform,Vector3.zero);G.anchors.Add(a);return a;
  }
  void Aim(Vector3 p){var d=p-P.Eye.position;P.SetLook(Mathf.Atan2(d.x,d.z)*Mathf.Rad2Deg,-Mathf.Atan2(d.y,new Vector2(d.x,d.z).magnitude)*Mathf.Rad2Deg);}
  IEnumerator Prepare(BossPhase phase)
  {
   G.qaSuppressAI=true;
   if(enemy){enemy.gameObject.SetActive(false);Destroy(enemy.gameObject);}
   foreach(var b in FindObjectsByType<Bolt>(FindObjectsSortMode.None)){b.gameObject.SetActive(false);Destroy(b.gameObject);}
   high.MoveTo(new Vector3(108,3.03f,4));near.MoveTo(new Vector3(100,.03f,3));
   P.ResetAt(new Vector3(100,.03f,5));P.SetLook(180,0);yield return new WaitForSeconds(.1f);
   enemy=EnemyActor.Spawn(EnemyKind.Elite,new Vector3(100,.03f,-6),0);boss=enemy.EnableBoss();
   if(phase!=BossPhase.EnergyMelee)enemy.Damage(BossEncounter.ShieldMaximum);
   if(phase==BossPhase.Combined)enemy.Damage(BossEncounter.HealthMaximum*.5f+1);
   yield return new WaitForSeconds(.1f);
  }
  bool Begin(BossSkill skill){bool result=boss.TryBeginSkill(skill);Add(skill+"_begins_with_warning",result&&boss.SkillStep==BossSkillStep.Telegraph&&!string.IsNullOrEmpty(boss.Warning),boss.Warning);G.qaSuppressAI=false;return result;}
  IEnumerator EndSkill(){float end=Time.time+10;while(Time.time<end&&(boss.SkillStep==BossSkillStep.Telegraph||boss.SkillStep==BossSkillStep.Execute))yield return null;G.qaSuppressAI=true;Add("skill_finishes_with_recovery",boss.SkillStep==BossSkillStep.Recover,boss.Skill.ToString());}
  IEnumerator Capture(string name){Aim(enemy.AimPoint);yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(output,name+".png"));yield return null;}
  IEnumerator Start()
  {
   var a=Environment.GetCommandLineArgs();int i=Array.IndexOf(a,"-qaOutput");output=i>=0?a[i+1]:RunStorage.Root;Directory.CreateDirectory(output);
   while(!UnityEngine.Rendering.SplashScreen.isFinished)yield return null;
   G.StartRun(false);G.qaSuppressAI=true;yield return new WaitForSeconds(.2f);
   foreach(var e in G.enemies)if(e)e.gameObject.SetActive(false);
   fixture=new GameObject("Boss skill counterplay fixture").transform;
   Box("Arena floor",new Vector3(103,-.5f,0),new Vector3(40,1,48));Box("High swap refuge",new Vector3(108,1.5f,4),new Vector3(3,3,3));
   var surface=fixture.gameObject.AddComponent<NavMeshSurface>();surface.collectObjects=CollectObjects.Children;surface.layerMask=Layers.WorldMask;surface.useGeometry=UnityEngine.AI.NavMeshCollectGeometry.PhysicsColliders;surface.BuildNavMesh();
   high=Anchor(new Vector3(108,3.03f,4));near=Anchor(new Vector3(100,.03f,3));Physics.SyncTransforms();

   yield return Prepare(BossPhase.EnergyMelee);
   Add("phase1_only_elbow_special",boss.SkillAllowed(BossSkill.SwapElbow)&&!boss.SkillAllowed(BossSkill.SweepLaser)&&!boss.SkillAllowed(BossSkill.Attraction),boss.Phase.ToString());
   Begin(BossSkill.SwapElbow);Vector3 oldBoss=enemy.transform.position,oldTarget=boss.SpatialTarget.transform.position;var selected=boss.SpatialTarget;
   yield return Capture("01-elbow-telegraph");yield return EndSkill();
   Add("elbow_real_both_endpoint_exchange",boss.SpatialSwaps==1&&Vector3.Distance(enemy.transform.position,oldTarget)<.15f&&Vector3.Distance(selected.transform.position,oldBoss)<.15f,"boss="+enemy.transform.position+";target="+selected.transform.position);
   Add("elbow_punishes_staying_near_anchor",P.health<100,"health="+P.health);
   yield return Prepare(BossPhase.EnergyMelee);Begin(BossSkill.SwapElbow);yield return new WaitForSeconds(.5f);Aim(high.AimPoint);bool swap=P.RequestSwap(high);yield return EndSkill();
   Add("elbow_escaped_by_player_swap",swap&&P.swapCount==1&&P.health==100,"health="+P.health+";swaps="+P.swapCount);

   yield return Prepare(BossPhase.Rifle);Add("phase2_gates_final_attacks",boss.SkillAllowed(BossSkill.Attraction)&&boss.SkillAllowed(BossSkill.AdvantageSwap)&&!boss.SkillAllowed(BossSkill.GroundWave)&&!boss.SkillAllowed(BossSkill.OmniFire),boss.Phase.ToString());
   Begin(BossSkill.AdvantageSwap);selected=boss.SpatialTarget;oldBoss=enemy.transform.position;oldTarget=selected.transform.position;yield return Capture("02-advantage-telegraph");yield return EndSkill();
   Add("boss_selects_high_advantage_and_exchanges",selected==high&&boss.SpatialSwaps==1&&Vector3.Distance(enemy.transform.position,oldTarget)<.15f&&Vector3.Distance(high.transform.position,oldBoss)<.15f,"chosen="+oldTarget+";boss="+enemy.transform.position);
   yield return Prepare(BossPhase.Rifle);Begin(BossSkill.AdvantageSwap);oldBoss=enemy.transform.position;oldTarget=boss.SpatialTarget.transform.position;
   var blocker=Box("New obstacle after warning",oldTarget+Vector3.up,Vector3.one*1.5f);Physics.SyncTransforms();yield return EndSkill();
   Add("boss_rechecks_obstructed_swap_destination",boss.SpatialSwaps==0&&Vector3.Distance(enemy.transform.position,oldBoss)<.15f,"swaps="+boss.SpatialSwaps);blocker.SetActive(false);Destroy(blocker);

   yield return Prepare(BossPhase.Rifle);Begin(BossSkill.Attraction);yield return EndSkill();Add("attraction_damages_occupied_field",P.health<100,"health="+P.health);
   yield return Prepare(BossPhase.Rifle);P.cooldown=3.5f;Begin(BossSkill.Attraction);Add("control_warning_accounts_for_swap_cooldown",boss.SkillSeconds>=P.cooldown+.4f,"warning="+boss.SkillSeconds+";cooldown="+P.cooldown);
   yield return new WaitForSeconds(3.6f);Aim(high.AimPoint);swap=P.RequestSwap(high);Vector3 locked=boss.DangerCenter;yield return EndSkill();
   Add("swap_breaks_world_fixed_attraction",swap&&P.health==100&&Vector3.Distance(P.transform.position,locked)>4.8f,"health="+P.health+";player="+P.transform.position);

   yield return Prepare(BossPhase.Combined);Begin(BossSkill.SweepLaser);yield return Capture("03-laser-telegraph");yield return EndSkill();Add("laser_hits_stationary_ground_target",P.health<100,"health="+P.health);
   yield return Prepare(BossPhase.Combined);Begin(BossSkill.SweepLaser);yield return new WaitForSeconds(.5f);Aim(high.AimPoint);swap=P.RequestSwap(high);yield return EndSkill();Add("laser_escaped_by_swap",swap&&P.health==100,"health="+P.health);

   yield return Prepare(BossPhase.Combined);Begin(BossSkill.GroundWave);yield return EndSkill();Add("wave_hits_ground_target",P.health<100,"health="+P.health);
   yield return Prepare(BossPhase.Combined);Begin(BossSkill.GroundWave);yield return new WaitForSeconds(1.4f);bool launch=P.Launch();yield return new WaitForSeconds(.65f);yield return Capture("04-wave-airborne");yield return EndSkill();Add("launch_avoids_expanding_ground_wave",launch&&P.health==100,"health="+P.health);

   yield return Prepare(BossPhase.Combined);near.gameObject.SetActive(false);Begin(BossSkill.OmniFire);yield return EndSkill();yield return new WaitForSeconds(.5f);Add("omni_actual_projectiles_hit_exposed_player",P.health<100,"health="+P.health);
   near.gameObject.SetActive(true);yield return Prepare(BossPhase.Combined);near.gameObject.SetActive(false);Begin(BossSkill.OmniFire);yield return new WaitForSeconds(.8f);Aim(enemy.AimPoint);bool shield=P.tactics.ActivateShield();yield return Capture("05-omni-shield");yield return EndSkill();yield return new WaitForSeconds(.5f);
   Add("directional_shield_counters_omni_stream",shield&&P.tactics.ShieldBlocks>0&&P.health==100,"health="+P.health+";blocks="+P.tactics.ShieldBlocks);
   near.gameObject.SetActive(true);yield return Prepare(BossPhase.Combined);near.gameObject.SetActive(false);Begin(BossSkill.OmniFire);var cover=Box("Omni cover",new Vector3(100,1.5f,0),new Vector3(5,3,1));Physics.SyncTransforms();yield return EndSkill();yield return new WaitForSeconds(.5f);Add("solid_cover_counters_omni_stream",P.health==100,"health="+P.health);cover.SetActive(false);Destroy(cover);
   near.gameObject.SetActive(true);G.qaSuppressAI=true;
   SettingsV04QA.Run(G,Add);Finish();
  }
  void Finish(){if(done)return;done=true;report.passed=report.checks.Count>0&&report.checks.TrueForAll(c=>c.passed);File.WriteAllText(Path.Combine(output,"boss-skills-report.json"),JsonUtility.ToJson(report,true));Application.Quit(report.passed?0:1);}
  void OnDestroy(){Application.logMessageReceived-=Error;}
 }
}
