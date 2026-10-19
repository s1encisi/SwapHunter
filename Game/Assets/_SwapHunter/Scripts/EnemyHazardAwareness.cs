using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
namespace SwapHunter
{
 // Per-unit memory: enumerating deployed fields does not make their locations known.
 public sealed class EnemyHazardAwareness:MonoBehaviour
 {
  sealed class Known {public int id;public Vector3 center;public float expires,dps,reactAt;public bool covered;}
  public static bool DecisionsEnabled=true;
  readonly List<Known> known=new List<Known>();
  EnemyActor owner;float nextObserve,nextChoose,holdUntil;Vector3 destination;bool avoiding,urgent;float reactAt;
  public int KnownCount=>known.Count;
  public int Decisions{get;private set;}
  public Vector3 ChosenDestination=>destination;
  public float ReactionReadyAt=>reactAt;
  public void Initialize(EnemyActor actor){owner=actor;}
  bool In(Known h,Vector3 feet)=>Time.time<h.expires&&ClassSkillArea.Contains(h.center,feet,feet+Vector3.up*1.1f);
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
      Vector3 a=path.corners[i-1],b=path.corners[i];float seconds=Vector3.Distance(a,b)/speed;
      for(int j=0;j<3;j++){float slice=seconds/3;float available=Mathf.Min(slice,Mathf.Max(0,remaining-travel));if(In(h,Vector3.Lerp(a,b,(j+.5f)/3)))inside+=available;travel+=slice;}
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
   if(!NavMesh.SamplePosition(requested,out var hit,.65f,NavMesh.AllAreas)||Mathf.Abs(hit.position.y-requested.y)>.6f)return false;
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
   if(Time.time<nextChoose)return avoiding;
   nextChoose=Time.time+.5f;
   // Exactly six candidates. Hard reachability/territory/occupancy checks precede scoring.
   Vector3 p=transform.position;
   var candidates=new[]{p,preferred,p+Vector3.right*4,p-Vector3.right*4,p+Vector3.forward*4,p-Vector3.forward*4};
   float best=float.NegativeInfinity,current=float.NegativeInfinity;Vector3 selected=p;
   bool lethal=PredictedDamage(p)>=owner.health;
   for(int i=0;i<candidates.Length;i++)
   {
    if(!Reachable(candidates[i],out var point,out var path))continue;
    float score=Score(point,observation,hasObservation,path);if(i==0)current=score;
    if(score>best){best=score;selected=point;}
   }
   if(float.IsNegativeInfinity(best)){avoiding=false;DemoGame.I.Record("d2_hazard_path","no reachable candidates");return false;}
   if(!urgent&&!lethal&&(Time.time<holdUntil||best<current+1.5f))return avoiding;
   bool safer=PredictedDamage(selected)<PredictedDamage(p)-.01f;
   if(!safer&&best<current+1.5f){urgent=false;avoiding=false;return false;}
   destination=selected;chosen=selected;avoiding=Vector3.Distance(p,selected)>.35f;holdUntil=Time.time+1;urgent=false;Decisions++;
   DemoGame.I.Record("d2_hazard_decision",owner.name+" chosen="+selected+" score="+best+" current="+current+" predicted="+PredictedDamage(selected));
   return avoiding;
  }
 }
}

