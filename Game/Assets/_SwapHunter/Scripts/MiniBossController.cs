using System;
using UnityEngine;
using UnityEngine.AI;
namespace SwapHunter
{
 public enum MiniBossAction { Pulse, Turn, Rush, Gate, Sweep, Transfer, Shield, AimBurst, Crossfire, Heat, TetherLine, Slam, Cart, Suppress, Jammer, DoublePulse }
 public sealed class MiniBossController:MonoBehaviour
 {
  static readonly string[] names={"校准监理","闸门执法官","升井监察机","站场壁垒官","双塔校射官","泄压炉卫","断桥牵引者","分拣执行官","巡天猎隼","运输督导","反相审查者","相位门卫"};
  static readonly float[] hp={1440,2400,2880,3360,3840,4320,3840,4320,4800,4800,4800,3840};
  static readonly MiniBossAction[,] actions={{MiniBossAction.Pulse,MiniBossAction.Turn},{MiniBossAction.Rush,MiniBossAction.Gate},{MiniBossAction.Sweep,MiniBossAction.Transfer},{MiniBossAction.Shield,MiniBossAction.AimBurst},{MiniBossAction.Crossfire,MiniBossAction.Transfer},{MiniBossAction.Heat,MiniBossAction.Rush},{MiniBossAction.TetherLine,MiniBossAction.Sweep},{MiniBossAction.Cart,MiniBossAction.Slam},{MiniBossAction.Transfer,MiniBossAction.AimBurst},{MiniBossAction.Suppress,MiniBossAction.Rush},{MiniBossAction.Jammer,MiniBossAction.Crossfire},{MiniBossAction.Shield,MiniBossAction.DoublePulse}};
  EnemyActor owner;DemoGame G=>DemoGame.I;Vector3[] posts=Array.Empty<Vector3>();Vector3 locked,second,direction,start,destination;float timer,total,nextAt,elapsed,shotAt,silenceUntil,lastLanded,fieldAt,gateAt;int sequence,shots;bool scripted,hit,componentLost;EnemyV2Device component;LineRenderer telegraph,secondary;
  public int Chapter{get;private set;}public string DisplayName=>names[Chapter];public string Id=>"B"+(Chapter+1).ToString("00");
  public EnemyAbilityPhase Phase{get;private set;}public MiniBossAction Action{get;private set;}public string Warning{get;private set;}="";
  public int CastCount{get;private set;}public int CompletedCasts{get;private set;}public int InterruptedCasts{get;private set;}
  public float SilenceResistanceLeft=>owner&&owner.Silenced?0:Mathf.Max(0,silenceUntil-Time.time);
  public void Initialize(EnemyActor actor,int chapter)
  {
   owner=actor;Chapter=Mathf.Clamp(chapter,0,11);owner.maximumHealth=owner.health=hp[Chapter];owner.VisualRig.ConfigureVariant(4,Chapter);owner.name=Id+" "+DisplayName;
   if(Chapter==1)owner.AddMiniBossPhysicalShield();else owner.DisablePhysicalShield();
   nextAt=Time.time+1.6f;lastLanded=Time.time;
   var mount=owner.VisualRig.Weakpoint("equipment");component=EnemyV2Device.Create(mount?mount:transform,mount?mount.position:owner.AimPoint-transform.forward*.5f,"暴露组件",240,Chapter==3,owner,()=>{componentLost=true;Interrupt("component");});
   D2WorldText.Create(transform,Id+" · "+DisplayName,owner.transform.position+Vector3.up*2.7f,.23f);
   G.Record("mini_boss_begin",Id+";health="+hp[Chapter]+";independent=true");
  }
  public void SetPosts(Vector3[] values){posts=values??Array.Empty<Vector3>();}
  public bool TryReceiveSilence(float seconds)
  {if(owner.Silenced||Time.time<silenceUntil)return false;silenceUntil=Time.time+Mathf.Min(1.5f,seconds)+4;if(Phase==EnemyAbilityPhase.Telegraph)Interrupt("silence");return true;}
  public void NotifyHurt(float damage){}
  public void AfterSwap(){if(scripted||Phase==EnemyAbilityPhase.Telegraph||Phase==EnemyAbilityPhase.Execute)Interrupt("swap");else nextAt=Mathf.Max(nextAt,Time.time+.95f);}
  public void OnKilled(){Clear();if(scripted){owner.motion.EndScripted();scripted=false;}EnemySkillReservations.Release(owner);G.Record("mini_boss_defeated",Id);}
  void Clear(){if(telegraph)Destroy(telegraph.gameObject);if(secondary)Destroy(secondary.gameObject);if(owner&&owner.VisualRig)owner.VisualRig.SetAbilityPresentation(Warning,1,false);Warning="";}
  void Stop(){if(owner.agent.enabled&&owner.agent.isOnNavMesh){owner.agent.isStopped=true;owner.agent.ResetPath();}}
  public void Tick(float dt)
  {
   if(!owner.Alive)return;
   if(owner.stunLeft>0){Stop();return;}
   if(Phase==EnemyAbilityPhase.Telegraph)
   {
    Stop();timer-=dt;owner.VisualRig.SetAbilityPresentation(Warning,1-Mathf.Clamp01(timer/total),true);
    if(Action==MiniBossAction.Pulse&&timer>.3f&&owner.tactical.SeesPlayer){locked=owner.tactical.LastKnownFeet;MoveRing(telegraph,locked,2.2f);}
    if(timer<=0)Execute();return;
   }
   if(Phase==EnemyAbilityPhase.Execute){ExecuteTick(dt);return;}
   if(Phase==EnemyAbilityPhase.Recover){Stop();timer-=dt;if(timer<=0){Phase=EnemyAbilityPhase.Ready;Clear();}return;}
   if(!owner.tactical.HasPlayerMemory){Stop();return;}
   Vector3 observed=owner.tactical.ObservedPlayerAim;Vector3 flat=Vector3.ProjectOnPlane(observed-owner.AimPoint,Vector3.up);
   if(flat.sqrMagnitude>.01f)owner.FaceSafely(Quaternion.RotateTowards(transform.rotation,Quaternion.LookRotation(flat),70*dt));
   if(Time.time>=nextAt&&!owner.Silenced&&owner.tactical.SeesPlayer){var action=actions[Chapter,sequence%2];if(Chapter==7&&sequence%3==2)action=Auxiliary();if(Begin(action)){sequence++;return;}}
   // Readable light pressure between major mechanics; no hidden target through a wall.
   if(Chapter!=1&&Chapter!=2&&Chapter!=6&&owner.tactical.SeesPlayer)owner.tactical.FireBasic(5,.4f);
   float preferred=Chapter==1||Chapter==6?6:15;float distance=flat.magnitude;
   if(distance>preferred+2&&owner.agent.enabled&&owner.agent.isOnNavMesh)
   {var territory=owner.GetComponent<MissionEncounterAgent>();Vector3 p=owner.tactical.LastKnownFeet;if(!territory||territory.Allows(p)){owner.agent.isStopped=false;owner.agent.stoppingDistance=preferred;owner.agent.speed=2.6f;owner.agent.SetDestination(p);}}
   else Stop();
  }
  MiniBossAction Auxiliary()
  {if(G.mission!=null){if(!G.mission.ObjectiveComplete("a"))return MiniBossAction.Sweep;if(!G.mission.ObjectiveComplete("b"))return MiniBossAction.AimBurst;if(!G.mission.ObjectiveComplete("c"))return MiniBossAction.Transfer;}return MiniBossAction.Turn;}
  bool Begin(MiniBossAction action)
  {
   if(action==MiniBossAction.Jammer&&(Time.time<fieldAt||!EnemyV2DynamicField.CanDeploy(transform.position)))action=MiniBossAction.Crossfire;
   if(action==MiniBossAction.Shield&&Time.time<fieldAt)action=Chapter==11?MiniBossAction.DoublePulse:MiniBossAction.AimBurst;
   if(action==MiniBossAction.Gate&&Time.time<gateAt)action=MiniBossAction.Rush;
   if(!EnemySkillReservations.TryClaim(owner,action==MiniBossAction.Jammer||action==MiniBossAction.TetherLine?"control":"pressure",4))return false;
   Action=action;Phase=EnemyAbilityPhase.Telegraph;CastCount++;locked=owner.tactical.LastKnownFeet;start=transform.position;direction=Vector3.ProjectOnPlane(locked-start,Vector3.up).normalized;if(direction.sqrMagnitude<.1f)direction=transform.forward;
   second=locked+Vector3.Cross(Vector3.up,direction)*3;destination=start+direction*(Chapter==9?10:Chapter==5?8:5);
   if(action==MiniBossAction.Transfer&&!ChoosePost(out destination)){action=Action=MiniBossAction.AimBurst;}
   if(Chapter==9&&action==MiniBossAction.Rush){destination=MapV2WorldActions.TrackDestination(destination);Vector3 rail=Vector3.ProjectOnPlane(destination-start,Vector3.up);destination=start+Vector3.ClampMagnitude(rail,10);}
   total=timer=Windup(action);Warning=ActionLabel(action);Stop();hit=false;shots=0;elapsed=shotAt=0;
   if(action==MiniBossAction.Rush||action==MiniBossAction.Crossfire||action==MiniBossAction.AimBurst||action==MiniBossAction.Suppress||action==MiniBossAction.Sweep)
   {telegraph=Shapes.Line(G.effects,owner.AimPoint,action==MiniBossAction.Rush?destination+Vector3.up*.1f:locked+Vector3.up*.8f,new Color(1,.3f,.1f),.07f);if(action==MiniBossAction.Crossfire)secondary=Shapes.Line(G.effects,owner.AimPoint,second+Vector3.up*.8f,new Color(1,.7f,.15f),.07f);}
   else{telegraph=Shapes.Ring(G.effects,(action==MiniBossAction.Transfer?destination:locked)+Vector3.up*.06f,action==MiniBossAction.Heat?3:2.2f,new Color(1,.35f,.1f),.075f);if(action==MiniBossAction.DoublePulse)secondary=Shapes.Ring(G.effects,second+Vector3.up*.06f,2.2f,new Color(1,.7f,.15f),.075f);}
   G.sound.Play("warning",owner.AimPoint);G.Toast(DisplayName+" · "+Warning,total+1.5f);G.Record("mini_boss_warning",Id+":"+action+";seconds="+total+";locked="+locked);return true;
  }
  float Windup(MiniBossAction a)
  {switch(a){case MiniBossAction.Pulse:return .9f;case MiniBossAction.Turn:return .7f;case MiniBossAction.Rush:return Chapter==1?.8f:Chapter==9?1.2f:1;case MiniBossAction.Gate:return .8f;case MiniBossAction.Transfer:return Chapter==2?.8f:Chapter==8?1.1f:.9f;case MiniBossAction.Crossfire:return Chapter==4?1.2f:1;case MiniBossAction.Heat:case MiniBossAction.Jammer:return 1.2f;case MiniBossAction.Slam:return .9f;case MiniBossAction.Shield:return Chapter==11?.9f:1;default:return 1;}}
  string ActionLabel(MiniBossAction a)
  {switch(a){case MiniBossAction.Pulse:return "定点灼射 · 离开光圈";case MiniBossAction.Turn:return "扫描转身 · 侧背暴露";case MiniBossAction.Rush:return Chapter==9?"道岔冲撞 · 离开轨道":"压力冲击 · 侧向避让";case MiniBossAction.Gate:return "闸口压迫 · 另一侧保持畅通";case MiniBossAction.Sweep:return "扇区横扫 · 躲柱/换层/内圈";case MiniBossAction.Transfer:return "转场落位 · 终点已经标出";case MiniBossAction.Shield:return "固定屏障 · 夺取盾后位";case MiniBossAction.AimBurst:return "扇区点射 · 断开枪线";case MiniBossAction.Crossfire:return "交叉校射 · 两次先后射击";case MiniBossAction.Heat:return "定点泄压 · 热区不追踪";case MiniBossAction.TetherLine:return "双点牵引 · 射击索座断线";case MiniBossAction.Slam:return "分拣冲压 · 离开锁定区域";case MiniBossAction.Cart:return "轨道封道 · 货物即将移动";case MiniBossAction.Suppress:return "装运压制 · 枪臂扇区锁定";case MiniBossAction.Jammer:return "局部审查 · 信标可摧毁";default:return "定位冲击 · 两处依次生效";}}
  bool ChoosePost(out Vector3 p)
  {
   p=transform.position;for(int i=0;i<posts.Length;i++){Vector3 candidate=posts[(sequence+i)%posts.Length];if(Vector3.Distance(candidate,transform.position)<2)continue;if(componentLost&&Vector3.Distance(candidate,transform.position)>12)continue;if(EnemyMotion.GroundAt(candidate,out var ground)&&EnemyMotion.ClearVolume(ground,owner.Radius,owner.Height,transform)){p=ground;return true;}}return false;
  }
  void Execute()
  {
   if(telegraph)Destroy(telegraph.gameObject);if(secondary)Destroy(secondary.gameObject);Phase=EnemyAbilityPhase.Execute;elapsed=0;start=transform.position;G.Record("mini_boss_execute",Id+":"+Action);
   switch(Action)
   {
    case MiniBossAction.Pulse:EnemyV2Danger.Area(owner,locked,2.2f,16);Recover(.8f);break;
    case MiniBossAction.Turn:owner.FaceSafely(Quaternion.LookRotation(-direction));Recover(.8f);break;
    case MiniBossAction.Gate:MapV2WorldActions.Try(Chapter,"gate",start,direction);gateAt=Time.time+10;Recover(.9f);break;
    case MiniBossAction.Cart:MapV2WorldActions.Try(Chapter,"cart",start,direction);Recover(2);break;
    case MiniBossAction.Shield:
     fieldAt=Time.time+12;DirectionalBarrier.Spawn(owner.AimPoint+direction*1.2f,direction,6,transform);
     if(Chapter==3)DirectionalBarrier.Spawn(owner.AimPoint+Quaternion.Euler(0,70,0)*direction*1.5f,Quaternion.Euler(0,70,0)*direction,6,transform);Recover(.7f);break;
    case MiniBossAction.Heat:EnemyV2Danger.Area(owner,locked,3,8,componentLost?1.5f:3,.25f);EnemySkillReservations.Hold(owner,componentLost?1.5f:3);Recover(1);break;
    case MiniBossAction.TetherLine:MiniBossTripwire.Spawn(owner,locked-Vector3.Cross(Vector3.up,direction)*3,locked+Vector3.Cross(Vector3.up,direction)*3);EnemySkillReservations.Hold(owner,3);Recover(1);break;
    case MiniBossAction.Jammer:EnemyV2DynamicField.Spawn(start);EnemySkillReservations.Hold(owner,4);fieldAt=Time.time+12;Recover(.9f);break;
    case MiniBossAction.Slam:EnemyV2Danger.Rectangle(owner,locked,direction,3,4,24);Recover(1);break;
    case MiniBossAction.Rush:owner.motion.BeginScripted();scripted=true;break;
    case MiniBossAction.Transfer:
     if(Chapter==4){if(EnemyMotion.ClearVolume(destination,owner.Radius,owner.Height,transform)&&owner.MoveTo(destination)){Shapes.Burst(start,Color.cyan,1);Shapes.Burst(destination,Color.cyan,1);}Recover(.8f);}
     else if(Chapter==8){if(Time.time-lastLanded<3){Recover(1);break;}owner.motion.BeginScripted();scripted=true;}
     else if(owner.agent.enabled&&owner.agent.isOnNavMesh){var path=new NavMeshPath();if(owner.agent.CalculatePath(destination,path)&&path.status==NavMeshPathStatus.PathComplete){owner.agent.isStopped=false;owner.agent.speed=4;owner.agent.stoppingDistance=.1f;owner.agent.SetPath(path);}else Recover(.8f);}else Recover(.8f);break;
   }
  }
  void ExecuteTick(float dt)
  {
   elapsed+=dt;owner.VisualRig.SetAbilityPresentation(Warning,Mathf.Clamp01(elapsed),true);
   switch(Action)
   {
    case MiniBossAction.Rush:
     Vector3 travel=Vector3.ProjectOnPlane(destination-start,Vector3.up);owner.motion.Push(travel.normalized*travel.magnitude/.65f*dt);MapV2WorldActions.Try(Chapter,"breach",transform.position,direction);
     if(!hit&&Vector3.Distance(G.player.transform.position,transform.position)<1.7f&&!Physics.Linecast(owner.AimPoint,G.player.Eye.position,Layers.WorldMask)){hit=true;G.player.Hurt(Chapter==9?26:Chapter==5?24:18,owner.AimPoint,false);}
     if(elapsed>=.65f){owner.motion.EndScripted();scripted=false;Recover(Chapter==1?.9f:1.1f);}break;
    case MiniBossAction.Transfer:
     if(Chapter==8){float t=Mathf.Clamp01(elapsed/1.1f);Vector3 p=Vector3.Lerp(start,destination,t)+Vector3.up*Mathf.Sin(t*Mathf.PI)*4;owner.motion.Push(p-transform.position);if(t>=1){owner.motion.EndScripted();scripted=false;lastLanded=Time.time;EnemyV2Danger.Area(owner,transform.position,2.2f,22);Recover(1);}}
     else if(Vector3.Distance(transform.position,destination)<.6f||elapsed>8)Recover(.8f);break;
    case MiniBossAction.Sweep:
     if(!hit){hit=true;Vector3 d=G.player.transform.position-transform.position;float min=Chapter==6?1.5f:0,max=Chapter==6?7:12;float angle=Chapter==6?60:50;if(d.magnitude>min&&d.magnitude<max&&Mathf.Abs(d.y)<1.7f&&Vector3.Angle(direction,Vector3.ProjectOnPlane(d,Vector3.up))<angle&&!Physics.Linecast(owner.AimPoint,G.player.Eye.position,Layers.WorldMask))G.player.Hurt(Chapter==6?22:20,owner.AimPoint,false);Shapes.Burst(owner.AimPoint+direction*2,new Color(1,.5f,.1f),2);}Recover(1);break;
    case MiniBossAction.Crossfire:
     if(shots==0){owner.ShootAt(locked+Vector3.up*.9f,Chapter==10?24:22,90);shots++;}
     if(shots==1&&elapsed>=(Chapter==10?.5f:.45f)){owner.ShootAt(second+Vector3.up*.9f,Chapter==10?24:22,90);shots++;Recover(.9f);}break;
    case MiniBossAction.AimBurst:case MiniBossAction.Suppress:
     if(elapsed>=shotAt){shotAt=elapsed+.23f;Vector3 current=owner.tactical.PlayerTorso;Vector3 d=Vector3.ProjectOnPlane(current-owner.AimPoint,Vector3.up);if(owner.tactical.SeesPlayer&&Vector3.Angle(direction,d)<45)owner.ShootAt(current+UnityEngine.Random.insideUnitSphere*.3f,7,65);}
     if(elapsed>=(Action==MiniBossAction.Suppress?2:.9f))Recover(Action==MiniBossAction.Suppress?1.2f:.9f);break;
    case MiniBossAction.DoublePulse:
     if(shots==0){EnemyV2Danger.Area(owner,locked,2.2f,22);shots++;}if(shots==1&&elapsed>=.6f){EnemyV2Danger.Area(owner,second,2.2f,22);shots++;Recover(1);}break;
    default:Recover(.8f);break;
   }
  }
  void Recover(float seconds){CompletedCasts++;Phase=EnemyAbilityPhase.Recover;timer=total=seconds;nextAt=Time.time+seconds+(owner.health<owner.maximumHealth*.5f?1.2f:2);Stop();}
  void Interrupt(string cause){InterruptedCasts++;if(scripted){owner.motion.EndScripted();scripted=false;}Clear();Phase=EnemyAbilityPhase.Recover;timer=total=.8f;nextAt=Time.time+2;EnemySkillReservations.Release(owner);G.Record("mini_boss_interrupt",Id+":"+cause);}
  static void MoveRing(LineRenderer ring,Vector3 center,float radius){if(!ring)return;for(int i=0;i<ring.positionCount;i++){float a=i*Mathf.PI*2/(ring.positionCount-1);ring.SetPosition(i,center+Vector3.up*.06f+new Vector3(Mathf.Cos(a),0,Mathf.Sin(a))*radius);}}
  void OnDisable(){Clear();if(owner)EnemySkillReservations.Release(owner);}
 }
 public sealed class MiniBossTripwire:MonoBehaviour
 {
  Vector3 a,b;float remaining=3,nextDamage;EnemyV2Device left,right;LineRenderer line;EnemyActor owner;
  public static void Spawn(EnemyActor owner,Vector3 a,Vector3 b)
  {var go=new GameObject("可拆双点牵引索");go.transform.SetParent(DemoGame.I.effects);var t=go.AddComponent<MiniBossTripwire>();t.owner=owner;t.a=a+Vector3.up*.35f;t.b=b+Vector3.up*.35f;t.left=EnemyV2Device.Create(go.transform,t.a,"索座",144,false,null,()=>{if(t)Destroy(t.gameObject);});t.right=EnemyV2Device.Create(go.transform,t.b,"索座",144,false,null,()=>{if(t)Destroy(t.gameObject);});t.line=Shapes.Line(go.transform,t.a,t.b,new Color(1,.55f,.05f),.06f);}
  void Update()
  {var g=DemoGame.I;if(!g.IsPlaying)return;if((remaining-=Time.deltaTime)<=0){Destroy(gameObject);return;}Vector3 point=g.player.transform.position+Vector3.up*.1f,delta=b-a;float u=Mathf.Clamp01(Vector3.Dot(point-a,delta)/delta.sqrMagnitude);if(Time.time>=nextDamage&&Vector3.Distance(point,a+u*delta)<.6f&&!Physics.Linecast(a,b,Layers.WorldMask)){nextDamage=Time.time+.8f;g.player.Hurt(16,a,false);}}
 }
}
