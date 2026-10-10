using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace SwapHunter
{
    // Explicit isolated placements/health/level fixtures; these are not a human combat playthrough.
    public static class TacticalSkillChecks
    {
        public static IEnumerator Run(DemoGame game, Action<string,bool,string> check) => new Suite(game,check).Run();
        sealed class Suite
        {
            readonly DemoGame g; readonly Action<string,bool,string> check;
            readonly List<GameObject> owned = new List<GameObject>();
            readonly Vector3 origin = new Vector3(-11,.03f,3);
            Keyboard keyboard;
            PlayerMotor P => g.player;
            PlayerClassSkill S => P.classSkill;
            ExpeditionStore Store => g.expeditionStore;
            public Suite(DemoGame game,Action<string,bool,string> report){g=game;check=report;}
            void C(string name,bool pass,string detail="")=>check("skills_"+name,pass,detail);
            void Aim(Vector3 point)
            {
                Vector3 d=point-P.Eye.position;
                P.SetLook(Mathf.Atan2(d.x,d.z)*Mathf.Rad2Deg,-Mathf.Atan2(d.y,new Vector2(d.x,d.z).magnitude)*Mathf.Rad2Deg);
            }
            void Place(Vector3 point){P.CancelActions();P.SetFeet(point);P.verticalSpeed=0;P.cooldown=0;Physics.SyncTransforms();}
            void Clear()
            {
                foreach(var go in owned)if(go){go.SetActive(false);UnityEngine.Object.Destroy(go);}owned.Clear();
            }
            GameObject Box(Vector3 center,Vector3 size)
            {
                var go=Shapes.Box("Skill QA obstruction",g.effects,center,size,1,true,Layers.World);owned.Add(go);Physics.SyncTransforms();return go;
            }
            EnemyActor Enemy(EnemyKind kind,Vector3 feet,float health=1000)
            {
                var e=EnemyActor.Spawn(kind,feet,0);
                if(!e)throw new InvalidOperationException("Skill QA could not place a real enemy at "+feet);
                owned.Add(e.gameObject);e.health=e.maximumHealth=health;e.suppressAI=true;e.transform.rotation=Quaternion.Euler(0,180,0);Physics.SyncTransforms();return e;
            }
            PhaseAnchor Anchor(Vector3 feet)
            {
                var go=new GameObject("Skill QA movable anchor");owned.Add(go);go.layer=Layers.Actor;go.transform.position=feet;
                var c=go.AddComponent<CapsuleCollider>();c.center=Vector3.up*.675f;c.height=1.35f;c.radius=.32f;
                var a=go.AddComponent<PhaseAnchor>();Physics.SyncTransforms();return a;
            }
            DamageableObjective Objective(Vector3 center,float hp=200)
            {
                var go=Box(center,new Vector3(.4f,.6f,.4f));var o=go.AddComponent<DamageableObjective>();o.Configure("skill-qa",hp,null);return o;
            }
            IEnumerator Prepare(int skill,int level=1,int general=0,int generalLevel=1,int primary=-1)
            {
                Clear();if(g.expeditionActive&&g.expeditionReceipt==null&&!g.AbandonExpedition())throw new InvalidOperationException("Cannot abandon skill fixture");
                g.OpenExpeditionBoard();yield return null;
                // Level edits are confined to the caller's isolated, already-owned QA assets.
                Store.Profile.classSkillLevels[skill]=level;Store.Profile.generalLevels[general]=generalLevel;
                Store.SelectClass(skill/2);Store.EquipClassSkill(skill);Store.EquipGeneralSkill(general);
                int gun=primary<0?TacticalPrototypeRules.StarterWeapon(skill/2):primary;
                Store.Equip(0,gun);Store.Equip(1,0);Store.SetAttachment(gun,0);Store.SetAttachment(0,0);
                g.qaSuppressAI=true;
                if(!g.BeginExpedition(0))throw new InvalidOperationException("Skill fixture deployment rejected: "+g.expeditionNotice);
                yield return new WaitForSeconds(.25f);Place(origin);P.SetLook(0,0);
            }
            IEnumerator Swap(PhaseAnchor anchor)
            {
                Aim(anchor.AimPoint);int before=P.swapCount;bool accepted=P.RequestSwap(anchor);yield return new WaitForSeconds(.16f);
                if(!accepted||P.swapCount!=before+1)throw new InvalidOperationException("Skill fixture Q failed: "+P.ValidateSwap(anchor));
            }
            public IEnumerator Run()
            {
                if(!RunStorage.IsIsolated||Store==null||!Store.IsTactical){C("isolated_precondition",false);yield break;}
                bool oldSuppress=g.qaSuppressAI;int oldDifficulty=g.difficulty;
                int[] levels=(int[])Store.Profile.classSkillLevels.Clone(),generals=(int[])Store.Profile.generalLevels.Clone();
                g.difficulty=1;
                try
                {
                    yield return ShockAndBoss();
                    yield return Mobility();
                    yield return Fortify();
                    yield return Reload();
                    yield return Scan();
                    yield return Precision();
                    yield return Remote();
                    yield return Denial();
                    yield return GeneralSkills();
                }
                finally
                {
                    if(keyboard!=null){InputSystem.RemoveDevice(keyboard);keyboard=null;}g.qaKeyboard=null;g.qaInputEnabled=false;
                    Clear();S.ResetCombat();P.ClearArmor();if(g.expeditionActive&&g.expeditionReceipt==null)g.AbandonExpedition();g.OpenExpeditionBoard();
                    Array.Copy(levels,Store.Profile.classSkillLevels,levels.Length);Array.Copy(generals,Store.Profile.generalLevels,generals.Length);
                    // Commit the restored fixture levels through an existing idle operation.
                    Store.SelectClass(Store.ActiveClass==0?1:0);Store.SelectClass(0);
                    g.qaSuppressAI=oldSuppress;g.difficulty=oldDifficulty;
                }
            }
            bool ShockSight(EnemyActor target,out string evidence)
            {
                Vector3 delta=target.AimPoint-P.Eye.position;float alignment=Vector3.Dot(delta.normalized,P.Eye.forward);
                bool found=CombatRay.Cast(P.Eye.position,delta.normalized,delta.magnitude+.05f,P.transform,out var hit);
                var controller=target.GetComponent<CharacterController>();
                var hitActor=found?hit.collider.GetComponentInParent<EnemyActor>():null;
                evidence="frame="+Time.frameCount+";eye="+P.Eye.position.ToString("F3")+";target="+target.transform.position.ToString("F3")+";aim="+target.AimPoint.ToString("F3")
                    +";distance="+delta.magnitude.ToString("F3")+";dot="+alignment.ToString("F3")
                    +";hit="+(found?hit.collider.name+"/"+hit.collider.GetType().Name+" actor="+(hitActor?hitActor.GetInstanceID().ToString():"none"):"none")
                    +";targetId="+target.GetInstanceID()+";controller="+(controller?controller.enabled+" bounds="+controller.bounds.ToString():"missing");
                return delta.magnitude<=ClassSkillTuning.ShockRange&&alignment>=Mathf.Cos(ClassSkillTuning.ShockHalfAngle*Mathf.Deg2Rad)
                    &&found&&hitActor==target&&!target.IsShieldCollider(hit.collider);
            }
            IEnumerator ShockAndBoss()
            {
                yield return Prepare(0);
                var normal=Enemy(EnemyKind.Assault,origin+Vector3.forward*3);Aim(normal.AimPoint);float hp=normal.health;
                ShockSight(normal,out var justSpawned);
                // Do not treat AddComponent's creation frame as an initialized physics fixture.
                yield return null;Physics.SyncTransforms();Aim(normal.AimPoint);
                bool ordinarySight=ShockSight(normal,out var initialized);
                bool cast=S.ActivateEquipped();
                C("shock_ordinary_no_damage",ordinarySight&&cast&&normal.stunLeft>=.59f&&normal.health==hp,
                    "level1;stun="+normal.stunLeft+";accepted="+cast+";outcome="+S.LastOutcome+";spawned={"+justSpawned+"};initialized={"+initialized+"}");
                normal.stunLeft=.9f;normal.ApplyOrdinaryStagger(.6f);C("ordinary_stagger_max",Mathf.Abs(normal.stunLeft-.9f)<.001f);
                normal.stunLeft=0;S.ResetCombat();var wall=Box(origin+new Vector3(0,1,1.5f),new Vector3(2,2,.25f));
                cast=S.ActivateEquipped();C("shock_world_blocked",cast&&normal.stunLeft==0&&normal.health==hp);wall.SetActive(false);
                Clear();yield return null;
                var shield=Enemy(EnemyKind.Shield,origin+Vector3.forward*3);Aim(shield.AimPoint);S.ResetCombat();cast=S.ActivateEquipped();
                C("shock_physical_shield_blocked",cast&&shield.stunLeft==0&&shield.health==1000,S.LastOutcome);
                Clear();yield return null;Place(origin);
                var actor=Enemy(EnemyKind.Elite,origin+Vector3.forward*2);var boss=actor.EnableBoss();boss.Absorb(BossEncounter.ShieldMaximum);
                actor.agent.enabled=false;actor.suppressAI=false;P.health=500;S.ResetCombat();Aim(actor.AimPoint);
                ShockSight(actor,out var bossSpawned);yield return null;Physics.SyncTransforms();Aim(actor.AimPoint);
                bool bossSight=ShockSight(actor,out var bossInitialized);
                bool begun=boss.TryBeginSkill(BossSkill.Attraction);float seconds=boss.SkillSeconds;cast=S.ActivateEquipped();
                C("shock_boss_spatial_not_cancelled",bossSight&&begun&&cast&&boss.SkillStep==BossSkillStep.Telegraph&&Mathf.Abs(boss.SkillSeconds-seconds)<.001f&&actor.stunLeft==0,
                    S.LastOutcome+";spawned={"+bossSpawned+"};initialized={"+bossInitialized+"}");
                g.qaSuppressAI=false;int initialCasts=boss.SpatialCasts,initialSwaps=P.swapCount;float end=Time.time+6,nextMelee=0;bool swapped=false;
                while(Time.time<end)
                {
                    Aim(actor.AimPoint);
                    if(Time.time>=nextMelee){P.combatTools.Melee();nextMelee=Time.time+.8f;}
                    if(!swapped&&P.cooldown<=0)swapped=P.RequestSwap(actor);
                    yield return new WaitForSeconds(.1f);
                }
                C("boss_spatial_progress_under_controls",swapped&&P.swapCount>initialSwaps&&boss.SpatialCasts>initialCasts,"6s real-clock melee/Q/shock sequence; casts="+boss.SpatialCasts+"; not a universal lockout proof");
                var burst=typeof(BossEncounter).GetField("burst",BindingFlags.NonPublic|BindingFlags.Instance);
                float deadline=Time.time+10;
                while(Time.time<deadline&&(int)burst.GetValue(boss)==0)yield return null;
                int before=(int)burst.GetValue(boss);Place(actor.transform.position+actor.transform.forward*2);Aim(actor.AimPoint);S.ResetCombat();
                BossSkillStep step=boss.SkillStep;float stun=actor.stunLeft;cast=S.ActivateEquipped();
                C("shock_interrupts_actual_basic_burst",before>0&&cast&&(int)burst.GetValue(boss)==0&&boss.SkillStep==step&&actor.stunLeft==stun,"observed native burst before="+before);
                g.qaSuppressAI=true;
            }
            IEnumerator Mobility()
            {
                yield return Prepare(1);keyboard=InputSystem.AddDevice<Keyboard>();g.qaKeyboard=keyboard;g.qaInputEnabled=true;
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.W));yield return new WaitForSeconds(.6f);
                float baseline=Vector3.ProjectOnPlane(P.controller.velocity,Vector3.up).magnitude;bool cast=S.ActivateEquipped();yield return new WaitForSeconds(.35f);
                float boosted=Vector3.ProjectOnPlane(P.controller.velocity,Vector3.up).magnitude;
                C("mobility_native_speed",cast&&baseline>0&&Mathf.Abs(boosted/baseline-1.25f)<.04f,"base="+baseline+";boost="+boosted);
                InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return null;InputSystem.RemoveDevice(keyboard);keyboard=null;g.qaKeyboard=null;g.qaInputEnabled=false;
                float life=S.MobilityLeft,cd=S.CooldownLeft;g.SetState(RunState.Paused);yield return new WaitForSecondsRealtime(.12f);
                C("mobility_pause_frozen",Mathf.Abs(life-S.MobilityLeft)<.001f&&Mathf.Abs(cd-S.CooldownLeft)<.001f);g.SetState(RunState.Playing);
                var anchor=Anchor(P.transform.position+Vector3.right*3);yield return Swap(anchor);bool retained=S.MobilityActive;
                bool fired=P.Fire();C("mobility_q_preserves_shot_ends",retained&&fired&&!S.MobilityActive&&S.CooldownLeft>0);
            }
            IEnumerator Fortify()
            {
                yield return Prepare(2,2);bool cast=S.ActivateEquipped();Vector3 center=S.FortifyCenter;
                Place(origin+Vector3.right*4);bool outside=Mathf.Approximately(S.FortifyDamageMultiplier(),1);
                Place(origin+Vector3.up*1.2f);bool floor=Mathf.Approximately(S.FortifyDamageMultiplier(),1);
                Place(origin);bool returned=Mathf.Abs(S.FortifyDamageMultiplier()-.8f)<.001f;
                C("fortify_fixed_range_floor_return",cast&&outside&&floor&&returned&&S.FortifyCenter==center);
                var wall=Box(origin+new Vector3(1,1,0),new Vector3(.25f,2,3));Place(origin+Vector3.right*2);
                bool blocked=Mathf.Approximately(S.FortifyDamageMultiplier(),1);wall.SetActive(false);Place(origin);
                C("fortify_world_occlusion",blocked);
                P.health=200;bool armor=P.TryApplyArmor(30);P.Hurt(100,P.Eye.position,shieldBlockable:false);
                C("damage_reduce_then_armor_then_hp",armor&&Mathf.Abs(P.health-150)<.01f&&P.TemporaryArmor==0,"100 * .8 - 30 = 50 HP");
                P.TryApplyArmor(30);float hp=P.health;bool duplicate=P.TryApplyArmor(40);P.Hurt(20,P.Eye.position,false,PlayerDamageKind.Spatial);P.Hurt(20,P.Eye.position,false,PlayerDamageKind.Environment);
                C("armor_no_stack_spatial_environment_bypass",!duplicate&&P.TemporaryArmor==30&&Mathf.Abs(P.health-(hp-40))<.01f);
                var anchor=Anchor(origin+Vector3.forward*5);yield return Swap(anchor);
                C("fortify_q_does_not_move_zone",S.FortifyCenter==center&&Mathf.Approximately(S.FortifyDamageMultiplier(),1));
            }
            IEnumerator Reload()
            {
                yield return Prepare(3);int weapon=P.weapon;P.ammo[weapon]=0;P.reserve[weapon]=8;
                bool reload=P.Reload();float before=P.reloadLeft;bool cast=S.ActivateEquipped();
                C("reload_remaining_multiplier",reload&&cast&&Mathf.Abs(P.reloadLeft-before*.7f)<.001f);
                var anchor=Anchor(origin+Vector3.right*3);yield return Swap(anchor);
                C("q_cancels_untransferred_reload",P.reloadLeft==0&&P.ammo[weapon]==0&&P.reserve[weapon]==8&&S.CooldownLeft>0);
                S.ResetCombat();P.ammo[weapon]=P.Capacity(weapon);bool full=S.ActivateEquipped();P.ammo[weapon]=0;P.reserve[weapon]=0;bool empty=S.ActivateEquipped();
                C("reload_invalid_no_cooldown",!full&&!empty&&S.CooldownLeft==0);
                P.reserve[weapon]=7;reload=P.Reload();cast=S.ActivateEquipped();yield return new WaitForSeconds(P.reloadLeft+.08f);
                C("accelerated_reload_real_ammo_transfer",reload&&cast&&P.ammo[weapon]==7&&P.reserve[weapon]==0);
            }
            IEnumerator Scan()
            {
                yield return Prepare(4);var enemy=Enemy(EnemyKind.Training,origin+Vector3.forward*3);Aim(enemy.AimPoint);
                Box(origin+new Vector3(0,1,1.5f),new Vector3(2,2,.25f));bool cast=S.ActivateEquipped();var result=P.ValidateSwap(enemy);
                C("scan_occluded_state_without_q_override",cast&&S.ScanActive&&S.ScanLeft>=2.99f&&result.Code==SwapFailureReason.LineOfSightBlocked,
                    "Enemy is inside20m and scan remains active behind World; HUD visual is not asserted by this state check");
            }
            IEnumerator Precision()
            {
                yield return Prepare(5,1,0,1,2);S.ActivateEquipped();P.SetLook(0,-75);bool fired=P.Fire();
                C("precision_miss_consumes",fired&&!S.PrecisionReady);P.CancelActions();yield return new WaitForSeconds(.35f);
                var shield=Enemy(EnemyKind.Shield,origin+Vector3.forward*3);Aim(shield.AimPoint);S.ResetCombat();S.ActivateEquipped();fired=P.Fire();
                C("precision_shield_consumes",fired&&!S.PrecisionReady&&shield.health==1000);P.CancelActions();Clear();yield return new WaitForSeconds(.35f);
                var enemy=Enemy(EnemyKind.Training,origin+Vector3.forward*3);Aim(enemy.AimPoint);S.ResetCombat();S.ActivateEquipped();int shots=g.totalShots;float hp=enemy.health;
                fired=P.Fire();yield return new WaitForSeconds(2/WeaponCatalog.Get(2).rate+.08f);
                float expected=WeaponCatalog.Get(2).damage*3.15f;
                C("precision_burst_first_only",fired&&g.totalShots-shots==3&&Mathf.Abs(hp-enemy.health-expected)<.1f,"damage="+(hp-enemy.health)+";expected="+expected);
            }
            IEnumerator Remote()
            {
                yield return Prepare(6);P.SetLook(0,80);bool cast=S.ActivateEquipped();var charge=S.RemoteCharge;bool early=S.ActivateEquipped();
                float life=charge?charge.LifetimeLeft:-1;g.SetState(RunState.Paused);yield return new WaitForSecondsRealtime(.12f);
                C("remote_arm_pause_no_early_detonation",cast&&!early&&charge&&charge.Live&&Mathf.Abs(charge.LifetimeLeft-life)<.001f&&S.CooldownLeft==0);g.SetState(RunState.Playing);
                yield return new WaitForSeconds(.65f);Vector3 center=charge.transform.position;
                var target=Objective(center+new Vector3(-1,.5f,0));var blocked=Objective(center+new Vector3(1.8f,.5f,0));
                Box(center+new Vector3(.85f,1,0),new Vector3(.2f,2,2));Place(new Vector3(center.x,origin.y,center.z+1));P.health=200;float hp=P.health;
                var energyActor=Enemy(EnemyKind.Elite,new Vector3(center.x,origin.y,center.z-1.6f));var energyBoss=energyActor.EnableBoss();
                bool detonated=S.ActivateEquipped();C("remote_real_blast_objective_los_self",detonated&&target.Health<200&&blocked.Health==200&&P.health<hp,"target="+target.Health+";blocked="+blocked.Health+";self="+(hp-P.health));
                C("remote_respects_boss_energy_layer",energyBoss.Energy<BossEncounter.ShieldMaximum&&energyActor.health==BossEncounter.HealthMaximum,"energy="+energyBoss.Energy);
                C("remote_cooldown_begins_at_end",S.CooldownLeft>17.9f&&!S.RemoteCharge);
                Clear();S.ResetCombat();Place(origin);P.SetLook(0,80);S.ActivateEquipped();charge=S.RemoteCharge;yield return new WaitForSeconds(.65f);
                target=Objective(charge.transform.position+new Vector3(-1,.5f,0));hp=P.health;yield return new WaitForSeconds(charge.LifetimeLeft+.1f);
                C("remote_expiry_never_explodes",!S.RemoteCharge&&target.Health==200&&P.health==hp&&S.CooldownLeft>17.5f);
            }
            IEnumerator Denial()
            {
                yield return Prepare(7);Aim(origin+Vector3.forward*3-Vector3.up*.03f);bool cast=S.ActivateEquipped();var field=S.DenialField;
                if(!field)throw new InvalidOperationException("Denial placement fixture rejected: "+S.LastFailure);
                Vector3 center=field.transform.position;var target=Objective(center+new Vector3(-1,.5f,0));var high=Objective(center+new Vector3(0,2,0));var blocked=Objective(center+new Vector3(1.8f,.5f,0));
                Box(center+new Vector3(.85f,1,0),new Vector3(.2f,2,2));Place(new Vector3(center.x,origin.y,center.z+1));float hp=P.health;
                C("denial_no_instant_full_damage",cast&&field.TicksApplied==0&&target.Health==200&&P.health==hp);
                yield return new WaitForSeconds(.32f);int ticks=field.TicksApplied;
                C("denial_real_ticks_and_self",ticks>=1&&Mathf.Abs((200-target.Health)-ticks*4)<.01f&&Mathf.Abs((hp-P.health)-ticks*4)<.01f,"ticks="+ticks);
                C("denial_world_floor_exclusion",high.Health==200&&blocked.Health==200);
                yield return new WaitForSeconds(4);C("denial_finishes_after_sixteen_ticks",field.TicksApplied==16&&Mathf.Abs(target.Health-136)<.01f&&!S.DenialField,"ticks="+field.TicksApplied);
            }
            IEnumerator GeneralSkills()
            {
                yield return Prepare(1,1,0,3);bool shield=P.tactics.ActivateShield();bool throwDenied=!P.combatTools.Throw();
                C("shield_level3_cooldown_mutual_slot",shield&&Mathf.Abs(P.tactics.ShieldLeft-4)<.001f&&Mathf.Abs(P.tactics.ShieldCooldownLeft-8)<.001f&&throwDenied);
                yield return Prepare(1,1,1,1);S.ActivateEquipped();bool thrown=P.combatTools.Throw();var knife=P.combatTools.Knife;
                C("knife_level1_mobility_end_no_silence",thrown&&knife&&!knife.HitSilence&&!knife.AreaSilence&&!S.MobilityActive&&!P.tactics.ActivateShield());
                yield return new WaitForSeconds(.85f);var target=Enemy(EnemyKind.Training,origin+Vector3.forward*5);bool recall=P.combatTools.Recall();yield return new WaitForSeconds(1.1f);
                C("knife_level1_real_return_damage",recall&&P.combatTools.ReturnHits>0&&target.health<1000&&target.silenceLeft==0,"hits="+P.combatTools.ReturnHits);
                Clear();yield return null;Place(origin);target=Enemy(EnemyKind.Training,origin+Vector3.forward*3);var neighbor=Enemy(EnemyKind.Training,origin+new Vector3(1.5f,0,3));Aim(target.AimPoint);
                var k2=ThrownKnife.Launch(P.combatTools,P,P.Eye.position,P.Eye.forward,2,true);owned.Add(k2.gameObject);yield return new WaitForSeconds(.18f);
                C("knife_level2_hit_only",target.silenceLeft>0&&neighbor.silenceLeft==0&&k2.HitSilence&&!k2.AreaSilence);
                k2.gameObject.SetActive(false);target.silenceLeft=neighbor.silenceLeft=0;var open=Enemy(EnemyKind.Training,origin+new Vector3(-1.5f,0,3));
                Box(origin+new Vector3(.75f,1,3),new Vector3(.18f,2,3));var k3=ThrownKnife.Launch(P.combatTools,P,P.Eye.position,P.Eye.forward,3,true);owned.Add(k3.gameObject);yield return new WaitForSeconds(.18f);
                C("knife_level3_area_world_blocked",target.silenceLeft>0&&open.silenceLeft>0&&neighbor.silenceLeft==0&&k3.AreaSilence);
                var old=ThrownKnife.Launch(P.combatTools,P,P.Eye.position,Vector3.up,1,false);owned.Add(old.gameObject);
                C("knife_legacy_rank1_effect_retained",old.HitSilence&&!old.AreaSilence);
            }
        }
    }
}