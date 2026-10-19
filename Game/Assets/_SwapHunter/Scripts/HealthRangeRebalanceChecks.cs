using System;
using System.Collections;
using Unity.AI.Navigation;
using UnityEngine;
namespace SwapHunter
{
 // Only this rebalance: actual initialization, unchanged damage/cadence, range rays
 // and one real long-range gunshot. No repeat of the 900-shot AI campaign fixtures.
 public static class HealthRangeRebalanceChecks
 {
  public static IEnumerator Run(Action<string,bool,string> add)=>new Fixture(add).Run();
  sealed class Fixture
  {
   readonly Action<string,bool,string> add;readonly Vector3 origin=new Vector3(3100,0,0);Transform floor;EnemyActor actor;GameObject rangeTarget;DemoGame G=>DemoGame.I;PlayerMotor P=>G.player;
   static readonly float[] previousHp={960,960,720,1440,2400,960,720,1440,840,600,600,840,960,1200};
   static readonly float[] previousMini={1440,2400,2880,3360,3840,4320,3840,4320,4800,4800,4800,3840};
   static readonly float[] previousDamage={72,36,57,114,39,42,186,354,63,285,435,48};
   static readonly float[] previousRate={7.5f,1.2f,10,4.5f,13,2.8f,2.7f,.8f,9,.85f,1,2.4f};
   static readonly float[] expectedRanges={140,90,160,140,100,90,220,250,160,90,220,140};
   static readonly float[] previousFloors={.6f,.3f,.6f,.6f,.35f,.3f,1,1,.6f,1,1,.6f};
   public Fixture(Action<string,bool,string> callback){add=callback;}
   static bool Near(float a,float b)=>Mathf.Abs(a-b)<.01f;
   void C(string name,bool pass,string detail=""){add(name,pass,detail);}
   void ClearActor(){if(actor){G.enemies.Remove(actor);actor.gameObject.SetActive(false);UnityEngine.Object.Destroy(actor.gameObject);}actor=null;}
   IEnumerator Spawn(EnemyKind kind,Vector3 feet)
   {ClearActor();yield return null;actor=EnemyActor.Spawn(kind,feet,0);if(!actor)throw new InvalidOperationException("Rebalance fixture spawn failed "+kind);actor.suppressAI=true;Physics.SyncTransforms();yield return null;}
   public IEnumerator Run()
   {
    if(!RunStorage.IsValidation)throw new InvalidOperationException("Rebalance requires isolated storage");bool oldAI=G.qaSuppressAI,oldPlayerEnabled=P.enabled;float oldScale=Time.timeScale;
    try
    {
     if(G.StoryModal)G.CloseStoryModal();if(G.expeditionActive&&G.expeditionReceipt==null)G.AbandonExpedition();G.OpenExpeditionBoard();G.StartRun(false);G.qaSuppressAI=true;G.ClearDeployments();foreach(var e in G.enemies)if(e)e.gameObject.SetActive(false);
     floor=new GameObject("Health/range isolated fixture").transform;Shapes.Box("Physical range floor",floor,origin+new Vector3(0,-.5f,120),new Vector3(80,1,320),1,true,Layers.World);Physics.SyncTransforms();
     var nav=floor.gameObject.AddComponent<NavMeshSurface>();nav.collectObjects=CollectObjects.Children;nav.layerMask=Layers.WorldMask;nav.useGeometry=UnityEngine.AI.NavMeshCollectGeometry.PhysicsColliders;RuntimeNavigationOwner.BuildFresh(nav);Physics.SyncTransforms();yield return null;
     P.ResetAt(origin+Vector3.up*.035f);P.SetLook(0,0);P.health=100000;P.enabled=false;Time.timeScale=1;
     for(int i=0;i<14;i++)
     {yield return Spawn((EnemyKind)i,origin+new Vector3(6,.035f,8));float expected=previousHp[i]*.5f;C("rebalance_hp_"+i,Near(actor.health,expected)&&Near(actor.maximumHealth,expected),"before="+previousHp[i]+";actual="+actor.health+";max="+actor.maximumHealth);}
     for(int i=0;i<12;i++)
     {yield return Spawn(EnemyKind.Elite,origin+new Vector3(6,.035f,8));actor.EnableMiniBoss(i);float expected=previousMini[i]*.5f;C("rebalance_mini_"+(i+1),Near(actor.health,expected)&&Near(actor.maximumHealth,expected),"before="+previousMini[i]+";actual="+actor.health+";max="+actor.maximumHealth);}
     yield return Spawn(EnemyKind.Elite,origin+new Vector3(6,.035f,8));var boss=actor.EnableBoss();C("rebalance_final_hp_only",Near(actor.health,3600)&&Near(actor.maximumHealth,3600)&&Near(boss.Energy,360),"beforeHP=7200;now="+actor.health+";energy="+boss.Energy);
     boss.Absorb(360);actor.Damage(1799);bool above=boss.Phase==BossPhase.Rifle&&Near(actor.health,1801);actor.Damage(1);C("rebalance_final_half_threshold",above&&boss.Phase==BossPhase.Combined&&Near(actor.health,1800),"phase="+boss.Phase+";hp="+actor.health);
     yield return Spawn(EnemyKind.BarrierGunner,origin+new Vector3(6,.035f,8));var device=actor.GetComponentInChildren<EnemyV2Device>();C("rebalance_device_unchanged",device&&Near(device.MaximumHealth,144)&&Near(device.Health,144),"device="+(device?device.Health:0));ClearActor();yield return null;
     var display=WeaponDisplayStats.BuildAll(G.config);
     rangeTarget=new GameObject("Range boundary receiver");rangeTarget.layer=Layers.Actor;var box=rangeTarget.AddComponent<BoxCollider>();box.size=new Vector3(1,1,.05f);
     for(int id=0;id<12;id++)
     {
      var stats=EffectiveWeaponStats.Read(id,G.config);var definition=WeaponCatalog.Get(id);bool same=Near(stats.Damage,previousDamage[id])&&Near(stats.Rate,previousRate[id])&&Near(definition.minimumDamageMultiplier,previousFloors[id]);
      C("rebalance_player_damage_"+id,same,"damage="+stats.Damage+";rate="+stats.Rate+";minimumMultiplier="+definition.minimumDamageMultiplier);
      bool match=Near(stats.Range,expectedRanges[id])&&Near(display[id].MaximumRange,stats.Range)&&Near(definition.DistanceMultiplier(0),1)&&Near(definition.DistanceMultiplier(stats.Range),previousFloors[id]);
      if(id!=9)
      {
       P.SetLoadout(id,id==0?1:0);P.SetLook(0,0);rangeTarget.transform.position=P.Eye.position+Vector3.forward*(stats.Range-.3f);Physics.SyncTransforms();bool inside=P.V2WeaponRay(Vector3.forward,out var hit,out _,out _,out bool shielded)&&hit.collider==box&&!shielded;
       rangeTarget.transform.position=P.Eye.position+Vector3.forward*(stats.Range+.3f);Physics.SyncTransforms();bool outside=!P.V2WeaponRay(Vector3.forward,out _,out _,out _,out _);match&=inside&&outside;
      }
      C("rebalance_effective_range_"+id,match,"range="+stats.Range+";falloff="+definition.falloffStart+"–"+definition.falloffEnd+";floor="+definition.minimumDamageMultiplier+";actual dual-ray bounds (grenade uses real flight)");
     }
     C("rebalance_skills_damage_unchanged",Near(ClassSkillTuning.RemoteDamage(1),192)&&Near(ClassSkillTuning.RemoteDamage(2),240)&&Near(ClassSkillTuning.RemoteDamage(3),288)&&Near(ClassSkillTuning.DenialDps(1),48)&&Near(ClassSkillTuning.DenialDps(2),60)&&Near(ClassSkillTuning.DenialDps(3),72),"remote=192/240/288;denialDps=48/60/72");
     rangeTarget.SetActive(false);UnityEngine.Object.Destroy(rangeTarget);rangeTarget=null;
     yield return Spawn(EnemyKind.Assault,origin+new Vector3(0,.035f,200));P.SetLoadout(7,0);Vector3 target=actor.TorsoHitbox.bounds.center,d=target-P.Eye.position;P.SetLook(Mathf.Atan2(d.x,d.z)*Mathf.Rad2Deg,-Mathf.Atan2(d.y,new Vector2(d.x,d.z).magnitude)*Mathf.Rad2Deg);yield return new WaitForSeconds(.3f);float hp=actor.health;bool fired=P.Fire();yield return null;
     C("rebalance_real_shot_200m",fired&&actor.health<hp&&actor.LastHitRegion==EnemyHitRegion.Torso,"fired="+fired+";bodyLoss="+(hp-actor.health)+";base=354;region="+actor.LastHitRegion+";distance="+Vector3.Distance(P.Eye.position,actor.AimPoint));
     var headRay=new Ray(P.Eye.position,(actor.HeadHitbox.bounds.center-P.Eye.position).normalized);bool headFound=EnemyActor.CastWeaponRay(headRay.origin,headRay.direction,250,P.transform,out var headHit);Vector3 headPoint=headFound?headHit.point:Vector3.zero;var resolved=headFound?actor.ResolveHitCollider(headRay,headHit.collider,ref headPoint):null;C("rebalance_far_head_resolution",headFound&&resolved&&actor.RegionOf(resolved)==EnemyHitRegion.Head,"actual head-part ray at 200m;no extra shot or damage");
     ClearActor();yield return null;P.ResetAt(origin+Vector3.up*.035f);P.SetLoadout(9,0);var grenade=PlayerOrdnance.Launch(P,P.Eye.position,(Vector3.up+Vector3.forward).normalized,285);bool payload=Near(grenade.ConfiguredDamage,285)&&Near(grenade.MaximumRange,90);float started=Time.time,maxHorizontal=0;
     while(grenade&&grenade.Live&&Time.time-started<5){maxHorizontal=Mathf.Max(maxHorizontal,Vector3.ProjectOnPlane(grenade.transform.position-P.Eye.position,Vector3.up).magnitude);yield return null;}
     C("rebalance_grenade_real_flight",payload&&maxHorizontal>75&&maxHorizontal<=90,"45deg horizontal sampled="+maxHorizontal+";baseDamage=285;speed=30;fuse=4.2;gravity=12;not an enemy projectile change");
    }
    finally{ClearActor();if(rangeTarget)UnityEngine.Object.Destroy(rangeTarget);if(floor){floor.gameObject.SetActive(false);UnityEngine.Object.Destroy(floor.gameObject);}P.enabled=oldPlayerEnabled;Time.timeScale=oldScale;G.qaSuppressAI=oldAI;G.SetState(RunState.Menu);}
    yield return null;
   }
  }
 }
}
