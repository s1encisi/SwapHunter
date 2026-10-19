using System;
using System.Collections;
using UnityEngine;
using Unity.AI.Navigation;
namespace SwapHunter
{
 // Isolated physical projectile statistics and state fixtures, not a human playthrough.
 public static class EnemyV2Checks
 {
  public static IEnumerator Run(Action<string,bool,string> record)=>new Fixture(record).Run();
  public static IEnumerator RunBlastShield(Action<string,bool,string> record)=>new Fixture(record).Run(true);
  sealed class Fixture
  {
   readonly Action<string,bool,string> add;readonly Vector3 origin=new Vector3(1900,0,0);Transform floor;EnemyActor actor;DemoGame G=>DemoGame.I;PlayerMotor P=>G.player;
   public Fixture(Action<string,bool,string> record){add=record;}
   void C(string id,bool pass,string detail=""){add(id,pass,detail);}
   void ClearActor(){if(actor){G.enemies.Remove(actor);actor.gameObject.SetActive(false);UnityEngine.Object.Destroy(actor.gameObject);}actor=null;}
   IEnumerator Fresh(EnemyKind kind,float distance)
   {
    ClearActor();foreach(var bolt in UnityEngine.Object.FindObjectsByType<Bolt>(FindObjectsSortMode.None))if(bolt&&!bolt.IsRetired)bolt.Retire();
    P.ResetAt(origin+new Vector3(0,.03f,distance));P.health=100000;P.SetLook(180,0);
    actor=EnemyActor.Spawn(kind,origin+Vector3.up*.03f,0);if(!actor)throw new InvalidOperationException("EnemyV2 fixture spawn failed: "+kind);
    actor.suppressAI=true;actor.transform.rotation=Quaternion.identity;Physics.SyncTransforms();yield return null;
   }
   public IEnumerator Run(bool blastOnly=false)
   {
    if(!RunStorage.IsValidation)throw new InvalidOperationException("EnemyV2 checks require isolated storage.");float oldScale=Time.timeScale;
    try
    {
     if(G.expeditionActive){G.AbandonExpedition();G.OpenExpeditionBoard();}G.StartRun(false);G.qaSuppressAI=true;G.ClearDeployments();foreach(var e in G.enemies)if(e)e.gameObject.SetActive(false);
     floor=new GameObject("MapV2 isolated projectile fixture").transform;Shapes.Box("physical floor",floor,origin+Vector3.down*.5f,new Vector3(100,1,100),1,true,Layers.World);
     var nav=floor.gameObject.AddComponent<NavMeshSurface>();nav.collectObjects=CollectObjects.Children;nav.layerMask=Layers.WorldMask;nav.useGeometry=UnityEngine.AI.NavMeshCollectGeometry.PhysicsColliders;RuntimeNavigationOwner.BuildFresh(nav);
     Time.timeScale=8;
     yield return BlastShieldChecks();
     if(!blastOnly)
     {
     foreach(float distance in new[]{10f,20f,35f})
     {
      yield return Fresh(EnemyKind.Assault,distance);float begin=Time.time,deadline=begin+260;int first=-1;Vector3 targetStart=P.transform.position;float drift=0;
      Vector3 center=P.controller.bounds.center;float queryWidth=0;for(int i=-100;i<=100;i++){var ray=new Ray(center+Vector3.right*(i*.005f)+Vector3.back*2,Vector3.forward);if(P.controller.Raycast(ray,out var h,4))queryWidth+=.005f;}
      while(actor.shotsFired<300&&Time.time<deadline)
      {actor.tactical.ObservePlayer();actor.tactical.FireBasic();if(first<0&&actor.shotsFired>0)first=Time.frameCount;P.health=100000;drift=Mathf.Max(drift,Vector3.Distance(targetStart,P.transform.position));yield return null;}
      yield return new WaitForSeconds(1.2f);float ratio=actor.shotsFired>0?(float)actor.ProjectileHits/actor.shotsFired:0;
      float min=distance==10?.8f:distance==20?.65f:.4f,max=distance==10?.95f:distance==20?.85f:.65f;
      C("AI_fixed_"+distance+"m_300",actor.shotsFired>=300&&ratio>=min&&ratio<=max,"shots="+actor.shotsFired+";hits="+actor.ProjectileHits+";ratio="+ratio.ToString("F4")+";firstShot="+(actor.FirstShotTime-begin).ToString("F3")+";muzzleBlocked="+actor.MuzzleBlockedShots+";controllerRadius="+P.controller.radius+";rayQueryWidth="+queryWidth+";targetDrift="+drift+";timeScale=8;real_Bolt_CombatRay;no_invulnerability_only_health_fixture");
     }
     yield return Fresh(EnemyKind.Assault,20);var wall=Shapes.Box("opaque test wall",floor,origin+new Vector3(0,1.4f,10),new Vector3(6,3,.5f),1,true,Layers.World);Physics.SyncTransforms();
     float until=Time.time+3;while(Time.time<until){actor.tactical.ObservePlayer();actor.tactical.FireBasic();yield return null;}
     C("AI_no_wall_tracking",actor.shotsFired==0&&!actor.tactical.HasPlayerMemory,"shots="+actor.shotsFired);wall.SetActive(false);UnityEngine.Object.Destroy(wall);
     for(int i=0;i<14;i++){yield return Fresh((EnemyKind)i,18);C("AI_archetype_hp_"+i,Mathf.Abs(actor.health-EnemyV2Definition.Health((EnemyKind)i,G.config))<.01f,"hp="+actor.health);}
     for(int i=0;i<12;i++)
     {
      yield return Fresh(EnemyKind.Elite,12);var boss=actor.EnableMiniBoss(i);boss.SetPosts(new[]{origin+new Vector3(6,.03f,4),origin+new Vector3(-6,.03f,4)});float deadline=Time.time+20;
      while(boss.CompletedCasts<2&&Time.time<deadline){actor.tactical.ObservePlayer();boss.Tick(Time.deltaTime);P.health=100000;yield return null;}
      C("AI_miniboss_"+boss.Id,boss.CompletedCasts>=2&&actor.boss==null,"hp="+actor.maximumHealth+";casts="+boss.CastCount+";completed="+boss.CompletedCasts+";independentFinalBoss="+(actor.boss==null));
      actor.ApplySilence(3);float silence=actor.silenceLeft;actor.ApplySilence(3);C("AI_miniboss_silence_"+boss.Id,silence<=1.5f&&actor.silenceLeft<=silence+.001f,"duration="+silence);
     }
     yield return Fresh(EnemyKind.Elite,15);actor.EnableBoss();C("AI_final_hp_energy",actor.maximumHealth==7200&&actor.boss.Energy==360,"health="+actor.maximumHealth+";energy="+actor.boss.Energy);
     yield return Fresh(EnemyKind.BarrierGunner,18);var device=actor.GetComponentInChildren<EnemyV2Device>();float bodyBefore=actor.health;var batch=new DamageBatch(DamageSource.PlayerBullet,P.transform,P.Eye.position);batch.V2Device(device,72,batch.NewEvent());batch.Apply();C("AI_component_independent",device&&device.Health==72&&actor.health==bodyBefore,"device="+(device?device.Health:0)+";body="+actor.health);
     var field=EnemyV2DynamicField.Spawn(origin+Vector3.right*12);C("AI_dynamic_field_endpoints",field&&EnemyV2DynamicField.BlocksQ(origin+Vector3.right*12)&&!EnemyV2DynamicField.BlocksQ(origin),"sphere_radius=6");if(field){field.Device.ApplyDamage(144,P.transform.position);yield return null;C("AI_field_destroyed",!EnemyV2DynamicField.BlocksQ(origin+Vector3.right*12));}
     yield return KnifeChecks();
     }
    }
    finally{Time.timeScale=oldScale;ClearActor();if(floor){floor.gameObject.SetActive(false);UnityEngine.Object.Destroy(floor.gameObject);}G.qaSuppressAI=true;G.SetState(RunState.Menu);}
    yield return null;
   }
   IEnumerator KnifeChecks()
   {
    yield return Fresh(EnemyKind.Assault,12);P.combatTools.ResetCombat();var tools=P.combatTools;bool first=tools.Throw();yield return new WaitForSeconds(.7f);bool second=tools.Throw();yield return new WaitForSeconds(.7f);bool third=tools.Throw();yield return new WaitForSeconds(.7f);
    C("K_three_independent",first&&second&&third&&tools.KnivesInHand==0&&tools.ActiveKnives().Length==3,"inHand="+tools.KnivesInHand+";inWorld="+tools.ActiveKnives().Length);
    C("K_body_execution",!actor.Alive,"ordinaryHpAfter="+actor.health);
    bool recall=tools.Recall();yield return new WaitForSeconds(3);C("K_recall_conservation",recall&&tools.KnivesInHand==3&&tools.ActiveKnives().Length==0,"inHand="+tools.KnivesInHand);
    yield return Fresh(EnemyKind.Elite,12);tools.ResetCombat();float before=actor.health;bool a=tools.Throw();yield return new WaitForSeconds(.7f);float firstSilence=actor.silenceLeft;bool b=tools.Throw();yield return new WaitForSeconds(.7f);
    C("K_elite_damage_and_shared_silence",a&&b&&Mathf.Abs(before-actor.health-168)<.1f&&tools.SilenceCooldownLeft>6&&actor.silenceLeft<firstSilence,"loss="+(before-actor.health)+";shared="+tools.SilenceCooldownLeft+";silenceBefore="+firstSilence+";silenceAfter="+actor.silenceLeft);
    tools.ResetCombat();yield return Fresh(EnemyKind.Assault,12);var wall=Shapes.Box("knife occlusion",floor,origin+new Vector3(0,1.5f,6),new Vector3(5,3,.5f),1,true,Layers.World);Physics.SyncTransforms();tools.Throw();yield return new WaitForSeconds(.7f);
    C("K_wall_preserves_silence",actor.Alive&&tools.Knife&&tools.Knife.WallStops>0&&tools.SilenceCooldownLeft==0,"wallStops="+(tools.Knife?tools.Knife.WallStops:0));wall.SetActive(false);UnityEngine.Object.Destroy(wall);tools.ResetCombat();
    var shield=DirectionalBarrier.Spawn(origin+new Vector3(0,1.5f,6),Vector3.forward,4,null);tools.Throw();yield return new WaitForSeconds(.7f);C("K_Ashield_direction",actor.Alive&&tools.Knife&&tools.Knife.WallStops>0&&tools.SilenceCooldownLeft==0);if(shield)UnityEngine.Object.Destroy(shield.gameObject);tools.ResetCombat();
   }
   IEnumerator BlastShieldChecks()
   {
    yield return Fresh(EnemyKind.Assault,12);P.ResetAt(origin+new Vector3(0,.03f,-1.2f));P.health=100000;Physics.SyncTransforms();Vector3 blast=origin+Vector3.forward*1.5f;
    float playerBefore=P.health,enemyBefore=actor.health;EnemyV2Danger.Area(actor,blast,3,24,shieldExplosion:true);yield return new WaitForSeconds(.2f);float playerBase=playerBefore-P.health,enemyBase=enemyBefore-actor.health;
    var shield=DirectionalBarrier.Spawn(origin+new Vector3(0,1.1f,.6f),Vector3.forward,10,null);
    playerBefore=P.health;enemyBefore=actor.health;EnemyV2Danger.Area(actor,blast,3,24,shieldExplosion:true);yield return new WaitForSeconds(.2f);float playerShield=playerBefore-P.health,enemyShield=enemyBefore-actor.health;
    playerBefore=P.health;enemyBefore=actor.health;EnemyV2Danger.Area(actor,blast,3,24);yield return new WaitForSeconds(.2f);float playerGround=playerBefore-P.health,enemyGround=enemyBefore-actor.health;
    shield.transform.rotation=Quaternion.Euler(0,180,0);playerBefore=P.health;enemyBefore=actor.health;EnemyV2Danger.Area(actor,blast,3,24,shieldExplosion:true);yield return new WaitForSeconds(.2f);float playerRear=playerBefore-P.health,enemyRear=enemyBefore-actor.health;
    C("AI_explosion_direction_vs_ground",playerBase>0&&enemyBase>0&&Mathf.Abs(playerShield/playerBase-.35f)<.002f&&Mathf.Abs(enemyShield/enemyBase-.35f)<.002f&&Mathf.Abs(playerGround/playerBase-1)<.002f&&Mathf.Abs(enemyGround/enemyBase-1)<.002f&&Mathf.Abs(playerRear/playerBase-1)<.002f&&Mathf.Abs(enemyRear/enemyBase-1)<.002f,"player baseline/front/ground/rear="+playerBase+"/"+playerShield+"/"+playerGround+"/"+playerRear+";enemy="+enemyBase+"/"+enemyShield+"/"+enemyGround+"/"+enemyRear);
    UnityEngine.Object.Destroy(shield.gameObject);yield return null;
   }
  }
 }
}
