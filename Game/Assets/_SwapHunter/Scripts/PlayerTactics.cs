using UnityEngine;
namespace SwapHunter {
[DefaultExecutionOrder(50)]
public sealed class PlayerTactics:MonoBehaviour {
 public const float ShieldDuration=4,ShieldCooldown=10;
 public float ShieldLeft=>barrier?Mathf.Max(0,barrier.Remaining):0;
 public float ShieldCooldownLeft{get;private set;}
 public int ShieldBlocks=>barrier?barrier.Blocks:0;
 public float CurrentShieldCooldown=>PlayerClassSkill.IsTacticalFlow?ClassSkillTuning.ShieldCooldown(G.expeditionStore.EquippedGeneralSkillLevel):ShieldCooldown;
 public Vector3 ShieldPosition=>barrier?barrier.transform.position:Vector3.zero;
 public bool ShieldActive=>barrier&&barrier.Remaining>0;
 PlayerMotor owner;DirectionalBarrier barrier;DemoGame G=>DemoGame.I;
 public void Initialize(PlayerMotor player){owner=player;}
 public void ResetCombat(){if(barrier)Destroy(barrier.gameObject);barrier=null;ShieldCooldownLeft=0;}
 void Update(){if(!owner||!G.IsPlaying||owner.health<=0)return;ShieldCooldownLeft=Mathf.Max(0,ShieldCooldownLeft-Time.deltaTime);if(G.Pressed(20))ActivateShield();}
 public bool ActivateShield(){
  if(owner&&owner.SwapActionsLocked)return false;
  if(PlayerClassSkill.IsTacticalFlow&&!G.expeditionStore.HasShield){G.Toast("当前未装配A盾",2);return false;}
  if(!G.IsPlaying||owner.health<=0||ShieldCooldownLeft>0||ShieldActive)return false;
  Vector3 normal=owner.Eye.forward.normalized,p=owner.Eye.position+normal*1.35f;
  if(Physics.CheckBox(p,new Vector3(1.35f,1.2f,.06f),Quaternion.LookRotation(normal),Layers.WorldMask,QueryTriggerInteraction.Ignore)){G.Toast("屏障展开空间被阻挡",1.5f);return false;}
  barrier=DirectionalBarrier.Spawn(p,normal,4,owner.transform);ShieldCooldownLeft=CurrentShieldCooldown;
  G.sound.Play("phase");G.Record("a_shield_open",p+" fixed direction "+normal);G.Toast("A盾固定展开 · 双方后向前可射，正面拦截",2);return true;
 }
 public bool Intercept(Vector3 from,Vector3 to,out Vector3 point)=>DirectionalBarrier.Intercept(from,to,out point);
 public bool BlockDirect(Vector3 from,Vector3 to)=>DirectionalBarrier.Intercept(from,to,out _);
 // Completed barriers are independent of the deployer's life, swap and silence.
}

public sealed partial class DemoGame {
 void DrawTacticsHud()
 {
  DrawDeploymentHud();
  if(tutorialCourse&&tutorialCourse.Active&&tutorialCourse.Step<5)return;
  if(!player||!player.tactics)return;
  var t=player.tactics;bool tactical=PlayerClassSkill.IsTacticalFlow;
  float panelX=24,panelY=viewWidth<1400?344:620,panelWidth=Mathf.Min(440,viewWidth*.38f),textX=panelX+16,textWidth=panelWidth-32;
  if(tactical)
  {
   DrawClassScanMarkers();
   Fill(new Rect(panelX,panelY,panelWidth,126),new Color(.015f,.03f,.045f,.76f));
   Text("临时护甲 "+player.TemporaryArmor.ToString("0"),new Rect(textX,panelY+7,180,22),14,Muted);
   float armorX=panelX+190,armorWidth=Mathf.Max(50,panelWidth-210);
   Fill(new Rect(armorX,panelY+18,armorWidth,4),new Color(.18f,.25f,.32f));
   Fill(new Rect(armorX,panelY+18,armorWidth*Mathf.Clamp01(player.TemporaryArmor/TacticalPrototypeRules.ArmorAmount),4),Cyan);
   var skill=player.classSkill;
   Text(KeyName(PlayerClassSkill.InputAction)+"  "+(skill?skill.AbilityName:"职业技能")+" · "+(skill?skill.Status:"不可用"),new Rect(textX,panelY+34,textWidth,27),16,skill&&skill.ActiveSeconds>0?Cyan:Muted);
   if(expeditionStore.EquippedGeneralSkill==TacticalPrototypeRules.KnifeGeneralIndex&&player.combatTools)
   {
    var tools=player.combatTools;
    Text(KeyName(22)+" 飞刀 "+tools.KnivesInHand+"/3 · "+KeyName(23)+" 全召回"+(tools.SilenceCooldownLeft>0?" · 沉默CD "+tools.SilenceCooldownLeft.ToString("0.0"):""),new Rect(textX,panelY+62,textWidth,40),16,Muted);
   }
   else
   {
    string shield=!expeditionStore.HasShield?"未装配":t.ShieldActive?"持续 "+t.ShieldLeft.ToString("F1")+"秒":t.ShieldCooldownLeft>0?"冷却 "+t.ShieldCooldownLeft.ToString("F1")+"秒":"就绪";
    Text(KeyName(20)+"  A盾 · "+shield,new Rect(textX,panelY+62,textWidth,27),16,t.ShieldActive?Cyan:Muted);
   }
   Text(KeyName(21)+" 近战 · 职业技能与通用技能独立",new Rect(textX,panelY+102,textWidth,22),14,Muted);
   return;
  }
  Fill(new Rect(panelX,panelY+54,panelWidth,72),new Color(.015f,.03f,.045f,.76f));
  string s=t.ShieldActive?"持续 "+t.ShieldLeft.ToString("F1")+"秒":t.ShieldCooldownLeft>0?"冷却 "+t.ShieldCooldownLeft.ToString("F1")+"秒":"就绪";
  Text(KeyName(20)+"  A盾 · "+s,new Rect(textX,panelY+60,textWidth,27),16,t.ShieldActive?Cyan:Muted);
  if(player.combatTools&&(!tutorialCourse||!tutorialCourse.Active||tutorialCourse.Step>=6))
  {
   var tools=player.combatTools;string toolText=KeyName(21)+" 近战";
   if(!tutorialCourse||!tutorialCourse.Active||tutorialCourse.Step>=7)toolText+="   "+KeyName(22)+" 飞刀 · "+tools.KnifeStatus+"   "+(tools.CanRecallKnife?KeyName(23)+" 召回":"研究 "+tools.KnifeRank+"/3");
   Text(toolText,new Rect(textX,panelY+91,textWidth,31),15,Muted);
  }
 }
 void DrawClassScanMarkers()
 {
  if(!player.classSkill||!player.classSkill.ScanActive)return;
  string[] arrows={"→","↗","↑","↖","←","↙","↓","↘"};
  foreach(var enemy in enemies)
  {
   if(!enemy||!enemy.Alive||Vector3.Distance(player.Eye.position,enemy.AimPoint)>ClassSkillTuning.ScanRange)continue;
   Vector3 v=player.cameraView.WorldToViewportPoint(enemy.AimPoint);
   if(v.z<=0||v.x<.03f||v.x>.97f||v.y<.04f||v.y>.96f)continue;
   Vector3 front=player.cameraView.WorldToViewportPoint(enemy.AimPoint+enemy.transform.forward);
   int direction=(Mathf.RoundToInt(Mathf.Atan2(front.y-v.y,front.x-v.x)*4/Mathf.PI)+8)%8;
   Vector2 point=ViewportToUi(v);
   Fill(new Rect(point.x-4,point.y-4,8,8),Cyan);
   Text("扫描 · "+enemy.Label+" "+arrows[direction],new Rect(point.x-105,point.y-32,210,24),15,Cyan,TextAnchor.MiddleCenter);
  }
 }
}
}
