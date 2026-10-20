using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
namespace SwapHunter
{
 [DefaultExecutionOrder(50)]
 public sealed class PlayerCombatTools:MonoBehaviour
 {
  PlayerMotor owner;float meleeCooldown,throwCooldown,silenceCooldown,pose,poseDuration;bool throwingPose;
  readonly ThrownKnife[] knives=new ThrownKnife[3];public const int KnifeCapacity=3;
  public int KnivesInHand{get{int n=0;foreach(var k in knives)if(!k)n++;return n;}}
  public int KnivesReturning{get{int n=0;foreach(var k in knives)if(k&&k.Returning)n++;return n;}}
  public float SilenceCooldownLeft=>silenceCooldown;
  public ThrownKnife[] ActiveKnives()=>System.Array.FindAll(knives,k=>k);
  public float PoseProgress=>pose>0?1-pose/poseDuration:1;
  public bool PoseActive=>pose>0;
  internal float MeleeCooldownLeft=>meleeCooldown;
  internal float ThrowCooldownLeft=>throwCooldown;
  public bool ThrowingPose=>throwingPose;
  public float GripError=>handTool&&owner?Vector3.Distance(handTool.TransformPoint(new Vector3(0,0,-.105f)),owner.ToolGripWorld):0;
  Transform handTool;ThrownKnife knife{get{foreach(var k in knives)if(k)return k;return null;}}
  public int MeleeHits {get;private set;}
  public int Throws {get;private set;}
  public int ReturnHits {get;private set;}
  public int KnifeRank=>PlayerClassSkill.IsTacticalFlow?(G.expeditionStore.EquippedGeneralSkill==TacticalPrototypeRules.KnifeGeneralIndex?G.expeditionStore.EquippedGeneralSkillLevel:0):G.expeditionActive?G.expeditionStore.Profile.knifeRank:3;
  public bool CanRecallKnife=>KnifeRank>=1;
  public bool KnifeEquipped=>!PlayerClassSkill.IsTacticalFlow||(G.expeditionStore.EquippedGeneralSkill==TacticalPrototypeRules.KnifeGeneralIndex&&KnifeRank>=1);
  public ThrownKnife Knife=>knife;
  public string KnifeStatus=>"手中 "+KnivesInHand+" / 在场 "+(3-KnivesInHand)+" / 召回 "+KnivesReturning+(silenceCooldown>0?" · 沉默 "+silenceCooldown.ToString("F1")+"s":" · 沉默就绪");
  DemoGame G=>DemoGame.I;
  public void Initialize(PlayerMotor p)
  {
   owner=p;handTool=KnifeGeometry.Create(owner.Eye,"Held tactical blade");handTool.localPosition=new Vector3(-.23f,-.25f,.55f);ImportedModels.SetLayer(handTool,Layers.ViewModel);handTool.gameObject.SetActive(false);
  }
  public void ResetCombat(){for(int i=0;i<3;i++){if(knives[i])Destroy(knives[i].gameObject);knives[i]=null;}silenceCooldown=meleeCooldown=throwCooldown=pose=0;MeleeHits=Throws=ReturnHits=0;if(handTool)handTool.gameObject.SetActive(false);}
  void Update()
  {
   if(!owner||!G.IsPlaying||owner.health<=0)return;
   meleeCooldown=Mathf.Max(0,meleeCooldown-Time.deltaTime);throwCooldown=Mathf.Max(0,throwCooldown-Time.deltaTime);silenceCooldown=Mathf.Max(0,silenceCooldown-Time.deltaTime);
   pose=Mathf.Max(0,pose-Time.deltaTime);
   
   if(G.Pressed(21))Melee();if(G.Pressed(22))Throw();if(G.Pressed(23))Recall();
   foreach(var k in knives)if(k&&!k.Flying&&!k.Returning&&Vector3.Distance(owner.Eye.position,k.transform.position)<1.8f&&!Physics.Linecast(owner.Eye.position,k.transform.position,Layers.WorldMask)){Recovered(k);Destroy(k.gameObject);}
  }
  void LateUpdate()
  {
   if(!handTool||!owner)return;
   handTool.gameObject.SetActive(pose>0&&(!throwingPose||PoseProgress<.22f));
   if(pose<=0)return;
   handTool.rotation=owner.ToolGripRotation;handTool.position=owner.ToolGripWorld+handTool.forward*.105f;
  }
  public bool Melee(){return owner&&owner.meleeEquipment&&owner.meleeEquipment.BeginAttack(false,true);}
  public void RegisterMeleeHit(){MeleeHits++;}
  public bool Throw()
  {
   if(!KnifeEquipped){G.Toast("当前通用技能槽未装配飞刀",2);return false;}
   if(!G.IsPlaying||owner.health<=0||owner.SwapActionsLocked||throwCooldown>0||owner.reloadLeft>0)return false;
   int slot=-1;for(int i=0;i<3;i++)if(!knives[i]){slot=i;break;}if(slot<0){G.Toast("三把刀均在场 · 按{key:23}召回",2);return false;}
   pose=poseDuration=.3f;throwingPose=true;throwCooldown=.65f;Throws++;knives[slot]=ThrownKnife.Launch(this,owner,owner.Eye.position,owner.Eye.forward,KnifeRank,true);
   if(owner.classSkill)owner.classSkill.EndForAttack("throw");
   G.sound.Play("knife_throw");G.Record("knife_throw","rank="+KnifeRank);return true;
  }
  public bool Recall()
  {
   if(!KnifeEquipped){G.Toast("当前通用技能槽未装配飞刀",2);return false;}
   if(!G.IsPlaying||!CanRecallKnife)return false;bool any=false;foreach(var k in knives)if(k&&!k.Returning){k.BeginReturn();any=true;}
   if(any){G.sound.Play("knife_recall");G.Record("knife_recall","count="+KnivesReturning);}return any;
  }
  internal void ReturnedHit(){ReturnHits++;}
  internal void Recovered(ThrownKnife k){for(int i=0;i<3;i++)if(knives[i]==k){knives[i]=null;G.sound.Play("confirm");return;}}
  internal void Recovered(){if(knife)Recovered(knife);}
  internal bool TrySilence(EnemyActor target,Vector3 point,bool direct,bool area)
  {if(!direct||silenceCooldown>0)return false;silenceCooldown=8;if(target&&target.Alive)target.ApplySilence(3.2f);if(area)foreach(var e in G.enemies)if(e&&e!=target&&e.Alive&&Vector3.Distance(e.AimPoint,point)<=4&&!Physics.Linecast(point,e.AimPoint,Layers.WorldMask))e.ApplySilence(3.2f);G.Record("knife_silence","sharedCooldown=8;area="+area);return true;}
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
  PlayerCombatTools tools;PlayerMotor owner;Vector3 direction,lastRecoverable;float distance;bool returnStarted;
  public bool HitSilence {get;private set;}
  public bool AreaSilence {get;private set;}
  readonly HashSet<int> hitThisLeg=new HashSet<int>();
  long legEvent;
  public static ThrownKnife Launch(PlayerCombatTools tools,PlayerMotor p,Vector3 origin,Vector3 dir,int rank,bool tacticalRules=false)
  {
   Transform root=KnifeGeometry.Create(DemoGame.I.effects,"Silence knife / outbound");root.position=origin;root.rotation=Quaternion.LookRotation(dir);
   var k=root.gameObject.AddComponent<ThrownKnife>();k.tools=tools;k.owner=p;k.HitSilence=rank>=2;k.AreaSilence=rank>=3;k.direction=dir.normalized;k.LaunchPoint=origin;k.lastRecoverable=p.transform.position+Vector3.up*.25f;k.legEvent=DamageResolution.NextEventId();return k;
  }
  public void BeginReturn(){Returning=true;Flying=false;if(!returnStarted){hitThisLeg.Clear();legEvent=DamageResolution.NextEventId();}returnStarted=true;gameObject.name="Silence knife / recall";}
  void Update()
  {
   if(!DemoGame.I.IsPlaying)return;
   if(!owner||owner.health<=0)return;
   if(!Flying&&!Returning)return;
   Vector3 old=transform.position;
   if(Returning)
   {
    Vector3 target=owner.Eye.position+owner.Eye.right*-.15f;
    Vector3 delta=target-old;
    if(delta.magnitude<.35f){tools.Recovered(this);Destroy(gameObject);return;}
    direction=delta.normalized;
   }
   float step=Mathf.Min((Returning?24:30)*Time.deltaTime,Returning?Vector3.Distance(owner.Eye.position+owner.Eye.right*-.15f,old):Mathf.Max(0,50-distance));
   // Sweep the real 3D segment. Stop at the nearest scene wall before considering enemies beyond it.
   bool wall=Physics.Raycast(old,direction,out var obstacle,step,Layers.WorldMask,QueryTriggerInteraction.Ignore);
   float allowed=wall?Mathf.Max(0,obstacle.distance-.025f):step;
   if(DirectionalBarrier.Intercept(old,old+direction*allowed,out var barrier)){allowed=Vector3.Distance(old,barrier);wall=true;}
   var hits=Physics.SphereCastAll(old,.07f,direction,allowed,1<<Layers.Actor,QueryTriggerInteraction.Collide);
   System.Array.Sort(hits,(a,b)=>a.distance.CompareTo(b.distance));
   var batch=new DamageBatch(Returning?DamageSource.KnifeReturn:DamageSource.KnifeOutbound,owner.transform,old);
   foreach(var hit in hits)
   {
    if(hit.collider.transform==owner.transform||hit.collider.transform.IsChildOf(owner.transform))continue;
    var component=hit.collider.GetComponentInParent<EnemyV2Device>();
    if(component){if(component.Alive&&hitThisLeg.Add(component.GetInstanceID()))batch.V2Device(component,Returning?66:84,legEvent);allowed=Mathf.Min(allowed,hit.distance);wall=true;break;}
    var device=hit.collider.GetComponentInParent<RelayDevice>();
    if(device)
    {
     if(device.Alive&&hitThisLeg.Add(device.GetInstanceID()))batch.Device(device,Returning?66:84,legEvent);
     allowed=Mathf.Min(allowed,hit.distance);wall=true;break;
    }
    var objective=hit.collider.GetComponentInParent<DamageableObjective>();
    if(objective&&objective.Alive)
    {
     if(hitThisLeg.Add(objective.GetInstanceID()))batch.Objective(objective,Returning?66:84,legEvent);
     allowed=Mathf.Min(allowed,hit.distance);wall=true;break;
    }
    var e=hit.collider.GetComponentInParent<EnemyActor>();if(!e||!e.Alive)continue;
    // Shields stop both legs even after this leg has previously touched the body.
    if(e.IsShieldCollider(hit.collider)){allowed=Mathf.Min(allowed,hit.distance);wall=true;break;}
    if(!hitThisLeg.Add(e.GetInstanceID()))continue;
    var sample=batch.Enemy(e,e.KnifeExecutable?e.health+1:Returning?66:84,legEvent);bool returning=Returning;var targetEnemy=e;Vector3 hitPoint=hit.point;
    if(sample!=null)sample.after=result=>{
     if(!result.applied)return;
     if(returning){RecallHits++;tools.ReturnedHit();}else OutboundHits++;
     tools.TrySilence(targetEnemy,hitPoint,HitSilence,AreaSilence);
     Shapes.Burst(hitPoint,new Color(.3f,.85f,1),.3f);DemoGame.I.sound.Play("hit",hitPoint);
    };
    if(!Returning){allowed=Mathf.Min(allowed,hit.distance);Flying=false;break;}
   }
   batch.Apply();
   transform.position=old+direction*allowed;transform.rotation=Quaternion.LookRotation(direction);
   Shapes.Trace(old,transform.position,Returning?new Color(.35f,1,.72f):new Color(.3f,.7f,1),.12f,.025f);
   if(!Returning)distance+=allowed;
   if(NavMesh.SamplePosition(transform.position,out var nav,2,NavMesh.AllAreas)&&!Physics.Linecast(transform.position,nav.position+Vector3.up*.25f,Layers.WorldMask))lastRecoverable=nav.position+Vector3.up*.25f;
   if(wall){WallStops++;Flying=Returning=false;DemoGame.I.Record("knife_blocked",transform.position.ToString());}
   if(distance>=50&&!Returning)Flying=false;
   if(!Flying&&!Returning&&!NavMesh.SamplePosition(transform.position,out var resting,2,NavMesh.AllAreas))
   {Vector3 from=transform.position;transform.position=lastRecoverable;Shapes.Trace(from,lastRecoverable,Color.cyan,1,.05f);DemoGame.I.Toast("飞刀落点不可达 · 已定位到可步行回收点",3);DemoGame.I.Record("knife_recovery_relocated","from="+from+";to="+lastRecoverable);}
  }
 }
}
