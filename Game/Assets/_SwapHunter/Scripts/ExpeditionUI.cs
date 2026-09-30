using System;
using UnityEngine;
namespace SwapHunter
{
 public sealed partial class DemoGame
 {
  static readonly Color PhaseBlue=new Color(.40f,.78f,1),Paper=new Color(.94f,.96f,1),PanelBlue=new Color(.035f,.07f,.14f);
  void ExpeditionChrome(string title,string sub)
  {
   Fill(FullScreenGuiRect,new Color(.014f,.022f,.05f));
   Fill(new Rect(0,0,18,900),PhaseBlue);Fill(new Rect(viewWidth-240,0,240,900),new Color(.035f,.10f,.22f));
   Text("SWAPHUNTER / PHASE OPERATIONS",new Rect(48,29,650,30),16,PhaseBlue);
   Text(title,new Rect(44,75,viewWidth-300,66),45,Paper);
   Text(sub,new Rect(48,141,viewWidth-120,40),18,Muted);
  }
  void ExpeditionAction(Action action)
  {try{action();expeditionNotice="";sound.Play("confirm");}catch(Exception e){expeditionNotice=e.Message;sound.Play("warning");}}
  public void SetExpeditionTabForReview(int tab) { expeditionTab = Mathf.Clamp(tab, 0, 3); }
  public bool DrawExpeditionScreen()
  {
   if(expeditionInventory&&expeditionActive){ExpeditionInventoryUI();return true;}
   if(!expeditionBoard||state!=RunState.Menu)return false;
   ExpeditionChrome(expeditionTab==0?"相位行动":"补给商店", "战斗 → 带回研究点 → 选择成长与装配 → 下一段行动");
   if(Button("主菜单",new Rect(viewWidth-195,40,145,47))){expeditionBoard=false;return true;}
   if(expeditionStore==null)
   {Text(expeditionNotice,new Rect(50,250,viewWidth-100,220),24);return true;}
   var p=expeditionStore.Profile;
   Text("研究点  "+p.credits+"    |    进度 "+Array.FindAll(p.cleared,v=>v).Length+" / 12",new Rect(viewWidth-570,102,515,35),21,Paper,TextAnchor.MiddleRight);
   string[] tabs={"行动地图","武器装配","角色专精","战术工具"};
   for(int i=0;i<tabs.Length;i++)if(Button(tabs[i],new Rect(48+i*205,200,192,47),expeditionTab==i))expeditionTab=i;
   if(expeditionTab==0)ExpeditionLevelsUI();else if(expeditionTab==1)ExpeditionWeaponsUI();else if(expeditionTab==2)ExpeditionSpecializationUI();else ExpeditionToolsUI();
   if(!string.IsNullOrEmpty(expeditionNotice))Text(expeditionNotice,new Rect(49,840,viewWidth-100,46),18,new Color(1,.69f,.3f));
   return true;
  }
  void ExpeditionLevelsUI()
  {
   var p=expeditionStore.Profile;float leftW=Mathf.Min(870,viewWidth*.56f),tile=(leftW-24)/3;
   for(int i=0;i<12;i++)
   {
    var level=ExpeditionCatalog.Levels[i];float x=48+(i%3)*(tile+10),y=271+(i/3)*132;
    bool unlocked=i<=p.highestUnlocked;Fill(new Rect(x,y,tile,118),expeditionSelected==i?new Color(.055f,.24f,.48f):PanelBlue);
    Text((i+1).ToString("00")+"   "+(p.cleared[i]?"已完成":unlocked?"可部署":"未解锁"),new Rect(x+13,y+10,tile-26,26),15,unlocked?PhaseBlue:Muted);
    bool enabled=GUI.enabled;GUI.enabled=enabled&&unlocked;
    if(Button(level.name,new Rect(x+9,y+43,tile-18,46),expeditionSelected==i))expeditionSelected=i;
    GUI.enabled=enabled;
    Text(i<4?"引导 / LEARN":i<8?"进阶 / COMBINE":"挑战 / MASTER",new Rect(x+13,y+93,tile-22,22),13,Muted);
   }
   var selected=ExpeditionCatalog.Levels[expeditionSelected];float rx=leftW+80,rw=viewWidth-rx-48;
   Fill(new Rect(rx,271,rw,523),PanelBlue);
   Text((expeditionSelected+1).ToString("00")+" / "+selected.name,new Rect(rx+22,291,rw-44,50),29,Paper);
   Text(selected.lesson,new Rect(rx+22,353,rw-44,62),21,PhaseBlue);
   Text(selected.brief,new Rect(rx+22,427,rw-44,145),20,Paper);
   int reward=p.cleared[expeditionSelected]?ExpeditionCatalog.ReplayReward(expeditionSelected):selected.reward;
   Text((p.cleared[expeditionSelected]?"重玩":"首通")+"奖励  "+reward+" 研究点\n"+(expeditionSelected==0?"练习章 · 重玩5点；首通奖励不变":expeditionSelected>=3?"数据 +40 · 可带追兵成功撤离":"引导章节，无可选数据")+"\n全部授权后回桥开启；失败保留成长",new Rect(rx+22,590,rw-44,110),18,Muted);
   if(Button("部署行动   →",new Rect(rx+22,719,rw-44,54),true))BeginExpedition(expeditionSelected);
  }
  void ExpeditionWeaponsUI()
  {
   var p=expeditionStore.Profile;float leftW=Mathf.Min(870,viewWidth*.56f),tile=(leftW-24)/3;
   for(int i=0;i<12;i++)
   {
    float x=48+(i%3)*(tile+10),y=271+(i/3)*132;var w=WeaponCatalog.Get(i);
    Fill(new Rect(x,y,tile,118),expeditionSelectedWeapon==i?new Color(.055f,.24f,.48f):PanelBlue);
    Text((i+1).ToString("00")+" / "+(p.primary==i?"主武器":p.secondary==i?"副武器":p.weapons[i]?"已解锁":"关卡奖励"),new Rect(x+12,y+8,tile-24,24),14,p.weapons[i]?PhaseBlue:Muted);
    if(Button(w.name,new Rect(x+9,y+39,tile-18,45),expeditionSelectedWeapon==i))expeditionSelectedWeapon=i;
    Text("威力 "+w.damage+" · 弹匣 "+w.magazine,new Rect(x+12,y+92,tile-24,22),14,Muted);
   }
   int id=expeditionSelectedWeapon;var weapon=WeaponCatalog.Get(id);float rx=leftW+80,rw=viewWidth-rx-48;
   Fill(new Rect(rx,271,rw,523),PanelBlue);
   Text(weapon.name,new Rect(rx+22,290,rw-44,45),30,Paper);
   Text((weapon.pellets>1?"单丸伤害  "+weapon.damage+" × "+weapon.pellets:"基础伤害  "+weapon.damage)+"\n单发间隔  "+(1f/weapon.rate).ToString("F2")+" 秒 · 装填 "+weapon.reload+" 秒\n弹匣  "+weapon.magazine+"   备弹  "+weapon.reserve,new Rect(rx+22,349,rw-44,88),18,Muted);
   Text(weapon.description,new Rect(rx+22,440,rw-44,62),17,Paper);
   for(int s=0;s<2;s++)if(Button(s==0?"主武器槽":"副武器槽",new Rect(rx+22+s*(rw-44)/2,505,(rw-50)/2,45),expeditionWeaponSlot==s))expeditionWeaponSlot=s;
   bool enabled=GUI.enabled;GUI.enabled=enabled&&p.weapons[id];
   if(Button(p.weapons[id]?"装备到所选槽位":"完成对应章节解锁",new Rect(rx+22,565,rw-44,48),true))ExpeditionAction(()=>expeditionStore.Equip(expeditionWeaponSlot,id));
   int attachment=p.attachments[id];
   if(Button("组件："+ExpeditionCatalog.Attachments[attachment]+"  ›",new Rect(rx+22,628,rw-44,47)))ExpeditionAction(()=>expeditionStore.SetAttachment(id,(attachment+1)%4));
   GUI.enabled=enabled;
   Text(ExpeditionCatalog.AttachmentRules[attachment]+"\n配件免费调整；部署后锁定，本局不收费。",new Rect(rx+22,692,rw-44,83),18,Muted);
  }
  void ExpeditionSpecializationUI()
  {
   var p=expeditionStore.Profile;float width=(viewWidth-128)/3;
   string[] rules={"换位后 2.5 秒伤害提高。\n基础 +8%，每阶再 +8%。\n需要主动抓住换位后的窗口。","受伤降低，代价是移动略慢。\n基础减伤 5%，移速 −2%；\n每阶叠加同等幅度。\n携带 3 个医疗包。","移动与换位循环更快，但更脆弱。\n基础移速 +3%、冷却 −5%；\n受伤 +3%，每阶叠加同等幅度。"};
   for(int i=0;i<3;i++)
   {
    int spec=i;float x=48+i*(width+16);int rank=p.ranks[i];Fill(new Rect(x,280,width,485),p.specialization==i?new Color(.055f,.18f,.37f):PanelBlue);
    Text("0"+(i+1)+" / "+ExpeditionCatalog.Specializations[i],new Rect(x+22,305,width-44,50),31,Paper);
    Text("研究等级 "+rank+" / 3",new Rect(x+22,374,width-44,35),20,PhaseBlue);
    Text(rules[i],new Rect(x+22,429,width-44,175),20,Paper);
    if(Button(p.specialization==i?"已选择":"选择专精",new Rect(x+22,620,width-44,46),p.specialization==i))ExpeditionAction(()=>expeditionStore.SelectSpecialization(spec));
    bool enabled=GUI.enabled;GUI.enabled=enabled&&rank<3&&p.credits>=ExpeditionCatalog.RankCost(rank);
    if(Button(rank>=3?"研究完成":"研究下一阶 / "+ExpeditionCatalog.RankCost(rank),new Rect(x+22,687,width-44,46)))ExpeditionAction(()=>expeditionStore.Upgrade(spec));
    GUI.enabled=enabled;
   }
   Text("仅当前专精生效 · 可免费切换 · 研究点来自章节首通、重玩与成功带出的数据",new Rect(50,795,viewWidth-100,36),19,Muted);
  }
  void ExpeditionInventoryUI()
  {
   ExpeditionChrome("战术背包","行动已暂停 / 任务物品使用独立空间 / 装配在部署前调整");
   float width=(viewWidth-144)/2;
   for(int i=0;i<2;i++)
   {
    int id=player.EquippedWeapons[i];var w=WeaponCatalog.Get(id);float x=48+i*(width+48);
    Fill(new Rect(x,235,width,213),PanelBlue);Text(i==0?"PRIMARY / 主武器":"SECONDARY / 副武器",new Rect(x+22,252,width-44,30),16,PhaseBlue);
    Text(w.name,new Rect(x+22,302,width-44,45),31);Text("弹匣 "+player.ammo[id]+" / "+w.magazine+"     备弹 "+player.reserve[id],new Rect(x+22,372,width-44,36),23,Muted);
   }
   Fill(new Rect(48,480,viewWidth-96,184),PanelBlue);
   Text("医疗包 ×"+expeditionMedkits+"     |     弹药包 ×"+expeditionAmmoPacks,new Rect(72,501,viewWidth-140,42),26);
   bool enabled=GUI.enabled;bool canHeal=expeditionMedkits>0&&player.health>0&&player.health<config.playerHealth;
   GUI.enabled=enabled&&canHeal;
   if(Button(expeditionMedkits<=0?"无医疗包":player.health>=config.playerHealth?"生命已满":"使用医疗包 / 恢复40生命",new Rect(72,562,345,55)))UseExpeditionMedkit();
   bool canAmmo=false;foreach(int id in player.EquippedWeapons)if(player.reserve[id]<ExpeditionReserveLimit(id))canAmmo=true;
   GUI.enabled=enabled&&expeditionAmmoPacks>0&&canAmmo;
   if(Button(expeditionAmmoPacks<=0?"无弹药包":!canAmmo?"备用弹药已满":"使用弹药包 / 补充两匣",new Rect(445,562,345,55)))UseExpeditionAmmo();
   GUI.enabled=enabled;
   Text("任务物品："+(expeditionBonus?"已携带数据，可带追兵撤离兑现":"未携带额外数据")+"\n授权 "+expeditionObjectives+" / "+expeditionMap.objectiveCount+" · 必需敌人 "+ExpeditionRequiredEnemies+" · 数据追兵 "+ExpeditionOptionalEnemies,new Rect(72,691,viewWidth-145,70),21,Muted);
   if(Button("返回行动   /   "+KeyName(18),new Rect(viewWidth-370,793,320,55),true)){expeditionInventory=false;SetState(RunState.Playing);}
  }
  public void DrawExpeditionOverlay()
  {
   float w=Mathf.Min(800,viewWidth-120),x=(viewWidth-w)/2;Fill(FullScreenGuiRect,new Color(.005f,.015f,.04f,.88f));Fill(new Rect(x,130,w,655),PanelBlue);
   if(expeditionPending)
   {
    Text("结算保存受阻",new Rect(x+34,167,w-68,60),38);Text(expeditionNotice,new Rect(x+34,265,w-68,185),22,Muted);
    if(Button("重试保存原结算",new Rect(x+34,533,w-68,60),true))FinishExpedition(expeditionPendingSuccess);
    return;
   }
   if(state==RunState.Paused)
   {
    Text("行动暂停",new Rect(x+34,167,w-68,60),42);Text(ExpeditionTitle+" / "+ExpeditionObjective,new Rect(x+34,257,w-68,105),21,Muted);
    if(Button("继续行动",new Rect(x+34,391,w-68,56),true))SetState(RunState.Playing);
    if(Button("战术背包",new Rect(x+34,461,w-68,52)))expeditionInventory=true;
    if(Button("设置",new Rect(x+34,527,w-68,52)))OpenSettings();
    if(Button(expeditionAbandonConfirm?"确认放弃 / 本局无收益":"放弃行动 / 保留永久成长",new Rect(x+34,619,w-68,52)))
    {if(expeditionAbandonConfirm)FinishExpedition(false);else expeditionAbandonConfirm=true;}
    return;
   }
   bool success=expeditionReceipt!=null&&expeditionReceipt.success;
   Text(success?(expeditionLevel==11?"封锁解除":"行动完成"):"行动中止",new Rect(x+34,165,w-68,63),42,success?Paper:new Color(1,.6f,.3f));
   Text(ExpeditionTitle+" / "+FormatTime(runSeconds),new Rect(x+34,245,w-68,40),23,PhaseBlue);
   Text(success?ExpeditionCatalog.Levels[expeditionLevel].ending:"永久武器与专精保留。本关从入口重新部署；可以先调整双武器和专精。",new Rect(x+34,307,w-68,126),23,Paper);
   Text("研究点 +"+(expeditionReceipt==null?0:expeditionReceipt.reward)+"    |    击杀 "+totalKills+"    |    换位 "+totalSwaps+"\n"+(success&&expeditionReceipt.first?"首通奖励已保存":"本次结算已保存"),new Rect(x+34,460,w-68,77),21,Muted);
   if(success&&expeditionLevel<11)
   {if(Button("下一段行动   →",new Rect(x+34,575,w-68,58),true))BeginExpedition(expeditionLevel+1);}
   else if(!success&&Button("重新部署本关",new Rect(x+34,575,w-68,58),true))BeginExpedition(expeditionLevel);
   if(Button("进入补给商店 / 调整下一场配置",new Rect(x+34,651,w-68,54))){OpenExpeditionBoard();expeditionTab=3;}
  }
 }
}






