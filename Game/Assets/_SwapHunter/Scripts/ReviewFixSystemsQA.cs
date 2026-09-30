using System.Collections;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

namespace SwapHunter
{
 public sealed partial class ReviewFixQA
 {
  readonly Vector3 systemsCenter=new Vector3(300,0,0);
  Transform systemsFixture;
  PhaseAnchor systemsHighAnchor;
  [System.Serializable] sealed class ContinuousSilenceResult
  {
   public string kind="Nonlethal control fixture: forced Combined phase, Boss HP restored to 359 and player HP to 100 every frame; actual Throw/Recall and knife collisions, no guns or swaps. Owner AI suppressed and Boss.Tick driven once per frame. Not natural combat or a complete chapter.";
   public float plannedSeconds=21,seconds,silencedSeconds,silenceCoverage,initialSwapCooldown;
   public int throws,returnHits,acceptedSilences,rejectedSilences,completedSpecials,canceledSpecials;
   public bool survived,passed;
   public string acceptanceCountMeaning="Boss TryReceiveSilence calls, including the rank-two area effect's repeated application to its directly hit target; not unique knife throws.";
   public System.Collections.Generic.List<string> completedSkills=new System.Collections.Generic.List<string>();
   public System.Collections.Generic.List<string> canceledSkills=new System.Collections.Generic.List<string>();
  }

  IEnumerator SystemsChecks()
  {
   if(G.campaignActive)G.FinishContract(false);
   if(G.expeditionActive){if(G.expeditionReceipt==null)G.FinishExpedition(false);G.OpenExpeditionBoard();}
   int oldDifficulty=G.difficulty;bool oldSuppress=G.qaSuppressAI;
   G.StartRun(true);G.combatComplete=true;G.qaSuppressAI=true;G.difficulty=1;ClearActors();
   systemsFixture=new GameObject("Review fix systems arena").transform;
   Shapes.Box("Systems floor",systemsFixture,systemsCenter+Vector3.down*.5f,new Vector3(64,1,64),1,true,Layers.World);
   Shapes.Box("Systems high platform",systemsFixture,systemsCenter+new Vector3(8,1.5f,6),new Vector3(4,3,4),1,true,Layers.World);
   var nav=systemsFixture.gameObject.AddComponent<NavMeshSurface>();nav.collectObjects=CollectObjects.Children;
   nav.layerMask=Layers.WorldMask;nav.useGeometry=NavMeshCollectGeometry.PhysicsColliders;nav.BuildNavMesh();
   var go=new GameObject("Systems high exchange anchor");go.layer=Layers.Actor;go.transform.SetParent(systemsFixture);
   go.transform.position=systemsCenter+new Vector3(8,3.03f,5);
   systemsHighAnchor=go.AddComponent<PhaseAnchor>();var capsule=go.AddComponent<CapsuleCollider>();capsule.radius=.32f;capsule.height=1.4f;capsule.center=Vector3.up*.7f;
   ImportedModels.Create(G.config.models.beacon,go.transform,Vector3.zero);G.anchors.Add(systemsHighAnchor);G.anchorOrigins.Add(go.transform.position);
   go.SetActive(false);Physics.SyncTransforms();yield return null;

   yield return SystemsKnifeReturn(false,true);
   yield return SystemsKnifeReturn(true,true);
   yield return SystemsKnifeReturn(false,false);
   foreach(string mode in new[]{"no_shield","front_shield","side_shield","high_platform"})yield return SystemsGroundWave(mode);
   yield return SystemsHandsAndGun();
   yield return SystemsSilenceWindows();
   yield return SystemsContinuousKnifeControl();
   yield return SystemsOpening(BossPhase.Rifle,"ground",false,BossSkill.Attraction);
   yield return SystemsOpening(BossPhase.Rifle,"high",true,BossSkill.AdvantageSwap);
   yield return SystemsOpening(BossPhase.Rifle,"high",false,BossSkill.Attraction);
   yield return SystemsOpening(BossPhase.Combined,"high",false,BossSkill.OmniFire);
   yield return SystemsOpening(BossPhase.Combined,"near",false,BossSkill.GroundWave);
   yield return SystemsOpening(BossPhase.Combined,"far",false,BossSkill.SweepLaser);
   yield return SystemsRotationAfterOpening();

   ClearActors();int index=G.anchors.IndexOf(systemsHighAnchor);
   if(index>=0){G.anchors.RemoveAt(index);G.anchorOrigins.RemoveAt(index);}
   systemsFixture.gameObject.SetActive(false);Destroy(systemsFixture.gameObject);systemsFixture=null;systemsHighAnchor=null;
   G.difficulty=oldDifficulty;G.qaSuppressAI=oldSuppress;G.SetState(RunState.Menu);
  }

  IEnumerator SystemsReset(Vector3 feet)
  {
   G.qaSuppressAI=true;ClearActors();P.ResetAt(feet);P.SetLook(0,0);G.SetState(RunState.Playing);
   systemsHighAnchor.gameObject.SetActive(false);systemsHighAnchor.MoveTo(systemsCenter+new Vector3(8,3.03f,5));
   Physics.SyncTransforms();yield return new WaitForSeconds(.1f);
  }

  EnemyActor SystemsBoss(BossPhase phase)
  {
   var enemy=EnemyActor.Spawn(EnemyKind.Elite,systemsCenter+Vector3.up*.03f,G.stage);
   if(!enemy){Check("systems_boss_spawn",false,"No valid spawn on the dedicated baked arena");return null;}
   enemy.transform.rotation=Quaternion.identity;enemy.EnableBoss();
   if(phase!=BossPhase.EnergyMelee)enemy.Damage(BossEncounter.ShieldMaximum);
   if(phase==BossPhase.Combined)enemy.Damage(BossEncounter.HealthMaximum*.5f+1);
   Physics.SyncTransforms();return enemy;
  }

  IEnumerator SystemsKnifeReturn(bool shieldFirst,bool physicalShield)
  {
   float sign=shieldFirst?-1:1;
   yield return SystemsReset(systemsCenter+new Vector3(0,.03f,6*sign));P.SetLook(sign>0?180:0,0);
   var guard=EnemyActor.Spawn(physicalShield?EnemyKind.Shield:EnemyKind.Training,systemsCenter+Vector3.up*.03f,G.stage);
   var second=EnemyActor.Spawn(EnemyKind.Training,systemsCenter+new Vector3(0,.03f,3*sign),G.stage);
   string id=physicalShield?(shieldFirst?"return_shield_first_stops":"return_body_then_shield_stops"):"return_unshielded_bodies_each_once";
   if(!guard||!second){Check(id,false,"Actor spawn failed");yield break;}
   guard.transform.rotation=Quaternion.identity;Physics.SyncTransforms();
   float guardBefore=guard.health,secondBefore=second.health;
   var blade=ThrownKnife.Launch(P.combatTools,P,systemsCenter+new Vector3(0,1.4f,-4*sign),Vector3.forward*sign,3);blade.BeginReturn();
   int wallStops=0,hits=0;float until=Time.time+.8f;
   while(Time.time<until){if(blade){wallStops=Mathf.Max(wallStops,blade.WallStops);hits=Mathf.Max(hits,blade.RecallHits);}yield return null;}
   bool expected=physicalShield?
    wallStops>0&&second.health==secondBefore&&Mathf.Abs(guard.health-(guardBefore-(shieldFirst?0:22)))<.01f&&hits==(shieldFirst?0:1):
    wallStops==0&&Mathf.Abs(guard.health-(guardBefore-22))<.01f&&Mathf.Abs(second.health-(secondBefore-22))<.01f&&hits==2;
   Check(id,expected,"Placed return-only blade; guard="+guardBefore+"->"+guard.health+"; second="+secondBefore+"->"+second.health+"; stops="+wallStops+"; hits="+hits);
   yield return Capture("systems-"+id);
  }

  IEnumerator SystemsGroundWave(string mode)
  {
   bool high=mode=="high_platform";
   yield return SystemsReset(systemsCenter+(high?new Vector3(8,3.03f,6):new Vector3(0,.03f,8)));
   var enemy=SystemsBoss(BossPhase.Combined);if(!enemy)yield break;var boss=enemy.boss;
   P.SetLook(mode=="side_shield"?90:180,0);bool needsShield=mode=="front_shield"||mode=="side_shield";
   bool opened=needsShield&&P.tactics.ActivateShield();bool began=boss.TryBeginSkill(BossSkill.GroundWave);
   float before=P.health,until=Time.time+5;
   while(began&&Time.time<until&&(boss.SkillStep==BossSkillStep.Telegraph||boss.SkillStep==BossSkillStep.Execute))
   {boss.Tick(Time.deltaTime);yield return null;}
   float expected=high?before:before-26;
   Check("groundwave_"+mode,began&&(!needsShield||opened)&&boss.SkillStep==BossSkillStep.Recover&&Mathf.Abs(P.health-expected)<.01f&&P.tactics.ShieldBlocks==0,
    "Forced GroundWave only; began="+began+"; shield="+opened+"; before="+before+"; after="+P.health+"; blocks="+P.tactics.ShieldBlocks+"; feetY="+P.transform.position.y+"; phase="+boss.SkillStep);
   yield return Capture("systems-groundwave-"+mode);
  }

  IEnumerator SystemsHandsAndGun()
  {
   yield return SystemsReset(systemsCenter+new Vector3(0,.03f,8));
   int ammo=P.ammo[0];bool melee=P.combatTools.Melee(),fired=P.Fire();
   Check("melee_pose_blocks_gun",melee&&P.combatTools.PoseActive&&!fired&&P.ammo[0]==ammo,"melee="+melee+"; fired="+fired+"; ammo="+P.ammo[0]);
   yield return Capture("systems-melee-gun-lock");yield return new WaitForSeconds(.42f);
   Check("gun_resumes_after_melee",!P.combatTools.PoseActive&&P.Fire()&&P.ammo[0]==ammo-1,"ammo="+P.ammo[0]);

   yield return SystemsReset(systemsCenter+new Vector3(0,.03f,8));
   ammo=P.ammo[0];bool thrown=P.combatTools.Throw();fired=P.Fire();
   Check("throw_pose_blocks_gun",thrown&&P.combatTools.PoseActive&&!fired&&P.ammo[0]==ammo,"throw="+thrown+"; fired="+fired+"; ammo="+P.ammo[0]);
   yield return new WaitForSeconds(.34f);
   Check("gun_resumes_after_throw",!P.combatTools.PoseActive&&P.Fire()&&P.ammo[0]==ammo-1,"ammo="+P.ammo[0]);

   yield return SystemsReset(systemsCenter+new Vector3(0,.03f,8));P.SetLoadout(2,1);yield return new WaitForSeconds(.23f);
   ammo=P.ammo[2];bool burst=P.Fire();yield return new WaitForSeconds(.03f);melee=P.combatTools.Melee();yield return new WaitForSeconds(.22f);
   Check("tool_interrupts_pending_burst",burst&&melee&&P.combatTools.PoseActive&&P.ammo[2]==ammo-1,"started="+burst+"; melee="+melee+"; rounds spent="+(ammo-P.ammo[2]));

   yield return SystemsReset(systemsCenter+new Vector3(0,.03f,8));P.SetLoadout(10,1);yield return new WaitForSeconds(.23f);
   ammo=P.ammo[10];bool charge=P.Fire();yield return new WaitForSeconds(.1f);thrown=P.combatTools.Throw();yield return new WaitForSeconds(.75f);
   Check("tool_cancels_pending_charge_without_ammo",charge&&thrown&&P.ammo[10]==ammo&&P.ChargeProgress==0,"charge="+charge+"; throw="+thrown+"; ammo="+P.ammo[10]+"; chargeProgress="+P.ChargeProgress);

   yield return SystemsReset(systemsCenter+new Vector3(0,.03f,8));ammo=P.ammo[0];bool shield=P.tactics.ActivateShield();fired=P.Fire();
   Check("a_shield_does_not_lock_gun",shield&&fired&&P.ammo[0]==ammo-1,"shield="+shield+"; fired="+fired);
   float health=P.health;Vector3 source=P.Eye.position+Vector3.forward*6;P.Hurt(8,source);
   Check("normal_direct_damage_still_blocked",shield&&P.health==health&&P.tactics.ShieldBlocks==1,"health="+P.health+"; blocks="+P.tactics.ShieldBlocks);
   Bolt.Spawn(null,source,P.Eye.position-source,24,8);yield return new WaitForSeconds(.4f);
   Check("normal_projectile_still_blocked",P.health==health&&P.tactics.ShieldBlocks==2,"health="+P.health+"; blocks="+P.tactics.ShieldBlocks);
  }

  IEnumerator SystemsSilenceWindows()
  {
   yield return SystemsReset(systemsCenter+new Vector3(0,.03f,8));var enemy=SystemsBoss(BossPhase.Rifle);if(!enemy)yield break;var boss=enemy.boss;
   bool began=boss.TryBeginSkill(BossSkill.Attraction);Aim(enemy.AimPoint);bool thrown=P.combatTools.Throw();float deadline=Time.time+1;
   while(!enemy.Silenced&&Time.time<deadline)yield return null;
   boss.Tick(Time.deltaTime);
   Check("actual_knife_interrupts_boss_special",began&&thrown&&enemy.Silenced&&boss.SkillStep==BossSkillStep.Ready,"throw="+thrown+"; silence="+enemy.silenceLeft+"; step="+boss.SkillStep);
   yield return new WaitForSeconds(.6f);float remaining=enemy.silenceLeft;enemy.ApplySilence(3.2f);
   Check("boss_silence_does_not_refresh",remaining>0&&remaining<2.8f&&Mathf.Abs(enemy.silenceLeft-remaining)<.001f,"before="+remaining+"; after="+enemy.silenceLeft);
   int shots=enemy.shotsFired;deadline=Time.time+4;
   while(enemy.Silenced&&Time.time<deadline){boss.Tick(Time.deltaTime);yield return null;}
   Check("silenced_boss_keeps_basic_rifle",enemy.shotsFired>shots,"shots before="+shots+"; after="+enemy.shotsFired+"; playerHP="+P.health);
   float resistance=boss.SilenceResistanceLeft;enemy.ApplySilence(3.2f);
   Check("boss_has_four_point_five_second_silence_resistance",!enemy.Silenced&&resistance>4.3f&&resistance<=4.6f,"resistance="+resistance+"; silence="+enemy.silenceLeft);
   boss.Tick(Time.deltaTime);
   Check("boss_specials_resume_during_resistance",!enemy.Silenced&&boss.SilenceResistanceLeft>0&&(boss.SkillStep==BossSkillStep.Telegraph||boss.SkillStep==BossSkillStep.Execute),"resistance="+boss.SilenceResistanceLeft+"; skill="+boss.Skill+"; step="+boss.SkillStep);
   yield return Capture("systems-boss-silence-resistance");
   // Leave the already-locked field while measuring the longer immunity window; no timer or health reset here.
   P.SetFeet(systemsCenter+new Vector3(-8,.03f,8));P.verticalSpeed=0;deadline=Time.time+4.9f;
   while(boss.SilenceResistanceLeft>0&&Time.time<deadline){enemy.ApplySilence(3.2f);boss.Tick(Time.deltaTime);yield return null;}
   bool resistedToEnd=!enemy.Silenced&&boss.SilenceResistanceLeft<=0;enemy.ApplySilence(3.2f);boss.Tick(Time.deltaTime);
   bool newSkillBlocked=!boss.TryBeginSkill(BossSkill.Attraction);
   Check("boss_silence_returns_after_resistance",resistedToEnd&&enemy.Silenced&&enemy.silenceLeft>3&&newSkillBlocked,"resistedToEnd="+resistedToEnd+"; silence="+enemy.silenceLeft+"; newSkillBlocked="+newSkillBlocked+"; step="+boss.SkillStep);

   yield return SystemsReset(systemsCenter+new Vector3(0,.03f,8));var normal=EnemyActor.Spawn(EnemyKind.Assault,systemsCenter+Vector3.up*.03f,G.stage);
   if(!normal){Check("ordinary_silence_refresh",false,"Actor spawn failed");yield break;}
   normal.transform.rotation=Quaternion.identity;normal.ApplySilence(3.2f);yield return new WaitForSeconds(.6f);remaining=normal.silenceLeft;normal.ApplySilence(3.2f);
   Check("ordinary_silence_refresh",remaining<2.8f&&normal.silenceLeft>=3.19f,"before="+remaining+"; after="+normal.silenceLeft);
   shots=normal.shotsFired;G.qaSuppressAI=false;yield return new WaitForSeconds(.8f);G.qaSuppressAI=true;
   Check("ordinary_silence_keeps_basic_rifle",normal.Silenced&&normal.shotsFired>shots,"silence="+normal.silenceLeft+"; shots="+normal.shotsFired);
  }

  IEnumerator SystemsContinuousKnifeControl()
  {
   yield return SystemsReset(systemsCenter+new Vector3(0,.03f,8));var enemy=SystemsBoss(BossPhase.Combined);if(!enemy)yield break;var boss=enemy.boss;
   var sample=new ContinuousSilenceResult{initialSwapCooldown=P.cooldown};float start=Time.time;
   int acceptedBefore=boss.SilenceAcceptedCount,rejectedBefore=boss.SilenceRejectedCount;
   while(Time.time-start<sample.plannedSeconds&&G.IsPlaying&&enemy.Alive)
   {
    // Health restoration isolates repeated crowd-control from death; it does not alter skill/silence timers.
    enemy.health=359;P.health=100;Aim(enemy.AimPoint);
    if(enemy.Silenced)sample.silencedSeconds+=Time.deltaTime;
    var knife=P.combatTools.Knife;
    if(!knife)P.combatTools.Throw();
    else if(!knife.Flying&&!knife.Returning)P.combatTools.Recall();
    BossSkill previousSkill=boss.Skill;BossSkillStep previousStep=boss.SkillStep;
    boss.Tick(Time.deltaTime);
    // A failed exchange can jump Telegraph -> Recover; only Execute -> Recover is a completed special.
    if(previousStep==BossSkillStep.Execute&&boss.SkillStep==BossSkillStep.Recover&&boss.Skill==previousSkill)
    {sample.completedSpecials++;sample.completedSkills.Add(previousSkill.ToString());}
    else if((previousStep==BossSkillStep.Telegraph||previousStep==BossSkillStep.Execute)&&boss.SkillStep==BossSkillStep.Ready&&boss.Skill==BossSkill.None)
    {sample.canceledSpecials++;sample.canceledSkills.Add(previousSkill.ToString());}
    yield return null;
   }
   sample.seconds=Time.time-start;sample.silenceCoverage=sample.seconds>0?Mathf.Clamp01(sample.silencedSeconds/sample.seconds):0;
   sample.throws=P.combatTools.Throws;sample.returnHits=P.combatTools.ReturnHits;
   sample.acceptedSilences=boss.SilenceAcceptedCount-acceptedBefore;sample.rejectedSilences=boss.SilenceRejectedCount-rejectedBefore;
   sample.survived=G.IsPlaying&&enemy.Alive;
   sample.passed=sample.survived&&sample.seconds>=20.5f&&sample.seconds<=24&&sample.throws>=6&&sample.acceptedSilences>=2&&sample.rejectedSilences>0&&sample.completedSpecials>=1;
   System.IO.File.WriteAllText(System.IO.Path.Combine(output,"boss-continuous-silence.json"),JsonUtility.ToJson(sample,true));
   Check("continuous_knife_allows_completed_boss_special",sample.passed,
    "Nonlethal control fixture with HP restoration; seconds="+sample.seconds.ToString("F2")+"; throws="+sample.throws+"; coverage="+sample.silenceCoverage.ToString("F3")+"; accepted="+sample.acceptedSilences+"; rejected="+sample.rejectedSilences+"; completed="+sample.completedSpecials+" ["+string.Join(",",sample.completedSkills)+"]; canceled="+sample.canceledSpecials+"; initialSwapCooldown="+sample.initialSwapCooldown);
   yield return Capture("systems-continuous-knife-control");
  }

  IEnumerator SystemsOpening(BossPhase phase,string situation,bool anchorAvailable,BossSkill expected)
  {
   Vector3 offset=situation=="high"?new Vector3(8,3.03f,7.4f):new Vector3(0,.03f,situation=="far"?20:8);
   yield return SystemsReset(systemsCenter+offset);systemsHighAnchor.gameObject.SetActive(anchorAvailable);Physics.SyncTransforms();
   var enemy=SystemsBoss(phase);if(!enemy)yield break;var boss=enemy.boss;yield return new WaitForSeconds(.2f);
   // No TryBeginSkill here: the normal scheduler chooses from the real spatial state.
   boss.Tick(Time.deltaTime);
   bool valid=boss.Skill==expected&&boss.SkillStep==BossSkillStep.Telegraph&&boss.SkillAllowed(boss.Skill)&&!string.IsNullOrEmpty(boss.Warning);
   if(expected==BossSkill.AdvantageSwap)valid&=boss.SpatialTarget==systemsHighAnchor;
   Check("opening_"+phase+"_"+situation+"_anchor_"+anchorAvailable,valid,"expected="+expected+"; actual="+boss.Skill+"; step="+boss.SkillStep+"; warning="+boss.Warning);
   Aim(enemy.AimPoint);yield return Capture("systems-opening-"+phase+"-"+situation+"-"+anchorAvailable);
  }

  IEnumerator SystemsRotationAfterOpening()
  {
   yield return SystemsReset(systemsCenter+new Vector3(0,.03f,8));var enemy=SystemsBoss(BossPhase.Combined);if(!enemy)yield break;var boss=enemy.boss;
   yield return new WaitForSeconds(.2f);boss.Tick(Time.deltaTime);bool openedWave=boss.Skill==BossSkill.GroundWave;float deadline=Time.time+8;
   while(Time.time<deadline&&G.IsPlaying&&(boss.Skill!=BossSkill.SweepLaser||boss.SkillStep!=BossSkillStep.Telegraph))
   {boss.Tick(Time.deltaTime);yield return null;}
   Check("contextual_opening_continues_rotation",openedWave&&boss.Skill==BossSkill.SweepLaser&&boss.SkillStep==BossSkillStep.Telegraph&&boss.SpatialCasts>=1,
    "firstWave="+openedWave+"; next="+boss.Skill+"; step="+boss.SkillStep+"; casts="+boss.SpatialCasts+"; playerHP="+P.health);
  }
 }
}
