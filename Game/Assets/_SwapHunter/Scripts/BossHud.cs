using UnityEngine;
namespace SwapHunter {
public sealed partial class DemoGame {
 void DrawBossHud(){
  BossEncounter b=null;foreach(var e in enemies)if(e&&e.Alive&&e.boss){b=e.boss;break;}if(!b)return;
  float x=viewWidth-610;
  DrawUiPanel(new Rect(x,130,560,116),true);
  string phase=b.Phase==BossPhase.EnergyMelee?"I / 能量盾 · 近战压迫":b.Phase==BossPhase.Rifle?"II / 破盾 · 步枪形态":"III / 核心失稳";
  Text("封锁指挥官   "+phase,new Rect(x+14,137,530,26),18,Color.white);
  Fill(new Rect(x+14,170,390,10),new Color(.12f,.18f,.22f));Fill(new Rect(x+14,170,390*b.Energy/BossEncounter.ShieldMaximum,10),Cyan);
  Text("能量 "+Mathf.CeilToInt(b.Energy),new Rect(x+417,160,130,24),16,Cyan);
  Fill(new Rect(x+14,195,390,9),new Color(.18f,.13f,.12f));Fill(new Rect(x+14,195,390*b.Owner.health/BossEncounter.HealthMaximum,9),Amber);
  Text("HP "+Mathf.CeilToInt(b.Owner.health),new Rect(x+417,186,130,24),16,Amber);
  string silence=b.Owner.Silenced?"沉默 "+b.Owner.silenceLeft.ToString("F1")+"秒 · 特殊技能封锁":b.SilenceResistanceLeft>0?"沉默抗性 "+b.SilenceResistanceLeft.ToString("F1")+"秒 · 暂时免疫沉默":"飞刀可打断特殊技能";
  Text(silence,new Rect(x+14,216,530,24),16,b.SilenceResistanceLeft>0?Amber:Cyan);
  if(!string.IsNullOrEmpty(b.Warning))Text(b.Warning,new Rect(x,256,560,60),19,Amber,TextAnchor.UpperRight);
 }
}}
