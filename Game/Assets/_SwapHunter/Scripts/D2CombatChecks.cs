using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.AI.Navigation;
namespace SwapHunter
{
    // Isolated rule fixtures plus real weapon/projectile/field paths; not a natural playthrough.
    public static class D2CombatChecks
    {
        public static IEnumerator Run(Action<string,bool,string> add)=>new Fixture(add).Run();
        sealed class Fixture
        {
            readonly Action<string,bool,string> add;
            readonly Vector3 origin=new Vector3(1400,0,0);
            readonly List<GameObject> extras=new List<GameObject>();
            DemoGame G=>DemoGame.I;PlayerMotor P=>G.player;
            Transform floor;EnemyActor actor;
            bool markSetting;
            public Fixture(Action<string,bool,string> add){this.add=add;}
            void C(string id,bool pass,string detail=""){add(id,pass,detail);}
            static bool Near(float a,float b)=>Mathf.Abs(a-b)<.06f;
            void ClearActor()
            {
                if(actor){G.enemies.Remove(actor);actor.gameObject.SetActive(false);UnityEngine.Object.Destroy(actor.gameObject);}actor=null;
            }
            IEnumerator Fresh(bool marked=false)
            {
                ClearActor();P.ResetAt(origin+new Vector3(0,.03f,-7));P.health=10000;P.SetLook(0,0);
                actor=EnemyActor.Spawn(EnemyKind.Assault,origin+Vector3.up*.03f,0);
                if(!actor)throw new InvalidOperationException("D2 damage fixture spawn failed.");
                actor.health=actor.maximumHealth=1000;actor.suppressAI=true;if(marked)actor.MarkPhase(5);
                Physics.SyncTransforms();yield return null;
            }
            void Aim(Vector3 point)
            {
                Vector3 d=point-P.Eye.position;P.SetLook(Mathf.Atan2(d.x,d.z)*Mathf.Rad2Deg,-Mathf.Atan2(d.y,new Vector2(d.x,d.z).magnitude)*Mathf.Rad2Deg);
            }
            public IEnumerator Run()
            {
                if(!RunStorage.IsValidation)throw new InvalidOperationException("D2 combat checks require isolated validation storage.");
                markSetting=DamageResolution.ExtendedPlayerMarking;
                try
                {
                    if(G.expeditionActive){G.AbandonExpedition();G.OpenExpeditionBoard();}
                    G.StartRun(false);G.qaSuppressAI=true;G.ClearDeployments();
                    foreach(var enemy in G.enemies)if(enemy)enemy.gameObject.SetActive(false);
                    floor=new GameObject("D2 isolated damage fixture").transform;
                    Shapes.Box("D2 owned floor",floor,origin+Vector3.down*.5f,new Vector3(50,1,50),1,true,Layers.World);
                    var nav=floor.gameObject.AddComponent<NavMeshSurface>();nav.collectObjects=CollectObjects.Children;nav.layerMask=Layers.WorldMask;
                    nav.useGeometry=UnityEngine.AI.NavMeshCollectGeometry.PhysicsColliders;RuntimeNavigationOwner.BuildFresh(nav);
                    yield return SourcesAndParts();
                    yield return RealAttackPaths();
                    yield return DeviceBatches();
                }
                finally
                {
                    DamageResolution.ExtendedPlayerMarking=markSetting;G.qaSuppressAI=true;ClearActor();
                    foreach(var extra in extras)if(extra){extra.SetActive(false);UnityEngine.Object.Destroy(extra);}
                    if(floor){floor.gameObject.SetActive(false);UnityEngine.Object.Destroy(floor.gameObject);}
                    G.ClearDeployments();G.SetState(RunState.Menu);
                }
                yield return null;
            }
            IEnumerator SourcesAndParts()
            {
                yield return Fresh(true);DamageResolution.ExtendedPlayerMarking=true;
                foreach(DamageSource source in Enum.GetValues(typeof(DamageSource)))
                {
                    actor.health=1000;
                    var result=DamageResolution.Single(actor,10,source,P.transform,P.Eye.position);
                    float expected=DamageResolution.IsPlayer(source)?12:10;
                    C("D01_mark_once_"+source,Near(result.healthLoss,expected)&&Near(1000-actor.health,expected),"effective="+result.healthLoss+";expected="+expected);
                }
                actor.health=1000;var head=DamageResolution.Single(actor,10,DamageSource.PlayerBullet,P.transform,P.Eye.position,EnemyHitRegion.Head);
                actor.health=1000;var melee=DamageResolution.Single(actor,10,DamageSource.PlayerMelee,P.transform,P.Eye.position,EnemyHitRegion.Head);
                C("D01_parts_only_for_bullets",Near(head.healthLoss,18)&&Near(melee.healthLoss,12),"bulletHead="+head.healthLoss+";melee="+melee.healthLoss);
                DamageResolution.ExtendedPlayerMarking=false;
                actor.health=1000;var oldBlast=DamageResolution.Single(actor,10,DamageSource.RemoteCharge,P.transform,P.Eye.position);
                actor.health=1000;var oldBullet=DamageResolution.Single(actor,10,DamageSource.PlayerBullet,P.transform,P.Eye.position);
                C("D01_independent_legacy_mark_switch",Near(oldBlast.healthLoss,10)&&Near(oldBullet.healthLoss,12));
                DamageResolution.ExtendedPlayerMarking=true;
                yield return Fresh();var boss=actor.EnableBoss();actor.MarkPhase(5);
                float before=boss.Energy;var absorbed=DamageResolution.Single(actor,10,DamageSource.RemoteCharge,P.transform,P.Eye.position);
                C("D01_mark_before_boss_energy",Near(before-boss.Energy,12)&&Near(absorbed.energyLoss,12)&&Near(absorbed.healthLoss,0));
            }
            IEnumerator RealAttackPaths()
            {
                yield return Fresh();P.SetLoadout(11,0);Aim(actor.AimPoint);Physics.SyncTransforms();yield return new WaitForSeconds(.23f);
                float before=actor.health;bool fired=P.Fire();
                float region=actor.LastHitRegion==EnemyHitRegion.Head?1.5f:actor.LastHitRegion==EnemyHitRegion.Limb?.8f:1;
                float expected=EffectiveWeaponStats.Read(11,G.config).Damage*region*G.ExpeditionDamageMultiplier*G.ModuleShotDamage(7);
                C("D01_phase_first_round_damages_before_mark",fired&&actor.Marked&&Near(before-actor.health,expected),"loss="+(before-actor.health)+";expected unmarked="+expected);
                yield return Fresh(true);before=actor.health;
                var grenade=PlayerOrdnance.Launch(P,actor.AimPoint,Vector3.forward,10);grenade.Explode();
                C("D01_real_player_grenade_marked",Near(before-actor.health,12),"loss="+(before-actor.health));
                yield return Fresh(true);before=actor.health;ClassSkillArea.Blast(P,actor.AimPoint,10);
                C("D01_real_remote_blast_marked",Near(before-actor.health,12),"loss="+(before-actor.health));
                yield return Fresh(true);before=actor.health;
                var field=ClassDenialField.Create(P,actor.transform.position,16);extras.Add(field.gameObject);
                float deadline=Time.time+4.5f;while(field&&field.Live&&Time.time<deadline)yield return null;
                C("D01_real_denial_16_ticks",field.TicksApplied==16&&Near(before-actor.health,76.8f),"ticks="+field.TicksApplied+";loss="+(before-actor.health));
                yield return Fresh(true);before=actor.health;Aim(actor.AimPoint);
                var knife=ThrownKnife.Launch(P.combatTools,P,P.Eye.position,(actor.AimPoint-P.Eye.position).normalized,1,true);extras.Add(knife.gameObject);
                deadline=Time.time+1;while(knife&&knife.Flying&&Time.time<deadline)yield return null;
                C("D01_real_knife_outbound",knife&&knife.OutboundHits==1&&Near(before-actor.health,33.6f),"loss="+(before-actor.health));
                before=actor.health;P.SetFeet(actor.transform.position+Vector3.forward*5);knife.BeginReturn();
                deadline=Time.time+1;while(knife&&knife.Returning&&knife.RecallHits==0&&Time.time<deadline)yield return null;
                C("D01_real_knife_return_separate_leg",knife&&knife.RecallHits==1&&Near(before-actor.health,26.4f),"loss="+(before-actor.health));
                if(knife){knife.gameObject.SetActive(false);UnityEngine.Object.Destroy(knife.gameObject);}
            }
            IEnumerator DeviceBatches()
            {
                yield return Fresh(true);var device=RelayDevice.Attach(actor,floor,origin+Vector3.right*3);Physics.SyncTransforms();
                var collider=device.GetComponentInChildren<Collider>();float before=actor.health;
                P.SetFeet(actor.transform.position+Vector3.forward*2);P.SetLoadout(0,1);Aim(device.AimPoint);Physics.SyncTransforms();yield return new WaitForSeconds(.23f);
                bool realRay=CombatRay.Cast(P.Eye.position,P.Eye.forward,4,P.transform,out var hit)&&hit.collider.GetComponentInParent<RelayDevice>()==device;
                bool fired=P.Fire();
                C("D02_real_device_ray_and_bullet_no_body_or_mark",realRay&&fired&&Near(device.Health,24)&&Near(actor.health,before),"ray="+realRay+";device="+device.Health+";carrier="+actor.health);
                var dedup=new DamageBatch(DamageSource.PlayerMelee,P.transform,P.Eye.position);long id=dedup.NewEvent();
                var one=dedup.Direct(collider,4,device.AimPoint,id);var duplicate=dedup.Direct(collider,4,device.AimPoint,id);dedup.Apply();
                C("D02_same_event_receiver_deduplicated",one!=null&&duplicate==null&&Near(device.Health,20));
                var ignored=new DamageBatch(DamageSource.EnemyBullet,null,P.Eye.position);var rejected=ignored.Direct(collider,100,device.AimPoint,ignored.NewEvent());ignored.Apply();
                C("D02_enemy_source_cannot_damage_device_or_parent",rejected==null&&Near(device.Health,20)&&Near(actor.health,before));
                yield return Fresh(true);device=RelayDevice.Attach(actor,floor,origin+Vector3.right*3);
                var pellets=new DamageBatch(DamageSource.PlayerBullet,P.transform,P.Eye.position);
                pellets.Enemy(actor,10000,pellets.NewEvent());pellets.Device(device,24,pellets.NewEvent());pellets.Device(device,24,pellets.NewEvent());
                C("D02_no_damage_before_batch_apply",actor.Alive&&Near(device.Health,48)&&!device.Recovery);
                pellets.Apply();
                C("D02_later_pellets_destroy_device_before_drop",!actor.Alive&&Near(device.Health,0)&&device.DeathResolved&&!device.Recovery,"device="+device.Health+";resolved="+device.DeathResolved);
                yield return Fresh(true);device=RelayDevice.Attach(actor,floor,origin+Vector3.right*3);
                var blast=new DamageBatch(DamageSource.RemoteCharge,P.transform,actor.AimPoint);id=blast.NewEvent();
                blast.Enemy(actor,10000,id);blast.Device(device,48,id);blast.Apply();
                C("D02_same_blast_body_and_device_no_drop",!actor.Alive&&Near(device.Health,0)&&device.DeathResolved&&!device.Recovery);
                yield return Fresh(true);device=RelayDevice.Attach(actor,floor,origin+Vector3.right*3);
                var recovery=new DamageBatch(DamageSource.PlayerBullet,P.transform,P.Eye.position);
                recovery.Device(device,20,recovery.NewEvent());recovery.Enemy(actor,10000,recovery.NewEvent());recovery.Apply();
                var pickup=device.Recovery;device.ResolveCarrierDeath();
                C("D02_damaged_usable_device_drops_once",Near(device.Health,28)&&pickup&&device.Recovery==pickup&&pickup.transform.parent==floor,"health="+device.Health);
                yield return Fresh(true);device=RelayDevice.Attach(actor,floor,origin+Vector3.right*3);
                P.SetFeet(actor.transform.position+Vector3.forward*1.8f);Aim(device.AimPoint);Physics.SyncTransforms();before=actor.health;
                bool melee=P.combatTools.Melee();
                C("D02_real_melee_device_priority",melee&&Near(device.Health,24)&&Near(actor.health,before),"device="+device.Health+";bodyLoss="+(before-actor.health));
            }
        }
    }
}
