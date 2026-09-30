using UnityEngine;
using UnityEngine.AI;
namespace SwapHunter
{
 // NavMesh provides a desired step, never writes the actor transform. The controller
 // resolves every step against the real collider world, including post-bake obstacles.
 [DefaultExecutionOrder(50)]
 public sealed class EnemyMotion:MonoBehaviour
 {
  EnemyActor owner;CharacterController capsule;NavMeshAgent agent;float fallingSpeed;
  public bool Airborne {get;private set;}
  public void Initialize(EnemyActor e,CharacterController c){owner=e;capsule=c;agent=e.agent;agent.updatePosition=false;agent.nextPosition=transform.position;}
  public static bool ClearVolume(Vector3 feet,float radius,float height,Transform ignore=null,Transform secondIgnore=null)
  {
   Physics.SyncTransforms();
   foreach(var c in Physics.OverlapCapsule(feet+Vector3.up*(radius+.04f),feet+Vector3.up*(height-radius),radius-.015f,Layers.CombatMask,QueryTriggerInteraction.Ignore))
   {
    if(ignore&&(c.transform==ignore||c.transform.IsChildOf(ignore)))continue;
    if(secondIgnore&&(c.transform==secondIgnore||c.transform.IsChildOf(secondIgnore)))continue;
    return false;
   }
   return true;
  }
  public static bool GroundAt(Vector3 feet,out Vector3 ground)
  {
   ground=feet;
   if(!Physics.Raycast(feet+Vector3.up*.2f,Vector3.down,out var floor,.5f,Layers.WorldMask,QueryTriggerInteraction.Ignore)||floor.normal.y<.65f)return false;
   if(!NavMesh.SamplePosition(floor.point,out var n,.35f,NavMesh.AllAreas)||Vector3.Distance(n.position,floor.point)>.25f)return false;
   ground=n.position;return true;
  }
  public static bool SafeDescent(Vector3 feet,float radius,float height,Transform ignore,Transform secondIgnore)
  {
   if(!Physics.Raycast(feet+Vector3.up*.1f,Vector3.down,out var floor,60,Layers.WorldMask,QueryTriggerInteraction.Ignore)||floor.normal.y<.65f)return false;
   return NavMesh.SamplePosition(floor.point,out var nav,.35f,NavMesh.AllAreas)&&Vector3.Distance(nav.position,floor.point)<.25f&&ClearVolume(floor.point+Vector3.up*.03f,radius,height,ignore,secondIgnore);
  }
  public static bool FindSpawn(EnemyKind kind,Vector3 requested,out Vector3 feet)
  {
   float radius=kind==EnemyKind.Elite?.45f:.36f,height=kind==EnemyKind.Elite?2.1f:1.8f;
   for(int ring=0;ring<=4;ring++)for(int k=0;k<(ring==0?1:16);k++)
   {
    float angle=k*Mathf.PI/8;Vector3 sample=requested+new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*(ring*.6f);
    if(!NavMesh.SamplePosition(sample,out var n,.65f,NavMesh.AllAreas)||Mathf.Abs(n.position.y-requested.y)>.6f)continue;
    if(!GroundAt(n.position,out var p)||!ClearVolume(p,radius,height))continue;
    if((kind==EnemyKind.Shield||kind==EnemyKind.Elite)&&Physics.CheckSphere(p+Vector3.up,kind==EnemyKind.Elite?.94f:.82f,Layers.WorldMask,QueryTriggerInteraction.Ignore))continue;
    feet=p;return true;
   }
   feet=requested;return false;
  }
  public bool Relocate(Vector3 feet)
  {
   Transform player=DemoGame.I.player?DemoGame.I.player.transform:null;
   if(!ClearVolume(feet,owner.Radius,owner.Height,transform,player))return false;
   bool ground=GroundAt(feet,out var nav);
   // A void destination is allowed: falling out of the arena is resolved as a fall, never a rescue teleport.
   if(agent.enabled)agent.enabled=false;capsule.enabled=false;transform.position=feet;Physics.SyncTransforms();capsule.enabled=true;
   Airborne=!ground;fallingSpeed=0;
   if(ground){agent.enabled=true;if(!agent.isOnNavMesh)return false;agent.Warp(feet);agent.nextPosition=feet;agent.ResetPath();}
   Physics.SyncTransforms();return true;
  }
  public void Push(Vector3 displacement)
  {
   if(!owner||!owner.Alive||!capsule.enabled)return;
   capsule.Move(owner.ConstrainShieldMotion(displacement));
   if(agent.enabled&&agent.isOnNavMesh)agent.nextPosition=transform.position;
  }
  void LateUpdate()
  {
   if(!owner||!owner.Alive||!capsule.enabled||!DemoGame.I.IsPlaying)return;
   if(Airborne)
   {
    if(transform.position.y < -12){owner.Damage(owner.maximumHealth+1);return;}
    fallingSpeed=Mathf.Max(-45,fallingSpeed-22*Time.deltaTime);
    Vector3 descent=Vector3.up*fallingSpeed*Time.deltaTime;
    Vector3 allowed=owner.ConstrainShieldMotion(descent);
    if(owner.HasPhysicalShield&&allowed.y>descent.y+.001f)
    {
     // A projecting shield can touch a ledge before the body. Back away through
     // real collision sweeps so it clears the edge instead of hanging from it.
     Vector3 retreat=-Vector3.ProjectOnPlane(transform.forward,Vector3.up).normalized*(2*Time.deltaTime);
     capsule.Move(owner.ConstrainShieldMotion(retreat));
     allowed=owner.ConstrainShieldMotion(descent);
     fallingSpeed=0;
    }
    var flags=capsule.Move(allowed);
    if((flags&CollisionFlags.Below)!=0&&GroundAt(transform.position,out var nav))
    {
     Airborne=false;fallingSpeed=0;agent.enabled=true;
     if(agent.isOnNavMesh){agent.Warp(transform.position);agent.nextPosition=transform.position;agent.ResetPath();}
     else{agent.enabled=false;Airborne=true;}
    }
    return;
   }
   if(!agent.enabled||!agent.isOnNavMesh)return;
   Vector3 wanted=agent.nextPosition-transform.position+Vector3.down*.06f;
   capsule.Move(owner.ConstrainShieldMotion(wanted));
   agent.nextPosition=transform.position;
  }
 }
}
