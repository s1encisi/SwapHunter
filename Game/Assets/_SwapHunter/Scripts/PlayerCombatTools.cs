using System.Collections.Generic;
using UnityEngine;
namespace SwapHunter
{
 public sealed class PlayerCombatTools:MonoBehaviour
 {
  PlayerMotor owner;float meleeCooldown,throwCooldown,pose,poseDuration;bool throwingPose;
  public float PoseProgress=>pose>0?1-pose/poseDuration:1;
  public bool PoseActive=>pose>0;
  public bool ThrowingPose=>throwingPose;
  public float GripError=>handTool&&owner?Vector3.Distance(handTool.TransformPoint(new Vector3(0,0,-.105f)),owner.ToolGripWorld):0;
  Transform handTool;ThrownKnife knife;
  public int MeleeHits {get;private set;}
  public int Throws {get;private set;}
  public int ReturnHits {get;private set;}
  public int KnifeRank=>DemoGame.I.expeditionActive?DemoGame.I.expeditionStore.Profile.knifeRank:3;
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
   if(!G.IsPlaying||owner.health<=0||meleeCooldown>0||owner.reloadLeft>0)return false;
   meleeCooldown=.75f;pose=poseDuration=.38f;throwingPose=false;G.sound.Play("knife_swipe");
   foreach(var e in G.enemies.ToArray())
   {
    if(!e||!e.Alive)continue;Vector3 delta=e.AimPoint-owner.Eye.position;
    if(delta.magnitude>2.5f||Vector3.Dot(delta.normalized,owner.Eye.forward)<.65f)continue;
    if(!CombatRay.Cast(owner.Eye.position,delta.normalized,delta.magnitude+.2f,owner.transform,out var hit)||hit.collider.GetComponentInParent<EnemyActor>()!=e)continue;
    if(!e.Hit(24,hit.point,hit.collider,owner.Eye.position))continue;
    MeleeHits++;if(e.Alive){e.stunLeft=Mathf.Max(e.stunLeft,.35f);e.motion.Push(Vector3.ProjectOnPlane(delta,Vector3.up).normalized*.85f);}
    Shapes.Impact(hit.point,-delta.normalized);G.sound.Play("hit",hit.point);
   }
   G.Record("player_melee","hits="+MeleeHits);return true;
  }
  public bool Throw()
  {
   if(!G.IsPlaying||owner.health<=0||throwCooldown>0||knife||owner.reloadLeft>0)return false;
   pose=poseDuration=.3f;throwingPose=true;throwCooldown=1.3f;Throws++;knife=ThrownKnife.Launch(this,owner,owner.Eye.position,owner.Eye.forward,KnifeRank);
   G.sound.Play("knife_throw");G.Record("knife_throw","rank="+KnifeRank);return true;
  }
  public bool Recall()
  {
   if(!G.IsPlaying||!knife||KnifeRank<3||knife.Returning)return false;
   knife.BeginReturn();G.sound.Play("knife_recall");G.Record("knife_recall",knife.transform.position+" -> "+owner.Eye.position);return true;
  }
  internal void ReturnedHit(){ReturnHits++;}
  internal void Recovered(){knife=null;throwCooldown=.5f;G.sound.Play("confirm");}
  void OnDisable(){ResetCombat();}
 }
 public static class KnifeGeometry
 {
  public static Transform Create(Transform parent,string name)
  {
   var prefab=Resources.Load<GameObject>("BlenderActors/combat-knife-v05");
   if(!prefab)throw new System.InvalidOperationException("Required original Blender phase knife is missing");
   var root=ImportedModels.Create(prefab,parent,Vector3.zero);root.name=name;return root.transform;
  }
 }
 public sealed class ThrownKnife:MonoBehaviour
 {
  public bool Returning {get;private set;}
  public bool Flying {get;private set;}=true;
  public int OutboundHits {get;private set;}
  public int RecallHits {get;private set;}
  public int WallStops {get;private set;}
  public Vector3 LaunchPoint {get;private set;}
  PlayerCombatTools tools;PlayerMotor owner;int rank;Vector3 direction;float distance,age;bool returnStarted;
  readonly HashSet<int> hitThisLeg=new HashSet<int>();
  public static ThrownKnife Launch(PlayerCombatTools tools,PlayerMotor p,Vector3 origin,Vector3 dir,int rank)
  {
   Transform root=KnifeGeometry.Create(DemoGame.I.effects,"Silence knife / outbound");root.position=origin;root.rotation=Quaternion.LookRotation(dir);
   var k=root.gameObject.AddComponent<ThrownKnife>();k.tools=tools;k.owner=p;k.rank=rank;k.direction=dir.normalized;k.LaunchPoint=origin;return k;
  }
  public void BeginReturn(){Returning=true;Flying=false;if(!returnStarted)hitThisLeg.Clear();returnStarted=true;gameObject.name="Silence knife / recall";}
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
   foreach(var hit in hits)
   {
    var e=hit.collider.GetComponentInParent<EnemyActor>();if(!e||!e.Alive)continue;
    // Physical shields stop both directions, including after this leg has hit the owner's body.
    if(e.IsShieldCollider(hit.collider)){allowed=Mathf.Min(allowed,hit.distance);wall=true;break;}
    if(hitThisLeg.Contains(e.GetInstanceID()))continue;
    hitThisLeg.Add(e.GetInstanceID());e.Damage(Returning?22:28);
    if(Returning){RecallHits++;tools.ReturnedHit();}else OutboundHits++;
    if(rank>=1)e.ApplySilence(3.2f);
    if(rank>=2)foreach(var nearby in DemoGame.I.enemies)
     if(nearby&&nearby.Alive&&Vector3.Distance(nearby.AimPoint,hit.point)<4&&!Physics.Linecast(hit.point,nearby.AimPoint,Layers.WorldMask))nearby.ApplySilence(3.2f);
    Shapes.Burst(hit.point,new Color(.3f,.85f,1),.3f);DemoGame.I.sound.Play("hit",hit.point);
    if(!Returning){allowed=Mathf.Min(allowed,hit.distance);Flying=false;break;}
   }
   transform.position=old+direction*allowed;transform.rotation=Quaternion.LookRotation(direction);
   Shapes.Trace(old,transform.position,Returning?new Color(.35f,1,.72f):new Color(.3f,.7f,1),.12f,.025f);
   distance+=allowed;
   if(wall){WallStops++;Flying=Returning=false;DemoGame.I.Record("knife_blocked",transform.position.ToString());}
   if(distance>=24&&!Returning)Flying=false;
  }
 }
}
