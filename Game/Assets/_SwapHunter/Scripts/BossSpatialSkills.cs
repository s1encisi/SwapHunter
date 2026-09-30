using UnityEngine;
namespace SwapHunter
{
 public enum BossSkill { None, SweepLaser, Attraction, AdvantageSwap, GroundWave, SwapElbow, OmniFire }
 public enum BossSkillStep { Ready, Telegraph, Execute, Recover }
 public sealed partial class BossEncounter
 {
  public BossSkill Skill {get;private set;}
  public BossSkillStep SkillStep {get;private set;}
  public int SpatialCasts {get;private set;}
  public int SpatialSwaps {get;private set;}
  public Vector3 DangerCenter=>skillCenter;
  public SwapTarget SpatialTarget=>skillTarget;
  public float SkillSeconds=>skillTime;
  float skillTime,nextSkillAt,skillAge,pulseAt,groundY;
  int skillSequence;bool skillHit,openingSkillPending=true;
  static readonly BossSkill[] RifleSkills={BossSkill.Attraction,BossSkill.AdvantageSwap};
  static readonly BossSkill[] CombinedSkills={BossSkill.OmniFire,BossSkill.GroundWave,BossSkill.SweepLaser,BossSkill.Attraction,BossSkill.AdvantageSwap};
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
  SwapTarget ChooseTarget(bool elbow)
  {
   SwapTarget best=null;float score=float.NegativeInfinity;
   foreach(var t in G.anchors)Consider(t);
   foreach(var t in G.enemies)Consider(t);
   return best;
   void Consider(SwapTarget t)
   {
    if(!ValidExchange(t))return;
    float playerDistance=Vector3.Distance(t.transform.position,G.player.transform.position);
    float travel=Vector3.Distance(t.transform.position,transform.position);
    if(travel<4||elbow&&playerDistance>4.2f)return;
    float s=elbow?-playerDistance:t.transform.position.y*4-Mathf.Abs(playerDistance-12)+travel*.1f;
    if(s>score){score=s;best=t;}
   }
  }
  public bool TryBeginSkill(BossSkill skill)
  {
   if(Owner.Silenced||!SkillAllowed(skill)||SkillStep==BossSkillStep.Telegraph||SkillStep==BossSkillStep.Execute||!G.IsPlaying)return false;
   skillTarget=null;
   if(skill==BossSkill.AdvantageSwap||skill==BossSkill.SwapElbow)
   {skillTarget=ChooseTarget(skill==BossSkill.SwapElbow);if(!skillTarget)return false;skillDestination=skillTarget.transform.position;}
   ClearWarning();ClearSpatialVisuals();Skill=skill;SkillStep=BossSkillStep.Telegraph;skillAge=0;skillHit=false;
   skillCenter=G.player.transform.position;groundY=skillCenter.y;
   if(Physics.Raycast(skillCenter+Vector3.up*.2f,Vector3.down,out var floor,30,Layers.WorldMask))groundY=floor.point.y;
   if(skill==BossSkill.SweepLaser||skill==BossSkill.GroundWave)
   {
    // Ground attacks remain on the arena floor even when the player already holds a tower.
    groundY=GroundThreatFloor();
   }
   skillCenter.y=groundY+.06f;
   skillForward=Vector3.ProjectOnPlane(skillCenter-transform.position,Vector3.up).normalized;
   if(skillForward.sqrMagnitude<.1f)skillForward=Vector3.forward;
   skillTime=(skill==BossSkill.SweepLaser||skill==BossSkill.Attraction)?Mathf.Max(1.5f,G.player.cooldown+.45f):skill==BossSkill.OmniFire?1.2f:1.5f;
   switch(skill)
   {
    case BossSkill.SweepLaser:Warning="扫地激光 · 危险带已锁定，换位离开";zone=Shapes.Line(G.effects,skillCenter-Vector3.Cross(Vector3.up,skillForward)*15,skillCenter+Vector3.Cross(Vector3.up,skillForward)*15,Danger,.12f);break;
    case BossSkill.Attraction:Warning="牵引场 · 换位离开橙色区域";zone=Shapes.Ring(G.effects,skillCenter,4.8f,Danger,.09f);break;
    case BossSkill.GroundWave:Warning="撼地波 · 换至高点或大跳，A盾无效";skillCenter=new Vector3(transform.position.x,groundY+.06f,transform.position.z);zone=Shapes.Ring(G.effects,skillCenter,2,Danger,.12f);break;
    case BossSkill.OmniFire:Warning="全向火力 · A盾朝向指挥官，或进入掩体";zone=Shapes.Ring(G.effects,transform.position+Vector3.up*.1f,2.4f,Danger,.1f);break;
    default:Warning=skill==BossSkill.SwapElbow?"换位肘击 · 离开标记目标附近":"指挥官即将换位 · 观察橙色落点";zone=Shapes.Ring(G.effects,skillDestination+Vector3.up*.08f,3.4f,Danger,.09f);pathPreview=Shapes.Line(G.effects,Owner.AimPoint,skillTarget.AimPoint,Danger,.06f);break;
   }
   StopMotion();G.sound.Play("warning",transform.position);G.Record("boss_telegraph",skill+" center="+skillCenter+" seconds="+skillTime);return true;
  }
  float GroundThreatFloor()
  {
   if(G.expeditionActive)return 0;
   Vector3 player=G.player.transform.position;float floorY=player.y;
   if(Physics.Raycast(player+Vector3.up*.2f,Vector3.down,out var floor,30,Layers.WorldMask))floorY=floor.point.y;
   for(int k=0;k<8;k++)
   {
    float angle=k*Mathf.PI/4;Vector3 sample=transform.position+new Vector3(Mathf.Cos(angle)*8,12,Mathf.Sin(angle)*8);
    if(Physics.Raycast(sample,Vector3.down,out var baseFloor,40,Layers.WorldMask)&&baseFloor.normal.y>.7f)floorY=Mathf.Min(floorY,baseFloor.point.y);
   }
   return floorY;
  }
  bool PlayerAboveGroundThreats()=>G.player.transform.position.y>GroundThreatFloor()+1.2f;
  BossSkill OpeningSkill()
  {
   if(Phase==BossPhase.EnergyMelee)return BossSkill.SwapElbow;
   bool high=PlayerAboveGroundThreats();
   if(Phase==BossPhase.Rifle)return high?BossSkill.AdvantageSwap:BossSkill.Attraction;
   if(high)return BossSkill.OmniFire;
   float distance=Vector3.ProjectOnPlane(G.player.transform.position-transform.position,Vector3.up).magnitude;
   return distance<=12?BossSkill.GroundWave:BossSkill.SweepLaser;
  }
  bool BeginNextSpatialSkill()
  {
   BossSkill[] rotation=Phase==BossPhase.Rifle?RifleSkills:CombinedSkills;
   BossSkill selected=Phase==BossPhase.EnergyMelee?BossSkill.SwapElbow:openingSkillPending?OpeningSkill():rotation[skillSequence%rotation.Length];
   openingSkillPending=false;
   if(!TryBeginSkill(selected))
   {
    if(selected!=BossSkill.AdvantageSwap)return false;
    BossSkill fallback=Phase==BossPhase.Combined&&PlayerAboveGroundThreats()?BossSkill.OmniFire:BossSkill.Attraction;
    G.Record("boss_skill_fallback",selected+" unavailable -> "+fallback);
    if(!TryBeginSkill(fallback))return false;
    selected=fallback;
   }
   if(Phase!=BossPhase.EnergyMelee)skillSequence=(System.Array.IndexOf(rotation,selected)+1)%rotation.Length;
   return true;
  }
  void StopMotion(){if(Owner.agent.enabled&&Owner.agent.isOnNavMesh){Owner.agent.ResetPath();Owner.agent.isStopped=true;}}
  bool TickSpatialSkills(float dt)
  {
   if(Owner.Silenced)
   {
    if(SkillStep==BossSkillStep.Telegraph||SkillStep==BossSkillStep.Execute){CancelSpatialSkill();ClearWarning();attackAt=Time.time+.4f;G.Record("boss_silence_interrupt","special ability interrupted; basic attacks remain");}
    return false;
   }
   if(SkillStep==BossSkillStep.Ready||SkillStep==BossSkillStep.Recover)
   {
    if(Time.time<nextSkillAt)return false;
    if(!BeginNextSpatialSkill()){nextSkillAt=Time.time+1.5f;return false;}
   }
   StopMotion();skillTime-=dt;
   if(SkillStep==BossSkillStep.Telegraph)
   {
    if(skillTime>0)return true;
    SkillStep=BossSkillStep.Execute;SpatialCasts++;skillAge=0;pulseAt=0;
    skillTime=Skill==BossSkill.SweepLaser?1.8f:Skill==BossSkill.GroundWave?2.7f:Skill==BossSkill.OmniFire?2:Skill==BossSkill.Attraction?2.3f:.6f;
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
    if(skillAge>=pulseAt){pulseAt+=.35f;Owner.ShootAt(skillCenter+Vector3.up*1.2f,8,38);}
    Vector3 delta=Vector3.ProjectOnPlane(skillCenter-player,Vector3.up);
    if(delta.magnitude<4.8f&&Mathf.Abs(player.y-groundY)<2.5f)
    {
     // Swept movement obeys scene collisions; swapping outside the world-fixed field breaks control.
     if(G.player.controller.enabled)G.player.controller.Move(delta.normalized*Mathf.Min(delta.magnitude,5.5f*dt));
     if(skillTime<=0&&!Physics.Linecast(skillCenter+Vector3.up,G.player.Eye.position,Layers.WorldMask))G.player.Hurt(24,skillCenter+Vector3.up);
    }
   }
   else if(Skill==BossSkill.SweepLaser)
   {
    float a=-6+12*Mathf.Clamp01((skillAge-dt)/1.8f),b=-6+12*Mathf.Clamp01(skillAge/1.8f);
    Vector3 center=skillCenter+skillForward*b+Vector3.up*.35f,side=Vector3.Cross(Vector3.up,skillForward);
    if(zone){zone.SetPosition(0,center-side*15);zone.SetPosition(1,center+side*15);zone.startWidth=zone.endWidth=.23f;}
    Vector3 offset=player-skillCenter;float along=Vector3.Dot(offset,skillForward);
    if(!skillHit&&along>=a-.4f&&along<=b+.4f&&Mathf.Abs(Vector3.Dot(offset,side))<15.3f&&player.y<groundY+1.2f&&player.y>groundY-1.8f)
    {skillHit=true;G.player.Hurt(28,center+Vector3.up*.3f);}
   }
   else if(Skill==BossSkill.GroundWave)
   {
    float radius=skillAge*12,previous=Mathf.Max(0,(skillAge-dt)*12);DrawRing(zone,skillCenter,radius);
    float distance=Vector3.ProjectOnPlane(player-skillCenter,Vector3.up).magnitude;
    if(!skillHit&&distance>=previous-.45f&&distance<=radius+.45f&&player.y<groundY+1.15f&&player.y>groundY-1.8f)
    {skillHit=true;G.player.Hurt(26,skillCenter,shieldBlockable:false);}
   }
   else if(Skill==BossSkill.OmniFire&&skillAge>=pulseAt)
   {
    pulseAt+=.24f;Vector3 source=Owner.AimPoint+Vector3.up*.25f;
    for(int i=0;i<24;i++){float angle=(i*15+skillAge*12)*Mathf.Deg2Rad;Bolt.Spawn(Owner,source,new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle)),23,5);}
    // The coherent aimed stream keeps A-shield and cover meaningful at any azimuth.
    Bolt.Spawn(Owner,source,G.player.Eye.position-source,32,7);G.sound.Play("rifle",source);
   }
   else if(Skill==BossSkill.SwapElbow&&skillTime<=0)
   {
    if(Vector3.Distance(player,transform.position)<3.4f&&!Physics.Linecast(Owner.AimPoint,G.player.Eye.position,Layers.WorldMask))G.player.Hurt(26,Owner.AimPoint);
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
  {SkillStep=BossSkillStep.Recover;Warning="";ClearSpatialVisuals();nextSkillAt=Time.time+(Phase==BossPhase.Combined?2.2f:3.2f);attackAt=Time.time+.65f;G.Record("boss_skill_recovery",Skill.ToString());}
  void ClearSpatialVisuals(){if(zone)Destroy(zone.gameObject);if(pathPreview)Destroy(pathPreview.gameObject);zone=pathPreview=null;}
  void CancelSpatialSkill(){ClearSpatialVisuals();Skill=BossSkill.None;SkillStep=BossSkillStep.Ready;nextSkillAt=Time.time+.75f;}
 }
}
