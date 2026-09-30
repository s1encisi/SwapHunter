using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
namespace SwapHunter
{
 public sealed class CoverSite:MonoBehaviour
 {
  public static readonly List<CoverSite> Active=new List<CoverSite>();
  public Collider Shape {get;private set;}
  void Awake(){Shape=GetComponent<Collider>();}
  void OnEnable(){if(!Active.Contains(this))Active.Add(this);}
  void OnDisable(){Active.Remove(this);}
 }
 public enum TacticalState { Approach, MeleeWarning, Recover, CoverSelection, MoveToCover, Hide, Peek, Fire, Reposition }
 public sealed class EnemyTacticalAI:MonoBehaviour
 {
  EnemyActor owner;CoverSite cover;Vector3 hide,peek,lockedMelee,lastKnownPlayer;float clock,rethink,fireAt,grenadeAt,nextSelect,lastSeen=-100;int burst,lastSwap=-1;
  public TacticalState State {get;private set;}
  public int CoverSelections {get;private set;}
  public int MeleeAttacks {get;private set;}
  public int StatesVisited {get;private set;}
  public Vector3 HidePoint=>hide;public Vector3 PeekPoint=>peek;
  public bool HasCover=>cover;
  LineRenderer meleeRing;
  DemoGame G=>DemoGame.I;
  public void Initialize(EnemyActor e){owner=e;grenadeAt=Time.time+7;Set(e.kind==EnemyKind.Shield?TacticalState.Approach:TacticalState.CoverSelection);}
  void Set(TacticalState next,float seconds=0){State=next;clock=seconds;StatesVisited|=1<<(int)next;if(owner&&owner.kind==EnemyKind.Shield)owner.UpdateShieldMeleePresentation(State,clock);}
  public void Invalidate(){cover=null;rethink=nextSelect=0;burst=0;if(meleeRing)Destroy(meleeRing.gameObject);Set(owner.kind==EnemyKind.Shield?TacticalState.Approach:TacticalState.Reposition);}
  bool Move(Vector3 p,float stop=.15f)
  {
   if(!owner.agent.enabled||!owner.agent.isOnNavMesh)return false;
   owner.agent.stoppingDistance=stop;owner.agent.isStopped=false;
   if(Time.time>=rethink){owner.agent.SetDestination(p);rethink=Time.time+.35f;}
   return Vector3.ProjectOnPlane(transform.position-p,Vector3.up).magnitude<stop+.22f;
  }
  void Stop(){if(owner.agent.enabled&&owner.agent.isOnNavMesh){owner.agent.isStopped=true;owner.agent.ResetPath();}}
  bool Visible(Vector3 feet)=>!Physics.Linecast(feet+Vector3.up*1.12f,G.player.Eye.position,Layers.WorldMask);
  bool Reachable(Vector3 requested,out Vector3 point)
  {
   point=requested;if(!NavMesh.SamplePosition(requested,out var hit,.6f,NavMesh.AllAreas)||Mathf.Abs(hit.position.y-requested.y)>.5f)return false;
   point=hit.position;if(!EnemyMotion.ClearVolume(point,owner.Radius,owner.Height,transform))return false;
   var path=new NavMeshPath();return NavMesh.CalculatePath(transform.position,point,NavMesh.AllAreas,path)&&path.status==NavMeshPathStatus.PathComplete;
  }
  bool SelectCover()
  {
   CoverSelections++;float best=float.NegativeInfinity;CoverSite chosen=null;Vector3 chosenHide=default,chosenPeek=default;
   foreach(var site in CoverSite.Active)
   {
    if(!site||!site.Shape||!site.gameObject.activeInHierarchy)continue;
    Bounds b=site.Shape.bounds;if(Vector3.Distance(b.center,transform.position)>24||b.size.y<1.0f)continue;
    Vector3 away=Vector3.ProjectOnPlane(b.center-G.player.transform.position,Vector3.up);
    Vector3 n=Mathf.Abs(away.x)>Mathf.Abs(away.z)?Vector3.right*Mathf.Sign(away.x):Vector3.forward*Mathf.Sign(away.z);
    Vector3 tangent=Vector3.Cross(Vector3.up,n);float deep=Mathf.Abs(n.x)*b.extents.x+Mathf.Abs(n.z)*b.extents.z;
    float side=Mathf.Abs(tangent.x)*b.extents.x+Mathf.Abs(tangent.z)*b.extents.z;
    Vector3 wanted=new Vector3(b.center.x,b.min.y,b.center.z)+n*(deep+.65f);
    if(!Reachable(wanted,out var h)||Visible(h)||Vector3.Distance(h,G.player.transform.position)<6)continue;
    for(int sign=-1;sign<=1;sign+=2)
    {
     if(!Reachable(wanted+tangent*(side+1.35f)*sign,out var p)||!Visible(p))continue;
     // A cover-to-peek transition must have a traversable nav path as well.
     var path=new NavMeshPath();if(!NavMesh.CalculatePath(h,p,NavMesh.AllAreas,path)||path.status!=NavMeshPathStatus.PathComplete)continue;
     float score=12-Vector3.Distance(transform.position,h)*.4f-Mathf.Abs(Vector3.Distance(p,G.player.transform.position)-12)*.15f;
     if(site==cover)score-=3;
     if(score>best){best=score;chosen=site;chosenHide=h;chosenPeek=p;}
    }
   }
   cover=chosen;hide=chosenHide;peek=chosenPeek;return chosen;
  }
  public void Tick(float dt)
  {
   clock-=dt;
   if(lastSwap!=G.totalSwaps){lastSwap=G.totalSwaps;Invalidate();}
   if(owner.kind==EnemyKind.Shield){TickMelee();return;}
   Vector3 player=G.player.transform.position;if(Visible(transform.position)){lastSeen=Time.time;lastKnownPlayer=player;}float distance=Vector3.Distance(transform.position,player);
   if(distance<5&&State!=TacticalState.Reposition){cover=null;Set(TacticalState.Reposition,1);rethink=0;}
   switch(State)
   {
    case TacticalState.CoverSelection:
    case TacticalState.Reposition:
     if(Time.time>=nextSelect){nextSelect=Time.time+1.2f;if(SelectCover()){Set(TacticalState.MoveToCover,6);rethink=0;break;}}
     // No usable cover: keep rifle standoff, move along a real path, and keep firing when visible.
     Vector3 away=Vector3.ProjectOnPlane(transform.position-player,Vector3.up).normalized;
     Vector3 destination=distance<8?transform.position+away*4:distance>15?player+away*12:transform.position;
     if(Reachable(destination,out var safe))Move(safe,.5f);else Stop();
     if(Visible(transform.position))Fire();
     if(clock<=0){Set(TacticalState.CoverSelection,1.2f);}break;
    case TacticalState.MoveToCover:
     if(!cover||clock<=0){Set(TacticalState.Reposition,1);break;}
     if(Visible(transform.position)&&distance>=5)Fire();
     if(Move(hide)){Stop();Set(TacticalState.Hide,.65f);}break;
    case TacticalState.Hide:
     if(!cover){Set(TacticalState.Reposition);break;}
     if(!Move(hide))break;Stop();
     if(clock<=0){Set(TacticalState.Peek,3);rethink=0;}break;
    case TacticalState.Peek:
     if(!cover||clock<=0){Set(TacticalState.Reposition);break;}
     if(Move(peek,.03f)&&Visible(transform.position)){Stop();burst=0;fireAt=Time.time+.18f;Set(TacticalState.Fire,1.4f);}break;
    case TacticalState.Fire:
     Stop();if(Visible(transform.position))Fire();
     if(clock<=0||burst>=3||!Visible(transform.position)){Set(TacticalState.Hide,.9f);rethink=0;}
     break;
   }
   if(!owner.Silenced&&Time.time>grenadeAt&&distance>7&&distance<20&&Time.time-lastSeen<6&&(G.stage>=2||G.expeditionActive&&G.expeditionLevel>=3))
   {grenadeAt=Time.time+11;owner.grenadesThrown++;GrenadeActor.Spawn(owner.AimPoint+Vector3.up*.4f,lastKnownPlayer+Vector3.up*.15f);G.Record("grenade_throw","last visible player position");}
  }
  void Fire()
  {
   if(Time.time<fireAt)return;
   Vector3 aim=Vector3.ProjectOnPlane(G.player.Eye.position-owner.AimPoint,Vector3.up);
   // A clear ray does not grant a backwards shot while the visible weapon is still turning.
   if(aim.sqrMagnitude<.001f||Vector3.Dot(transform.forward,aim.normalized)<.85f)return;
   owner.ShootAt(G.player.Eye.position+Random.insideUnitSphere*.25f,6,38);burst++;fireAt=Time.time+(burst>=3?1.65f:.25f);if(burst>=3&&State!=TacticalState.Fire)burst=0;
  }
  void TickMelee()
  {
   owner.UpdateShieldMeleePresentation(State,clock);
   float distance=Vector3.Distance(G.player.transform.position,transform.position);
   if(State==TacticalState.MeleeWarning)
   {
    Stop();if(clock>0)return;
    if(distance<2.8f&&Vector3.Distance(G.player.transform.position,lockedMelee)<1.8f&&Visible(transform.position))G.player.Hurt(17,owner.AimPoint);
    MeleeAttacks++;G.sound.Play("knife_swipe",owner.AimPoint);if(meleeRing)Destroy(meleeRing.gameObject);Set(TacticalState.Recover,1.1f);return;
   }
   if(State==TacticalState.Recover){Stop();if(clock<=0)Set(TacticalState.Approach);return;}
   owner.agent.speed=3.6f;
   if(distance>2.35f||!Visible(transform.position)){Move(G.player.transform.position,1.9f);return;}
   Stop();lockedMelee=G.player.transform.position;Set(TacticalState.MeleeWarning,.65f);
   meleeRing=Shapes.Ring(G.effects,transform.position+Vector3.up*.07f,2.7f,new Color(1,.5f,.2f),.07f);G.sound.Play("warning",transform.position);
  }
  void OnDisable(){if(meleeRing)Destroy(meleeRing.gameObject);}
 }
}
