using UnityEngine;
namespace SwapHunter
{
 public sealed class TutorialCourse:MonoBehaviour
 {
  public bool Active {get;private set;}
  public bool Completed {get;private set;}
  public int Step {get;private set;}
  public EnemyActor TrainingTarget=>target;
  public PhaseAnchor RecallAnchor {get;private set;}
  EnemyActor recallDummy;
  public Vector3 LaunchPoint=>new Vector3(2,.03f,8);
  public bool LaunchUsed {get;private set;}
  public int AirSwaps {get;private set;}
  public bool LaunchedAirSwap {get;private set;}
  EnemyActor target;Transform props;LineRenderer marker;bool launched,knifeRepositioned;float stageHealth,moved,shotAt;int swaps,hits,throws,returns,blocks;
  DemoGame G=>DemoGame.I;PlayerMotor P=>G.player;
  public string Title=>"实战教学 "+Mathf.Min(Step+1,9)+" / 9";
  public string Instruction=>Step==0?"向前移动到青色标记，使用鼠标观察":
   Step==1?G.KeyName(12)+" 射击前方训练机；瞄准与装填可随时使用":
   Step==2?"瞄准训练机，按 "+G.KeyName(6)+" 交换双方位置；交换不改变朝向":
   Step==3?G.KeyName(4)+" 跳起，在空中瞄准训练机并按 "+G.KeyName(6):
   Step==4?"走到地面大跳装置，按 "+G.KeyName(8)+" 弹射；空中瞄准高台信标并换位":
   Step==5?"返回地面青圈；面向训练机按 "+G.KeyName(20)+"，用A盾拦住来弹":
   Step==6?"接近训练机，按 "+G.KeyName(21)+" 使用战术刀推开它":
   Step==7?"向前方训练机投刀 → 换位至左侧青色信标 → "+G.KeyName(23)+" 召回，切过中间目标":
   "面对盾卫，先换位取得侧背角度，再用武器和技能击败它";
  public void Begin()
  {
   Active=true;Completed=false;Step=0;AirSwaps=0;LaunchedAirSwap=LaunchUsed=launched=false;
   props=new GameObject("Playable tutorial devices").transform;
   Shapes.Box("One-use tutorial launch pad",props,LaunchPoint,new Vector3(1.8f,.08f,1.8f),4);
   marker=Shapes.Ring(props,new Vector3(0,.08f,5),1,new Color(.1f,.9f,1),.09f);
   foreach(var e in G.enemies)if(e&&e.Alive){target=e;break;}
   if(target){target.maximumHealth=target.health=500;stageHealth=target.health;}
   moved=0;G.Record("tutorial_step","0");G.Toast(Instruction,5);
  }
  void Mark(Vector3 point)
  {if(marker)Destroy(marker.gameObject);marker=Shapes.Ring(props,point+Vector3.up*.06f,1,new Color(.1f,.9f,1),.08f);}
  void PlaceTarget(EnemyKind kind,Vector3 point)
  {
   if(target){target.gameObject.SetActive(false);Destroy(target.gameObject);}
   target=EnemyActor.Spawn(kind,point,0);if(target){target.suppressAI=kind==EnemyKind.Training;target.maximumHealth=target.health=kind==EnemyKind.Training?500:120;stageHealth=target.health;}
  }
  void Next()
  {
   Step++;swaps=P.swapCount;hits=P.combatTools.MeleeHits;throws=P.combatTools.Throws;returns=P.combatTools.ReturnHits;blocks=P.tactics.ShieldBlocks;
   if(target)stageHealth=target.health;
   G.Record("tutorial_step",Step.ToString());G.sound.Play("confirm");
   if(Step==3)AirSwaps=0;
   if(Step==4){Mark(LaunchPoint);P.cooldown=0;}
   if(Step==5){Mark(new Vector3(0,0,8));PlaceTarget(EnemyKind.Training,new Vector3(0,0,17));shotAt=Time.time+2;}
   if(Step==6){PlaceTarget(EnemyKind.Training,new Vector3(0,0,13));Mark(target?target.transform.position:new Vector3(0,0,13));}
   if(Step==7)
   {
    knifeRepositioned=false;PlaceTarget(EnemyKind.Training,new Vector3(0,0,17));
    recallDummy=EnemyActor.Spawn(EnemyKind.Training,new Vector3(-4,0,15),0);
    var go=new GameObject("Tutorial recall anchor");go.layer=Layers.Actor;go.transform.SetParent(props);go.transform.position=new Vector3(-8,.03f,13);
    RecallAnchor=go.AddComponent<PhaseAnchor>();var c=go.AddComponent<CapsuleCollider>();c.radius=.32f;c.height=1.4f;c.center=Vector3.up*.7f;ImportedModels.Create(G.config.models.beacon,go.transform,Vector3.zero);
    Mark(go.transform.position);P.cooldown=0;
   }
   if(Step==8){if(recallDummy){recallDummy.gameObject.SetActive(false);Destroy(recallDummy.gameObject);}if(RecallAnchor){RecallAnchor.gameObject.SetActive(false);Destroy(RecallAnchor.gameObject);}PlaceTarget(EnemyKind.Shield,new Vector3(0,0,13));Mark(new Vector3(-6,0,15));P.health=G.config.playerHealth;P.cooldown=0;}
   if(Step>=9){Completed=true;Active=false;G.SetState(RunState.Victory);G.Record("tutorial_complete","movement/shoot/both-endpoints/air/launch/shield/melee/knife/spatial-combat");return;}
   G.Toast(Instruction,5);
  }
  public void NotifySwap(bool airborne)
  {
   if(!Active)return;if(airborne)AirSwaps++;
   if(Step==4&&launched&&airborne)LaunchedAirSwap=true;
  }
  void Update()
  {
   if(!Active||!G.IsPlaying)return;
   if(!target&&Step>0&&Step<8)PlaceTarget(EnemyKind.Training,new Vector3(0,0,13));
   if(target&&!target.Alive&&Step<8){PlaceTarget(EnemyKind.Training,new Vector3(0,0,13));G.Toast("训练机已重置，继续当前练习",2);}
   switch(Step)
   {
    case 0:moved+=P.CurrentSpeed*Time.deltaTime;if(moved>3&&P.transform.position.z>4)Next();break;
    case 1:if(target&&target.health<stageHealth)Next();break;
    case 2:if(P.swapCount>swaps)Next();break;
    case 3:if(AirSwaps>0){Next();}break;
    case 4:
     if(LaunchedAirSwap&&P.transform.position.y>3)Next();
     else if(launched&&P.Grounded){launched=LaunchUsed=LaunchedAirSwap=false;G.Toast("未到达高台，教学装置已重新充能",3);}break;
    case 5:
     if(P.tactics.ShieldBlocks>blocks){Next();break;}
     if(target&&Vector3.Distance(P.transform.position,new Vector3(0,0,8))<4&&Time.time>=shotAt)
     {shotAt=Time.time+1.8f;Bolt.Spawn(target,target.AimPoint,P.Eye.position-target.AimPoint,15,5);G.sound.Play("warning",target.AimPoint);}break;
    case 6:if(P.combatTools.MeleeHits>hits)Next();break;
    case 7:
     if(P.combatTools.Knife&&Vector3.Distance(P.Eye.position,P.combatTools.Knife.LaunchPoint)>2.5f)knifeRepositioned=true;
     if(P.combatTools.Throws>throws&&P.combatTools.ReturnHits>returns&&knifeRepositioned)Next();break;
    case 8:if(target&&!target.Alive&&P.swapCount>swaps)Next();else if(target&&!target.Alive){PlaceTarget(EnemyKind.Shield,new Vector3(0,0,13));G.Toast("这一轮请先交换位置，再攻击盾卫侧背",3);}break;
   }
  }
  public bool Interact()
  {
   if(Step!=4){G.Toast(Instruction,2);return false;}
   if(Vector3.Distance(P.transform.position,LaunchPoint)>2.6f){G.Toast("靠近青色大跳装置再交互",2);return false;}
   if(LaunchUsed){G.Toast("本次弹射已使用 · 空中瞄准高台信标",2);return false;}
   if(!P.Launch())return false;LaunchUsed=launched=true;G.Record("tutorial_launch","one use; resets only after failed teaching attempt");return true;
  }
  public void RetryCheckpoint()
  {
   P.ResetAt(new Vector3(0,.03f,8));G.SetState(RunState.Playing);swaps=P.swapCount;hits=P.combatTools.MeleeHits;throws=P.combatTools.Throws;returns=P.combatTools.ReturnHits;blocks=P.tactics.ShieldBlocks;
   if(Step==4){LaunchUsed=launched=LaunchedAirSwap=false;}
   PlaceTarget(Step==8?EnemyKind.Shield:EnemyKind.Training,new Vector3(0,0,Step==5||Step==7?17:13));G.Toast(Instruction,4);
  }
  public void End(){Active=false;Completed=false;if(props)Destroy(props.gameObject);props=null;}
  void OnDestroy(){if(props)Destroy(props.gameObject);}
 }
 public sealed partial class DemoGame
 {
  public TutorialCourse tutorialCourse;
  public void StartTutorial(){StartRun(false);if(tutorialCourse)Destroy(tutorialCourse);tutorialCourse=gameObject.AddComponent<TutorialCourse>();tutorialCourse.Begin();}
  bool DrawTutorialCompletion()
  {
   if(!tutorialCourse||!tutorialCourse.Completed||state!=RunState.Victory)return false;
   float w=760,x=(viewWidth-w)/2;Fill(FullScreenGuiRect,new Color(.01f,.025f,.05f,.9f));DrawUiPanel(new Rect(x,190,w,510),true);
   Text("你已掌握换位战斗",new Rect(x+35,228,w-70,60),38,Color.white);
   Text("移动与射击 → 双方换位 → 空中与大跳\nA盾创造窗口 → 近战解围 → 飞刀回收\n现在把这些工具带进十二段相位行动。",new Rect(x+35,328,w-70,155),24,Muted);
   if(Button("进入相位行动",new Rect(x+35,524,w-70,58),true))OpenExpeditionBoard();
   if(Button("重新体验教学",new Rect(x+35,603,w-70,50)))StartTutorial();return true;
  }
 }
}
