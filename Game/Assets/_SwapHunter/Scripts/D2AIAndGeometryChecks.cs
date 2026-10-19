using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;
namespace SwapHunter
{
 public static class D2AIAndGeometryChecks
 {
  public static IEnumerator Run(Action<string,bool,string> add)=>new Fixture(add).Run();
  sealed class Fixture
  {
   readonly Action<string,bool,string> add;readonly Vector3 origin=new Vector3(1600,0,0);
   readonly List<EnemyActor> actors=new List<EnemyActor>();readonly List<ClassDenialField> fields=new List<ClassDenialField>();
   DemoGame G=>DemoGame.I;PlayerMotor P=>G.player;Transform fixture;GameObject wall;bool playerEnabled,hazardEnabled,relayEnabled;
   public Fixture(Action<string,bool,string> add){this.add=add;}
   void C(string id,bool pass,string detail="")=>add(id,pass,detail);
   EnemyActor Spawn(EnemyKind kind,Vector3 feet)
   {
    var e=EnemyActor.Spawn(kind,feet,0);if(!e)throw new InvalidOperationException("D2 AI fixture spawn failed: "+feet);
    actors.Add(e);return e;
   }
   void ClearActors()
   {
    foreach(var e in actors)if(e){G.enemies.Remove(e);e.gameObject.SetActive(false);UnityEngine.Object.Destroy(e.gameObject);}actors.Clear();
   }
   void CancelFields(){foreach(var field in fields)if(field)field.Cancel();fields.Clear();}
   void Aim(Vector3 point)
   {Vector3 d=point-P.Eye.position;P.SetLook(Mathf.Atan2(d.x,d.z)*Mathf.Rad2Deg,-Mathf.Atan2(d.y,new Vector2(d.x,d.z).magnitude)*Mathf.Rad2Deg);}
   public IEnumerator Run()
   {
    if(!RunStorage.IsValidation)throw new InvalidOperationException("D2 AI checks require isolated validation storage.");
    playerEnabled=P.enabled;hazardEnabled=EnemyHazardAwareness.DecisionsEnabled;relayEnabled=FireControlRelay.FeatureEnabled;
    try
    {
     if(G.StoryModal)G.CloseStoryModal();if(G.expeditionActive){G.AbandonExpedition();G.OpenExpeditionBoard();}
     G.StartRun(false);G.qaSuppressAI=true;G.ClearDeployments();P.enabled=false;P.health=10000;
     foreach(var e in G.enemies)if(e)e.gameObject.SetActive(false);
     fixture=new GameObject("D2 local observation fixture").transform;
     Shapes.Box("D2 owned navigation floor",fixture,origin+Vector3.down*.5f,new Vector3(48,1,48),1,true,Layers.World);
     var nav=fixture.gameObject.AddComponent<NavMeshSurface>();nav.collectObjects=CollectObjects.Children;nav.layerMask=Layers.WorldMask;nav.useGeometry=NavMeshCollectGeometry.PhysicsColliders;RuntimeNavigationOwner.BuildFresh(nav);
     wall=Shapes.Box("D2 observation occluder",fixture,origin+new Vector3(0,2,3.5f),new Vector3(14,4,.5f),1,true,Layers.World);wall.SetActive(false);
     EnemyHazardAwareness.DecisionsEnabled=true;
     yield return LocalObservation();
     yield return HazardKnowledge();
     CancelFields();ClearActors();fixture.gameObject.SetActive(false);UnityEngine.Object.Destroy(fixture.gameObject);fixture=null;
     G.SetState(RunState.Menu);yield return null;
     yield return RealChapterGeometry();
    }
    finally
    {
     G.qaSuppressAI=true;CancelFields();ClearActors();if(fixture){fixture.gameObject.SetActive(false);UnityEngine.Object.Destroy(fixture.gameObject);}
     P.enabled=playerEnabled;EnemyHazardAwareness.DecisionsEnabled=hazardEnabled;FireControlRelay.FeatureEnabled=relayEnabled;
     if(G.expeditionActive&&G.expeditionReceipt==null)G.AbandonExpedition();G.OpenExpeditionBoard();G.SetState(RunState.Menu);
    }
    yield return null;
   }
   IEnumerator LocalObservation()
   {
    P.ResetAt(origin+new Vector3(0,.03f,1.8f));P.health=10000;
    var shield=Spawn(EnemyKind.Shield,origin+Vector3.up*.03f);shield.transform.rotation=Quaternion.identity;Physics.SyncTransforms();yield return null;
    shield.tactical.Tick(0);int attacks=shield.tactical.MeleeAttacks;
    C("A01_fixture_real_melee_warning",shield.tactical.State==TacticalState.MeleeWarning);
    EnemyTacticalAI.NotifySwap(origin+Vector3.right*100,origin+Vector3.right*105,null);
    C("A01_far_unseen_swap_preserves_warning",shield.tactical.State==TacticalState.MeleeWarning);
    shield.tactical.Tick(.66f);
    C("A01_far_swap_does_not_restart_commitment",shield.tactical.MeleeAttacks==attacks+1&&shield.tactical.State==TacticalState.Recover);
    ClearActors();yield return null;
    P.SetFeet(origin+new Vector3(0,.03f,8));var actor=Spawn(EnemyKind.Assault,origin+Vector3.up*.03f);actor.transform.rotation=Quaternion.identity;
    Physics.SyncTransforms();actor.tactical.ObservePlayer();Vector3 observed=actor.tactical.ObservedPlayerAim;
    C("A01_current_sight_updates_memory",actor.tactical.HasPlayerMemory&&Vector3.Distance(observed,P.transform.position+Vector3.up*1.1f)<.01f);
    wall.SetActive(true);P.SetFeet(origin+new Vector3(3,.03f,9));Physics.SyncTransforms();actor.tactical.ObservePlayer();
    C("A01_hidden_position_not_read",actor.tactical.HasPlayerMemory&&Vector3.Distance(actor.tactical.ObservedPlayerAim,observed)<.01f&&Vector3.Distance(observed,P.transform.position+Vector3.up*1.1f)>2);
    yield return new WaitForSeconds(3.1f);
    actor.tactical.ObservePlayer();
    C("A01_memory_expires_after_three_seconds",!actor.tactical.HasPlayerMemory&&Vector3.Distance(actor.tactical.ObservedPlayerAim,actor.AimPoint+actor.transform.forward*10)<.01f);
    wall.SetActive(false);ClearActors();yield return null;
   }
   IEnumerator HazardKnowledge()
   {
    P.ResetAt(origin+new Vector3(-10,.03f,-10));P.health=10000;
    var actor=Spawn(EnemyKind.Assault,origin+Vector3.up*.03f);actor.transform.rotation=Quaternion.identity;
    var hazard=actor.GetComponent<EnemyHazardAwareness>();wall.SetActive(true);Physics.SyncTransforms();
    var field=ClassDenialField.Create(P,origin+new Vector3(0,.03f,7),16);fields.Add(field);
    hazard.TryMovement(actor.transform.position,actor.AimPoint+Vector3.forward*10,false,out _);
    C("A02_global_field_not_automatic_knowledge",hazard.KnownCount==0&&hazard.PredictedDamage(field.transform.position)==0);
    wall.SetActive(false);Physics.SyncTransforms();yield return new WaitForSeconds(.16f);
    float observedAt=Time.time;bool moved=hazard.TryMovement(actor.transform.position,actor.AimPoint+Vector3.forward*10,false,out _);
    C("A02_seen_field_has_reaction_delay",hazard.KnownCount==1&&!moved&&hazard.ReactionReadyAt-observedAt>=.34f&&hazard.ReactionReadyAt-observedAt<=.61f,"delay="+(hazard.ReactionReadyAt-observedAt));
    float health=actor.health,ground=hazard.PredictedDamage(field.transform.position),upper=hazard.PredictedDamage(field.transform.position+Vector3.up*2);
    C("A02_prediction_readonly_and_same_floor",ground>0&&upper==0&&Mathf.Abs(actor.health-health)<.001f,"ground="+ground+";upper="+upper);
    field.Cancel();CancelFields();ClearActors();yield return null;
    actor=Spawn(EnemyKind.Assault,origin+Vector3.up*.03f);actor.transform.rotation=Quaternion.identity;hazard=actor.GetComponent<EnemyHazardAwareness>();
    field=ClassDenialField.Create(P,actor.transform.position,16);fields.Add(field);health=actor.health;
    // With tactical ticks suppressed, this memory can only arrive from a real field hit.
    yield return new WaitForSeconds(.28f);
    C("A02_actual_hurt_confirms_field",actor.health<health&&hazard.KnownCount==1);
    actor.stunLeft=.7f;float decisionTime=Time.time;int beforeDecisions=hazard.Decisions;
    bool early=hazard.TryMovement(actor.transform.position,actor.AimPoint+Vector3.forward*10,false,out _);
    C("A02_swap_stagger_blocks_early_reaction",!early&&hazard.Decisions==beforeDecisions);
    yield return new WaitForSeconds(.75f);
    Vector3 initial=actor.transform.position;
    bool choice=hazard.TryMovement(initial,actor.AimPoint+Vector3.forward*10,false,out var chosen);
    var path=new NavMeshPath();bool reachable=NavMesh.CalculatePath(initial,chosen,NavMesh.AllAreas,path)&&path.status==NavMeshPathStatus.PathComplete;
    C("A02_bounded_legal_escape_candidate",choice&&reachable&&EnemyMotion.ClearVolume(chosen,actor.Radius,actor.Height,actor.transform)&&Vector3.Distance(initial,chosen)<=4.7f&&hazard.PredictedDamage(chosen)<hazard.PredictedDamage(initial),"chosen="+chosen+";prediction="+hazard.PredictedDamage(chosen));
    C("A02_choice_never_teleports",Vector3.Distance(actor.transform.position,initial)<.01f);
    int choices=hazard.Decisions;hazard.TryMovement(initial,actor.AimPoint+Vector3.forward*10,false,out _);
    C("A02_same_frame_does_not_rescore",hazard.Decisions==choices);
    G.qaSuppressAI=false;yield return new WaitForSeconds(.55f);G.qaSuppressAI=true;
    C("A02_escape_uses_motion_not_teleport",Vector3.Distance(initial,actor.transform.position)>.05f&&Vector3.Distance(initial,actor.transform.position)<2.8f,"moved="+Vector3.Distance(initial,actor.transform.position));
    float end=Time.time+field.SecondsLeft+.15f;while(Time.time<end)yield return null;
    hazard.TryMovement(actor.transform.position,actor.AimPoint+Vector3.forward*10,false,out _);
    C("A02_expired_hazard_restores_normal_choice",hazard.KnownCount==0&&hazard.PredictedDamage(actor.transform.position)==0);
    ClearActors();CancelFields();yield return null;
   }
   IEnumerator RealChapterGeometry()
   {
    if(G.StoryModal)G.CloseStoryModal();G.OpenExpeditionBoard();
    string folder=Path.Combine(RunStorage.Root,"D2-real-geometry");Directory.CreateDirectory(folder);
    File.WriteAllText(Path.Combine(folder,"expedition-v6.json"),JsonUtility.ToJson(TacticalDemoProfile.Create(0,true)));
    G.expeditionStore=new ExpeditionStore(folder,true);G.qaSuppressAI=true;FireControlRelay.FeatureEnabled=true;
    C("R_GEOMETRY_real_chapter_start",G.BeginExpedition(4));P.enabled=false;
    yield return new WaitForSeconds(3.65f);
    var relay=G.expeditionMap.root.GetComponent<FireControlRelay>();
    if(!relay||!relay.Sender||!relay.Receiver||!relay.Device){C("R_GEOMETRY_fixture_members",false);yield break;}
    var sender=relay.Sender;var receiver=relay.Receiver;
    var expectedR=new Vector3(-4,0,22);var expectedS=new Vector3(-7.6f,4.4f,28);var source=new Vector3(-6,0,18);
    P.CancelActions();P.SetFeet(source);P.cooldown=0;Aim(sender.AimPoint);Physics.SyncTransforms();
    bool clear=!Physics.Linecast(relay.Device.AimPoint,relay.ReceivePoint,Layers.WorldMask,QueryTriggerInteraction.Ignore);
    C("R_GEOMETRY_real_offsets_and_clear_link",Vector3.Distance(sender.transform.position,expectedR)<.15f&&Vector3.Distance(receiver.transform.position,expectedS)<.15f&&clear,"R="+sender.transform.position+";S="+receiver.transform.position+";device="+relay.Device.AimPoint);
    bool volumes=EnemyMotion.ClearVolume(sender.transform.position,sender.Radius,sender.Height,sender.transform,P.transform)&&EnemyMotion.ClearVolume(receiver.transform.position,receiver.Radius,receiver.Height,receiver.transform);
    float edgeMargin=(-7-receiver.transform.position.x)-receiver.Radius;
    C("R_GEOMETRY_actor_volumes_and_tower_margin",volumes&&edgeMargin>.1f,"S edge capsule margin="+edgeMargin);
    var validity=P.ValidateSwap(sender);bool aimed=P.FindTarget()==sender;int count=G.totalSwaps;
    C("R_GEOMETRY_source_Q_visible_and_legal",aimed&&validity.IsValid,"Q="+validity.Code);
    bool accepted=P.RequestSwap(sender);yield return new WaitForSeconds(G.config.swapWindup+.12f);
    C("R_GEOMETRY_transaction_really_exchanged",accepted&&G.totalSwaps==count+1&&Vector3.Distance(P.transform.position,expectedR)<.15f&&Vector3.Distance(sender.transform.position,source)<.15f);
    yield return new WaitForSeconds(G.config.swapStun+.15f);Physics.SyncTransforms();relay.Revalidate();
    bool blocked=Physics.Linecast(relay.Device.AimPoint,relay.ReceivePoint,out var hit,Layers.WorldMask,QueryTriggerInteraction.Ignore);
    bool board=blocked&&hit.point.x>=-7.15f&&hit.point.x<=-6.85f&&hit.point.y>=3.65f&&hit.point.y<=4.45f&&hit.point.z>=23.9f&&hit.point.z<=34.1f;
    C("R_GEOMETRY_floor_break_survives_stagger",sender.stunLeft<=.001f&&blocked&&board&&relay.State==RelayLinkState.Disconnected&&relay.Reason.Contains("遮挡"),"device="+relay.Device.AimPoint+";hit="+(blocked?hit.collider.name+" @ "+hit.point:"none")+";state="+relay.State);
    Vector3 reconnect=sender.transform.position+Vector3.right*4;
    var path=new NavMeshPath();bool route=NavMesh.CalculatePath(sender.transform.position,reconnect,NavMesh.AllAreas,path)&&path.status==NavMeshPathStatus.PathComplete;
    Vector3 deviceAtReconnect=relay.Device.AimPoint+(reconnect-sender.transform.position);
    var territory=sender.GetComponent<MissionEncounterAgent>();
    C("R_GEOMETRY_legal_reconnection_route",route&&(!territory||territory.Allows(reconnect))&&EnemyMotion.ClearVolume(reconnect,sender.Radius,sender.Height,sender.transform)&&!Physics.Linecast(deviceAtReconnect,relay.ReceivePoint,Layers.WorldMask,QueryTriggerInteraction.Ignore),"candidate="+reconnect+";path="+path.status);
    G.AbandonExpedition();G.OpenExpeditionBoard();
   }
  }
 }
}
