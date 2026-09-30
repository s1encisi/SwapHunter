using System.Collections.Generic;
using UnityEngine;
namespace SwapHunter
{
 public sealed partial class DemoGame
 {
  sealed class Deployment
  {public EnemyKind kind;public Vector3 requested,point;public int arena;public float remaining;public bool located,optional;public LineRenderer ring,beam;}
  readonly List<Deployment> deployments=new List<Deployment>();
  public int PendingDeployments=>deployments.Count;
  public Vector3 NextDeploymentPoint=>deployments.Count>0?deployments[0].point:Vector3.zero;
  public int DeploymentWarnings {get;private set;}
  public int CompletedDeployments {get;private set;}
  public void QueueDeployment(EnemyKind kind,Vector3 requested,int arena,bool optional=false)
  {
   var entry=new Deployment{kind=kind,requested=requested,arena=arena,optional=optional};deployments.Add(entry);LocateDeployment(entry);
  }
  bool LocateDeployment(Deployment entry)
  {
   bool found=false;Vector3 chosen=entry.requested;float best=float.NegativeInfinity;
   var entrances=new List<Vector3>{entry.requested};if(expeditionActive&&expeditionMap!=null)entrances.AddRange(expeditionMap.enemySpawns);
   bool groundBoss=expeditionActive&&expeditionLevel==11&&entry.kind==EnemyKind.Elite;
   if(groundBoss)entrances.RemoveAll(p=>Mathf.Abs(p.y)>.2f);
   foreach(var entrance in entrances)for(int ring=0;ring<4;ring++)for(int k=0;k<(ring==0?1:8);k++)
   {
    float angle=k*Mathf.PI/4;Vector3 requested=entrance+new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*ring*3;
    if(!EnemyMotion.FindSpawn(entry.kind,requested,out var p))continue;
    if(groundBoss&&Mathf.Abs(p.y)>.2f)continue;
    Vector3 delta=p-player.transform.position;if(delta.magnitude<7)continue;
    bool duplicate=false;foreach(var other in deployments)if(other!=entry&&other.located&&Vector3.Distance(other.point,p)<2){duplicate=true;break;}if(duplicate)continue;
    float front=Vector3.Dot(Vector3.ProjectOnPlane(delta,Vector3.up).normalized,player.transform.forward);
    float score=front*3-Vector3.Distance(p,entry.requested)*.45f;
    if(score>best){best=score;chosen=p;found=true;}
   }
   if(!found){entry.remaining=.75f;entry.located=false;return false;}
   RemoveWarning(entry);entry.point=chosen;entry.located=true;
   bool behind=Vector3.Dot((chosen-player.transform.position).normalized,player.transform.forward)<-.1f;
   entry.remaining=behind?3.2f:2.2f;
   entry.ring=Shapes.Ring(effects,chosen+Vector3.up*.08f,1.15f,new Color(1,.58f,.16f),.08f);
   entry.beam=Shapes.Line(effects,chosen+Vector3.up*.1f,chosen+Vector3.up*4,new Color(1,.58f,.16f),.06f);
   DeploymentWarnings++;sound.Play("warning",chosen);Toast((behind?"侧后方":"前方")+(entry.optional?"数据追兵":"增援")+"部署预警 · 观察橙色光柱",2.1f);Record("spawn_warning",entry.kind+" at "+chosen+" seconds="+entry.remaining+" optional="+entry.optional);return true;
  }
  void UpdateDeployments()
  {
   if(!IsPlaying)return;
   for(int i=deployments.Count-1;i>=0;i--)
   {
    var entry=deployments[i];entry.remaining-=Time.deltaTime;if(entry.remaining>0)continue;
    if(!entry.located){LocateDeployment(entry);continue;}
    // Never silently lose an enemy budget. If a marked point becomes occupied, replan and warn again.
    float radius=entry.kind==EnemyKind.Elite?.45f:.36f,height=entry.kind==EnemyKind.Elite?2.1f:1.8f;
    if(Vector3.Distance(entry.point,player.transform.position)<4.5f||!EnemyMotion.ClearVolume(entry.point,radius,height))
    {entry.located=false;RemoveWarning(entry);LocateDeployment(entry);continue;}
    var actor=EnemyActor.Spawn(entry.kind,entry.point,entry.arena);
    if(!actor){entry.located=false;RemoveWarning(entry);entry.remaining=.75f;continue;}
    actor.expeditionOptional=entry.optional;actor.stunLeft=.5f;CompletedDeployments++;Record("spawn_deployed",entry.kind+" at "+actor.transform.position+" optional="+entry.optional);RemoveWarning(entry);deployments.RemoveAt(i);
   }
  }
  static void RemoveWarning(Deployment entry){if(entry.ring)Object.Destroy(entry.ring.gameObject);if(entry.beam)Object.Destroy(entry.beam.gameObject);entry.ring=entry.beam=null;}
  public void ClearDeployments(){foreach(var entry in deployments)RemoveWarning(entry);deployments.Clear();}
  void DrawDeploymentHud()
  {
   if(deployments.Count==0)return;var entry=deployments[0];
   Vector3 local=player.transform.InverseTransformPoint(entry.point);
   string direction=(local.z<0?"后方":"前方")+(Mathf.Abs(local.x)>3?(local.x<0?"偏左":"偏右"):"");
   bool bossActive=false;foreach(var e in enemies)if(e&&e.Alive&&e.boss){bossActive=true;break;}
   int categoryCount=0;foreach(var pending in deployments)if(pending.optional==entry.optional)categoryCount++;
   Text((entry.optional?"数据追兵":"增援")+"部署 "+categoryCount+" · "+(entry.located?direction+" / "+Mathf.Max(0,entry.remaining).ToString("F1")+"秒":"等待可用入口"),new Rect(viewWidth-460,bossActive?324:168,410,30),17,Amber,TextAnchor.MiddleRight);
  }
 }
}
