using UnityEngine;
namespace SwapHunter
{
 public enum BossPhase { EnergyMelee, Rifle, Combined, Defeated }
 // Continuous life layers and basic attacks; spatial skills share this state in BossSpatialSkills.
 public sealed partial class BossEncounter:MonoBehaviour
 {
  public const float ShieldMaximum=360,HealthMaximum=720,SilenceResistanceDuration=4.5f;
  public EnemyActor Owner {get;private set;}
  public BossPhase Phase {get;private set;}
  public float Energy {get;private set;}
  public int PhaseTransitions {get;private set;}
  public string Warning {get;private set;}="";
  float attackAt,windup,meleeSwing,silenceResistantUntil;Vector3 meleePoint;LineRenderer telegraph;int burst;
  public float SilenceResistanceLeft=>Owner&&Owner.Silenced?0:Mathf.Max(0,silenceResistantUntil-Time.time);
  public int SilenceAcceptedCount {get;private set;}
  public int SilenceRejectedCount {get;private set;}
  public bool TryReceiveSilence(float seconds)
  {
   if(seconds<=0)return false;
   if(Owner.Silenced||Time.time<silenceResistantUntil){SilenceRejectedCount++;return false;}
   // Covers the normal 1.5s warning + 2.7s ground wave; swap-CD extensions remain interruptible.
   // Repeated hits cannot extend the current silence or the following resistance window.
   SilenceAcceptedCount++;silenceResistantUntil=Time.time+seconds+SilenceResistanceDuration;
   return true;
  }
  DemoGame G=>DemoGame.I;
  public void Initialize(EnemyActor owner)
  {
   Owner=owner;Energy=ShieldMaximum;Phase=BossPhase.EnergyMelee;Owner.maximumHealth=HealthMaximum;Owner.health=HealthMaximum;attackAt=Time.time+1.5f;CancelSpatialSkill();
   Owner.InitializeBossPresentation();
   G.Record("boss_begin","Energy Shield 360 -> HP 720");
  }
  public float Absorb(float damage)
  {
   if(Energy<=0)return damage;
   float absorbed=Mathf.Min(Energy,Mathf.Max(0,damage));Energy-=absorbed;
   if(absorbed>0)Shapes.Burst(Owner.AimPoint,new Color(.1f,.8f,1),.4f);
   if(Energy<=0)Change(BossPhase.Rifle);
   return Mathf.Max(0,damage-absorbed);
  }
  public void RefreshPhase()
  {
   if(Owner.health<=0){Change(BossPhase.Defeated);return;}
   if(Energy<=0&&Owner.health<=HealthMaximum*.5f&&Phase==BossPhase.Rifle)Change(BossPhase.Combined);
  }
  void Change(BossPhase next)
  {
   if(Phase==next)return;
   bool committedMelee=windup>0&&next!=BossPhase.Defeated;
   Phase=next;PhaseTransitions++;if(!committedMelee)ClearWarning();CancelSpatialSkill();
   skillSequence=0;openingSkillPending=true;nextSkillAt=Time.time+.15f;burst=0;attackAt=Time.time+.9f;
   Owner.UpdateBossPresentation(next==BossPhase.EnergyMelee||committedMelee,committedMelee?1-windup/.75f:0,0,G.player.Eye.position);
   G.Record("boss_phase",next.ToString());
   if(next!=BossPhase.Defeated){G.sound.Play("warning",transform.position);G.Toast(next==BossPhase.Rifle?"能量盾击破 · 指挥官切换步枪形态":"核心失稳 · 观察攻击预警并选择反制",3);}
  }
  public void Tick(float dt)
  {
   RefreshPhase();if(Phase==BossPhase.Defeated)return;
   meleeSwing=Mathf.Max(0,meleeSwing-dt);float elbowSwing=Skill==BossSkill.SwapElbow&&SkillStep==BossSkillStep.Execute&&SkillSeconds<.22f?1-SkillSeconds/.22f:0;
   Vector3 visualAim=Skill==BossSkill.Attraction&&(SkillStep==BossSkillStep.Telegraph||SkillStep==BossSkillStep.Execute)?DangerCenter+Vector3.up*1.2f:G.player.Eye.position;
   Vector3 facing=Vector3.ProjectOnPlane(visualAim-Owner.AimPoint,Vector3.up);
   if(facing.sqrMagnitude>.01f)transform.rotation=Quaternion.RotateTowards(transform.rotation,Quaternion.LookRotation(facing),110*dt);
   Owner.UpdateBossPresentation(Phase==BossPhase.EnergyMelee||windup>0||meleeSwing>0,windup>0?1-windup/.75f:0,Mathf.Max(meleeSwing/.22f,elbowSwing),visualAim);
   if(windup<=0&&TickSpatialSkills(dt))return;
   Vector3 p=G.player.transform.position;Vector3 d=Vector3.ProjectOnPlane(p-transform.position,Vector3.up);
   bool melee=Phase==BossPhase.EnergyMelee;
   if(Owner.agent.enabled&&Owner.agent.isOnNavMesh)
   {
    Owner.agent.stoppingDistance=melee?2.3f:11;Owner.agent.speed=melee?3.6f:2.6f;
    Owner.agent.isStopped=windup>0; if(windup<=0&&d.magnitude>Owner.agent.stoppingDistance+.2f)Owner.agent.SetDestination(p);else if(windup<=0)Owner.agent.ResetPath();
   }
   if(windup>0)
   {
    windup-=dt;
    if(windup<=0)
    {
     if(Vector3.Distance(G.player.transform.position,transform.position)<3.1f&&!Physics.Linecast(Owner.AimPoint,G.player.Eye.position,Layers.WorldMask))G.player.Hurt(24,Owner.AimPoint);
     meleeSwing=.22f;G.sound.Play("knife_swipe",Owner.AimPoint);
     Shapes.Burst(meleePoint+Vector3.up*.5f,new Color(1,.45f,.1f),1);ClearWarning();attackAt=Time.time+1.4f;
    }
    return;
   }
   if(Time.time<attackAt)return;
   if(melee)
   {
    if(d.magnitude>2.9f)return;windup=.75f;meleePoint=G.player.transform.position;Warning="近战压迫 · 拉开距离或换位";
    telegraph=Shapes.Ring(G.effects,transform.position+Vector3.up*.08f,3,new Color(1,.35f,.12f),.08f);G.sound.Play("warning",transform.position);
   }
   else if(Vector3.Dot(transform.forward,d.normalized)>.85f&&!Physics.Linecast(Owner.AimPoint,G.player.Eye.position,Layers.WorldMask))
   {
    Owner.ShootAt(G.player.Eye.position,8,38);burst++;attackAt=Time.time+(burst>=3?1.7f:.25f);if(burst>=3)burst=0;
   }
  }
  void ClearWarning(){windup=0;Warning="";if(telegraph)Destroy(telegraph.gameObject);telegraph=null;}
  void OnDisable(){ClearWarning();CancelSpatialSkill();}
 }
}
