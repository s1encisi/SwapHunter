using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace SwapHunter
{
 public sealed partial class ReviewFixQA
 {
  [Serializable] public sealed class FlowEvent
  {public float seconds,hp;public string action,detail;public Vector3 feet;}
  [Serializable] public sealed class FlowRun
  {
   public int chapter,seed,difficulty,shots,hits,kills,swaps,shieldBlocks,medicalUsed,ammoPacksUsed,objectives,waves,remainingEnemies;
   public bool began,completed,won,walkedEntrance,entrySwap,bridgeWalked,aiSuppressionObserved;
   public float seconds,gameSeconds,initialHp,finalHp,minHp,walkedMeters,bridgeWalkMeters,moveSpeedScale;
   public int[] initialAmmo,initialReserve,finalAmmo,finalReserve;
   public string result="running",reason="",lastGoal="",lastPath="";
   public Vector3 finalFeet;
   public List<string> bossPhases=new List<string>(),bossSkills=new List<string>(),supplies=new List<string>(),navigationDiagnostics=new List<string>();
   public List<FlowEvent> events=new List<FlowEvent>();
  }
  [Serializable] public sealed class FlowReport
  {
   public string version,kind="One omniscient automated strategy attempt per requested chapter (10 and/or 12); default carbine/shotgun, breach rank0. Before-start unlock receipts are fixtures. During each run: normal spawn, virtual keyboard movement, real weapon/reload/swap/interactions, finite inventory, live AI; no positioning, damage injection, resource refill, cooldown changes or forced Boss phases. Strategy planning failures are not proof of a game soft lock. This is not human playtesting or a player population/balance study.";
   public int requestedChapter;
   public bool completed;
   public List<FlowRun> runs=new List<FlowRun>();
  }
  FlowReport flowReport;
  Keyboard flowKeys;Mouse flowMouse;
  NavMeshPath flowPath;
  int flowCorner;
  ExpeditionNode flowNode;
  PhaseAnchor flowRouteAnchor;
  Vector3 flowApproach,flowDetour;
  bool flowHasDetour;
  string flowLastPlan="",flowLastSwapRejection="";
  float flowNextPlan;
  readonly List<Key> flowHeld=new List<Key>();

  IEnumerator FlowChecks()
  {
   flowReport=new FlowReport{version=Application.version};
   var args=Environment.GetCommandLineArgs();int chapterAt=Array.IndexOf(args,"-reviewFlowChapter");
   if(chapterAt>=0)
   {
    int selected;if(chapterAt+1>=args.Length||!int.TryParse(args[chapterAt+1],out selected)||(selected!=10&&selected!=12))
    {Check("flow_requested_chapter",false,"Use -reviewFlowChapter 10 or 12; omit to run both");yield break;}
    flowReport.requestedChapter=selected;
   }
   Check("flow_isolated_profile",RunStorage.IsValidation&&G.expeditionStore!=null,"Flow preparation uses only the validation profile under "+RunStorage.Root);
   if(!RunStorage.IsValidation||G.expeditionStore==null)yield break;
   // Unlocks only, before either measured run. No rank purchases or combat-stat edits.
   G.OpenExpeditionBoard();
   for(int level=G.expeditionStore.Profile.highestUnlocked;level<11;level++)
   {string id=G.expeditionStore.Begin(level);G.expeditionStore.Settle(id,true,false,30);}
   G.expeditionStore.Equip(0,0);G.expeditionStore.Equip(1,1);G.expeditionStore.SelectSpecialization(0);
   G.expeditionStore.SetAttachment(0,0);G.expeditionStore.SetAttachment(1,0);
   bool defaultRank=G.expeditionStore.Profile.ranks[0]==0;
   Check("flow_default_breach_rank_zero",defaultRank,"No rank reset is performed; observed rank="+G.expeditionStore.Profile.ranks[0]);
   if(!defaultRank)yield break;
   flowKeys=InputSystem.AddDevice<Keyboard>();flowMouse=InputSystem.AddDevice<Mouse>();
   G.qaKeyboard=flowKeys;G.qaMouse=flowMouse;G.qaInputEnabled=true;
   try
   {
    if(flowReport.requestedChapter==0||flowReport.requestedChapter==10)yield return FlowChapter(9,5010);
    if(flowReport.requestedChapter==0||flowReport.requestedChapter==12)yield return FlowChapter(11,5012);
    flowReport.completed=true;FlowSave();
   }
   finally
   {
    G.qaKeyboard=null;G.qaMouse=null;G.qaInputEnabled=false;
    InputSystem.RemoveDevice(flowKeys);InputSystem.RemoveDevice(flowMouse);
   }
  }
  void FlowSave(){File.WriteAllText(Path.Combine(output,"flow-report.json"),JsonUtility.ToJson(flowReport,true));}
  void FlowRecord(FlowRun run,string action,string detail)
  {run.events.Add(new FlowEvent{seconds=G.runSeconds,hp=P.health,feet=P.transform.position,action=action,detail=detail});Debug.Log("FLOW_CH"+run.chapter+" "+action+" "+detail);FlowSave();}
  void FlowInput(params Key[] keys){InputSystem.QueueStateEvent(flowKeys,new KeyboardState(keys));}
  void FlowAim(Vector3 point)
  {
   Vector3 d=point-P.Eye.position;
   P.SetLook(Mathf.Atan2(d.x,d.z)*Mathf.Rad2Deg-P.MechanicalRecoil.y,-Mathf.Atan2(d.y,new Vector2(d.x,d.z).magnitude)*Mathf.Rad2Deg+P.MechanicalRecoil.x);
  }
  IEnumerator FlowChapter(int level,int seed)
  {
   var run=new FlowRun{chapter=level+1,seed=seed,difficulty=G.options.difficulty,moveSpeedScale=G.options.moveSpeedScale};flowReport.runs.Add(run);
   G.OpenExpeditionBoard();UnityEngine.Random.InitState(seed);
   run.began=G.BeginExpedition(level);
   if(!run.began){run.completed=true;run.result="deployment_failed";run.reason=G.expeditionNotice;FlowSave();Check("flow_chapter_"+run.chapter,false,run.reason);yield break;}
   float began=Time.realtimeSinceStartup,nextSave=began+3,lastProgress=began;
   Vector3 start=P.transform.position,previous=start,progressPoint=start;
   int medicalAtStart=G.expeditionMedkits,ammoAtStart=G.expeditionAmmoPacks,progressHits=G.totalHits,progressObjectives=0,lastWave=-1,lastSwaps=G.totalSwaps;
   run.initialHp=run.minHp=P.health;run.initialAmmo=new[]{P.ammo[0],P.ammo[1]};run.initialReserve=new[]{P.reserve[0],P.reserve[1]};
   flowPath=null;flowNode=null;flowRouteAnchor=null;flowHasDetour=false;flowNextPlan=0;flowLastPlan=flowLastSwapRejection="";
   FlowInput();FlowRecord(run,"normal_spawn","Default BeginExpedition spawn; seed="+seed);
   yield return Capture("flow-ch"+run.chapter+"-spawn");
   bool bossCaptured=false,objectivesCaptured=false,ammoAttempted=false;
   while(Time.realtimeSinceStartup-began<300)
   {
    if(G.expeditionReceipt!=null){run.won=G.expeditionReceipt.success;run.result=run.won?"extracted":"failed_settlement";break;}
    if(P.health<=0||G.state==RunState.Dead){run.result="dead";run.reason="Normal combat or fall death";break;}
    if(!G.IsPlaying){run.result="unexpected_state";run.reason=G.state+"; "+G.expeditionNotice;break;}
    run.aiSuppressionObserved|=G.qaSuppressAI;
    foreach(var e in G.enemies)if(e&&e.Alive)run.aiSuppressionObserved|=e.suppressAI;
    if(run.aiSuppressionObserved){run.result="invalid_fixture";run.reason="AI suppression unexpectedly enabled; test refuses to modify it during a run";break;}
    run.minHp=Mathf.Min(run.minHp,P.health);
    Vector3 now=P.transform.position;float step=Vector3.Distance(now,previous);
    if(step<1)
    {
     run.walkedMeters+=step;
     if(run.entrySwap&&G.expeditionMap.returnBridgeReady&&now.z>=17&&now.z<=27&&Mathf.Abs(now.x)<2.1f&&previous.z>now.z)run.bridgeWalkMeters+=previous.z-now.z;
    }
    previous=now;
    if(G.totalSwaps!=lastSwaps){lastSwaps=G.totalSwaps;flowPath=null;flowRouteAnchor=null;flowHasDetour=false;flowNextPlan=0;FlowRecord(run,"swap_completed","swaps="+lastSwaps+"; feet="+now);}
    if(Vector3.Distance(now,progressPoint)>1||G.totalHits!=progressHits||G.expeditionObjectives!=progressObjectives)
    {lastProgress=Time.realtimeSinceStartup;progressPoint=now;progressHits=G.totalHits;progressObjectives=G.expeditionObjectives;}
    if(G.expeditionWave!=lastWave){lastWave=G.expeditionWave;FlowRecord(run,"wave","wave="+lastWave+"; required="+G.ExpeditionRequiredEnemies);}
    if(Time.realtimeSinceStartup>=nextSave){nextSave=Time.realtimeSinceStartup+3;FlowMetrics(run,began,medicalAtStart,ammoAtStart);FlowSave();}
    if(Time.realtimeSinceStartup-lastProgress>30)
    {run.result="strategy_stalled";run.reason="Automatic policy made no movement >1m, hits or objective progress for 30s; not evidence of a game soft lock. Goal="+run.lastGoal+"; path="+run.lastPath+"; feet="+now+"; see navigationDiagnostics";break;}

    if(P.health<=55&&G.expeditionMedkits>0&&G.UseExpeditionMedkit())FlowRecord(run,"medical_pack","Normal finite inventory use; remaining="+G.expeditionMedkits);
    if(!ammoAttempted&&G.expeditionAmmoPacks>0&&P.reserve[0]<24&&P.ammo[0]<12)
    {ammoAttempted=true;yield return FlowAmmoPack(run);flowNextPlan=0;continue;}
    foreach(var supply in G.expeditionMap.root.GetComponentsInChildren<SupplyPickup>())
     if(supply.collected){string id=supply.transform.position.ToString("F2");if(!run.supplies.Contains(id)){run.supplies.Add(id);FlowRecord(run,"world_supply",id);}}

    if(!run.entrySwap)
    {
     run.walkedEntrance|=G.totalSwaps==0&&now.z-start.z>2;
     if(now.z>27&&G.totalSwaps>0)
     {run.entrySwap=true;FlowInput();FlowRecord(run,"crossed_gap","Walked normally then exchanged first anchor");yield return Capture("flow-ch"+run.chapter+"-entry-swap");continue;}
     var anchor=G.expeditionMap.anchors[0];FlowAim(anchor.AimPoint);
     if(!P.preparing&&P.ValidateSwap(anchor)=="可换位")
     {FlowInput();if(P.RequestSwap(anchor))FlowRecord(run,"entry_swap_requested",anchor.transform.position.ToString());}
     else if(P.preparing)FlowInput();
     else if(now.z<15)FlowInput(Key.W);
     else {FlowInput();run.reason="Entrance swap unavailable: "+P.ValidateSwap(anchor);}
     yield return null;continue;
    }

    var bossEnemy=FlowBoss();
    if(bossEnemy)
    {
     var boss=bossEnemy.boss;
     if(!run.bossPhases.Contains(boss.Phase.ToString())){run.bossPhases.Add(boss.Phase.ToString());FlowRecord(run,"boss_phase",boss.Phase.ToString());}
     string skill=boss.Skill+"/"+boss.SkillStep;
     if(boss.Skill!=BossSkill.None&&!run.bossSkills.Contains(skill)){run.bossSkills.Add(skill);FlowRecord(run,"boss_skill",skill);}
     if(!bossCaptured){bossCaptured=true;FlowInput();yield return Capture("flow-ch"+run.chapter+"-boss-live");}
     if(FlowBossCounter(bossEnemy,run)){FlowInput();yield return null;continue;}
    }
    if(P.preparing){FlowInput();yield return null;continue;}

    // Real node proximity, line of sight and UseExpeditionNode checks remain authoritative.
    bool interacted=false;
    foreach(var node in G.expeditionMap.nodes)
    {
     bool wanted=node.kind==ExpeditionNodeKind.Objective&&!node.used||node.kind==ExpeditionNodeKind.Extraction&&G.ExpeditionMainComplete;
     if(!wanted||Vector3.Distance(P.Eye.position,node.Point)>2.6f)continue;
     FlowAim(node.Point);
     if(G.FindExpeditionNode()==node&&G.UseExpeditionNode(node))
     {FlowInput();interacted=true;flowPath=null;flowRouteAnchor=null;flowHasDetour=false;flowNextPlan=0;FlowRecord(run,"interaction",node.DisplayLabel);break;}
    }
    if(interacted){yield return null;continue;}
    if(G.expeditionObjectives==G.expeditionMap.objectiveCount&&!objectivesCaptured)
    {objectivesCaptured=true;FlowInput();yield return Capture("flow-ch"+run.chapter+"-authorizations-complete");}
    if(G.expeditionMap.returnBridgeReady&&now.z<17&&run.bridgeWalkMeters>8)run.bridgeWalked=true;

    Vector3 aimPoint;int visible;
    EnemyActor target=FlowTarget(out aimPoint,out visible);
    if(target)
    {
     Vector3 flat=Vector3.ProjectOnPlane(target.transform.position-now,Vector3.up);float distance=flat.magnitude;
     if(target.HasPhysicalShield&&distance<4.5f&&P.cooldown<=0&&P.ValidateSwap(target)=="可换位")
     {FlowInput();FlowAim(target.AimPoint);if(P.RequestSwap(target))FlowRecord(run,"close_shield_swap",target.name);yield return null;continue;}
     // Hold an actual world-fixed shield while firing; protect the current line, not the HUD.
     if(!bossEnemy&&(visible>=2||P.health<90)&&P.tactics.ShieldCooldownLeft<=0)
     {P.SetLook(Mathf.Atan2(flat.x,flat.z)*Mathf.Rad2Deg,0);if(P.tactics.ActivateShield())FlowRecord(run,"a_shield","Threat-facing barrier");}
     FlowAim(aimPoint);
     int selected=distance<4.5f&&P.ammo[1]+P.reserve[1]>0?1:P.ammo[0]+P.reserve[0]>0?0:1;
     if(P.weapon!=selected)P.SwitchWeapon(selected);
     if(P.ammo[P.weapon]==0)P.Reload();else P.Fire();
     Vector3 dodge=Vector3.zero;
     if(distance<4.8f)dodge=FlowSafeMove(-flat.normalized);
     else if(target.kind==EnemyKind.Sniper&&target.warningLeft>0)dodge=FlowSafeMove(Vector3.Cross(Vector3.up,flat.normalized));
     FlowDirection(dodge);
    }
    else
    {
     if(P.ammo[P.weapon]<P.Capacity(P.weapon)*.7f&&P.reserve[P.weapon]>0)P.Reload();
     if(Time.time>=flowNextPlan)
     {flowNextPlan=Time.time+1.5f;FlowPlan(run);}
     if(flowRouteAnchor)
     {
      FlowAim(flowRouteAnchor.AimPoint);string reason=P.ValidateSwap(flowRouteAnchor);
      if(reason=="可换位")
      {
       FlowInput();if(P.RequestSwap(flowRouteAnchor))FlowRecord(run,"navigation_swap_requested",run.lastGoal+"; anchor="+flowRouteAnchor.transform.position+"; plannedApproach="+flowApproach);
       yield return null;continue;
      }
      string rejection=flowRouteAnchor.GetInstanceID()+":"+reason;
      if(rejection!=flowLastSwapRejection){flowLastSwapRejection=rejection;FlowRecord(run,"navigation_swap_current_rejection",flowRouteAnchor.transform.position+"; "+reason+"; walking toward "+flowApproach);}
     }
     Vector3 next=FlowNextCorner();next=FlowAvoidAnchor(next,run);Vector3 delta=Vector3.ProjectOnPlane(next-now,Vector3.up);
     if(delta.sqrMagnitude>.04f){P.SetLook(Mathf.Atan2(delta.x,delta.z)*Mathf.Rad2Deg,0);FlowInput(Key.W);}
     else FlowInput();
    }
    yield return null;
   }
   FlowInput();
   if(run.result=="running"){run.result="watchdog";run.reason="300 second per-chapter limit; goal="+run.lastGoal+"; path="+run.lastPath+"; feet="+P.transform.position;}
   FlowMetrics(run,began,medicalAtStart,ammoAtStart);run.completed=true;
   if(!run.won&&run.reason.Length==0)run.reason="State="+G.state+"; HP="+P.health+"; required="+G.ExpeditionRequiredEnemies;
   FlowRecord(run,"run_end",run.result+"; "+run.reason);
   yield return Capture("flow-ch"+run.chapter+"-"+run.result);
   Check("flow_chapter_"+run.chapter,run.won&&run.walkedEntrance&&run.entrySwap&&run.bridgeWalked&&!run.aiSuppressionObserved,
    "result="+run.result+"; seconds="+run.seconds.ToString("F1")+"; hp="+run.finalHp+"; shots/hits="+run.shots+"/"+run.hits+"; medical="+run.medicalUsed+"; ammoPacks="+run.ammoPacksUsed+"; reason="+run.reason);
   if(G.expeditionActive&&G.expeditionReceipt==null)G.FinishExpedition(false); // Stop failed strategy via normal abandonment settlement.
   G.OpenExpeditionBoard();yield return null;
  }
  void FlowMetrics(FlowRun run,float began,int medicalAtStart,int ammoAtStart)
  {
   run.seconds=Time.realtimeSinceStartup-began;run.gameSeconds=G.runSeconds;run.finalHp=P.health;run.minHp=Mathf.Min(run.minHp,P.health);run.finalFeet=P.transform.position;
   run.shots=G.totalShots;run.hits=G.totalHits;run.kills=G.totalKills;run.swaps=G.totalSwaps;run.shieldBlocks=P.tactics.ShieldBlocks;
   run.medicalUsed=medicalAtStart-G.expeditionMedkits;run.ammoPacksUsed=ammoAtStart-G.expeditionAmmoPacks;
   run.objectives=G.expeditionObjectives;run.waves=G.expeditionWave;run.remainingEnemies=G.ExpeditionRequiredEnemies;
   run.finalAmmo=new[]{P.ammo[0],P.ammo[1]};run.finalReserve=new[]{P.reserve[0],P.reserve[1]};
  }
  IEnumerator FlowAmmoPack(FlowRun run)
  {
   FlowInput();yield return null;FlowInput(Key.Tab);yield return null;yield return null;
   bool used=G.expeditionInventory&&G.UseExpeditionAmmo();FlowRecord(run,"ammo_pack","Tab-opened inventory; used="+used+"; remaining="+G.expeditionAmmoPacks);
   FlowInput();yield return null;
   if(G.expeditionInventory){FlowInput(Key.Tab);yield return null;yield return null;}
   FlowInput();yield return null;
  }
  EnemyActor FlowBoss(){foreach(var e in G.enemies)if(e&&e.Alive&&e.boss)return e;return null;}
  bool FlowBossCounter(EnemyActor enemy,FlowRun run)
  {
   var boss=enemy.boss;bool active=boss.SkillStep==BossSkillStep.Telegraph||boss.SkillStep==BossSkillStep.Execute;
   if(!active)return false;
   if(boss.Skill==BossSkill.OmniFire&&P.tactics.ShieldCooldownLeft<=0)
   {Vector3 d=enemy.AimPoint-P.Eye.position;P.SetLook(Mathf.Atan2(d.x,d.z)*Mathf.Rad2Deg,0);if(P.tactics.ActivateShield())FlowRecord(run,"boss_a_shield","OmniFire counter");}
   bool danger=boss.Skill==BossSkill.Attraction?Vector3.Distance(P.transform.position,boss.DangerCenter)<4.8f:
    (boss.Skill==BossSkill.SweepLaser||boss.Skill==BossSkill.GroundWave)&&P.transform.position.y<1.2f;
   if(!danger||P.cooldown>0||P.preparing)return false;
   PhaseAnchor chosen=null;float best=float.NegativeInfinity;
   foreach(var anchor in G.expeditionMap.anchors)
   {
    if(!anchor||P.ValidateSwap(anchor)!="可换位")continue;
    if(boss.Skill!=BossSkill.Attraction&&anchor.transform.position.y<1.3f)continue;
    if(boss.Skill==BossSkill.Attraction&&Vector3.Distance(anchor.transform.position,boss.DangerCenter)<5.5f)continue;
    float score=anchor.transform.position.y*4+Vector3.Distance(anchor.transform.position,enemy.transform.position)*.3f;
    if(score>best){best=score;chosen=anchor;}
   }
   if(!chosen)return false;FlowAim(chosen.AimPoint);
   bool requested=P.RequestSwap(chosen);if(requested)FlowRecord(run,"boss_counter_swap",boss.Skill+" -> "+chosen.transform.position);
   return requested;
  }
  EnemyActor FlowTarget(out Vector3 point,out int visible)
  {
   EnemyActor best=null;point=Vector3.zero;visible=0;float score=float.PositiveInfinity;
   foreach(var e in G.enemies)
   {
    if(!e||!e.Alive||Vector3.Distance(P.Eye.position,e.AimPoint)>38)continue;
    Vector3 aim;if(!FlowHittable(e,out aim))continue;visible++;
    float s=Vector3.Distance(P.transform.position,e.transform.position)-(e.kind==EnemyKind.Sniper?15:0)-(e.boss?5:0);
    if(s<score){score=s;best=e;point=aim;}
   }
   return best;
  }
  bool FlowHittable(EnemyActor enemy,out Vector3 point)
  {return FlowHittableFrom(enemy,P.Eye.position,out point);}
  bool FlowHittableFrom(EnemyActor enemy,Vector3 eye,out Vector3 point)
  {
   Vector3 feet=enemy.transform.position;
   Vector3[] candidates={feet+Vector3.up*(enemy.Height-.18f),enemy.AimPoint,
    feet+enemy.transform.right*.15f+Vector3.up*.10f,feet-enemy.transform.right*.15f+Vector3.up*.10f,
    feet+enemy.transform.right*.46f+Vector3.up*1.12f,feet-enemy.transform.right*.46f+Vector3.up*1.12f};
   foreach(var candidate in candidates)
   {
    Vector3 delta=candidate-eye;
    if(EnemyActor.CastWeaponRay(eye,delta.normalized,delta.magnitude+.3f,P.transform,out var hit)&&hit.collider.GetComponentInParent<EnemyActor>()==enemy&&!enemy.IsShieldCollider(hit.collider))
    {point=candidate;return true;}
   }
   point=enemy.AimPoint;return false;
  }
  Vector3 FlowSafeMove(Vector3 preferred)
  {
   if(!NavMesh.SamplePosition(P.transform.position,out var from,.7f,NavMesh.AllAreas))return Vector3.zero;
   foreach(var direction in new[]{preferred,Vector3.Cross(Vector3.up,preferred),-Vector3.Cross(Vector3.up,preferred)})
   {
    Vector3 wanted=P.transform.position+direction.normalized*.8f;
    if(NavMesh.SamplePosition(wanted,out var to,.2f,NavMesh.AllAreas)&&Mathf.Abs(to.position.y-P.transform.position.y)<.45f&&!NavMesh.Raycast(from.position,to.position,out var hit,NavMesh.AllAreas))return direction.normalized;
   }
   return Vector3.zero;
  }
  void FlowDirection(Vector3 world)
  {
   flowHeld.Clear();Vector3 local=P.transform.InverseTransformDirection(world);
   if(local.z>.3f)flowHeld.Add(Key.W);else if(local.z<-.3f)flowHeld.Add(Key.S);
   if(local.x>.3f)flowHeld.Add(Key.D);else if(local.x<-.3f)flowHeld.Add(Key.A);
   FlowInput(flowHeld.ToArray());
  }
  NavMeshPath FlowCalculate(Vector3 target)
  {return FlowCalculate(P.transform.position,target);}
  NavMeshPath FlowCalculate(Vector3 start,Vector3 target)
  {
   if(!NavMesh.SamplePosition(start,out var from,.8f,NavMesh.AllAreas)||Mathf.Abs(from.position.y-start.y)>.6f||!NavMesh.SamplePosition(target,out var to,.35f,NavMesh.AllAreas)||Mathf.Abs(to.position.y-target.y)>.35f)return null;
   var path=new NavMeshPath();return NavMesh.CalculatePath(from.position,to.position,NavMesh.AllAreas,path)&&path.status==NavMeshPathStatus.PathComplete?path:null;
  }
  static float FlowLength(NavMeshPath path)
  {float distance=0;for(int i=1;i<path.corners.Length;i++)distance+=Vector3.Distance(path.corners[i-1],path.corners[i]);return distance;}
  NavMeshPath FlowNodePath(ExpeditionNode node)
  {return FlowNodePath(node,P.transform.position);}
  NavMeshPath FlowNodePath(ExpeditionNode node,Vector3 start)
  {
   NavMeshPath best=null;float shortest=float.PositiveInfinity;
   for(int i=0;i<16;i++)
   {
    float angle=i*Mathf.PI/8;Vector3 p=node.transform.position+new Vector3(Mathf.Sin(angle)*1.85f,.03f,Mathf.Cos(angle)*1.85f);
    if(Physics.CheckCapsule(p+Vector3.up*.36f,p+Vector3.up*1.45f,.29f,Layers.WorldMask,QueryTriggerInteraction.Ignore))continue;
    if(Physics.Linecast(p+Vector3.up*1.62f,node.Point,out var obstruction,Layers.WorldMask)&&obstruction.collider.GetComponentInParent<ExpeditionNode>()!=node)continue;
    var path=FlowCalculate(start,p);if(path==null)continue;float length=FlowLength(path);
    if(length<shortest){best=path;shortest=length;}
   }
   return best;
  }
  void FlowPlan(FlowRun run)
  {
   flowPath=null;flowNode=null;flowRouteAnchor=null;flowCorner=0;float shortest=float.PositiveInfinity;
   run.navigationDiagnostics.Clear();var goals=new List<ExpeditionNode>();Physics.SyncTransforms();
   foreach(var node in G.expeditionMap.nodes)
   {
    bool wanted=node.kind==ExpeditionNodeKind.Objective&&!node.used||node.kind==ExpeditionNodeKind.Extraction&&G.ExpeditionMainComplete&&G.expeditionMap.returnBridgeReady;
    if(!wanted)continue;goals.Add(node);var path=FlowNodePath(node);
    run.navigationDiagnostics.Add(node.DisplayLabel+": "+(path==null?"no complete direct walking path to a clear, visible interaction position":"walking path available"));
    if(path==null)continue;float length=FlowLength(path);
    if(length<shortest){shortest=length;flowPath=path;flowNode=node;}
   }
   if(flowNode)run.lastGoal=flowNode.DisplayLabel;
   else if(goals.Count>0&&FlowAnchorRoute(run,goals)) { }
   else
   {
    EnemyActor nearest=null;float distance=float.PositiveInfinity;
    foreach(var e in G.enemies)if(e&&e.Alive){float d=Vector3.Distance(P.transform.position,e.transform.position);if(d<distance){distance=d;nearest=e;}}
    if(nearest)
    {
     run.lastGoal="Find firing line to "+nearest.name+" at "+nearest.transform.position.ToString("F2");
     string diagnostics;var firingPoints=FlowFiringPoints(nearest,out diagnostics);
     run.navigationDiagnostics.Add(run.lastGoal+": "+diagnostics);
     flowPath=FlowBestPath(P.transform.position,firingPoints);
     run.navigationDiagnostics.Add("Firing-position walk from player: "+(flowPath==null?"no complete path":"complete path, length="+FlowLength(flowPath).ToString("F1")));
     if(flowPath==null)FlowAnchorRoute(run,new List<ExpeditionNode>(),nearest,firingPoints);
    }
    else run.lastGoal=goals.Count>0?"Pending objectives; policy has not found a walk/anchor route":"Wait for next normal deployment";
   }
   if(flowPath!=null){flowCorner=flowPath.corners.Length>1?1:0;run.lastPath=(flowRouteAnchor?"walk to predicted exchange viewpoint; ":"complete walking path; ")+"corners="+flowPath.corners.Length+"; length="+FlowLength(flowPath).ToString("F1");}
   else run.lastPath="No complete route found by this policy; inspect navigationDiagnostics";
   string plan=run.lastGoal+" / "+(flowRouteAnchor?flowRouteAnchor.transform.position.ToString("F2"):flowPath!=null?"walk":"unplanned");
   if(plan!=flowLastPlan){flowLastPlan=plan;FlowRecord(run,"navigation_plan",plan+"\n"+string.Join("\n",run.navigationDiagnostics));}
  }

  sealed class FlowLink
  {public PhaseAnchor anchor;public Vector3 approach;public NavMeshPath path;public float cost;}
  bool FlowStandingRoom(Vector3 feet,Transform ignored=null)
  {
   foreach(var c in Physics.OverlapCapsule(feet+Vector3.up*.38f,feet+Vector3.up*1.45f,.285f,Layers.CombatMask,QueryTriggerInteraction.Ignore))
   {
    if(c.transform==P.transform||c.transform.IsChildOf(P.transform))continue;
    if(ignored&&(c.transform==ignored||c.transform.IsChildOf(ignored)))continue;
    return false;
   }
   return true;
  }
  bool FlowPredictExchange(Vector3 feet,PhaseAnchor anchor,out string reason)
  {
   Vector3 eye=feet+Vector3.up*1.62f,delta=anchor.AimPoint-eye;
   if(delta.magnitude>G.config.swapRange-.2f){reason="outside predicted swap range";return false;}
   if(!FlowStandingRoom(feet,anchor.transform)){reason="approach occupancy";return false;}
   if(CombatRay.Cast(eye,delta.normalized,delta.magnitude+.05f,P.transform,out var hit)&&hit.collider.GetComponentInParent<SwapTarget>()!=anchor)
   {reason="occluded by "+hit.collider.name;return false;}
   reason="geometry permits exchange (actual ValidateSwap still required)";return true;
  }
  static void FlowAddHeight(List<float> heights,float value)
  {foreach(float old in heights)if(Mathf.Abs(old-value)<.2f)return;heights.Add(value);}
  static void FlowReject(Dictionary<string,int> counts,string reason)
  {if(counts.ContainsKey(reason))counts[reason]++;else counts.Add(reason,1);}
  NavMeshPath FlowBestPath(Vector3 start,List<Vector3> points)
  {
   NavMeshPath best=null;float shortest=float.PositiveInfinity;
   foreach(var point in points){var path=FlowCalculate(start,point);if(path==null)continue;float length=FlowLength(path);if(length<shortest){shortest=length;best=path;}}
   return best;
  }
  List<Vector3> FlowFiringPoints(EnemyActor enemy,out string diagnostics)
  {
   // Firing positions can be on either a tower or the ground. Sampling only the
   // enemy's altitude incorrectly strands the policy on disconnected high ground.
   var heights=new List<float>();FlowAddHeight(heights,P.transform.position.y);FlowAddHeight(heights,enemy.transform.position.y);FlowAddHeight(heights,G.expeditionMap.spawn.y);
   foreach(var node in G.expeditionMap.nodes)if(node)FlowAddHeight(heights,node.transform.position.y);
   foreach(var anchor in G.expeditionMap.anchors)if(anchor)FlowAddHeight(heights,anchor.transform.position.y);
   var points=new List<Vector3>();var rejected=new Dictionary<string,int>();
   foreach(float height in heights)foreach(float radius in new[]{4f,6f,10f,16f,22f})for(int i=0;i<24;i++)
   {
    float angle=i*Mathf.PI/12;Vector3 sample=new Vector3(enemy.transform.position.x+Mathf.Cos(angle)*radius,height,enemy.transform.position.z+Mathf.Sin(angle)*radius);
    if(!NavMesh.SamplePosition(sample,out var nav,.7f,NavMesh.AllAreas)||Mathf.Abs(nav.position.y-height)>.5f){FlowReject(rejected,"no matching navigable floor");continue;}
    Vector3 feet=nav.position+Vector3.up*.035f;
    if(!FlowStandingRoom(feet)){FlowReject(rejected,"occupied standing volume");continue;}
    Vector3 aim;if(!FlowHittableFrom(enemy,feet+Vector3.up*1.62f,out aim)){FlowReject(rejected,"no legal weapon ray (world/actor/shield obstruction)");continue;}
    bool duplicate=false;foreach(var p in points)if(Vector3.Distance(p,feet)<.25f){duplicate=true;break;}
    if(!duplicate)points.Add(feet);
   }
   var reasons=new List<string>();foreach(var rejection in rejected)reasons.Add(rejection.Key+"="+rejection.Value);
   diagnostics="enemy="+enemy.name+"; feet="+enemy.transform.position.ToString("F2")+"; legal firing positions="+points.Count+"; rejected samples: "+string.Join(", ",reasons);return points;
  }
  List<Vector3> FlowApproaches(PhaseAnchor anchor,List<float> heights,out string diagnostics)
  {
   var result=new List<Vector3>();var rejected=new Dictionary<string,int>();string reason;
   if(FlowPredictExchange(P.transform.position,anchor,out reason))result.Add(P.transform.position);else FlowReject(rejected,"current position: "+reason);
   foreach(float height in heights)foreach(float radius in new[]{6f,10f,16f,20f})for(int i=0;i<24;i++)
   {
    float angle=i*Mathf.PI/12;
    Vector3 sample=new Vector3(anchor.transform.position.x+Mathf.Sin(angle)*radius,height,anchor.transform.position.z+Mathf.Cos(angle)*radius);
    if(!NavMesh.SamplePosition(sample,out var nav,1,NavMesh.AllAreas)||Mathf.Abs(nav.position.y-height)>.65f){FlowReject(rejected,"no matching navigable floor");continue;}
    Vector3 feet=nav.position+Vector3.up*.035f;
    if(!FlowPredictExchange(feet,anchor,out reason)){FlowReject(rejected,reason);continue;}
    bool duplicate=false;foreach(var old in result)if(Vector3.Distance(old,feet)<.25f){duplicate=true;break;}
    if(!duplicate)result.Add(feet);
   }
   var parts=new List<string>();foreach(var pair in rejected)parts.Add(pair.Key+"="+pair.Value);
   diagnostics="clear viewpoints="+result.Count+"; rejected samples: "+string.Join(", ",parts);return result;
  }
  bool FlowAnchorRoute(FlowRun run,List<ExpeditionNode> goals,EnemyActor firingTarget=null,List<Vector3> firingPoints=null)
  {
   // Small graph of current position and authored anchors. Edges are a complete
   // walking path to a physically checked viewpoint, then one predicted swap.
   // Execute only the first edge and replan after the real two-way exchange.
   var anchors=new List<PhaseAnchor>();foreach(var a in G.expeditionMap.anchors)if(a&&a.Alive)anchors.Add(a);
   int count=anchors.Count+1;var origins=new Vector3[count];origins[0]=P.transform.position;
   for(int i=1;i<count;i++)origins[i]=anchors[i-1].transform.position;
   var heights=new List<float>();FlowAddHeight(heights,P.transform.position.y);FlowAddHeight(heights,G.expeditionMap.spawn.y);
   foreach(var n in G.expeditionMap.nodes)if(n)FlowAddHeight(heights,n.transform.position.y);
   foreach(var a in anchors)FlowAddHeight(heights,a.transform.position.y);
   var links=new FlowLink[count,count];bool walkingReturn=goals.Exists(n=>n.kind==ExpeditionNodeKind.Extraction)&&!run.bridgeWalked;
   for(int target=1;target<count;target++)
   {
    var anchor=anchors[target-1];string label="anchor "+target+" at "+anchor.transform.position.ToString("F2");
    if(walkingReturn&&anchor.transform.position.z<27){run.navigationDiagnostics.Add(label+": excluded to retain physical bridge return");continue;}
    if(!FlowStandingRoom(anchor.transform.position,anchor.transform)){run.navigationDiagnostics.Add(label+": player destination occupied");continue;}
    string diagnostics;var viewpoints=FlowApproaches(anchor,heights,out diagnostics);run.navigationDiagnostics.Add(label+": "+diagnostics);
    for(int source=0;source<count;source++)
    {
     if(source==target)continue;
     if(FlowCalculate(origins[source],anchor.transform.position)!=null)
     {if(source==0)run.navigationDiagnostics.Add(label+": already on current walking island; no topological swap needed");continue;}
     FlowLink best=null;
     foreach(var viewpoint in viewpoints)
     {
      var path=FlowCalculate(origins[source],viewpoint);if(path==null)continue;
      float cost=FlowLength(path)+5;
      if(best==null||cost<best.cost)best=new FlowLink{anchor=anchor,approach=viewpoint,path=path,cost=cost};
     }
     links[source,target]=best;
     run.navigationDiagnostics.Add(label+" from "+(source==0?"player":"anchor "+source)+": "+(best==null?"no complete walking path to any predicted viewpoint":"viewpoint reachable; walk="+(best.cost-5).ToString("F1")));
    }
   }
   var distance=new float[count];var visited=new bool[count];var first=new FlowLink[count];var hops=new int[count];
   for(int i=0;i<count;i++)distance[i]=float.PositiveInfinity;distance[0]=0;
   float bestTotal=float.PositiveInfinity;FlowLink chosen=null;ExpeditionNode chosenGoal=null;int chosenHops=0;
   for(int step=0;step<count;step++)
   {
    int source=-1;float nearest=float.PositiveInfinity;
    for(int i=0;i<count;i++)if(!visited[i]&&distance[i]<nearest){source=i;nearest=distance[i];}
    if(source<0)break;visited[source]=true;
    if(first[source]!=null)foreach(var goal in goals)
    {
     var finalWalk=FlowNodePath(goal,origins[source]);if(finalWalk==null)continue;
     float total=distance[source]+FlowLength(finalWalk);
     if(total<bestTotal){bestTotal=total;chosen=first[source];chosenGoal=goal;chosenHops=hops[source];}
    }
    if(first[source]!=null&&firingTarget)
    {
     var finalWalk=FlowBestPath(origins[source],firingPoints);
     run.navigationDiagnostics.Add("Firing line to "+firingTarget.name+" at "+firingTarget.transform.position.ToString("F2")+" from anchor "+source+": "+(finalWalk==null?"no complete walk to any legal firing position":"reachable firing position"));
     if(finalWalk!=null)
     {
      float total=distance[source]+FlowLength(finalWalk);
      if(total<bestTotal){bestTotal=total;chosen=first[source];chosenGoal=null;chosenHops=hops[source];}
     }
    }
    for(int target=1;target<count;target++)
    {
     var edge=links[source,target];if(edge==null||distance[source]+edge.cost>=distance[target])continue;
     distance[target]=distance[source]+edge.cost;first[target]=source==0?edge:first[source];hops[target]=hops[source]+1;
    }
   }
   if(chosen==null){run.navigationDiagnostics.Add("Anchor graph: no sampled walk/exchange route to "+(firingTarget?"legal firing positions for "+firingTarget.name+" at "+firingTarget.transform.position.ToString("F2"):"any pending objective"));return false;}
   flowNode=chosenGoal;flowRouteAnchor=chosen.anchor;flowApproach=chosen.approach;flowPath=chosen.path;
   run.lastGoal=(chosenGoal?chosenGoal.DisplayLabel:"Firing line to "+firingTarget.name+" at "+firingTarget.transform.position.ToString("F2"))+" via "+chosen.anchor.transform.position.ToString("F2")+" ("+chosenHops+" predicted exchange(s))";
   run.navigationDiagnostics.Add("Selected first edge: "+run.lastGoal+"; viewpoint="+flowApproach.ToString("F2"));return true;
  }
  bool FlowWalkSegmentClear(Vector3 destination)
  {
   Vector3 delta=destination-P.transform.position;
   foreach(var hit in Physics.CapsuleCastAll(P.transform.position+Vector3.up*.38f,P.transform.position+Vector3.up*1.45f,.29f,delta.normalized,delta.magnitude,Layers.CombatMask,QueryTriggerInteraction.Ignore))
    if(hit.collider.transform!=P.transform&&!hit.collider.transform.IsChildOf(P.transform))return false;
   return true;
  }
  Vector3 FlowAvoidAnchor(Vector3 next,FlowRun run)
  {
   if(flowHasDetour)
   {
    if(Vector3.ProjectOnPlane(flowDetour-P.transform.position,Vector3.up).magnitude>.3f)return flowDetour;
    flowHasDetour=false;flowNextPlan=0;
   }
   Vector3 delta=Vector3.ProjectOnPlane(next-P.transform.position,Vector3.up);if(delta.magnitude<.2f)return next;
   PhaseAnchor blocking=null;float nearest=float.PositiveInfinity;
   foreach(var hit in Physics.CapsuleCastAll(P.transform.position+Vector3.up*.38f,P.transform.position+Vector3.up*1.45f,.29f,delta.normalized,Mathf.Min(1.2f,delta.magnitude),Layers.CombatMask,QueryTriggerInteraction.Ignore))
   {
    if(hit.collider.transform==P.transform||hit.collider.transform.IsChildOf(P.transform)||hit.distance>=nearest)continue;
    nearest=hit.distance;blocking=hit.collider.GetComponentInParent<PhaseAnchor>();
   }
   if(!blocking)return next;
   Vector3 forward=delta.normalized,side=Vector3.Cross(Vector3.up,forward);
   foreach(float sign in new[]{1f,-1f})
   {
    Vector3 candidate=blocking.transform.position+side*(1.15f*sign)-forward*.35f;
    if(!NavMesh.SamplePosition(candidate,out var nav,.6f,NavMesh.AllAreas)||Mathf.Abs(nav.position.y-P.transform.position.y)>.35f)continue;
    Vector3 point=nav.position+Vector3.up*.035f;
    if(!FlowStandingRoom(point)||!FlowWalkSegmentClear(point))continue;
    flowDetour=point;flowHasDetour=true;FlowRecord(run,"walk_around_anchor","Actual capsule lookahead blocked by "+blocking.transform.position+"; walking via "+point);return point;
   }
   string failure="Local walking lookahead blocked by anchor at "+blocking.transform.position.ToString("F2")+"; sampled side viewpoints blocked";
   if(!run.navigationDiagnostics.Contains(failure))run.navigationDiagnostics.Add(failure);return P.transform.position;
  }
  Vector3 FlowNextCorner()
  {
   if(flowPath==null||flowPath.corners.Length==0)return P.transform.position;
   while(flowCorner<flowPath.corners.Length)
   {
    Vector3 d=flowPath.corners[flowCorner]-P.transform.position;
    if(Vector3.ProjectOnPlane(d,Vector3.up).magnitude>.35f||Mathf.Abs(d.y)>.55f)return flowPath.corners[flowCorner];
    flowCorner++;
   }
   return P.transform.position;
  }
 }
}
