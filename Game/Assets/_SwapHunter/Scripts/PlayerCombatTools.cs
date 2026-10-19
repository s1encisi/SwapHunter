using System.Collections.Generic;
using UnityEngine;
namespace SwapHunter
{
 [DefaultExecutionOrder(50)]
 public sealed class PlayerCombatTools:MonoBehaviour
 {
  PlayerMotor owner;float meleeCooldown,throwCooldown,pose,poseDuration;bool throwingPose;
  public float PoseProgress=>pose>0?1-pose/poseDuration:1;
  public bool PoseActive=>pose>0;
  internal float MeleeCooldownLeft=>meleeCooldown;
  internal float ThrowCooldownLeft=>throwCooldown;
  public bool ThrowingPose=>throwingPose;
  public float GripError=>handTool&&owner?Vector3.Distance(handTool.TransformPoint(new Vector3(0,0,-.105f)),owner.ToolGripWorld):0;
  Transform handTool;ThrownKnife knife;
  public int MeleeHits {get;private set;}
  public int Throws {get;private set;}
  public int ReturnHits {get;private set;}
  public int KnifeRank=>PlayerClassSkill.IsTacticalFlow?(G.expeditionStore.EquippedGeneralSkill==TacticalPrototypeRules.KnifeGeneralIndex?G.expeditionStore.EquippedGeneralSkillLevel:0):G.expeditionActive?G.expeditionStore.Profile.knifeRank:3;
  public bool CanRecallKnife=>PlayerClassSkill.IsTacticalFlow?KnifeRank>=1:KnifeRank>=3;
  public bool KnifeEquipped=>!PlayerClassSkill.IsTacticalFlow||(G.expeditionStore.EquippedGeneralSkill==TacticalPrototypeRules.KnifeGeneralIndex&&KnifeRank>=1);
  public ThrownKnife Knife=>knife;
  public string KnifeStatus=>knife?(knife.Returning?"回收中":knife.Flying?"飞行中":"已落点"):throwCooldown>0?"冷却 "+throwCooldown.ToString("F1"):"就绪";
  DemoGame G=>DemoGame.I;
  public void Initialize(PlayerMotor p)
  {
   owner=p;handTool=KnifeGeometry.Create(owner.Eye,"Held tactical blade");handTool.localPosition=new Vector3(-.23f,-.25f,.55f);ImportedModels.SetLayer(handTool,Layers.ViewModel);handTool.gameObject.SetActive(false);
  }
  public void ResetCombat(){if(knife)Destroy(knife.gameObject);knife=null;meleeCooldown=throwCooldown=pose=0;MeleeHits=Throws=ReturnHits=0;if(handTool)handTool.gameObject.SetActive(false);}
  void Update()
  {
   if(!owner||!G.IsPlaying||owner.health<=0)return;
   meleeCooldown=Mathf.Max(0,meleeCooldown-Time.deltaTime);throwCooldown=Mathf.Max(0,throwCooldown-Time.deltaTime);
   pose=Mathf.Max(0,pose-Time.deltaTime);
   
   if(G.Pressed(21))Melee();if(G.Pressed(22))Throw();if(G.Pressed(23))Recall();
   if(knife&&!knife.Flying&&!knife.Returning&&Vector3.Distance(owner.Eye.position,knife.transform.position)<1.8f){Destroy(knife.gameObject);knife=null;throwCooldown=.3f;G.sound.Play("confirm");}
  }
  void LateUpdate()
  {
   if(!handTool||!owner)return;
   handTool.gameObject.SetActive(pose>0&&(!throwingPose||PoseProgress<.22f));
   if(pose<=0)return;
   handTool.rotation=owner.ToolGripRotation;handTool.position=owner.ToolGripWorld+handTool.forward*.105f;
  }
  public bool Melee()
  {
   if(!G.IsPlaying||owner.health<=0||owner.SwapActionsLocked||meleeCooldown>0||owner.reloadLeft>0)return false;
   meleeCooldown=.75f;pose=poseDuration=.38f;throwingPose=false;
   if(owner.classSkill)owner.classSkill.EndForAttack("melee");
   G.sound.Play("knife_swipe");
   D2HoldInteraction.CancelForAction();
   var contacts=new Dictionary<int,RaycastHit>();var deviceCarriers=new HashSet<int>();
   void Probe(Vector3 aim)
   {
    Vector3 delta=aim-owner.Eye.position;
    if(delta.magnitude>2.5f||Vector3.Dot(delta.normalized,owner.Eye.forward)<.65f)return;
    if(!EnemyActor.CastWeaponRay(owner.Eye.position,delta.normalized,delta.magnitude+.2f,owner.transform,out var hit))return;
    var device=hit.collider.GetComponentInParent<RelayDevice>();
    var enemy=device?null:hit.collider.GetComponentInParent<EnemyActor>();
    var objective=device||enemy?null:hit.collider.GetComponentInParent<DamageableObjective>();
    int id=device?device.GetInstanceID():enemy?enemy.GetInstanceID():objective?objective.GetInstanceID():0;
    if(id!=0)contacts[id]=hit;
    if(device&&device.Carrier)deviceCarriers.Add(device.Carrier.GetInstanceID());
   }
   foreach(var enemy in G.enemies.ToArray())if(enemy&&enemy.Alive)Probe(enemy.AimPoint);
   foreach(var objective in DamageableObjective.ActiveSnapshot())if(objective&&objective.Alive)Probe(objective.AimPoint);
   foreach(var device in RelayDevice.ActiveSnapshot())if(device&&device.Alive)Probe(device.AimPoint);
   var batch=new DamageBatch(DamageSource.PlayerMelee,owner.transform,owner.Eye.position);long damageEvent=batch.NewEvent();
   foreach(var contact in contacts.Values)
   {
    var device=contact.collider.GetComponentInParent<RelayDevice>();
    var enemy=device?null:contact.collider.GetComponentInParent<EnemyActor>();
    if(enemy&&deviceCarriers.Contains(enemy.GetInstanceID()))continue;
    var sample=batch.Direct(contact.collider,24,contact.point,damageEvent,false);if(sample==null)continue;
    Vector3 point=contact.point,delta=point-owner.Eye.position;var target=enemy;
    sample.after=result=>{
     if(!result.applied)return;
     MeleeHits++;
     if(target&&target.Alive){target.stunLeft=Mathf.Max(target.stunLeft,.35f);target.motion.Push(Vector3.ProjectOnPlane(delta,Vector3.up).normalized*.85f);}
     Shapes.Impact(point,-delta.normalized);G.sound.Play("hit",point);
    };
   }
   batch.Apply();
   G.Record("player_melee","hits="+MeleeHits);return true;
  }
  public bool Throw()
  {
   if(!KnifeEquipped){G.Toast("当前通用技能槽未装配飞刀",2);return false;}
   if(!G.IsPlaying||owner.health<=0||owner.SwapActionsLocked||throwCooldown>0||knife||owner.reloadLeft>0)return false;
   pose=poseDuration=.3f;throwingPose=true;throwCooldown=1.3f;Throws++;knife=ThrownKnife.Launch(this,owner,owner.Eye.position,owner.Eye.forward,KnifeRank,PlayerClassSkill.IsTacticalFlow);
   if(owner.classSkill)owner.classSkill.EndForAttack("throw");
   G.sound.Play("knife_throw");G.Record("knife_throw","rank="+KnifeRank);return true;
  }
  public bool Recall()
  {
   if(!KnifeEquipped){G.Toast("当前通用技能槽未装配飞刀",2);return false;}
   if(!G.IsPlaying||!knife||!CanRecallKnife||knife.Returning)return false;
   knife.BeginReturn();G.sound.Play("knife_recall");G.Record("knife_recall",knife.transform.position+" -> "+owner.Eye.position);return true;
  }
  internal void ReturnedHit(){ReturnHits++;}
  internal void Recovered(){knife=null;throwCooldown=.5f;G.sound.Play("confirm");}
  void OnDisable(){ResetCombat();}
 }
 public static class KnifeGeometry
 {
  public static Transform Create(Transform parent,string name) => ActorEquipmentVisuals.CreateBlade(parent,name);
 }
 public sealed class ThrownKnife:MonoBehaviour
 {
  public bool Returning {get;private set;}
  public bool Flying {get;private set;}=true;
  public int OutboundHits {get;private set;}
  public int RecallHits {get;private set;}
  public int WallStops {get;private set;}
  public Vector3 LaunchPoint {get;private set;}
  PlayerCombatTools tools;PlayerMotor owner;Vector3 direction;float distance,age;bool returnStarted;
  public bool HitSilence {get;private set;}
  public bool AreaSilence {get;private set;}
  readonly HashSet<int> hitThisLeg=new HashSet<int>();
  long legEvent;
  public static ThrownKnife Launch(PlayerCombatTools tools,PlayerMotor p,Vector3 origin,Vector3 dir,int rank,bool tacticalRules=false)
  {
   Transform root=KnifeGeometry.Create(DemoGame.I.effects,"Silence knife / outbound");root.position=origin;root.rotation=Quaternion.LookRotation(dir);
   var k=root.gameObject.AddComponent<ThrownKnife>();k.tools=tools;k.owner=p;k.HitSilence=rank>=(tacticalRules?2:1);k.AreaSilence=rank>=(tacticalRules?3:2);k.direction=dir.normalized;k.LaunchPoint=origin;k.legEvent=DamageResolution.NextEventId();return k;
  }
  public void BeginReturn(){Returning=true;Flying=false;if(!returnStarted){hitThisLeg.Clear();legEvent=DamageResolution.NextEventId();}returnStarted=true;gameObject.name="Silence knife / recall";}
  void Update()
  {
   if(!DemoGame.I.IsPlaying)return;
   if(!owner||owner.health<=0){Destroy(gameObject);return;}
   age+=Time.deltaTime;
   if(age>18&&!Returning){Destroy(gameObject);tools.Recovered();return;}
   if(!Flying&&!Returning)return;
   Vector3 old=transform.position;
   if(Returning)
   {
    Vector3 target=owner.Eye.position+owner.Eye.right*-.15f;
    Vector3 delta=target-old;
    if(delta.magnitude<.35f){tools.Recovered();Destroy(gameObject);return;}
    direction=delta.normalized;
   }
   float step=Mathf.Min((Returning?24:30)*Time.deltaTime,Returning?Vector3.Distance(owner.Eye.position+owner.Eye.right*-.15f,old):100);
   // Sweep the real 3D segment. Stop at the nearest scene wall before considering enemies beyond it.
   bool wall=Physics.Raycast(old,direction,out var obstacle,step,Layers.WorldMask,QueryTriggerInteraction.Ignore);
   float allowed=wall?Mathf.Max(0,obstacle.distance-.025f):step;
   var hits=Physics.SphereCastAll(old,.07f,direction,allowed,1<<Layers.Actor,QueryTriggerInteraction.Collide);
   System.Array.Sort(hits,(a,b)=>a.distance.CompareTo(b.distance));
   var batch=new DamageBatch(Returning?DamageSource.KnifeReturn:DamageSource.KnifeOutbound,owner.transform,old);
   foreach(var hit in hits)
   {
    var device=hit.collider.GetComponentInParent<RelayDevice>();
    if(device)
    {
     if(device.Alive&&hitThisLeg.Add(device.GetInstanceID()))batch.Device(device,Returning?22:28,legEvent);
     allowed=Mathf.Min(allowed,hit.distance);wall=true;break;
    }
    var objective=hit.collider.GetComponentInParent<DamageableObjective>();
    if(objective&&objective.Alive)
    {
     if(hitThisLeg.Add(objective.GetInstanceID()))batch.Objective(objective,Returning?22:28,legEvent);
     allowed=Mathf.Min(allowed,hit.distance);wall=true;break;
    }
    var e=hit.collider.GetComponentInParent<EnemyActor>();if(!e||!e.Alive)continue;
    // Shields stop both legs even after this leg has previously touched the body.
    if(e.IsShieldCollider(hit.collider)){allowed=Mathf.Min(allowed,hit.distance);wall=true;break;}
    if(!hitThisLeg.Add(e.GetInstanceID()))continue;
    var sample=batch.Enemy(e,Returning?22:28,legEvent);bool returning=Returning;var targetEnemy=e;Vector3 hitPoint=hit.point;
    // Sample area-control recipients before damage can disable a carrier's colliders.
    var silenceTargets=new List<EnemyActor>();
    if(AreaSilence)foreach(var nearby in DemoGame.I.enemies)
     if(nearby&&nearby.Alive&&Vector3.Distance(nearby.AimPoint,hitPoint)<4&&!Physics.Linecast(hitPoint,nearby.AimPoint,Layers.WorldMask))silenceTargets.Add(nearby);
    if(sample!=null)sample.after=result=>{
     if(!result.applied)return;
     if(returning){RecallHits++;tools.ReturnedHit();}else OutboundHits++;
     if(HitSilence&&targetEnemy)targetEnemy.ApplySilence(3.2f);
     foreach(var nearby in silenceTargets)if(nearby&&nearby.Alive)nearby.ApplySilence(3.2f);
     Shapes.Burst(hitPoint,new Color(.3f,.85f,1),.3f);DemoGame.I.sound.Play("hit",hitPoint);
    };
    if(!Returning){allowed=Mathf.Min(allowed,hit.distance);Flying=false;break;}
   }
   batch.Apply();
   transform.position=old+direction*allowed;transform.rotation=Quaternion.LookRotation(direction);
   Shapes.Trace(old,transform.position,Returning?new Color(.35f,1,.72f):new Color(.3f,.7f,1),.12f,.025f);
   distance+=allowed;
   if(wall){WallStops++;Flying=Returning=false;DemoGame.I.Record("knife_blocked",transform.position.ToString());}
   if(distance>=24&&!Returning)Flying=false;
  }
 }
}
