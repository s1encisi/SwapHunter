using UnityEngine;
namespace SwapHunter
{
 public sealed partial class DemoGame
 {
  void ExpeditionToolsUI()
  {
   var p=expeditionStore.Profile;float width=(viewWidth-128)/3;
   string[] titles={"命中沉默","范围沉默","飞刀召回"};
   string[] rules={"飞刀命中后沉默 3.2 秒。\n打断指挥官特殊技能；\n限制掷弹兵投雷。\n普通移动、步枪和近战仍有效。","命中位置周围 4 米内的敌人\n同时沉默；实体墙体隔断传播。\n用一次投刀为穿越火线\n或换位创造窗口。","投出 → 移动或换位 → 召回。\n回刀沿真实路径造成 22 伤害，\n同一返回段每个敌人只受击一次。\n墙体挡住刀路，可走近拾回。"};
   for(int i=0;i<3;i++)
   {
    float x=48+i*(width+16);bool owned=p.knifeRank>i,next=p.knifeRank==i;int cost=ExpeditionStore.KnifeUpgradeCost(i);
    DrawUiPanel(new Rect(x,278,width,430),next);
    Text("0"+(i+1)+" / "+titles[i],new Rect(x+22,300,width-44,52),28,owned?Cyan:Paper);
    Text(owned?"已研究":next?"下一项研究":"先完成前置研究",new Rect(x+22,368,width-44,34),18,next?Amber:Muted);
    Text(rules[i],new Rect(x+22,425,width-44,165),20,Paper);
    bool enabled=GUI.enabled;GUI.enabled=enabled&&next&&p.credits>=cost;
    string label=owned?"已获得":!next?"前置尚未完成":p.credits<cost?"还需 "+(cost-p.credits)+" 研究点":"研究 / "+cost+" 点";
    if(Button(label,new Rect(x+22,621,width-44,54),next))ExpeditionAction(()=>expeditionStore.UpgradeKnife());
    GUI.enabled=enabled;
   }
   Text("基础投刀 28 伤害 · 近战 24 伤害并推开敌人 · A盾持续 4 秒 / 冷却 10 秒\n研究点也用于角色专精：优先控制、空间连击，或被动属性，由你选择。武器与配件可免费调整。",new Rect(49,740,viewWidth-98,75),19,Muted);
  }
 }
}
