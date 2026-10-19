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
  static readonly Dictionary<int,EnemyTacticalAI> occupied=new Dictionary<int,EnemyTacticalAI>();
  EnemyActor owner;CoverSite cover;Vector3 hide,peek,lockedMelee,lastKnownPlayer,patrolHome;float clock,rethink,fireAt,grenadeAt,nextSelect,lastSeen=-100,patrolAt;int burst,patrolIndex; EnemyHazardAwareness hazard;
  public TacticalState State {get;private set;}
  public int CoverSelections {get;private set;}
  public int MeleeAttacks {get;private set;}
  public int StatesVisited {get;private set;}
  public Vector3 HidePoint=>hide;public Vector3 PeekPoint=>peek;
  public bool HasCover=>cover;
  public bool LocksFacing=>State==TacticalState.MeleeWarning&&clock<=.2f||State==TacticalState.Recover;
  float sharedObservationUntil;
  public bool HasPlayerMemory=>Time.time-lastSeen<=3||Time.time<sharedObservationUntil;
  public Vector3 ObservedPlayerAim=>HasPlayerMemory?lastKnownPlayer+Vector3.up*1.1f:owner.AimPoint+transform.forward*10;
  public void ObservePlayer()
  {
   if(!owner||!G||!G.player)return;Vector3 delta=PlayerTorso-owner.AimPoint;
   bool inView=delta.sqrMagnitude<60*60&&(delta.sqrMagnitude<9||Vector3.Dot(transform.forward,delta.normalized)>.5f||Time.time-lastSeen<.4f);
   if(inView&&Visible(transform.position)){lastKnownPlayer=G.player.transform.position;lastSeen=Time.time;if(Time.time-lastVisible>.25f)seenSince=Time.time;lastVisible=Time.time;}
   else if(Time.time-lastVisible>.25f)seenSince=-1;
  }
  public static void NotifySwap(Vector3 source,Vector3 destination,EnemyActor swapped)
  {
   var g=DemoGame.I;if(!g)return;
   foreach(var e in g.enemies)
   {
    if(!e||!e.Alive||!e.tactical||e==swapped)continue;
    bool seen=(Vector3.Distance(e.AimPoint,source)<28&&!Physics.Linecast(e.AimPoint,source+Vector3.up,Layers.WorldMask))||(Vector3.Distance(e.AimPoint,destination)<28&&!Physics.Linecast(e.AimPoint,destination+Vector3.up,Layers.WorldMask));
    if(seen)e.tactical.LocalPositionChanged();
   }
   FireControlRelay.RevalidateAll();
  }
  void LocalPositionChanged()
  {
   ObservePlayer();rethink=nextSelect=0;
   if(State!=TacticalState.MeleeWarning&&State!=TacticalState.Recover&&State!=TacticalState.Fire)cover=null;
  }
  LineRenderer meleeRing,sniperLine;float seenSince=-1,lastVisible=-100,progressAt;Vector3 progressPosition,sniperLock;int blockedMuzzles;float sniperWindup;
  public bool SeesPlayer=>owner&&G&&G.player&&Visible(transform.position);
  public Vector3 LastKnownFeet=>lastKnownPlayer;
  public Vector3 PlayerTorso=>G.player.controller?G.player.controller.bounds.center:G.player.transform.position+Vector3.up*.9f;
  public void NotifyHurt(){if(Visible(transform.position)){lastKnownPlayer=G.player.transform.position;lastSeen=Time.time;}nextSelect=rethink=0;}
  public void ShareObservation(Vector3 feet,float seconds=3){lastKnownPlayer=feet;lastSeen=Time.time;sharedObservationUntil=Time.time+seconds;nextSelect=0;}
  public void NotifyBlockedMuzzle(){if(++blockedMuzzles>=2){blockedMuzzles=0;ReleaseCover();Set(TacticalState.Reposition,1);nextSelect=rethink=0;}}
  public void RequestReposition(){ReleaseCover();nextSelect=rethink=0;Set(TacticalState.Reposition,1);}
  void ReleaseCover(){if(cover&&occupied.TryGetValue(cover.GetInstanceID(),out var e)&&e==this)occupied.Remove(cover.GetInstanceID());cover=null;}
  DemoGame G=>DemoGame.I;
  public void Initialize(EnemyActor e){owner=e;patrolHome=transform.position;hazard=gameObject.AddComponent<EnemyHazardAwareness>();hazard.Initialize(e);grenadeAt=Time.time+7;Set(e.kind==EnemyKind.Shield?TacticalState.Approach:TacticalState.CoverSelection);}
  void Set(TacticalState next,float seconds=0){State=next;clock=seconds;StatesVisited|=1<<(int)next;if(owner&&owner.kind==EnemyKind.Shield)owner.UpdateShieldMeleePresentation(State,clock);}
  public void Invalidate(){patrolHome=transform.position;ReleaseCover();rethink=nextSelect=0;burst=0;seenSince=-1;fireAt=Time.time+owner.stunLeft+.3f;sniperWindup=owner.warningLeft=0;if(sniperLine)Destroy(sniperLine.gameObject);if(meleeRing)Destroy(meleeRing.gameObject);Set(owner.kind==EnemyKind.Shield?TacticalState.Approach:TacticalState.Reposition);}
  bool Move(Vector3 p,float stop=.15f)
  {
   if(!owner.agent.enabled||!owner.agent.isOnNavMesh)return false;
   var territory=owner.GetComponent<MissionEncounterAgent>();
   if(territory&&!territory.Allows(p)){Stop();return false;}
   if(Time.time-progressAt>=1.5f){if(owner.agent.hasPath&&Vector3.Distance(progressPosition,transform.position)<.15f){owner.agent.ResetPath();ReleaseCover();nextSelect=0;Set(TacticalState.Reposition,1);G.Record("enemy_repath","no_progress");}progressAt=Time.time;progressPosition=transform.position;}
   owner.agent.stoppingDistance=stop;owner.agent.isStopped=false;
   if(Time.time>=rethink){owner.agent.SetDestination(p);rethink=Time.time+.35f;}
   return Vector3.ProjectOnPlane(transform.position-p,Vector3.up).magnitude<stop+.22f;
  }
  void Stop(){if(owner.agent.enabled&&owner.agent.isOnNavMesh){owner.agent.isStopped=true;owner.agent.ResetPath();}}
  bool Visible(Vector3 feet)=>G&&G.player&&(!Physics.Linecast(feet+Vector3.up*1.12f,PlayerTorso,Layers.WorldMask)||!Physics.Linecast(feet+Vector3.up*1.12f,G.player.Eye.position,Layers.WorldMask));
  bool ObservedRay(Vector3 feet)=>HasPlayerMemory&&!Physics.Linecast(feet+Vector3.up*1.12f,ObservedPlayerAim,Layers.WorldMask);
  bool Reachable(Vector3 requested,out Vector3 point)
  {
   point=requested;if(!NavMesh.SamplePosition(requested,out var hit,.6f,NavMesh.AllAreas)||Mathf.Abs(hit.position.y-requested.y)>.5f)return false;
   point=hit.position;
   var territory=owner.GetComponent<MissionEncounterAgent>();
   if(territory&&!territory.Allows(point))return false;
   if(!EnemyMotion.ClearVolume(point,owner.Radius,owner.Height,transform))return false;
   var path=new NavMeshPath();return NavMesh.CalculatePath(transform.position,point,NavMesh.AllAreas,path)&&path.status==NavMeshPathStatus.PathComplete;
  }
  bool SelectCover()
  {
   if(!HasPlayerMemory)return false;
   CoverSelections++;float best=float.NegativeInfinity;CoverSite chosen=null;Vector3 chosenHide=default,chosenPeek=default;
   foreach(var site in CoverSite.Active)
   {
    if(!site||!site.Shape||!site.gameObject.activeInHierarchy)continue;
    if(occupied.TryGetValue(site.GetInstanceID(),out var occupant)&&occupant&&occupant!=this&&occupant.owner&&occupant.owner.Alive)continue;
    Bounds b=site.Shape.bounds;if(Vector3.Distance(b.center,transform.position)>24||b.size.y<1.0f)continue;
    Vector3 away=Vector3.ProjectOnPlane(b.center-lastKnownPlayer,Vector3.up);
    Vector3 n=Mathf.Abs(away.x)>Mathf.Abs(away.z)?Vector3.right*Mathf.Sign(away.x):Vector3.forward*Mathf.Sign(away.z);
    Vector3 tangent=Vector3.Cross(Vector3.up,n);float deep=Mathf.Abs(n.x)*b.extents.x+Mathf.Abs(n.z)*b.extents.z;
    float side=Mathf.Abs(tangent.x)*b.extents.x+Mathf.Abs(tangent.z)*b.extents.z;
    Vector3 wanted=new Vector3(b.center.x,b.min.y,b.center.z)+n*(deep+.65f);
    if(!Reachable(wanted,out var h)||ObservedRay(h)||Vector3.Distance(h,lastKnownPlayer)<6)continue;
    for(int sign=-1;sign<=1;sign+=2)
    {
     if(!Reachable(wanted+tangent*(side+1.35f)*sign,out var p)||!ObservedRay(p))continue;
     // A cover-to-peek transition must have a traversable nav path as well.
     var path=new NavMeshPath();if(!NavMesh.CalculatePath(h,p,NavMesh.AllAreas,path)||path.status!=NavMeshPathStatus.PathComplete)continue;
     float score=12-Vector3.Distance(transform.position,h)*.4f-Mathf.Abs(Vector3.Distance(p,lastKnownPlayer)-12)*.15f+1.5f*FireControlRelay.ConnectionValue(owner,p)-6*Mathf.Clamp01(hazard.PredictedDamage(p,path)/Mathf.Max(1,owner.health));
     if(site==cover)score-=3;
     if(score>best){best=score;chosen=site;chosenHide=h;chosenPeek=p;}
    }
   }
   ReleaseCover();cover=chosen;if(cover)occupied[cover.GetInstanceID()]=this;hide=chosenHide;peek=chosenPeek;return chosen;
  }
  public void Tick(float dt)
  {
   clock-=dt;
   ObservePlayer();
   if(!HasPlayerMemory)
   {
    owner.FaceSafely(transform.rotation*Quaternion.Euler(0,18*dt,0));
    if(Time.time>=patrolAt){patrolAt=Time.time+4;float angle=(++patrolIndex%4)*Mathf.PI*.5f;Vector3 p=patrolHome+new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*2.5f;if(Reachable(p,out var safe))Move(safe,.3f);else Stop();}
    return;
   }
   if(owner.kind==EnemyKind.Sniper){TickSniper(dt);return;}
   if(State!=TacticalState.MeleeWarning&&State!=TacticalState.Recover&&State!=TacticalState.Fire)
   {
    Vector3 preferred=cover?(State==TacticalState.Peek?peek:hide):transform.position;
    if(hazard.TryMovement(preferred,ObservedPlayerAim,HasPlayerMemory,out var safeHazard)){bool arrived=Move(safeHazard,.2f);if(arrived&&owner.kind!=EnemyKind.Shield&&Visible(transform.position))Fire();return;}
   }
   if(owner.kind==EnemyKind.Shield){TickMelee();return;}
   Vector3 player=HasPlayerMemory?lastKnownPlayer:transform.position;float distance=HasPlayerMemory?Vector3.Distance(transform.position,player):12;
   if(HasPlayerMemory&&distance<5&&State!=TacticalState.Reposition){cover=null;Set(TacticalState.Reposition,1);rethink=0;}
   switch(State)
   {
    case TacticalState.CoverSelection:
    case TacticalState.Reposition:
     if(Time.time>=nextSelect){nextSelect=Time.time+1.2f;if(SelectCover()){Set(TacticalState.MoveToCover,6);rethink=0;break;}}
     // No usable cover: keep rifle standoff, move along a real path, and keep firing when visible.
     Vector3 away=Vector3.ProjectOnPlane(transform.position-player,Vector3.up).normalized;
     Vector3 destination=!HasPlayerMemory?transform.position:distance<8?transform.position+away*4:distance>15?player+away*12:transform.position;
     if(Reachable(destination,out var safe))Move(safe,.5f);else Stop();
     if(HasPlayerMemory&&Visible(transform.position))Fire();
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
     Stop();if(HasPlayerMemory&&Visible(transform.position))Fire();
     if(clock<=0||burst>=3||!Visible(transform.position)){Set(TacticalState.Hide,.9f);rethink=0;}
     break;
   }
   if(false&&!owner.Silenced&&Time.time>grenadeAt&&distance>7&&distance<20&&Time.time-lastSeen<3&&(G.stage>=2||G.expeditionActive&&G.expeditionLevel>=3))
   {grenadeAt=Time.time+11;owner.grenadesThrown++;GrenadeActor.Spawn(owner.AimPoint+Vector3.up*.4f,lastKnownPlayer+Vector3.up*.15f);G.Record("grenade_throw","last visible player position");}
  }
  public void FireBasic(float damage=6,float interval=.25f)
  {
   if(Time.time<fireAt||!HasPlayerMemory||!SeesPlayer||seenSince<0||Time.time-seenSince<.4f)return;
   Vector3 aim=Vector3.ProjectOnPlane(PlayerTorso-owner.AimPoint,Vector3.up);
   if(aim.sqrMagnitude<.001f||Vector3.Dot(transform.forward,aim.normalized)<.9f)return;
   float distance=aim.magnitude;
   // Candidate 02's 900 real projectiles showed that nominal controller radius
   // overestimated its ray-query hit width. Tune the physical cone, never a hit roll.
   float spread=distance<=20?Mathf.Lerp(.28f,.36f,Mathf.InverseLerp(10,20,distance)):Mathf.Lerp(.36f,.46f,Mathf.InverseLerp(20,35,distance));
   float moving=owner.agent.enabled&&owner.agent.isOnNavMesh?owner.agent.velocity.magnitude:0;
   spread*=1+Mathf.Clamp01(moving/3)*.6f;
   Vector3 sight=(PlayerTorso-owner.MuzzlePosition).normalized;
   Vector3 right=Vector3.Cross(Vector3.up,sight).normalized,up=Vector3.Cross(sight,right).normalized;
   Vector3 point=PlayerTorso+right*Random.Range(-spread,spread)+up*Random.Range(-spread*.45f,spread*.45f);
   owner.ShootAt(point,damage,65);burst++;fireAt=Time.time+(burst>=3?1.25f:interval);if(burst>=3&&State!=TacticalState.Fire)burst=0;
  }
  void Fire(){FireBasic(owner.kind==EnemyKind.Elite?8:6);}
  void TickSniper(float dt)
  {
   if(!HasPlayerMemory){sniperWindup=owner.warningLeft=0;if(sniperLine)Destroy(sniperLine.gameObject);Stop();return;}float distance=Vector3.Distance(transform.position,lastKnownPlayer);
   if(distance<7){sniperWindup=owner.warningLeft=0;if(sniperLine)Destroy(sniperLine.gameObject);RequestReposition();Vector3 retreat=transform.position+Vector3.ProjectOnPlane(transform.position-lastKnownPlayer,Vector3.up).normalized*5;if(Reachable(retreat,out var r))Move(r);return;}
   if(!SeesPlayer){if(sniperWindup>0){sniperWindup=owner.warningLeft=0;if(sniperLine)Destroy(sniperLine.gameObject);RequestReposition();}if(SelectCover())Move(peek);else Move(lastKnownPlayer,12);return;}
   Stop();if(owner.Silenced){sniperWindup=owner.warningLeft=0;if(sniperLine)Destroy(sniperLine.gameObject);FireBasic(6);return;}
   if(owner.SniperRearmWork>0)return;
   if(sniperWindup<=0){if(Time.time<fireAt||seenSince<0||Time.time-seenSince<.4f)return;sniperWindup=1.2f;sniperLock=PlayerTorso;G.sound.Play("warning",owner.AimPoint);}
   sniperWindup-=dt;owner.warningLeft=Mathf.Max(0,sniperWindup);if(sniperWindup>.2f)sniperLock=PlayerTorso;
   if(!sniperLine)sniperLine=Shapes.Line(G.effects,owner.MuzzlePosition,sniperLock,new Color(1,.16f,.12f),.024f);
   sniperLine.SetPosition(0,owner.MuzzlePosition);sniperLine.SetPosition(1,sniperLock);
   owner.VisualRig.SetAim(sniperLock);
   if(sniperWindup<=0){owner.ShootAt(sniperLock,30,90);owner.BeginSniperRearm();if(sniperLine)Destroy(sniperLine.gameObject);fireAt=Time.time+.8f;}
  }
  void TickMelee()
  {
   owner.UpdateShieldMeleePresentation(State,clock);
   float distance=HasPlayerMemory?Vector3.Distance(lastKnownPlayer,transform.position):float.PositiveInfinity;
   if(State==TacticalState.MeleeWarning)
   {
    Stop();if(clock>0)return;
    if(distance<2.8f&&Vector3.Dot(transform.forward,(lockedMelee-transform.position).normalized)>.25f&&Vector3.Distance(G.player.transform.position,lockedMelee)<1.25f&&Visible(transform.position))G.player.Hurt(17,owner.AimPoint);
    MeleeAttacks++;G.sound.Play("knife_swipe",owner.AimPoint);if(meleeRing)Destroy(meleeRing.gameObject);Set(TacticalState.Recover,.8f);return;
   }
   if(State==TacticalState.Recover){Stop();if(clock<=0)Set(TacticalState.Approach);return;}
   owner.agent.speed=3.6f;
   if(distance>2.35f||!Visible(transform.position)){if(HasPlayerMemory)Move(lastKnownPlayer,1.9f);else Stop();return;}
   Stop();lockedMelee=G.player.transform.position;Set(TacticalState.MeleeWarning,.6f);
   meleeRing=Shapes.Ring(G.effects,transform.position+Vector3.up*.07f,2.7f,new Color(1,.5f,.2f),.07f);G.sound.Play("warning",transform.position);
  }
  void OnDisable(){ReleaseCover();if(meleeRing)Destroy(meleeRing.gameObject);if(sniperLine)Destroy(sniperLine.gameObject);}
 }
}
