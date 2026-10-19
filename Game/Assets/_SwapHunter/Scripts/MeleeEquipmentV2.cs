using System;using System.Collections.Generic;using UnityEngine;
namespace SwapHunter
{
 public sealed class KnifePickupV2:MonoBehaviour
 {
  static readonly List<KnifePickupV2> active=new List<KnifePickupV2>();
  public string InstanceId{get;private set;} public int KnifeId{get;private set;} public bool Closed{get;private set;}
  public KnifeV2Visual Visual{get;private set;} public bool Available=>Visual&&!reserved&&!collected;
  bool reserved,collected;GameObject displayMarker;public Vector3 Point=>Visual?Visual.Grip.position:transform.position;
  public string Hint=>KnifeV2Visual.Names[KnifeId]+" · 槽3替换 / 成功撤离永久拥有";
  public static KnifePickupV2 Create(Transform parent,int id,Vector3 position,Quaternion rotation,bool closed=false)
  {
   var go=new GameObject("刀具拾取 / "+KnifeV2Visual.Names[id]);go.transform.SetParent(parent,false);go.transform.localPosition=position;go.transform.localRotation=rotation;
   var item=go.AddComponent<KnifePickupV2>();item.KnifeId=id;item.Closed=closed;item.InstanceId=Guid.NewGuid().ToString("N");
   item.Visual=KnifeV2Visual.Create(id,go.transform,0);item.Visual.SetFold(closed?0:1);
   item.displayMarker=new GameObject("刀具陈列提示");item.displayMarker.transform.SetParent(go.transform,false);
   Shapes.Ring(item.displayMarker.transform,item.Point+Vector3.up*.025f,.20f,new Color(.92f,.58f,.18f),.012f);
   var title=D2WorldText.Create(item.displayMarker.transform,KnifeV2Visual.Names[id],item.Point+Vector3.up*.24f,.22f);title.color=new Color(.95f,.78f,.48f);title.GetComponent<ExpeditionWorldLabel>().maximumDistance=10;
   var lamp=new GameObject("低亮度陈列灯");lamp.transform.SetParent(item.displayMarker.transform,false);lamp.transform.position=item.Point+Vector3.up*.10f;var light=lamp.AddComponent<Light>();light.type=LightType.Point;light.range=1.3f;light.intensity=.35f;light.color=new Color(1,.62f,.25f);light.shadows=LightShadows.None;
   return item;
  }
  void OnEnable(){active.Add(this);}void OnDisable(){active.Remove(this);}
  public static KnifePickupV2 Focus(PlayerMotor p)
  {
   KnifePickupV2 chosen=null;float best=-1;
   foreach(var item in active)
   {
    if(!item||!item.Available)continue;Vector3 d=item.Point-p.Eye.position;float length=d.magnitude;
    if(length>2.3f||Vector3.Dot(p.Eye.forward,d.normalized)<.72f||Physics.Linecast(p.Eye.position,item.Point,Layers.WorldMask,QueryTriggerInteraction.Ignore))continue;
    float score=Vector3.Dot(p.Eye.forward,d.normalized)*4-length;if(score>best){best=score;chosen=item;}
   }
   return chosen;
  }
  public bool CanBegin(PlayerMotor p,out string reason)
  {
   reason="";if(!Available){reason="刀具已被取走";return false;}
   Vector3 flat=Vector3.ProjectOnPlane(Point-p.transform.position,Vector3.up);
   if(flat.magnitude>.8f){reason="靠近握柄后拾取（0.8米内）";return false;}
   bool table=Point.y-p.transform.position.y>.55f;
   Vector3 shoulder=p.V2PickupShoulder(table);
   if(!p.V2PickupReachable(Visual.Grip,table)){reason="当前姿态够不到握柄，请靠近或调整方向";return false;}
   if(Physics.Linecast(shoulder,Point,Layers.WorldMask,QueryTriggerInteraction.Ignore)){reason="握柄被遮挡";return false;}
   return true;
  }
  public bool Reserve(PlayerMotor p){if(!CanBegin(p,out _))return false;reserved=true;if(displayMarker)displayMarker.SetActive(false);return true;}
  public void CancelReserve(){if(!collected){reserved=false;if(displayMarker)displayMarker.SetActive(true);}}
  public KnifeV2Visual Commit(){if(!reserved||collected||!Visual)return null;collected=true;reserved=false;if(displayMarker)displayMarker.SetActive(false);var model=Visual;Visual=null;return model;}
 }
 [DefaultExecutionOrder(100)]
 public sealed class MeleeEquipmentV2:MonoBehaviour
 {
  PlayerMotor p;DemoGame G=>DemoGame.I;KnifeV2Visual held;KnifePickupV2 pickup;
  readonly HashSet<int> collected=new HashSet<int>();readonly HashSet<string> acquiredIds=new HashSet<string>();
  float pickupAge=-1,grabTime,readyTime,endTime,equipLeft,attackAge=-1,inspectAge=-1;bool pickupCommitted,table,heavy,quick,damageCommitted,wasEquipped;
  Vector3 originalGrip,originalFeet,reachStart;Quaternion originalRotation;
  public int KnifeId{get;private set;}public bool Equipped{get;private set;}
  public bool PickingUp=>pickupAge>=0;
  public bool AttackActive=>attackAge>=0;
  public bool Ready=>(!PickingUp||pickupCommitted&&pickupAge>=readyTime)&&equipLeft<=0;
  public KnifeV2Visual HeldVisual=>held;
  public float LastPickupGripError{get;private set;}
  public float LastPickupWorldTransferError{get;private set;}
  public float PickupSeconds=>pickupAge;
  public bool PickupCommitted=>pickupCommitted;
  public int[] AttemptPickups {get{var a=new int[collected.Count];collected.CopyTo(a);return a;}}
  public string CurrentName=>KnifeV2Visual.Names[KnifeId];
  public void Initialize(PlayerMotor player){p=player;ResetForAttempt();}
  public void ResetForAttempt()
  {
   CancelPickup();collected.Clear();acquiredIds.Clear();attackAge=inspectAge=-1;Equipped=false;equipLeft=0;
   KnifeId=G.expeditionActive&&G.expeditionStore!=null&&G.expeditionStore.IsTactical&&G.expeditionStore.Profile.deployment!=null?G.expeditionStore.Profile.deployment.knifeId:0;
   ReplaceHeld(KnifeV2Visual.Create(KnifeId,p.Eye,Layers.ViewModel));if(held)held.gameObject.SetActive(false);
  }
  void ReplaceHeld(KnifeV2Visual model){if(held&&held!=model)Destroy(held.gameObject);held=model;KnifeId=model.KnifeId;held.transform.SetParent(p.Eye,true);}
  public void Inspect(){if(Equipped&&Ready)inspectAge=0;}
  public void Equip()
  {if(!G.IsPlaying||p.health<=0||p.SwapActionsLocked||p.TacticalAdvanceActive)return;CancelPickup();p.SuspendReloadV2();Equipped=true;equipLeft=.30f;inspectAge=-1;G.sound.Play("knife_swipe");}
  public void Unequip(){CancelPickup();Equipped=false;attackAge=inspectAge=-1;if(held)held.gameObject.SetActive(false);p.SetV2EquipmentVisibility(true);p.RestoreFirearmHands();}
  public bool TryPickup(KnifePickupV2 target)
  {
   if(!G.IsPlaying||p.health<=0||p.SwapActionsLocked||p.TacticalAdvanceActive||PickingUp||AttackActive||target==null)return false;
   if(!target.CanBegin(p,out var reason)){G.Toast(reason,2);return false;}
   if(!target.Reserve(p))return false;
   p.SuspendReloadV2();pickup=target;pickupAge=0;pickupCommitted=false;wasEquipped=Equipped;
   originalGrip=target.Point;originalRotation=target.Visual.Grip.rotation;originalFeet=p.transform.position;reachStart=p.Eye.TransformPoint(new Vector3(.2f,-.3f,.4f));
   table=originalGrip.y-p.transform.position.y>.55f;grabTime=table?.24f:.35f;readyTime=table?.50f:.70f;endTime=table?.60f:.80f;
   if(target.Closed&&target.KnifeId==3){readyTime+=.12f;endTime+=.12f;}
   G.Record("knife_pickup_reserve",target.InstanceId+";knife="+target.KnifeId);return true;
  }
  public void CancelForAction(){CancelPickup();if(quick&&AttackActive)Equipped=wasEquipped;attackAge=inspectAge=-1;}
  public void CancelPickup()
  {
   if(!PickingUp)return;
   if(pickup&&!pickupCommitted)pickup.CancelReserve();
   bool keep=pickupCommitted;pickup=null;pickupAge=-1;Equipped=keep||wasEquipped;
   if(p){p.SetPickupBody(0);p.SetPickupProjectionV2(false);}
   if(G)G.Record("knife_pickup_cancel",keep?"after_commit_owned":"before_commit_world");
  }
  void Update()
  {
   if(!p||!G.IsPlaying)return;
   if(p.health<=0){CancelPickup();return;}
   float dt=Time.deltaTime;equipLeft=Mathf.Max(0,equipLeft-dt);
   if(G.Pressed(25))Equip();if(G.Pressed(28))Inspect();
   if(PickingUp)
   {
    pickupAge+=dt;
    if(pickupCommitted&&pickupAge>=readyTime&&(G.Pressed(12)||G.Pressed(13))){BeginAttack(G.Pressed(13),false);return;}
    bool action=G.Pressed(10)||G.Pressed(11)||G.Pressed(12)||G.Pressed(6);
    if(action||Vector3.Distance(originalFeet,p.transform.position)>.25f||(!pickupCommitted&&(!pickup||!pickup.Visual||Vector3.Distance(pickup.Point,originalGrip)>.12f))){CancelPickup();return;}
    if(!pickupCommitted&&pickupAge>=grabTime)
    {
     p.SetPickupBody(table?.1f:1,table);p.PoseKnifeHands(pickup.Visual.Grip,1,originalGrip);
     LastPickupGripError=p.V2KnifeGripError(pickup.Visual.Grip);
     if(LastPickupGripError>.035f){G.Toast("握柄尚未接触，请靠近后再拾取",2);CancelPickup();return;}
     var visual=pickup.Commit();if(!visual){CancelPickup();return;}
     pickupCommitted=true;acquiredIds.Add(pickup.InstanceId);collected.Add(visual.KnifeId);
     // Same mesh and exact world pose cross the ownership boundary; no duplicate pickup instance.
     Vector3 pos=visual.transform.position;Quaternion rot=visual.transform.rotation;ReplaceHeld(visual);held.transform.SetPositionAndRotation(pos,rot);LastPickupWorldTransferError=Vector3.Distance(pos,held.transform.position);
     EquipedAfterGrab();G.Record("knife_pickup_commit",pickup.InstanceId+";knife="+KnifeId);G.sound.Play("reload_in");
    }
    if(pickupAge>=endTime){pickupAge=-1;pickup=null;Equipped=true;p.SetPickupBody(0);}
   }
   else if(Equipped&&Ready)
   {if(G.Pressed(12))BeginAttack(false,false);else if(G.Pressed(13))BeginAttack(true,false);}
   if(inspectAge>=0){inspectAge+=dt;if(inspectAge>=1.2f)inspectAge=-1;}
   if(attackAge>=0)
   {
    attackAge+=dt;
    if(!damageCommitted&&attackAge>=(heavy?.26f:.10f)){damageCommitted=true;ApplyMelee();}
    if(attackAge>=(heavy?.9f:.5f)){attackAge=-1;if(quick)Equipped=wasEquipped;}
   }
  }
  void EquipedAfterGrab(){Equipped=true;held.gameObject.SetActive(true);}
  public bool BeginAttack(bool isHeavy,bool quickAttack)
  {
   if(!G.IsPlaying||p.health<=0||p.SwapActionsLocked||p.TacticalAdvanceActive||AttackActive||equipLeft>0)return false;
   if(PickingUp){if(pickupAge<readyTime)return false;CancelPickup();}
   p.SuspendReloadV2();wasEquipped=Equipped;Equipped=true;heavy=isHeavy;quick=quickAttack;attackAge=0;damageCommitted=false;inspectAge=-1;
   if(p.classSkill)p.classSkill.EndForAttack("melee");D2HoldInteraction.CancelForAction();G.sound.Play("knife_swipe");return true;
  }
  void ApplyMelee()
  {
   float range=heavy?1.6f:1.8f;var batch=new DamageBatch(DamageSource.PlayerMelee,p.transform,p.Eye.position);long evt=batch.NewEvent();
   var seen=new HashSet<int>();foreach(var enemy in G.enemies.ToArray())
   {
    if(!enemy||!enemy.Alive)continue;Vector3 delta=enemy.AimPoint-p.Eye.position;
    if(delta.magnitude>range||Vector3.Dot(delta.normalized,p.Eye.forward)<.65f)continue;
    if(!EnemyActor.CastWeaponRay(p.Eye.position,delta.normalized,range,p.transform,out var hit))continue;
    int id=hit.collider.GetInstanceID();if(!seen.Add(id))continue;
    bool device=hit.collider.GetComponentInParent<RelayDevice>()||hit.collider.GetComponentInParent<EnemyV2Device>();
    bool back=!device&&Vector3.Dot(enemy.transform.forward,(p.transform.position-enemy.transform.position).normalized)<-.65f;
    batch.Direct(hit.collider,(heavy?144:72)*(back?3:1),hit.point,evt,false);Shapes.Impact(hit.point,hit.normal,enemy.IsShieldCollider(hit.collider));
   }
   batch.Apply();G.Record("knife_melee","heavy="+heavy+";knife="+KnifeId);
  }
  void LateUpdate()
  {
   if(!p||!held)return;p.SetPickupProjectionV2(PickingUp);
   ImportedModels.SetLayer(held.transform,PickingUp?Layers.Actor:Layers.ViewModel);
   bool show=Equipped||PickingUp||AttackActive;
   if(!show&&p.V2ItemsSelected)return;
   p.SetV2EquipmentVisibility(!show||(PickingUp&&pickupAge<.10f));held.gameObject.SetActive(show&&(!PickingUp||pickupCommitted));
   if(!show)return;
   Vector3 rest=p.Eye.TransformPoint(new Vector3(.20f,-.25f,.48f));Quaternion rotation=p.Eye.rotation*Quaternion.Euler(-12,-12,0)*(KnifeId==2?Quaternion.Euler(165,0,15):Quaternion.identity);
   if(PickingUp)
   {
    float bend=table?.1f:Mathf.Sin(Mathf.Clamp01(pickupAge/endTime)*Mathf.PI);
    p.SetPickupBody(bend,table);float eyeHeight=p.IsCrouching?.98f:1.62f;
    float approach=Mathf.Min(.26f,Vector3.ProjectOnPlane(originalGrip-originalFeet,Vector3.up).magnitude*.65f);
    p.Eye.localPosition=Vector3.up*(table?eyeHeight:Mathf.Lerp(eyeHeight,.78f,bend))+Vector3.forward*(table?0:approach*bend);
    float reach=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.1f,grabTime-.08f,pickupAge));
    Vector3 hand=Vector3.Lerp(reachStart,originalGrip,reach);
    if(pickupCommitted)
    {
     float lift=Mathf.SmoothStep(0,1,Mathf.InverseLerp(grabTime,readyTime,pickupAge));
     hand=Vector3.Lerp(originalGrip,rest,lift);if(KnifeId==3)held.SetFold(Mathf.InverseLerp(grabTime,readyTime,pickupAge));
     held.AlignGrip(hand,Quaternion.Slerp(originalRotation,rotation,lift));
     p.PoseKnifeHands(held.Grip,1);
     ImportedModels.SetLayer(held.transform,Layers.Actor);
    }
    else if(pickup&&pickup.Visual)p.PoseKnifeHands(pickup.Visual.Grip,Mathf.InverseLerp(grabTime-.1f,grabTime,pickupAge),hand);
    return;
   }
   p.SetPickupBody(0);
   float sweep=attackAge<0?0:Mathf.Sin(Mathf.Clamp01(attackAge/(heavy?.55f:.32f))*Mathf.PI);
   Vector3 position=rest+p.Eye.forward*sweep*(heavy?.32f:.20f)+p.Eye.right*sweep*-.2f;
   if(inspectAge>=0){float t=inspectAge/1.2f;held.SetInspect(t);rotation*=Quaternion.Euler(0,Mathf.Sin(t*Mathf.PI)*60,Mathf.Sin(t*Mathf.PI)*25);}
   if(equipLeft>0)position-=p.Eye.up*.25f*(equipLeft/.3f);
   held.AlignGrip(position,rotation*Quaternion.Euler(sweep*35,sweep*-35,0));p.PoseKnifeHands(held.Grip,1);
  }
  void OnDisable(){CancelPickup();}
 }
}

