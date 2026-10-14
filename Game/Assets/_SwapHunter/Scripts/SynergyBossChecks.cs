using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.AI.Navigation;
namespace SwapHunter
{
    // Real Update/time, Damage/ApplySilence and projectile/area paths on a small isolated floor.
    // Explicit placement, phase damage and large fixture HP do not claim a natural Boss playthrough.
    public static class SynergyBossChecks
    {
        public static IEnumerator Run(Action<string,bool,string> add) { return new Fixture(add).Run(); }
        sealed class Fixture
        {
            readonly Action<string,bool,string> add;
            DemoGame G=>DemoGame.I; PlayerMotor P=>G.player;
            Transform floor;EnemyActor actor;BossEncounter boss;PhaseAnchor anchor;
            readonly Vector3 origin=new Vector3(1000,0,0);
            readonly List<GameObject> extra=new List<GameObject>();
            public Fixture(Action<string,bool,string> add){this.add=add;}
            void C(string id,bool pass,string detail=""){add(id,pass,detail);}
            static bool Close(float a,float b,float tolerance=.03f)=>Mathf.Abs(a-b)<tolerance;
            void DestroyActor()
            {
                if(actor){G.enemies.Remove(actor);actor.gameObject.SetActive(false);UnityEngine.Object.Destroy(actor.gameObject);}
                actor=null;boss=null;
            }
            IEnumerator Fresh(BossPhase phase)
            {
                G.qaSuppressAI=true;DestroyActor();
                foreach(var shot in UnityEngine.Object.FindObjectsByType<Bolt>(FindObjectsSortMode.None))shot.Retire();
                anchor.MoveTo(origin+new Vector3(0,.03f,5));
                P.ResetAt(origin+new Vector3(0,.03f,7));P.health=10000;P.SetLook(180,0);
                Physics.SyncTransforms();yield return null;
                actor=EnemyActor.Spawn(EnemyKind.Elite,origin+new Vector3(0,.03f,-8),0);
                if(!actor)throw new InvalidOperationException("Boss fixture failed to spawn on its own navigation surface.");
                boss=actor.EnableBoss();
                if(phase!=BossPhase.EnergyMelee)actor.Damage(BossEncounter.ShieldMaximum);
                if(phase==BossPhase.Combined)actor.Damage(BossEncounter.HealthMaximum*.5f+1);
                yield return null;
            }
            bool Begin(BossSkill skill)
            {
                bool began=boss.TryBeginSkill(skill);C("B_fixture_begin_"+skill,began&&boss.SkillStep==BossSkillStep.Telegraph,boss.Warning);
                G.qaSuppressAI=false;return began;
            }
            IEnumerator UntilExecute()
            {
                float deadline=Time.time+6;
                while(actor&&actor.Alive&&boss.SkillStep==BossSkillStep.Telegraph&&Time.time<deadline)yield return null;
                C("B_fixture_reaches_execute",boss.SkillStep==BossSkillStep.Execute,boss.Skill+";step="+boss.SkillStep);
            }
            IEnumerator UntilRecover(bool repeatedStagger=false)
            {
                float deadline=Time.time+8;
                while(actor&&actor.Alive&&(boss.SkillStep==BossSkillStep.Telegraph||boss.SkillStep==BossSkillStep.Execute)&&Time.time<deadline)
                {if(repeatedStagger)actor.MarkPhase(5);yield return null;}
                C("B_fixture_reaches_recovery",boss.SkillStep==BossSkillStep.Recover,boss.Skill+";step="+boss.SkillStep);
            }
            public IEnumerator Run()
            {
                if(!RunStorage.IsValidation)throw new InvalidOperationException("Boss contracts require isolated validation storage.");
                try
                {
                    if(G.expeditionActive){G.AbandonExpedition();G.OpenExpeditionBoard();}
                    G.StartRun(false);G.qaSuppressAI=true;G.ClearDeployments();
                    foreach(var enemy in G.enemies)if(enemy)enemy.gameObject.SetActive(false);
                    floor=new GameObject("SSU Boss contract fixture").transform;
                    Shapes.Box("Owned test floor",floor,origin+Vector3.down*.5f,new Vector3(70,1,70),1,true,Layers.World);
                    var nav=floor.gameObject.AddComponent<NavMeshSurface>();nav.collectObjects=CollectObjects.Children;
                    nav.layerMask=Layers.WorldMask;nav.useGeometry=UnityEngine.AI.NavMeshCollectGeometry.PhysicsColliders;
                    RuntimeNavigationOwner.BuildFresh(nav);
                    var go=new GameObject("SSU exchange anchor");go.transform.SetParent(floor);go.layer=Layers.Actor;
                    anchor=go.AddComponent<PhaseAnchor>();var capsule=go.AddComponent<CapsuleCollider>();capsule.height=1.4f;capsule.radius=.32f;capsule.center=Vector3.up*.7f;
                    G.anchors.Add(anchor);Physics.SyncTransforms();
                    yield return ClockAndSilence();
                    yield return PhaseAndDeath();
                    yield return MarkAndDamage();
                }
                finally
                {
                    G.qaSuppressAI=true;DestroyActor();if(anchor)G.anchors.Remove(anchor);
                    foreach(var go in extra)if(go){go.SetActive(false);UnityEngine.Object.Destroy(go);}
                    if(floor){floor.gameObject.SetActive(false);UnityEngine.Object.Destroy(floor.gameObject);}
                    G.ClearDeployments();G.SetState(RunState.Menu);
                }
                yield return null;
            }
            IEnumerator ClockAndSilence()
            {
                yield return Fresh(BossPhase.Combined);Begin(BossSkill.SweepLaser);
                var cast=boss.CurrentCast;Vector3 center=boss.DangerCenter,forward=boss.DangerForward;
                float started=Time.time;int casts=boss.SpatialCasts;
                yield return UntilRecover(true);
                C("B1_short_staggers_do_not_freeze_or_relock",boss.SpatialCasts==casts+1&&ReferenceEquals(cast,boss.CurrentCast)
                    &&Vector3.Distance(center,boss.DangerCenter)<.001f&&Vector3.Distance(forward,boss.DangerForward)<.001f
                    &&Time.time-started<=cast.warningSeconds+cast.executionSeconds+.35f,
                    "Repeated real MarkPhase(.2s stagger); elapsed="+(Time.time-started)+"; expected warning+execute="+(cast.warningSeconds+cast.executionSeconds));
                yield return Fresh(BossPhase.Rifle);actor.ApplySilence(3.2f);actor.stunLeft=1.2f;
                int shots=actor.shotsFired;Vector3 before=actor.transform.position;G.qaSuppressAI=false;
                yield return new WaitForSeconds(.35f);
                C("B1_basic_actions_still_staggered",actor.shotsFired==shots&&Vector3.Distance(actor.transform.position,before)<.03f);
                yield return new WaitForSeconds(1.2f);
                C("B1_basic_fire_resumes_even_while_silenced",actor.shotsFired>shots&&actor.Silenced);

                yield return Fresh(BossPhase.Combined);Begin(BossSkill.GroundWave);cast=boss.CurrentCast;
                actor.ApplySilence(3.2f);yield return null;
                C("B2_silence_cancels_only_telegraph_to_recovery",boss.SkillStep==BossSkillStep.Recover&&boss.SpatialCasts==0
                    &&ReferenceEquals(cast,boss.CurrentCast)&&boss.RecoverySecondsLeft>cast.recoverySeconds-.2f&&!boss.DangerOutline);
                float silence=actor.silenceLeft;actor.ApplySilence(3.2f);
                C("B2_repeat_during_silence_does_not_refresh",boss.SilenceAcceptedCount==1&&boss.SilenceRejectedCount==1&&actor.silenceLeft<=silence);
                yield return new WaitForSeconds(2.35f);
                C("B2_recovery_done_but_silence_blocks_next",actor.Silenced&&boss.SpatialCasts==0&&!boss.TryBeginSkill(BossSkill.OmniFire));
                float until=Time.time+2;
                while(actor.Silenced&&Time.time<until)yield return null;
                float resistance=boss.SilenceResistanceLeft;actor.ApplySilence(3.2f);
                C("B2_resistance_4_5_seconds_no_refresh",resistance>4.2f&&resistance<=4.5f&&!actor.Silenced
                    &&boss.SilenceAcceptedCount==1&&boss.SilenceResistanceLeft<=resistance);
                yield return new WaitForSeconds(resistance+.08f);actor.ApplySilence(3.2f);
                C("B2_reaccept_after_resistance",actor.Silenced&&boss.SilenceAcceptedCount==2);

                foreach(var skill in new[]{BossSkill.SweepLaser,BossSkill.GroundWave,BossSkill.OmniFire})
                {
                    yield return Fresh(BossPhase.Combined);Begin(skill);yield return UntilExecute();
                    cast=boss.CurrentCast;int pulses=boss.SpatialPulses;float age=boss.SkillAgeSeconds;
                    actor.ApplySilence(3.2f);yield return UntilRecover(true);
                    C("B2_execute_commitment_"+skill,ReferenceEquals(cast,boss.CurrentCast)&&boss.SkillAgeSeconds>=cast.executionSeconds-.1f
                        &&boss.SkillAgeSeconds>age+.8f&&(skill!=BossSkill.OmniFire||boss.SpatialPulses>=pulses+4),
                        "Silence plus repeated stagger; age="+boss.SkillAgeSeconds+"; pulsesAfter="+(boss.SpatialPulses-pulses));
                }
            }
            IEnumerator PhaseAndDeath()
            {
                yield return Fresh(BossPhase.EnergyMelee);Begin(BossSkill.SwapElbow);
                var cast=boss.CurrentCast;var outline=boss.DangerOutline;float seconds=boss.SkillSeconds;Vector3 destination=boss.SpatialDestination;
                actor.Damage(400);
                C("B3_energy_overflow_400",boss.Energy==0&&Close(actor.health,680)&&boss.Phase==BossPhase.Rifle);
                C("B3_warning_survives_phase_change",boss.SkillStep==BossSkillStep.Telegraph&&ReferenceEquals(cast,boss.CurrentCast)
                    &&cast.phase==BossPhase.EnergyMelee&&boss.DangerOutline==outline&&Close(seconds,boss.SkillSeconds,.001f)
                    &&Vector3.Distance(destination,boss.SpatialDestination)<.001f);
                yield return UntilRecover();C("B3_original_elbow_executes_after_energy_break",boss.SpatialSwaps==1&&boss.SpatialCasts==1);

                yield return Fresh(BossPhase.Rifle);Begin(BossSkill.Attraction);yield return UntilExecute();
                cast=boss.CurrentCast;int pulses=boss.SpatialPulses;actor.Damage(361);
                C("B3_hp_phase_immediate_cast_retained",boss.Phase==BossPhase.Combined&&Close(actor.health,359)
                    &&boss.SkillStep==BossSkillStep.Execute&&ReferenceEquals(cast,boss.CurrentCast)&&cast.phase==BossPhase.Rifle);
                yield return UntilRecover();
                C("B3_old_cast_recovery_and_remaining_pulses",boss.SpatialPulses>pulses&&boss.RecoverySecondsLeft>3.0f&&Close(cast.recoverySeconds,3.2f));
                float deadline=Time.time+5;
                while(ReferenceEquals(cast,boss.CurrentCast)&&Time.time<deadline)yield return null;
                C("B3_next_cast_uses_new_phase",boss.CurrentCast!=null&&!ReferenceEquals(cast,boss.CurrentCast)
                    &&boss.CurrentCast.phase==BossPhase.Combined&&Close(boss.CurrentCast.recoverySeconds,2.2f));

                yield return Fresh(BossPhase.Combined);Begin(BossSkill.OmniFire);yield return UntilExecute();
                var flying=Bolt.Spawn(actor,origin+new Vector3(0,8,0),Vector3.right,4,1);
                int projectiles=boss.SpatialProjectiles,shots=actor.shotsFired;actor.Damage(10000);actor.ShootAt(P.Eye.position,8,38);
                C("B3_death_clears_active_cast",boss.Phase==BossPhase.Defeated&&boss.Skill==BossSkill.None&&boss.CurrentCast==null&&!boss.DangerOutline);
                yield return new WaitForSeconds(.35f);
                C("B3_dead_boss_no_later_attacks",boss.SpatialProjectiles==projectiles&&actor.shotsFired==shots);
                C("B3_existing_flight_survives_owner_death",flying&&!flying.IsRetired&&flying.life<3&&flying.transform.position.x>origin.x);
                if(flying)flying.Retire();
            }
            IEnumerator MeasureDamage(float[] result)
            {
                actor.health=1000;actor.Hit(10,actor.AimPoint,actor.TorsoHitbox,P.Eye.position);result[0]=1000-actor.health;
                actor.health=1000;actor.Hit(24,actor.AimPoint,actor.TorsoHitbox,P.Eye.position);result[1]=1000-actor.health;
                actor.health=1000;var grenade=PlayerOrdnance.Launch(P,actor.AimPoint,Vector3.forward,40);grenade.Explode();result[2]=1000-actor.health;
                actor.health=1000;ClassSkillArea.Blast(P,actor.AimPoint,40);result[3]=1000-actor.health;
                actor.health=1000;var knife=ThrownKnife.Launch(P.combatTools,P,actor.AimPoint+Vector3.back*2,Vector3.forward,1,true);extra.Add(knife.gameObject);
                float deadline=Time.time+1;
                while(knife&&knife.OutboundHits==0&&Time.time<deadline)yield return null;
                result[4]=1000-actor.health;if(knife){knife.gameObject.SetActive(false);UnityEngine.Object.Destroy(knife.gameObject);}
                actor.health=1000;var field=ClassDenialField.Create(P,actor.transform.position,16);extra.Add(field.gameObject);
                deadline=Time.time+1;while(field.TicksApplied==0&&Time.time<deadline)yield return null;
                result[5]=field.TicksApplied>0?(1000-actor.health)/field.TicksApplied:0;field.Cancel();
            }
            IEnumerator MarkAndDamage()
            {
                G.qaSuppressAI=true;DestroyActor();
                foreach(var shot in UnityEngine.Object.FindObjectsByType<Bolt>(FindObjectsSortMode.None))shot.Retire();
                P.ResetAt(origin+new Vector3(0,.03f,12));P.health=10000;
                actor=EnemyActor.Spawn(EnemyKind.Assault,origin+new Vector3(0,.03f,-6),0);actor.suppressAI=true;
                if(actor.agent.enabled)actor.agent.isStopped=true;
                float[] plain=new float[6],marked=new float[6];yield return MeasureDamage(plain);
                actor.MarkPhase(5);yield return MeasureDamage(marked);
                C("D1_gun_and_melee_hit_gateway_marked",Close(plain[0],10)&&Close(plain[1],24)&&Close(marked[0],12)&&Close(marked[1],28.8f),
                    "Real Hit/Torso gateway shared by guns and melee; explicit hit geometry, not input timing.");
                string[] sources={"grenade","remote_blast","flying_knife","denial_tick"};float[] expected={40,40,28,4};
                for(int i=2;i<6;i++)C("D1_mark_does_not_scale_"+sources[i-2],Close(plain[i],expected[i-2])&&Close(marked[i],plain[i]),"plain="+plain[i]+";marked="+marked[i]);
                P.SetLook(0,0);bool shield=P.tactics.ActivateShield();float hp=P.health;Vector3 source=P.Eye.position+Vector3.forward*5;
                P.Hurt(10,source,true,PlayerDamageKind.Spatial);
                C("D1_spatial_kind_can_still_be_shield_blockable",shield&&P.health==hp&&P.tactics.ShieldBlocks>0);
                P.Hurt(10,source,false,PlayerDamageKind.Normal);
                C("D1_normal_kind_does_not_force_shield_block",P.health<hp);
            }
        }
    }
}
