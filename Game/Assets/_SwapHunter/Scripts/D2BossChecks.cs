using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.AI.Navigation;

namespace SwapHunter
{
    // Focused fixtures for D2 next-action policy and the existing cast commitment that it must preserve.
    public static class D2BossChecks
    {
        public static IEnumerator Run(Action<string,bool,string> add) => new Fixture(add).Run();
        sealed class Fixture
        {
            readonly Action<string,bool,string> add;
            readonly Vector3 origin=new Vector3(1200,0,0);
            Transform fixture; EnemyActor actor; BossEncounter boss; PhaseAnchor anchor; GameObject wall;
            DemoGame G=>DemoGame.I; PlayerMotor P=>G.player;
            bool playerEnabled;
            public Fixture(Action<string,bool,string> add){this.add=add;}
            void C(string key,bool pass,string detail="")=>add(key,pass,detail);
            IEnumerator Fresh(BossPhase phase)
            {
                if(actor){G.enemies.Remove(actor);actor.gameObject.SetActive(false);UnityEngine.Object.Destroy(actor.gameObject);}
                wall.SetActive(false);anchor.gameObject.SetActive(true);anchor.MoveTo(origin+new Vector3(0,.03f,5));
                P.SetFeet(origin+new Vector3(0,.03f,7));P.health=10000;P.cooldown=0;
                Physics.SyncTransforms();yield return null;
                actor=EnemyActor.Spawn(EnemyKind.Elite,origin+new Vector3(0,.03f,-8),0);boss=actor.EnableBoss();
                if(phase!=BossPhase.EnergyMelee)actor.Damage(BossEncounter.ShieldMaximum);
                if(phase==BossPhase.Combined)actor.Damage(BossEncounter.HealthMaximum*.5f+1);
                yield return null;
            }
            public IEnumerator Run()
            {
                if(!RunStorage.IsValidation)throw new InvalidOperationException("D2 Boss fixtures require isolated validation storage.");
                PolicyChecks();
                try
                {
                    if(G.expeditionActive){G.AbandonExpedition();G.OpenExpeditionBoard();}
                    G.StartRun(false);G.qaSuppressAI=true;G.ClearDeployments();
                    foreach(var enemy in G.enemies)if(enemy)enemy.gameObject.SetActive(false);
                    fixture=new GameObject("D2 Boss decision fixture").transform;
                    Shapes.Box("D2 decision floor",fixture,origin+Vector3.down*.5f,new Vector3(70,1,70),1,true,Layers.World);
                    var nav=fixture.gameObject.AddComponent<NavMeshSurface>();nav.collectObjects=CollectObjects.Children;
                    nav.layerMask=Layers.WorldMask;nav.useGeometry=UnityEngine.AI.NavMeshCollectGeometry.PhysicsColliders;RuntimeNavigationOwner.BuildFresh(nav);
                    wall=Shapes.Box("Occlusion fixture",fixture,origin+new Vector3(0,3,0),new Vector3(12,6,.6f),1,true,Layers.World);wall.SetActive(false);
                    var go=new GameObject("D2 exchange anchor");go.transform.SetParent(fixture);go.layer=Layers.Actor;
                    anchor=go.AddComponent<PhaseAnchor>();var collider=go.AddComponent<CapsuleCollider>();collider.height=1.4f;collider.radius=.32f;collider.center=Vector3.up*.7f;G.anchors.Add(anchor);
                    playerEnabled=P.enabled;P.enabled=false;
                    yield return Fresh(BossPhase.Rifle);
                    bool observed=boss.TryGetSpatialObservation(out var snapshot);
                    C("B01_visible_snapshot",observed&&snapshot.currentSight&&Vector3.Distance(snapshot.position,P.transform.position)<.001f);
                    float end=Time.time+2.65f;
                    while(Time.time<end){boss.ObserveSpatialTarget(Time.deltaTime);yield return null;}
                    boss.TryGetSpatialObservation(out snapshot);
                    C("B01_stationary_uses_continuous_observation",snapshot.stationarySeconds>=2.5f&&snapshot.highSeconds==0,"stationary="+snapshot.stationarySeconds);
                    Vector3 locked=snapshot.position;
                    wall.SetActive(true);P.SetFeet(origin+new Vector3(3,.03f,9));Physics.SyncTransforms();yield return null;
                    bool remembered=boss.TryGetSpatialObservation(out var rememberedSnapshot);
                    bool started=boss.TryBeginSkill(BossSkill.Attraction);
                    C("B01_hidden_start_uses_same_last_seen_snapshot",remembered&&!rememberedSnapshot.currentSight&&started
                        &&Vector3.ProjectOnPlane(boss.DangerCenter-locked,Vector3.up).magnitude<.001f
                        &&Vector3.Distance(rememberedSnapshot.position,P.transform.position)>1,"age="+(remembered?rememberedSnapshot.age:-1));
                    var cast=boss.CurrentCast;Vector3 center=boss.DangerCenter;
                    actor.Damage(BossEncounter.HealthMaximum*.5f+1);actor.stunLeft=10;float left=boss.SkillSeconds;boss.Tick(.1f);
                    C("B01_new_decision_keeps_committed_cast",boss.Phase==BossPhase.Combined&&ReferenceEquals(cast,boss.CurrentCast)
                        &&cast.phase==BossPhase.Rifle&&boss.SkillSeconds<left&&boss.DangerCenter==center);
                    actor.ApplySilence(3.2f);boss.Tick(.01f);
                    C("B01_silence_recovers_original_cast",boss.SkillStep==BossSkillStep.Recover&&ReferenceEquals(cast,boss.CurrentCast)&&boss.RecoverySecondsLeft>3);
                    yield return new WaitForSeconds(1.1f);
                    C("B01_hidden_observation_expires",!boss.TryGetSpatialObservation(out _));

                    yield return Fresh(BossPhase.EnergyMelee);anchor.gameObject.SetActive(false);
                    C("B01_no_forced_exchange_target",!boss.TryBeginSkill(BossSkill.SwapElbow)&&boss.Skill==BossSkill.None&&boss.SpatialSwaps==0);
                    yield return Fresh(BossPhase.Combined);wall.SetActive(true);Physics.SyncTransforms();yield return null;
                    yield return new WaitForSeconds(.8f);boss.Tick(.01f);
                    C("B01_no_observation_returns_to_basic_actions",boss.Skill==BossSkill.None&&boss.LastSpatialDecision!=null
                        &&boss.LastSpatialDecision.observation=="unavailable"&&boss.CurrentCast==null);
                    wall.SetActive(false);Physics.SyncTransforms();yield return new WaitForSeconds(1.05f);boss.Tick(.01f);
                    C("B01_visible_candidate_decision_logged",boss.LastSpatialDecision!=null&&boss.LastSpatialDecision.selected!=BossSkill.None
                        &&boss.LastSpatialDecision.candidates.Count==6&&boss.SkillStep==BossSkillStep.Telegraph,
                        boss.LastSpatialDecision==null?"no decision":JsonUtility.ToJson(boss.LastSpatialDecision));
                }
                finally
                {
                    G.qaSuppressAI=true;if(P)P.enabled=playerEnabled;
                    if(actor){G.enemies.Remove(actor);actor.gameObject.SetActive(false);UnityEngine.Object.Destroy(actor.gameObject);}
                    if(anchor)G.anchors.Remove(anchor);
                    if(fixture){fixture.gameObject.SetActive(false);UnityEngine.Object.Destroy(fixture.gameObject);}
                    G.ClearDeployments();G.SetState(RunState.Menu);
                }
                yield return null;
            }
            void PolicyChecks()
            {
                var same=new BossSpatialCandidate{skill=BossSkill.Attraction,legal=true,score=3,reason="fixture"};
                var other=new BossSpatialCandidate{skill=BossSkill.OmniFire,legal=true,score=1,reason="fixture"};
                var list=new List<BossSpatialCandidate>{same,other};BossEncounter.ApplySpatialHistory(list,BossSkill.Attraction,BossSkill.Attraction);
                C("B01_third_repeat_excluded_if_alternative",!same.legal&&other.legal&&list[0]==other&&Mathf.Abs(same.score+1.5f)<.001f);
                same=new BossSpatialCandidate{skill=BossSkill.SwapElbow,legal=true,score=1,reason="fixture"};list=new List<BossSpatialCandidate>{same};
                BossEncounter.ApplySpatialHistory(list,BossSkill.SwapElbow,BossSkill.SwapElbow);
                C("B01_single_phase_skill_can_repeat",same.legal);
                var observed=new BossSpatialObservation(new Vector3(0,2,0),0,0,0,2.5f,0,1.5f,true);
                C("B01_height_threshold_independent_of_loadout",observed.High&&!observed.Low&&observed.highSeconds==2.5f);
            }
        }
    }
}
