using UnityEngine;
using UnityEngine.AI;
namespace SwapHunter
{
 public sealed class EnemyV2Ability:MonoBehaviour
 {
  EnemyActor owner;DemoGame G=>DemoGame.I;float clock,total,nextAt,executeClock,shotAt;Vector3 locked,start,direction;LineRenderer warning,beam;EnemyV2Device equipment,repairTarget;DirectionalBarrier deployed;bool scripted;
  public EnemyAbilityPhase Phase{get;private set;}
  public string AbilityName{get;private set;}="";
  public float Progress=>total<=0?0:1-Mathf.Clamp01(clock/total);
  public bool LockFacing=>Phase==EnemyAbilityPhase.Telegraph||Phase==EnemyAbilityPhase.Execute;
  public void Initialize(EnemyActor actor)
  {
   owner=actor;nextAt=Time.time+2+(Mathf.Abs(GetInstanceID())%13)*.13f;
   if(owner.kind==EnemyKind.Elite||owner.kind==EnemyKind.BarrierGunner||owner.kind==EnemyKind.Engineer||owner.kind==EnemyKind.Leaper||owner.kind==EnemyKind.Breacher||owner.kind==EnemyKind.Tether||owner.kind==EnemyKind.Suppressor||owner.kind==EnemyKind.ScoutDrone)
   {var mount=owner.VisualRig.Weakpoint("equipment");Vector3 p=mount?mount.position:owner.AimPoint-transform.forward*.4f;equipment=EnemyV2Device.Create(mount?mount:transform,p,owner.kind==EnemyKind.Elite?"通信模块":"技能组件",owner.kind==EnemyKind.Elite?240:144,owner.kind==EnemyKind.BarrierGunner||owner.kind==EnemyKind.Suppressor,owner,()=>Interrupt("component_destroyed"));}
  }
  public void NotifyHurt(float damage){if(owner.kind==EnemyKind.Engineer&&damage>=72)Interrupt("heavy_hit");if(owner.kind==EnemyKind.Tether&&damage>=72)EnemyV2Tether.BreakOwner(owner);}
  public void Interrupt(string reason)
  {
   if(owner)EnemyV2Tether.BreakOwner(owner);
   if(scripted){owner.motion.EndScripted();scripted=false;}
   if(Phase!=EnemyAbilityPhase.Ready){Clear();Phase=EnemyAbilityPhase.Recover;clock=total=.55f;nextAt=Mathf.Max(nextAt,Time.time+2);if(owner)G.Record("enemy_skill_interrupted",owner.kind+":"+reason);}
   if(owner)EnemySkillReservations.Release(owner);
  }
  void Clear(){if(warning)Destroy(warning.gameObject);if(beam)Destroy(beam.gameObject);if(owner&&owner.VisualRig)owner.VisualRig.SetAbilityPresentation(AbilityName,1,false);}
  void Stop(){if(owner.agent.enabled&&owner.agent.isOnNavMesh){owner.agent.isStopped=true;owner.agent.ResetPath();}}
  bool Ready=>owner.tactical&&!owner.tactical.IsPreparingGrenade&&!owner.Silenced&&(!equipment||equipment.Alive)&&Time.time>=nextAt&&owner.tactical.HasPlayerMemory;
  bool Begin(string name,float warningTime,float cooldown,string channel="pressure",int capacity=1)
  {
   if(!EnemySkillReservations.TryClaim(owner,channel,warningTime+1.5f,capacity))return false;
   AbilityName=name;clock=total=warningTime;Phase=EnemyAbilityPhase.Telegraph;nextAt=Time.time+cooldown;start=transform.position;locked=owner.tactical.LastKnownFeet;direction=Vector3.ProjectOnPlane(locked-start,Vector3.up).normalized;
   if(direction.sqrMagnitude<.1f)direction=transform.forward;
   warning=Shapes.Ring(G.effects,locked+Vector3.up*.06f,owner.kind==EnemyKind.Mortar?3:1.3f,new Color(1,.6f,.15f),.045f);G.sound.Play("warning",owner.AimPoint);G.Record("enemy_skill_warning",owner.kind+":"+name);Stop();return true;
  }
  public bool Tick(float dt)
  {
   if(!owner||!owner.Alive)return false;
   if((Phase==EnemyAbilityPhase.Telegraph||Phase==EnemyAbilityPhase.Execute)&&owner.tactical.EvadingKnownDanger){Interrupt("known_hazard");return false;}
   if(owner.Silenced&&Phase==EnemyAbilityPhase.Telegraph)Interrupt("silence");
   if(Phase==EnemyAbilityPhase.Recover){Stop();clock-=dt;owner.VisualRig.SetAbilityPresentation(AbilityName,Progress,true);if(clock<=0){Phase=EnemyAbilityPhase.Ready;Clear();}return true;}
   if(Phase==EnemyAbilityPhase.Telegraph)
   {Stop();clock-=dt;owner.VisualRig.SetAbilityPresentation(AbilityName,Progress,true);if(owner.kind==EnemyKind.ScoutDrone&&!owner.tactical.SeesPlayer){Interrupt("lost_observation");return true;}if(clock<=0)Execute();return true;}
   if(Phase==EnemyAbilityPhase.Execute){TickExecute(dt);return true;}
   if(!Ready)return false;float distance=Vector3.Distance(transform.position,owner.tactical.LastKnownFeet);
   if(owner.kind==EnemyKind.Engineer)return TryRepair();
   if(!owner.tactical.SeesPlayer)return false;
   // Let the tactical controller finish its flank instead of resetting its path
   // and cover selection on every blocked ability tick.
   if(DirectionalBarrier.BlocksSegment(owner.MuzzlePosition,owner.tactical.PlayerTorso)&&owner.kind!=EnemyKind.Mortar&&owner.kind!=EnemyKind.Breacher)return false;
   switch(owner.kind)
   {
    case EnemyKind.Elite:return Begin("侧移压制",.7f,8,"command");
    case EnemyKind.BarrierGunner:
     if(distance<10||distance>35||deployed)return false;
     if(!EnemyMotion.ClearVolume(transform.position-transform.forward,owner.Radius,owner.Height,transform))return false;
     return Begin("展开屏障",.8f,12,"barrier",2);
    case EnemyKind.Leaper:
     if(distance<3||distance>12)return false;
     if(!ReachableLanding(owner.tactical.LastKnownFeet,8,4,out var leap))return false;
     if(!Begin("跃袭",.65f,6,"leap",2))return false;locked=leap;return true;
    case EnemyKind.Breacher:if(distance<4||distance>16)return false;return Begin("破障冲刺",.9f,8);
    case EnemyKind.Mortar:
     if(distance<18||distance>35||!EnemyV2MortarShell.ArcClear(owner.AimPoint+Vector3.up*.4f,owner.tactical.LastKnownFeet))return false;
     return Begin("曲射榴弹",.8f,6);
    case EnemyKind.ScoutDrone:return Begin("镜头标记",.8f,8,"observation");
    case EnemyKind.Jammer:if(!EnemyV2DynamicField.CanDeploy(transform.position))return false;return Begin("部署反相信标",1,12,"control");
    case EnemyKind.Tether:if(distance<10||distance>22)return false;return Begin("投索",.8f,9,"control");
    case EnemyKind.Suppressor:if(distance<8)return false;return Begin("三足架设",1,5.2f,"suppress");
   }return false;
  }
  bool ReachableLanding(Vector3 requested,float maxDistance,float maxHeight,out Vector3 point)
  {
   point=requested;Vector3 delta=Vector3.ProjectOnPlane(requested-transform.position,Vector3.up);if(delta.magnitude>maxDistance)requested=transform.position+delta.normalized*maxDistance;
   if(!NavMesh.SamplePosition(requested,out var hit,1.1f,NavMesh.AllAreas)||Mathf.Abs(hit.position.y-transform.position.y)>maxHeight)return false;
   point=hit.position;if(!EnemyMotion.ClearVolume(point,owner.Radius,owner.Height,transform))return false;
   var territory=owner.GetComponent<MissionEncounterAgent>();if(territory&&!territory.Allows(point))return false;
   return EnemyV2MortarShell.ArcClear(transform.position+Vector3.up*.3f,point+Vector3.up*.3f);
  }
  bool TryRepair()
  {
   repairTarget=null;float best=20;
   foreach(var d in EnemyV2Device.ActiveSnapshot())
   {if(!d||!d.Alive||!d.Repairable||d.Repaired||d.Health>=d.MaximumHealth||d.Carrier==owner)continue;float distance=Vector3.Distance(owner.AimPoint,d.AimPoint);if(distance<best&&!Physics.Linecast(owner.AimPoint,d.AimPoint,Layers.WorldMask)){best=distance;repairTarget=d;}}
   if(!repairTarget)return false;
   if(best>3)
   {if(owner.agent.enabled&&owner.agent.isOnNavMesh&&NavMesh.SamplePosition(repairTarget.transform.position,out var n,2,NavMesh.AllAreas)){var path=new NavMeshPath();if(owner.agent.CalculatePath(n.position,path)&&path.status==NavMeshPathStatus.PathComplete){owner.agent.isStopped=false;owner.agent.stoppingDistance=2.5f;owner.agent.SetPath(path);return true;}}return false;}
   return Begin("现场维修",.7f,10,"repair");
  }
  void Execute()
  {
   if(warning)Destroy(warning.gameObject);Phase=EnemyAbilityPhase.Execute;executeClock=0;start=transform.position;shotAt=0;
   G.Record("enemy_skill_execute",owner.kind+":"+AbilityName);
   switch(owner.kind)
   {
    case EnemyKind.Elite:
     int count=0;foreach(var e in G.enemies)if(e&&e!=owner&&e.Alive&&e.tactical&&Vector3.Distance(e.transform.position,transform.position)<22){e.tactical.RequestReposition();if(++count>=2)break;}Recover(.6f);break;
    case EnemyKind.BarrierGunner:deployed=DirectionalBarrier.Spawn(owner.AimPoint+direction*1.3f,direction,6,transform);EnemySkillReservations.Hold(owner,6);Recover(.5f);break;
    case EnemyKind.Leaper:case EnemyKind.Breacher:owner.motion.BeginScripted();scripted=true;break;
    case EnemyKind.Mortar:EnemyV2MortarShell.Spawn(owner,locked);EnemySkillReservations.Hold(owner,1.6f);Recover(.6f);break;
    case EnemyKind.ScoutDrone:
     foreach(var e in G.enemies)if(e&&e.Alive&&e.tactical&&Vector3.Distance(e.transform.position,transform.position)<25)e.tactical.ShareObservation(locked,4);
     beam=Shapes.Line(G.effects,owner.AimPoint,locked+Vector3.up,new Color(1,.5f,.1f),.035f);G.Toast("被侦察机标记 · 已共享最后观察位置",2);Recover(4);break;
    case EnemyKind.Jammer:EnemyV2DynamicField.Spawn(transform.position);EnemySkillReservations.Hold(owner,4);Recover(.5f);break;
    case EnemyKind.Tether:EnemyV2Tether.Fire(owner,locked+Vector3.up*.9f);EnemySkillReservations.Hold(owner,3);Recover(.5f);break;
    case EnemyKind.Engineer:if(!repairTarget){Recover(.5f);break;}beam=Shapes.Line(G.effects,owner.AimPoint,repairTarget.AimPoint,Color.cyan,.04f);break;
    case EnemyKind.Suppressor:break;
   }
  }
  void TickExecute(float dt)
  {
   executeClock+=dt;owner.VisualRig.SetAbilityPresentation(AbilityName,Mathf.Clamp01(executeClock),true);
   if(owner.kind==EnemyKind.Leaper)
   {float t=Mathf.Clamp01(executeClock/.6f);Vector3 next=Vector3.Lerp(start,locked,t)+Vector3.up*Mathf.Sin(t*Mathf.PI)*2;owner.motion.Push(next-transform.position);if(t>=1){owner.motion.EndScripted();scripted=false;if(Vector3.Distance(G.player.transform.position,transform.position)<2&&!Physics.Linecast(owner.AimPoint,G.player.Eye.position,Layers.WorldMask))G.player.Hurt(16,owner.AimPoint,false);Recover(.45f);}return;}
   if(owner.kind==EnemyKind.Breacher)
   {owner.motion.Push(direction*(7/.6f)*dt);MapV2WorldActions.Try(G.expeditionLevel,"breach",transform.position,direction);if(Vector3.Distance(G.player.transform.position,transform.position)<1.5f){G.player.Hurt(20,owner.AimPoint,false);executeClock=.6f;}if(executeClock>=.6f){owner.motion.EndScripted();scripted=false;Recover(1);}return;}
   if(owner.kind==EnemyKind.Engineer)
   {Stop();if(!repairTarget||!repairTarget.Alive||Vector3.Distance(owner.AimPoint,repairTarget.AimPoint)>3.4f||Physics.Linecast(owner.AimPoint,repairTarget.AimPoint,Layers.WorldMask)){Interrupt("repair_link_broken");return;}if(beam){beam.SetPosition(0,owner.AimPoint);beam.SetPosition(1,repairTarget.AimPoint);}repairTarget.Repair(repairTarget.MaximumHealth*.04f*dt);if(executeClock>=3){repairTarget.FinishRepair();Recover(.6f);}return;}
   if(owner.kind==EnemyKind.Suppressor)
   {Stop();Vector3 to=owner.tactical.PlayerTorso-owner.AimPoint;if(owner.tactical.SeesPlayer&&Vector3.Angle(direction,Vector3.ProjectOnPlane(to,Vector3.up))<=45&&executeClock>=shotAt){shotAt=executeClock+.18f;owner.ShootAt(owner.tactical.PlayerTorso+Random.insideUnitSphere*.4f,5,65);}if(executeClock>=2.4f)Recover(1.8f);return;}
   Recover(.6f);
  }
  void Recover(float seconds){Phase=EnemyAbilityPhase.Recover;clock=total=seconds;}
  void OnDisable(){if(scripted&&owner)owner.motion.EndScripted();Clear();if(equipment)Destroy(equipment.gameObject);if(owner)EnemySkillReservations.Release(owner);}
 }
}
