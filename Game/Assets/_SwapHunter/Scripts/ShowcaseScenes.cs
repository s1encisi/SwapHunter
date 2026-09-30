using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
namespace SwapHunter
{
 public sealed partial class ShowcaseCapture
 {
  bool Need(string id)=>(only=="gunplay"&&id.StartsWith("gunplay-",StringComparison.Ordinal))||string.IsNullOrEmpty(only)||only==id||only=="boss"&&id.Contains("boss-")||only=="core"&&!id.Contains("boss-");
  void FireAt(EnemyActor target)
  {
   if(!target||!target.Alive)return;Look(target.AimPoint);
   if(P.ammo[P.weapon]<=0){P.Reload();return;}
   if(Vector3.Dot(P.Eye.forward,(target.AimPoint-P.Eye.position).normalized)>.985f)P.Fire();
  }
  IEnumerator RecordGroundSwap()
  {
   const string id="02-shoot-swap";if(!Need(id))yield break;EndScenario();G.StartRun(false);yield return new WaitForSeconds(.3f);var target=G.enemies.Find(e=>e&&e.Alive);Look(target.AimPoint,false);bool swapped=false;
   yield return Record(id,6,"射击、交换两端位置、转身续战","正常校准场出生，所有伤害来自实际枪械","自动实机演示",t=>{
    Keys(t<.95f?new[]{Key.W}:new Key[0]);if(target&&target.Alive)Look(target.AimPoint);
    if(t>1.25f&&t<1.55f)FireAt(target);
    if(t>2.15f&&!swapped){swapped=P.RequestSwap(target);}
    if(t>3.4f)FireAt(target);
   });
  }
  IEnumerator RecordAirSwap()
  {
   const string id="03-air-swap";if(!Need(id))yield break;EndScenario();G.StartRun(false);yield return new WaitForSeconds(.3f);var target=G.enemies.Find(e=>e&&e.Alive);Look(target.AimPoint,false);bool jumped=false,swapped=false;
   yield return Record(id,5,"跳跃中交换，敌人在原空中位置下落","正常校准场出生；实际跳跃与双方换位","自动实机演示",t=>{
    Keys(t<.85f?new[]{Key.W}:new Key[0]);if(target)Look(target.AimPoint);
    if(t>1.1f&&!jumped){Keys(Key.Space);jumped=true;}
    if(t>1.35f&&!swapped&&!P.Grounded)swapped=P.RequestSwap(target);
    if(t>3.1f)FireAt(target);
   });
  }
  IEnumerator RecordLaunch()
  {
   const string id="04-launch";if(!Need(id))yield break;EndScenario();G.BeginExpedition(2);yield return new WaitForSeconds(.3f);var pad=G.expeditionMap.nodes.Find(n=>n.kind==ExpeditionNodeKind.Launch);var target=G.expeditionMap.anchors[0];bool launched=false,swapped=false;
   yield return Record(id,8,"大跳装置、空中观察、跨断桥交换","从第三章正常入口出发，装置按E真实消耗一次","自动实机演示",t=>{
    Keys();
    if(!launched){Look(pad.transform.position+Vector3.back*.9f+Vector3.up*1.62f);if(Vector3.Distance(P.transform.position,pad.transform.position)>2.15f)Keys(Key.W);else{Look(pad.Point,false);Keys(Key.E);launched=true;}}
    else if(!swapped){Look(target.AimPoint);if(!P.Grounded&&P.transform.position.y>3.8f&&Vector3.Dot(P.Eye.forward,(target.AimPoint-P.Eye.position).normalized)>.99f)swapped=P.RequestSwap(target);}
    else{var e=G.enemies.Find(x=>x&&x.Alive&&x.kind!=EnemyKind.Shield);if(t>5)FireAt(e);}
   });
  }
  IEnumerator RecordShield()
  {
   const string id="05-directional-shield";if(!Need(id))yield break;EndScenario();G.StartRun(true);yield return new WaitForSeconds(3.5f);var sniper=G.enemies.Find(e=>e&&e.Alive&&e.kind==EnemyKind.Sniper);if(!sniper){report.errors.Add("shield scene sniper missing");yield break;}Look(sniper.AimPoint,false);bool opened=false;
   yield return Record(id,7,"定向屏障创造射击与重新走位窗口","自由试招正常入口，真实敌弹与有限4秒屏障","自动实机演示",t=>{
    Keys();if(sniper&&sniper.Alive)Look(sniper.AimPoint);
    if(t>.25f&&!opened)opened=P.tactics.ActivateShield();
    if(t>3.7f)FireAt(sniper);
    if(t>5)Keys(Key.A);
   });
  }
  IEnumerator PrepareKnifeLesson()
  {
   EndScenario();G.StartTutorial();yield return new WaitForSeconds(.25f);yield return Press(Key.W,1.1f);yield return new WaitForSeconds(.2f);
   Look(G.tutorialCourse.TrainingTarget.AimPoint,false);P.Fire();yield return new WaitForSeconds(.2f);P.RequestSwap(G.tutorialCourse.TrainingTarget);yield return new WaitForSeconds(.2f);
   P.cooldown=0;yield return Press(Key.Space);yield return new WaitForSeconds(.12f);Look(G.tutorialCourse.TrainingTarget.AimPoint,false);P.RequestSwap(G.tutorialCourse.TrainingTarget);yield return new WaitForSeconds(1);
   P.SetFeet(G.tutorialCourse.LaunchPoint);P.verticalSpeed=0;yield return new WaitForSeconds(.15f);G.Interact();yield return new WaitForSeconds(.35f);Look(G.anchors[0].AimPoint,false);P.RequestSwap(G.anchors[0]);yield return new WaitForSeconds(.2f);
   P.SetFeet(new Vector3(0,.03f,8));P.verticalSpeed=0;Look(G.tutorialCourse.TrainingTarget.AimPoint,false);P.tactics.ActivateShield();yield return new WaitForSeconds(2.7f);
   P.SetFeet(new Vector3(0,.03f,11));Look(G.tutorialCourse.TrainingTarget.AimPoint,false);P.combatTools.Melee();yield return new WaitForSeconds(.4f);P.SetFeet(new Vector3(0,.03f,10));Look(G.tutorialCourse.TrainingTarget.AimPoint,false);yield return new WaitForSeconds(.2f);
  }
  IEnumerator RecordKnife()
  {
   const string id="06-three-point-knife";if(!Need(id))yield break;yield return PrepareKnifeLesson();
   if(G.tutorialCourse.Step!=7){report.errors.Add("Knife preparation failed step="+G.tutorialCourse.Step);yield break;}
   var target=G.tutorialCourse.TrainingTarget;var anchor=G.tutorialCourse.RecallAnchor;bool thrown=false,swapped=false,recalled=false;
   yield return Record(id,5,"投刀、向侧面换位、改变真实回刀路径","三点教学练习；先前练习与定位在片段开始前完成，片段内仅正常投刀/换位/召回","自动实机演示 / 三点练习",t=>{
    Keys();if(!thrown){Look(target.AimPoint);if(t>.7f)thrown=P.combatTools.Throw();}
    else if(!swapped){Look(anchor.AimPoint);if(t>1.85f&&Vector3.Dot(P.Eye.forward,(anchor.AimPoint-P.Eye.position).normalized)>.99f)swapped=P.RequestSwap(anchor);}
    else{if(P.combatTools.Knife)Look(P.combatTools.Knife.transform.position);if(t>3.1f&&!recalled)recalled=P.combatTools.Recall();}
   });
  }
  IEnumerator RecordCover()
  {
   const string id="07-cover-combat";if(!Need(id))yield break;EndScenario();G.BeginExpedition(1);yield return new WaitForSeconds(.3f);P.SetLook(0,0);yield return Press(Key.W,2.4f);Look(G.expeditionMap.anchors[0].AimPoint,false);P.RequestSwap(G.expeditionMap.anchors[0]);yield return new WaitForSeconds(.3f);
   bool shield=false;
   yield return Record(id,9,"盾卫逼近、步枪探身，重新判断空间","第二章正常步行到断桥并换入战区后开始录制；没有清敌或健康覆盖","自动实机演示",t=>{
    Keys(t<2?new[]{Key.A}:t>4&&t<6?new[]{Key.D}:new Key[0]);
    var rifle=G.enemies.Find(e=>e&&e.Alive&&e.kind==EnemyKind.Assault);if(rifle)Look(rifle.AimPoint);
    if(t>1&&!shield)shield=P.tactics.ActivateShield();
    if(P.health<60)G.UseExpeditionMedkit();
    var melee=G.enemies.Find(e=>e&&e.Alive&&e.kind==EnemyKind.Shield);if(melee&&Vector3.Distance(P.transform.position,melee.transform.position)<4&&P.cooldown<=0){Look(melee.AimPoint);if(P.ValidateSwap(melee)=="可换位")P.RequestSwap(melee);}
    if(t>5)FireAt(rifle);
   });
  }
  IEnumerator RecordBossScenarios()
  {
   string[] ids={"09-boss-elbow","10-boss-attraction","11-boss-exchange","12-boss-laser","13-boss-wave","14-boss-omni"};
   BossSkill[] skills={BossSkill.SwapElbow,BossSkill.Attraction,BossSkill.AdvantageSwap,BossSkill.SweepLaser,BossSkill.GroundWave,BossSkill.OmniFire};
   for(int index=0;index<ids.Length;index++)
   {
    string id=ids[index];if(!Need(id))continue;EndScenario();G.BeginExpedition(11);G.qaSuppressAI=true;yield return new WaitForSeconds(3.5f);G.ClearDeployments();foreach(var e in G.enemies)if(e)e.suppressAI=true;
    Vector3 bossFeet=index==2?new Vector3(0,.03f,32):new Vector3(0,.03f,55);
    var enemy=EnemyActor.Spawn(EnemyKind.Elite,bossFeet,0);if(!enemy){report.errors.Add(id+" boss spawn failed");continue;}var boss=enemy.boss;
    // The selected phase is explicitly declared as setup, and is never presented as a played victory.
    if(index>0)enemy.Damage(BossEncounter.ShieldMaximum);if(index>=3)enemy.Damage(361);
    Vector3 player=index==0?new Vector3(-9,3.34f,44):index==4?new Vector3(-7,.03f,43):new Vector3(0,.03f,47);
    P.ResetAt(player);Look(enemy.AimPoint,false);var high=G.expeditionMap.anchors.Find(x=>x.transform.position.y>3&&x.transform.position.x<0);
    var pad=G.expeditionMap.nodes.Find(n=>n.kind==ExpeditionNodeKind.Launch&&n.transform.position.z>40&&n.transform.position.z<45);
    bool begun=false,counter=false;float beganAt=0;
    yield return Record(id,6,"指挥官 · "+new[]{"换位肘击","牵引场","夺取高点","扫地激光","撼地波","全向火力"}[index],"第12章真实场地，预设阶段与单技能时序；普通战斗参数，未展示虚构通关","Boss机制自动演示 / 预设阶段",t=>{
     Keys();if(P.health<55)G.UseExpeditionMedkit();
     if(!begun&&t>.4f){begun=boss.TryBeginSkill(skills[index]);beganAt=t;G.qaSuppressAI=false;}
     float age=t-beganAt;
     // Keep the elbow consequence visible: demonstrate the real hit near its marked landing point, without an automatic step off the platform.
     if(index==0)Look(enemy.AimPoint);
     else if(index==1||index==3){if(begun&&age>.65f&&!counter){Look(high.AimPoint);if(Vector3.Dot(P.Eye.forward,(high.AimPoint-P.Eye.position).normalized)>.99f)counter=P.RequestSwap(high);}else Look(enemy.AimPoint);}
     else if(index==2)Look(enemy.AimPoint);
     else if(index==4){if(begun&&age>1.3f&&!counter){Look(pad.Point,false);counter=G.Interact();}else Look(enemy.AimPoint);}
     else{Look(enemy.AimPoint);if(begun&&age>.7f&&!counter)counter=P.tactics.ActivateShield();if(age>3)FireAt(enemy);}
    });
   }
  }
 }
}
