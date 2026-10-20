using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
namespace SwapHunter
{
 // Per-unit memory: enumerating deployed fields does not make their locations known.
 public sealed class EnemyHazardAwareness:MonoBehaviour
 {
  sealed class Known {public int id;public Vector3 center;public float expires,dps,reactAt;public bool covered,radial,groundArea;public float radius=3;}
  public static bool DecisionsEnabled=true;
  readonly List<Known> known=new List<Known>();
  EnemyActor owner;float nextObserve,nextChoose;Vector3 destination;bool avoiding,urgent;float reactAt;
  public int KnownCount=>known.Count;
  public int Decisions{get;private set;}
  public Vector3 ChosenDestination=>destination;
  public float ReactionReadyAt=>reactAt;
  public void Initialize(EnemyActor actor){owner=actor;}
  public void RefreshPerception(){if(owner&&owner.Alive)Observe();}
  public bool ShouldEscape=>DecisionsEnabled&&Time.time>=reactAt&&Covered(transform.position);
  bool Covered(Vector3 feet)
  {foreach(var h in known)if(h.dps>0&&In(h,feet))return true;return false;}
  bool In(Known h,Vector3 feet)
  {
   if(Time.time>=h.expires)return false;
   if(!h.radial)return ClassSkillArea.Contains(h.center,feet,feet+Vector3.up*1.1f);
   bool inShape=h.groundArea?Mathf.Abs(feet.y-h.center.y)<1.8f&&Vector3.ProjectOnPlane(feet-h.center,Vector3.up).sqrMagnitude<h.radius*h.radius:Vector3.Distance(h.center,feet+Vector3.up*.4f)<h.radius;
   return inShape&&!Physics.Linecast(h.center+Vector3.up*(h.groundArea?.35f:0),feet+Vector3.up*1.1f,Layers.WorldMask);
  }
  void Learn(ClassDenialField field,bool hurt)
  {
   if(!field||!field.Live)return;int id=field.GetInstanceID();var h=known.Find(k=>k.id==id);
   bool isNew=h==null;
   if(isNew){h=new Known{id=id,reactAt=Time.time+.35f+Mathf.Abs(id%26)*.01f};known.Add(h);}
   h.center=field.transform.position;h.expires=Time.time+field.SecondsLeft;h.dps=field.DamagePerSecond;
   bool covered=In(h,transform.position);
   if(isNew||covered&&!h.covered){urgent=true;reactAt=Mathf.Max(reactAt,h.reactAt,Time.time+owner.stunLeft);DemoGame.I.Record("d2_hazard_observed",owner.name+" id="+id+" cause="+(hurt?"damage":"sight"));}
   h.covered=covered;
  }
  public void NotifyHurt(ClassDenialField field){if(owner)Learn(field,true);}
  void ObserveWorldDanger(int id,Vector3 center,float dps,float remaining,float radius=0)
  {
   Vector3 delta=center+Vector3.up*.15f-owner.AimPoint;
   bool covered=radius>0?Mathf.Abs(transform.position.y-center.y)<1.8f&&Vector3.ProjectOnPlane(transform.position-center,Vector3.up).sqrMagnitude<radius*radius:ClassSkillArea.Contains(center,transform.position,owner.AimPoint);
   if(!covered&&(delta.sqrMagnitude>22*22||Vector3.Dot(transform.forward,delta.normalized)<-.15f||Physics.Linecast(owner.AimPoint,center+Vector3.up*.15f,Layers.WorldMask)))return;
   var h=known.Find(k=>k.id==id);bool created=h==null;
   if(created){h=new Known{id=id,reactAt=Time.time+.35f+Mathf.Abs(id%26)*.01f};known.Add(h);}
   h.center=center;h.expires=Time.time+remaining;h.dps=dps;
   if(radius>0){h.radial=true;h.groundArea=true;h.radius=radius;}
   if(created||covered&&!h.covered){urgent=true;reactAt=Mathf.Max(reactAt,h.reactAt,Time.time+owner.stunLeft);DemoGame.I.Record("enemy_world_hazard_observed",owner.name+";id="+id+";inside="+covered);}
   h.covered=covered;
  }
  void Observe()
  {
   for(int i=known.Count-1;i>=0;i--)if(Time.time>=known[i].expires)known.RemoveAt(i);
   if(Time.time<nextObserve)return;nextObserve=Time.time+.15f;
   foreach(var field in ClassDenialField.Active)
   {
    if(!field||!field.Live)continue;Vector3 target=field.transform.position+Vector3.up*.15f;Vector3 delta=target-owner.AimPoint;
    bool occupying=ClassSkillArea.Contains(field.transform.position,transform.position,owner.AimPoint);
    if(delta.sqrMagnitude>22*22||(!occupying&&Vector3.Dot(transform.forward,delta.normalized)<-.15f))continue;
    if(!Physics.Linecast(owner.AimPoint,target,Layers.WorldMask,QueryTriggerInteraction.Ignore))Learn(field,false);
   }
   foreach(var grenade in DemoGame.I.effects.GetComponentsInChildren<GrenadeActor>())
   {
    if(!grenade||!grenade.Live)continue;
    Vector3 center=grenade.BlastCenter,delta=center-owner.AimPoint;
    if(delta.sqrMagnitude>18*18||Physics.Linecast(owner.AimPoint,center,Layers.WorldMask))continue;
    if(delta.sqrMagnitude>8*8&&Vector3.Dot(transform.forward,delta.normalized)<-.15f)continue;
    int id=grenade.GetInstanceID();var h=known.Find(k=>k.id==id);
    if(h==null){h=new Known{id=id,radial=true,radius=DemoGame.I.config.grenadeRadius,reactAt=Time.time+.35f+(Mathf.Abs(id)%20)*.01f};known.Add(h);urgent=true;reactAt=Mathf.Max(reactAt,h.reactAt);}
    h.center=center;h.expires=Time.time+grenade.remaining;h.dps=DemoGame.I.config.grenadeDamage/Mathf.Max(.35f,grenade.remaining);h.covered=In(h,transform.position);
   }
   foreach(var steam in MapV2SteamValve.ActiveSnapshot())if(steam&&steam.Live)ObserveWorldDanger(steam.GetInstanceID(),steam.DangerCenter,owner.IsBoss?32:steam.DamagePerSecond,1);
   foreach(var danger in EnemyV2Danger.ActiveSnapshot())if(danger&&danger.Live&&danger.Radius>=3)ObserveWorldDanger(danger.GetInstanceID(),danger.Center,danger.DamagePerSecond*(owner.IsBoss?4:10),danger.SecondsLeft,danger.Radius);
  }
  public float PredictedDamage(Vector3 candidate,NavMeshPath path=null)
  {
   float damage=0;float speed=Mathf.Max(.1f,owner.agent.speed);
   foreach(var h in known)
   {
    float remaining=h.expires-Time.time;if(remaining<=0)continue;float travel=0,inside=0;
    if(path!=null&&path.corners.Length>1)
    {
     for(int i=1;i<path.corners.Length;i++)
     {
      Vector3 a=path.corners[i-1],b=path.corners[i];float distance=Vector3.Distance(a,b),seconds=distance/speed;
      // A long path edge can cross a small field between three coarse probes.
      // Bound probe spacing in world metres so route safety is independent of
      // how many corners the navigation mesh happened to generate.
      int slices=Mathf.Max(1,Mathf.CeilToInt(distance/.5f));float slice=seconds/slices;
      for(int j=0;j<slices;j++){float available=Mathf.Min(slice,Mathf.Max(0,remaining-travel));if(In(h,Vector3.Lerp(a,b,(j+.5f)/slices)))inside+=available;travel+=slice;}
     }
    }
    if(In(h,candidate))inside+=Mathf.Min(2,Mathf.Max(0,remaining-travel));
    damage+=inside*h.dps;
   }
   return damage;
  }
  bool Reachable(Vector3 requested,out Vector3 result,out NavMeshPath path)
  {
   result=requested;path=new NavMeshPath();if(!owner.agent.enabled||!owner.agent.isOnNavMesh)return false;
   if(!NavMesh.SamplePosition(requested,out var hit,1.35f,NavMesh.AllAreas)||Mathf.Abs(hit.position.y-requested.y)>1.1f)return false;
   result=hit.position;var territory=owner.GetComponent<MissionEncounterAgent>();
   if(territory&&!territory.Allows(result)||!EnemyMotion.ClearVolume(result,owner.Radius,owner.Height,transform))return false;
   return NavMesh.CalculatePath(transform.position,result,NavMesh.AllAreas,path)&&path.status==NavMeshPathStatus.PathComplete;
  }
  float Score(Vector3 p,Vector3 observation,bool hasObservation,NavMeshPath path)
  {
   float travel=0;for(int i=1;i<path.corners.Length;i++)travel+=Vector3.Distance(path.corners[i-1],path.corners[i]);
   bool shot=hasObservation&&!Physics.Linecast(p+Vector3.up*1.1f,observation,Layers.WorldMask);
   var territory=owner.GetComponent<MissionEncounterAgent>();float role=(!territory||territory.Allows(p))?1:0;
   return 4*(shot?1:0)+2*role+1.5f*FireControlRelay.ConnectionValue(owner,p)-travel/Mathf.Max(.1f,owner.agent.speed)-6*Mathf.Clamp01(PredictedDamage(p,path)/Mathf.Max(1,owner.health));
  }
  public bool TryMovement(Vector3 preferred,Vector3 observation,bool hasObservation,out Vector3 chosen)
  {
   chosen=destination;Observe();
   if(!DecisionsEnabled||known.Count==0){avoiding=false;return false;}
   if(Time.time<reactAt||owner.stunLeft>0)return false;
   if(urgent)nextChoose=0;
   if(Time.time<nextChoose)return avoiding;
   nextChoose=Time.time+.5f;urgent=false;
   Vector3 p=transform.position;
   bool threatened=Covered(p),preferredRisk=Covered(preferred);
   if(!preferredRisk&&Reachable(preferred,out var intended,out var intendedPath))preferredRisk=PredictedDamage(intended,intendedPath)>.1f;
   if(!threatened&&!preferredRisk){avoiding=false;return false;}
   var candidates=new List<Vector3>{p,preferred,p+Vector3.right*4,p-Vector3.right*4,p+Vector3.forward*4,p-Vector3.forward*4,p+new Vector3(4,0,4),p+new Vector3(-4,0,4),p+new Vector3(4,0,-4),p-new Vector3(4,0,4)};
   if(avoiding)candidates.Add(destination);
   // Include the actual hazard boundary, so a large grenade/field is not
   // mistaken for a safe endpoint after a fixed four-metre sidestep.
   foreach(var h in known)
   {
    if(!In(h,p)&&!In(h,preferred))continue;
    float radius=h.radial?h.radius:ClassSkillTuning.AreaRadius;
    for(int i=0;i<8;i++){float angle=i*Mathf.PI/4;candidates.Add(new Vector3(h.center.x,p.y,h.center.z)+new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*(radius+owner.Radius+.7f));}
   }
   float best=float.NegativeInfinity,bestExposure=float.PositiveInfinity;Vector3 selected=p;bool found=false,selectedSafe=false;
   foreach(var candidate in candidates)
   {
    if(!Reachable(candidate,out var point,out var path))continue;
    bool safe=!Covered(point);float exposure=PredictedDamage(point,path),score=Score(point,observation,hasObservation,path);
    // While threatened, a clear endpoint outranks a firing lane. Among clear
    // endpoints prefer the route that spends least time taking known damage.
    if(!found||safe&&!selectedSafe||safe==selectedSafe&&(exposure<bestExposure-.1f||Mathf.Abs(exposure-bestExposure)<=.1f&&score>best))
    {found=true;selectedSafe=safe;bestExposure=exposure;best=score;selected=point;}
   }
   if(!found){avoiding=false;DemoGame.I.Record("d2_hazard_path","no reachable candidates");return false;}
   bool changed=!avoiding||Vector3.Distance(destination,selected)>.4f;
   destination=selected;chosen=selected;avoiding=true;
   // Keep control at the safe endpoint while the old cover destination/path
   // remains hazardous; returning false here used to walk straight back in.
   if(changed){Decisions++;DemoGame.I.Record("d2_hazard_decision",owner.name+" chosen="+selected+" safe="+selectedSafe+" exposure="+bestExposure);}
   return true;
  }
 }
}

