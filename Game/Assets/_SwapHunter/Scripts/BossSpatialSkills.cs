using UnityEngine;
namespace SwapHunter
{
 public enum BossSkill { None, SweepLaser, Attraction, AdvantageSwap, GroundWave, SwapElbow, OmniFire }
 public enum BossSkillStep { Ready, Telegraph, Execute, Recover }
 // Immutable values owned by one accepted cast, including its original recovery phase.
 public sealed class BossCastParameters
 {
  public readonly BossSkill skill;public readonly BossPhase phase;
  public readonly float warningSeconds,executionSeconds,recoverySeconds,damage,radius;
  public readonly float pulseInterval,projectileDamage,projectileSpeed,aimedDamage,aimedSpeed;
  public readonly float halfWidth=15,waveSpeed=12,pullSpeed=5.5f;
  internal BossCastParameters(BossSkill skill,BossPhase phase,float warning)
  {
   this.skill=skill;this.phase=phase;warningSeconds=warning;recoverySeconds=phase==BossPhase.Combined?2.2f:3.2f;
   executionSeconds=skill==BossSkill.SweepLaser?1.8f:skill==BossSkill.GroundWave?2.7f:skill==BossSkill.OmniFire?2:skill==BossSkill.Attraction?2.3f:.6f;
   damage=skill==BossSkill.SweepLaser?28:skill==BossSkill.Attraction?24:26;
   radius=skill==BossSkill.Attraction?4.8f:3.4f;
   pulseInterval=skill==BossSkill.Attraction?.35f:.24f;
   projectileDamage=skill==BossSkill.Attraction?8:5;projectileSpeed=skill==BossSkill.Attraction?38:23;
   aimedDamage=7;aimedSpeed=32;
  }
 }
 public sealed partial class BossEncounter
 {
  public BossSkill Skill {get;private set;}
  public BossSkillStep SkillStep {get;private set;}
  public int SpatialCasts {get;private set;}
  public int SpatialSwaps {get;private set;}
  public int SpatialPulses {get;private set;}
  public int SpatialProjectiles {get;private set;}
  public BossCastParameters CurrentCast {get;private set;}
  public float RecoverySecondsLeft=>SkillStep==BossSkillStep.Recover?Mathf.Max(0,nextSkillAt-Time.time):0;
  public float TelegraphProgress=>CurrentCast==null?0:Mathf.Clamp01(1-skillTime/CurrentCast.warningSeconds);
  bool PresentationUsesEnergyForm=>(SkillStep==BossSkillStep.Telegraph||SkillStep==BossSkillStep.Execute)&&CurrentCast!=null
   ?CurrentCast.phase==BossPhase.EnergyMelee:Phase==BossPhase.EnergyMelee;
  public Vector3 DangerCenter=>skillCenter;
  public LineRenderer DangerOutline=>zone;
  public LineRenderer SwapPathPreview=>pathPreview;
  public Vector3 DangerForward=>skillForward;
  public Vector3 SpatialDestination=>skillDestination;
  public float DangerGroundY=>groundY;
  public float SkillAgeSeconds=>skillAge;
  public SwapTarget SpatialTarget=>skillTarget;
  public float SkillSeconds=>skillTime;
  float skillTime,nextSkillAt,skillAge,pulseAt,groundY;
  bool skillHit;
  Vector3 skillCenter,skillForward,skillDestination;
  SwapTarget skillTarget;
  LineRenderer zone,pathPreview;
  static readonly Color Danger=new Color(1,.32f,.12f);

  public bool SkillAllowed(BossSkill skill)
  {
   if(Phase==BossPhase.Defeated||skill==BossSkill.None)return false;
   if(skill==BossSkill.SwapElbow)return Phase==BossPhase.EnergyMelee;
   if(skill==BossSkill.Attraction||skill==BossSkill.AdvantageSwap)return Phase!=BossPhase.EnergyMelee;
   return Phase==BossPhase.Combined;
  }
  bool ValidExchange(SwapTarget target)
  {
   if(!target||!target.Alive||target==Owner||target is EnemyActor shielded&&shielded.HasPhysicalShield)return false;
   if(Vector3.Distance(target.transform.position,transform.position)>28)return false;
   if(!EnemyMotion.ClearVolume(target.transform.position,Owner.Radius,Owner.Height,transform,target.transform)
      ||!EnemyMotion.ClearVolume(transform.position,target.Radius,target.Height,transform,target.transform))return false;
   return !Physics.Linecast(Owner.AimPoint,target.AimPoint,Layers.WorldMask,QueryTriggerInteraction.Ignore);
  }
  SwapTarget ChooseTarget(bool elbow,Vector3 observedPlayer)
  {
   SwapTarget best=null;float score=float.NegativeInfinity;
   foreach(var t in G.anchors)Consider(t);
   foreach(var t in G.enemies)Consider(t);
   return best;
   void Consider(SwapTarget t)
   {
    if(!ValidExchange(t))return;
    float playerDistance=Vector3.Distance(t.transform.position,observedPlayer);
    float travel=Vector3.Distance(t.transform.position,transform.position);
    if(travel<4||elbow&&playerDistance>4.2f)return;
    float s=elbow?-playerDistance:t.transform.position.y*4-Mathf.Abs(playerDistance-12)+travel*.1f;
    if(s>score){score=s;best=t;}
   }
  }
  public bool TryBeginSkill(BossSkill skill)
  {
   if(!TryGetSpatialObservation(out var observed))return false;
   var target=skill==BossSkill.AdvantageSwap||skill==BossSkill.SwapElbow?ChooseTarget(skill==BossSkill.SwapElbow,observed.position):null;
   return TryBeginSkill(skill,observed,target);
  }
  bool TryBeginSkill(BossSkill skill,BossSpatialObservation observed,SwapTarget proposedTarget)
  {
   if(!Owner||!Owner.Alive||Owner.Silenced||!SkillAllowed(skill)||SkillStep==BossSkillStep.Telegraph||SkillStep==BossSkillStep.Execute||!G.IsPlaying
    ||SkillStep==BossSkillStep.Recover&&Time.time<nextSkillAt)return false;
   skillTarget=null;
   if(skill==BossSkill.AdvantageSwap||skill==BossSkill.SwapElbow)
   {skillTarget=proposedTarget;if(!ValidExchange(skillTarget))return false;skillDestination=skillTarget.transform.position;}
   ClearWarning();ClearSpatialVisuals();Skill=skill;SkillStep=BossSkillStep.Telegraph;skillAge=0;skillHit=false;
   skillCenter=observed.position;groundY=skillCenter.y;
   if(Physics.Raycast(skillCenter+Vector3.up*.2f,Vector3.down,out var floor,30,Layers.WorldMask))groundY=floor.point.y;
   if(skill==BossSkill.SweepLaser||skill==BossSkill.GroundWave)
   {
    // Ground attacks remain on the arena floor even when the player already holds a tower.
    groundY=observed.ground;
   }
   skillCenter.y=groundY+.06f;
   skillForward=Vector3.ProjectOnPlane(skillCenter-transform.position,Vector3.up).normalized;
   if(skillForward.sqrMagnitude<.1f)skillForward=Vector3.forward;
   skillTime=(skill==BossSkill.SweepLaser||skill==BossSkill.Attraction)?observed.reactionWindow:skill==BossSkill.OmniFire?1.2f:skill==BossSkill.GroundWave&&G.expeditionActive&&G.expeditionLevel==11?2.5f:1.5f;
   CurrentCast=new BossCastParameters(skill,Phase,skillTime);
   switch(skill)
   {
    case BossSkill.SweepLaser:Warning="扫地激光 · 危险带已锁定，{key:6} 换位离开";zone=Shapes.Line(G.effects,skillCenter-Vector3.Cross(Vector3.up,skillForward)*15,skillCenter+Vector3.Cross(Vector3.up,skillForward)*15,Danger,.12f);break;
    case BossSkill.Attraction:Warning="牵引场 · {key:6} 换位离开橙色区域";zone=Shapes.Ring(G.effects,skillCenter,CurrentCast.radius,Danger,.09f);break;
    case BossSkill.GroundWave:Warning="撼地波 · 上黄色检修台，或 {key:6} 换位 / {key:4} 大跳；屏障无效";skillCenter=new Vector3(transform.position.x,groundY+.06f,transform.position.z);zone=Shapes.Ring(G.effects,skillCenter,2,Danger,.12f);break;
    case BossSkill.OmniFire:Warning="全向火力 · {key:20} 屏障朝向指挥官，或进入实体掩体";zone=Shapes.Ring(G.effects,transform.position+Vector3.up*.1f,2.4f,Danger,.1f);break;
    default:Warning=skill==BossSkill.SwapElbow?"换位肘击 · 离开标记目标附近":"指挥官即将换位 · 观察橙色落点";zone=Shapes.Ring(G.effects,skillDestination+Vector3.up*.08f,CurrentCast.radius,Danger,.09f);pathPreview=Shapes.Line(G.effects,Owner.AimPoint,skillTarget.AimPoint,Danger,.06f);break;
   }
   RememberSpatialCast(skill);StopMotion();G.sound.Play("warning",transform.position);G.Record("boss_telegraph",skill+" center="+skillCenter+" seconds="+skillTime);return true;
  }
  float GroundThreatFloor(Vector3 observedPlayer)
  {
   if(G.expeditionActive)return 0;
   Vector3 player=observedPlayer;float floorY=player.y;
   if(Physics.Raycast(player+Vector3.up*.2f,Vector3.down,out var floor,30,Layers.WorldMask))floorY=floor.point.y;
   for(int k=0;k<8;k++)
   {
    float angle=k*Mathf.PI/4;Vector3 sample=transform.position+new Vector3(Mathf.Cos(angle)*8,12,Mathf.Sin(angle)*8);
    if(Physics.Raycast(sample,Vector3.down,out var baseFloor,40,Layers.WorldMask)&&baseFloor.normal.y>.7f)floorY=Mathf.Min(floorY,baseFloor.point.y);
   }
   return floorY;
  }
  bool BeginNextSpatialSkill()
  {
   if(!TryGetSpatialObservation(out var observed))
   {
    LastSpatialDecision=new BossSpatialDecision{sequence=++decisionSequence,observation="unavailable",observationAge=Time.time-lastObservedAt};
    G.Record("boss_spatial_decision",JsonUtility.ToJson(LastSpatialDecision));return false;
   }
   var decision=BuildSpatialDecision(observed);LastSpatialDecision=decision;
   foreach(var candidate in decision.candidates)
   {
    if(!candidate.legal)continue;
    if(TryBeginSkill(candidate.skill,observed,candidate.target))
    {decision.selected=candidate.skill;G.Record("boss_spatial_decision",JsonUtility.ToJson(decision));return true;}
    candidate.reason+="; start rejected with original snapshot";
   }
   G.Record("boss_spatial_decision",JsonUtility.ToJson(decision));return false;
  }
  void StopMotion(){if(Owner.agent.enabled&&Owner.agent.isOnNavMesh){Owner.agent.ResetPath();Owner.agent.isStopped=true;}}
  bool TickSpatialSkills(float dt,bool allowNewAction)
  {
   if(Owner.Silenced&&SkillStep==BossSkillStep.Telegraph)
   {
    RecoverSkill();G.Record("boss_silence_interrupt","telegraph cancelled; original recovery; execution is never revoked");
   }
   if(SkillStep==BossSkillStep.Ready||SkillStep==BossSkillStep.Recover)
   {
    if(!allowNewAction||Owner.Silenced||Time.time<nextSkillAt)return false;
    if(!BeginNextSpatialSkill()){nextSkillAt=Time.time+1f;return false;}
   }
   StopMotion();skillTime-=dt;
   if(SkillStep==BossSkillStep.Telegraph)
   {
    if(skillTime>0)return true;
    SkillStep=BossSkillStep.Execute;SpatialCasts++;skillAge=0;pulseAt=0;
    skillTime=CurrentCast.executionSeconds;
    if(Skill==BossSkill.AdvantageSwap||Skill==BossSkill.SwapElbow)
    {
     if(!ExchangeTarget()){G.Record("boss_swap_cancel","target moved or endpoint obstructed");RecoverSkill();return true;}
     Warning=Skill==BossSkill.SwapElbow?"肘击将至 · 离开近身范围":"位置已交换 · 继续压制核心";
    }
    G.Record("boss_skill_execute",Skill.ToString());
   }
   skillAge+=dt;
   Vector3 player=G.player.transform.position;
   if(Skill==BossSkill.Attraction)
   {
    // The control zone is dangerous because rifle fire commits to that world position.
    // Swapping out breaks both the pull and the locked firing line, without disabling shooting.
    if(skillAge>=pulseAt){pulseAt+=CurrentCast.pulseInterval;SpatialPulses++;SpatialProjectiles++;Owner.ShootAt(skillCenter+Vector3.up*1.2f,CurrentCast.projectileDamage,CurrentCast.projectileSpeed);}
    Vector3 delta=Vector3.ProjectOnPlane(skillCenter-player,Vector3.up);
    if(delta.magnitude<CurrentCast.radius&&Mathf.Abs(player.y-groundY)<2.5f)
    {
     // Swept movement obeys scene collisions; swapping outside the world-fixed field breaks control.
     if(G.player.controller.enabled)G.player.controller.Move(delta.normalized*Mathf.Min(delta.magnitude,CurrentCast.pullSpeed*dt));
     if(skillTime<=0&&!Physics.Linecast(skillCenter+Vector3.up,G.player.Eye.position,Layers.WorldMask))G.player.Hurt(CurrentCast.damage,skillCenter+Vector3.up);
    }
   }
   else if(Skill==BossSkill.SweepLaser)
   {
    float a=-6+12*Mathf.Clamp01((skillAge-dt)/CurrentCast.executionSeconds),b=-6+12*Mathf.Clamp01(skillAge/CurrentCast.executionSeconds);
    Vector3 center=skillCenter+skillForward*b+Vector3.up*.35f,side=Vector3.Cross(Vector3.up,skillForward);
    if(zone){zone.SetPosition(0,center-side*CurrentCast.halfWidth);zone.SetPosition(1,center+side*CurrentCast.halfWidth);zone.startWidth=zone.endWidth=.23f;}
    Vector3 offset=player-skillCenter;float along=Vector3.Dot(offset,skillForward);
    if(!skillHit&&along>=a-.4f&&along<=b+.4f&&Mathf.Abs(Vector3.Dot(offset,side))<CurrentCast.halfWidth+.3f&&player.y<groundY+1.2f&&player.y>groundY-1.8f)
    {skillHit=true;G.player.Hurt(CurrentCast.damage,center+Vector3.up*.3f,kind:PlayerDamageKind.Spatial);}
   }
   else if(Skill==BossSkill.GroundWave)
   {
    float radius=skillAge*CurrentCast.waveSpeed,previous=Mathf.Max(0,(skillAge-dt)*CurrentCast.waveSpeed);DrawRing(zone,skillCenter,radius);
    float distance=Vector3.ProjectOnPlane(player-skillCenter,Vector3.up).magnitude;
    if(!skillHit&&distance>=previous-.45f&&distance<=radius+.45f&&player.y<groundY+1.15f&&player.y>groundY-1.8f)
    {skillHit=true;G.player.Hurt(CurrentCast.damage,skillCenter,shieldBlockable:false,kind:PlayerDamageKind.Spatial);}
   }
   else if(Skill==BossSkill.OmniFire&&skillAge>=pulseAt)
   {
    pulseAt+=CurrentCast.pulseInterval;SpatialPulses++;Vector3 source=Owner.AimPoint+Vector3.up*.25f;
    for(int i=0;i<24;i++){float angle=(i*15+skillAge*12)*Mathf.Deg2Rad;Bolt.Spawn(Owner,source,new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle)),CurrentCast.projectileSpeed,CurrentCast.projectileDamage,EnemyProjectileStyle.OmniRing);SpatialProjectiles++;}
    // The coherent aimed stream keeps A-shield and cover meaningful at any azimuth.
    Bolt.Spawn(Owner,source,G.player.Eye.position-source,CurrentCast.aimedSpeed,CurrentCast.aimedDamage,EnemyProjectileStyle.OmniAimed);SpatialProjectiles++;G.sound.Play("rifle",source);
   }
   else if(Skill==BossSkill.SwapElbow&&skillTime<=0)
   {
    if(Vector3.Distance(player,transform.position)<CurrentCast.radius&&!Physics.Linecast(Owner.AimPoint,G.player.Eye.position,Layers.WorldMask))G.player.Hurt(CurrentCast.damage,Owner.AimPoint);
    Shapes.Burst(Owner.AimPoint,Danger,1);
   }
   if(skillTime<=0)RecoverSkill();return true;
  }
  bool ExchangeTarget()
  {
   if(!ValidExchange(skillTarget)||Vector3.Distance(skillTarget.transform.position,skillDestination)>.5f)return false;
   Vector3 old=transform.position,destination=skillTarget.transform.position;
   var colliders=skillTarget.GetComponentsInChildren<Collider>();var enabled=new bool[colliders.Length];
   for(int i=0;i<colliders.Length;i++){enabled[i]=colliders[i].enabled;colliders[i].enabled=false;}
   bool success=false;
   try
   {
    if(Owner.MoveTo(destination)&&skillTarget.MoveTo(old))success=true;
    else {Owner.MoveTo(old);skillTarget.MoveTo(destination);}
   }
   finally {for(int i=0;i<colliders.Length;i++)if(colliders[i])colliders[i].enabled=enabled[i];Physics.SyncTransforms();}
   if(!success)return false;
   skillTarget.AfterSwap();SpatialSwaps++;Shapes.Echo(old);Shapes.Echo(destination);G.sound.Play("phase");
   G.Record("boss_swap_endpoints","boss="+old+" -> "+transform.position+"; target="+destination+" -> "+skillTarget.transform.position);return true;
  }
  static void DrawRing(LineRenderer line,Vector3 center,float radius)
  {if(!line)return;for(int i=0;i<line.positionCount;i++){float a=i*Mathf.PI*2/(line.positionCount-1);line.SetPosition(i,center+new Vector3(Mathf.Cos(a),0,Mathf.Sin(a))*radius);}}
  void RecoverSkill()
  {SkillStep=BossSkillStep.Recover;Warning="";ClearSpatialVisuals();nextSkillAt=Time.time+CurrentCast.recoverySeconds;attackAt=Time.time+.65f;G.Record("boss_skill_recovery",Skill.ToString());}
  void ClearSpatialVisuals(){if(zone)Destroy(zone.gameObject);if(pathPreview)Destroy(pathPreview.gameObject);zone=pathPreview=null;}
  void CancelSpatialSkill(){ClearSpatialVisuals();CurrentCast=null;Skill=BossSkill.None;SkillStep=BossSkillStep.Ready;nextSkillAt=Time.time+.75f;}
 }
}
