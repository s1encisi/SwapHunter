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
   if(TacticalExpedition&&ExpeditionAwaitingRetry){DrawExpeditionOverlay();return true;}
   if(expeditionInventory&&expeditionActive){ExpeditionInventoryUI();return true;}
   if(!expeditionBoard||state!=RunState.Menu)return false;
   string title=expeditionTab==0?"相位行动":expeditionTab==1?"武器装配":TacticalExpedition?(expeditionTab==2?"职业技能":"补给采购"):"补给商店";
   ExpeditionChrome(title,TacticalExpedition?TacticalPrototypeRules.ClassNames[expeditionStore.Profile.activeClass]+" · 关外整备与采购 → 带着出发配置行动 → 成功结算信用点":"战斗 → 带回研究点 → 选择成长与装配 → 下一段行动");
   if(Button("主菜单",new Rect(viewWidth-195,40,145,47))){expeditionBoard=false;return true;}
   if(expeditionStore==null)
   {Text(expeditionNotice,new Rect(50,250,viewWidth-100,220),24);return true;}
   var p=expeditionStore.Profile;
   Text((TacticalExpedition?"信用点  ":"研究点  ")+p.credits+"    |    进度 "+Array.FindAll(p.cleared,v=>v).Length+" / 12",new Rect(viewWidth-570,102,515,35),21,Paper,TextAnchor.MiddleRight);
   string[] tabs=TacticalExpedition?new[]{"行动地图","武器装配","职业技能","补给采购"}:new[]{"行动地图","武器装配","角色专精","战术工具"};
   for(int i=0;i<tabs.Length;i++)if(Button(tabs[i],new Rect(48+i*205,200,192,47),expeditionTab==i))expeditionTab=i;
   if(expeditionTab==0)ExpeditionLevelsUI();else if(expeditionTab==1)ExpeditionWeaponsUI();
   else if(expeditionTab==2){if(TacticalExpedition)ExpeditionAssaultSkillsUI();else ExpeditionSpecializationUI();}
   else{if(TacticalExpedition)ExpeditionProcurementUI();else ExpeditionToolsUI();}
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
   var selected=ExpeditionCatalog.Levels[expeditionSelected];var definition=MissionCatalog.Get(expeditionSelected);float rx=leftW+80,rw=viewWidth-rx-48;
   Fill(new Rect(rx,271,rw,523),PanelBlue);
   Text((expeditionSelected+1).ToString("00")+" / "+selected.name,new Rect(rx+22,291,rw-44,50),29,Paper);
   Text(selected.lesson,new Rect(rx+22,353,rw-44,62),21,PhaseBlue);
   Text(definition.instruction,new Rect(rx+22,427,rw-44,145),20,Paper);
   int reward=p.cleared[expeditionSelected]?ExpeditionCatalog.ReplayReward(expeditionSelected):selected.reward;
   string bonusRule=expeditionSelected<3?definition.bonusRisk:"额外数据 +40（成功撤离） · "+definition.bonusRisk;
   if(TacticalExpedition)
   {
    reward=TacticalPrototypeRules.BaseReward(expeditionSelected)+(p.cleared[expeditionSelected]?0:TacticalPrototypeRules.FirstClearExtra(expeditionSelected));
    bonusRule=expeditionSelected<3?definition.bonusRisk:"可选目标 +"+TacticalPrototypeRules.OptionalReward+" 信用点（成功撤离）";
    Text("成功奖励 "+reward+" 信用点 · "+bonusRule+"\n撤离："+definition.extraction+"\n失败或主动退出恢复出发状态；允许不带道具。",new Rect(rx+22,578,rw-44,128),17,Muted);
    if(!expeditionStore.HasCurrentClassStarter||expeditionStore.EquippedClassSkill<0||expeditionStore.EquippedGeneralSkill<0)
    {if(Button("选择职业技能与入门装备",new Rect(rx+22,719,rw-44,54),true))expeditionTab=2;}
    else if(Button("部署行动   →",new Rect(rx+22,719,rw-44,54),true))BeginExpedition(expeditionSelected);
   }
   else
   {
    Text((p.cleared[expeditionSelected]?"重玩":"首通")+"奖励  "+reward+" 研究点\n"+bonusRule+"\n撤离："+definition.extraction+"；失败保留成长",new Rect(rx+22,580,rw-44,125),17,Muted);
    if(Button("部署行动   →",new Rect(rx+22,719,rw-44,54),true))BeginExpedition(expeditionSelected);
   }
  }
  void ExpeditionWeaponsUI() { DrawWeaponLibrary(); }
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
   if(TacticalExpedition){DrawTacticalInventory();return;}
   ExpeditionChrome("战术背包","行动已暂停 / 任务物品使用独立空间 / 装配在部署前调整");
   float width=(viewWidth-144)/2;
   for(int i=0;i<2;i++)
   {
    DrawInventoryWeaponCard(i, new Rect(48+i*(width+48),235,width,213));
   }
   Fill(new Rect(48,480,viewWidth-96,184),PanelBlue);
   Text(TacticalExpedition?"本次医疗包 ×"+expeditionMedkits+"    |    "+TacticalAttemptSummary():"医疗包 ×"+expeditionMedkits+"     |     弹药包 ×"+expeditionAmmoPacks,new Rect(72,501,viewWidth-140,42),26);
   bool enabled=GUI.enabled;bool canHeal=expeditionMedkits>0&&player.health>0&&player.health<config.playerHealth;
   GUI.enabled=enabled&&canHeal;
   string healText=TacticalExpedition?"使用医疗包 / 恢复"+TacticalPrototypeRules.MedkitHeal.ToString("0.#")+"生命":"使用医疗包 / 恢复40生命";
   if(Button(expeditionMedkits<=0?"无医疗包":player.health>=config.playerHealth?"生命已满":healText,new Rect(72,562,345,55)))UseExpeditionMedkit();
   GUI.enabled=enabled;
   if(TacticalExpedition)Text("弹药包与应急护甲：后续开放\n场地补给只用于本次行动",new Rect(445,561,viewWidth-520,64),18,Muted);
   else
   {
    bool canAmmo=false;foreach(int id in player.EquippedWeapons)if(player.reserve[id]<ExpeditionReserveLimit(id))canAmmo=true;
    GUI.enabled=enabled&&expeditionAmmoPacks>0&&canAmmo;
    if(Button(expeditionAmmoPacks<=0?"无弹药包":!canAmmo?"备用弹药已满":"使用弹药包 / 补充两匣",new Rect(445,562,345,55)))UseExpeditionAmmo();
    GUI.enabled=enabled;
   }
   var definition=mission!=null?mission.Definition:MissionCatalog.Get(expeditionLevel);
   int collected=mission!=null?mission.CompletedObjectives:expeditionObjectives;
   int required=definition.requiredObjectives;
   string progress=required>0?"主目标 "+Mathf.Min(collected,required)+" / "+required:"无终端目标 · 解决指定威胁";
   if(definition.objectives.Length>required)progress+=" · 额外目标 "+Mathf.Max(0,collected-required)+" / "+(definition.objectives.Length-required);
   Text("任务物品："+(expeditionBonus?"已携带额外数据，成功撤离 +40":"未携带额外数据")+"\n"+progress+" · 指定必清 "+ExpeditionRequiredEnemies+" · 可遗留敌人 "+ExpeditionOptionalEnemies,new Rect(72,691,viewWidth-145,70),21,Muted);
   if(Button("返回行动   /   "+KeyName(18),new Rect(viewWidth-370,793,320,55),true)){expeditionInventory=false;SetState(RunState.Playing);}
  }
  public void DrawExpeditionOverlay()
  {
   if(TacticalExpedition){DrawTacticalExpeditionOverlay();return;}
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

  string TacticalAttemptSummary()
  {
   var deployment=expeditionStore.Profile.deployment;
   return deployment==null?"":"已尝试 "+deployment.attempts+" 次 · 死亡 "+deployment.deaths+" 次";
  }
  void ExpeditionAssaultSkillsUI()
  {
   var store=expeditionStore;int cls=store.Profile.activeClass;float left=48,classWidth=220,portraitX=288,portraitWidth=248,detailX=560,detailWidth=viewWidth-detailX-48;
   string[] roles={"近距突入 / 震荡与机动","正面压制 / 阵地与装填","观察点杀 / 扫描与精确射击","范围控制 / 爆破与区域封锁"};
   for(int i=0;i<4;i++)
   {
    int selected=i;float y=278+i*83;
    if(Button(TacticalPrototypeRules.ClassNames[i],new Rect(left,y,classWidth,45),cls==i))ExpeditionAction(()=>store.SelectClass(selected));
    Text(roles[i],new Rect(left+6,y+49,classWidth-12,23),13,Muted);
   }
   Text("入门武器",new Rect(left,625,classWidth,28),19,Paper);
   Text("脉冲卡宾枪 +\n"+WeaponCatalog.Get(TacticalPrototypeRules.StarterWeapon(cls)).name,new Rect(left,663,classWidth,62),17,Muted);
   bool enabled=GUI.enabled;GUI.enabled=enabled&&!store.HasWeaponStarter;
   if(Button(store.HasWeaponStarter?"入门武器已领取":"免费领取本职业武器",new Rect(left,744,classWidth,46),true))ExpeditionAction(()=>store.ClaimWeaponStarter());GUI.enabled=enabled;
   DrawUiPanel(new Rect(portraitX,278,portraitWidth,512),true);
   Text(TacticalPrototypeRules.ClassNames[cls],new Rect(portraitX+12,293,portraitWidth-24,34),25,Paper,TextAnchor.MiddleCenter);
   if(classPreview&&classPreview.Texture)GUI.DrawTexture(new Rect(portraitX+10,335,portraitWidth-20,376),classPreview.Texture,ScaleMode.ScaleToFit);
   else Text(classPreview&&!string.IsNullOrEmpty(classPreview.Error)?"角色预览暂不可用":"角色模型准备中",new Rect(portraitX+12,450,portraitWidth-24,40),17,Muted,TextAnchor.MiddleCenter);
   if(Button("左转",new Rect(portraitX+12,727,67,36))&&classPreview)classPreview.Rotate(-30);
   if(Button("复位",new Rect(portraitX+90,727,67,36))&&classPreview)classPreview.ResetView();
   if(Button("右转",new Rect(portraitX+168,727,67,36))&&classPreview)classPreview.Rotate(30);
   Text("职业技能 · 选择一项装配",new Rect(detailX,278,detailWidth,34),24,Paper);
   Text("按 "+KeyName(24)+" 使用；首次任选一项免费，第二项可独立购买。",new Rect(detailX,319,detailWidth,28),16,Muted);
   float card=(detailWidth-16)/2;
   for(int i=0;i<2;i++)DrawSkillCard(cls*2+i,false,new Rect(detailX+i*(card+16),356,card,224));
   Text("通用战术 · A盾与完整飞刀套组二选一",new Rect(detailX,596,detailWidth,33),22,Paper);
   for(int i=0;i<2;i++)DrawSkillCard(i,true,new Rect(detailX+i*(card+16),639,card,167));
  }
  void DrawSkillCard(int index,bool general,Rect rect)
  {
   var store=expeditionStore;int level=store.SkillLevel(index,general);
   bool selected=(general?store.EquippedGeneralSkill:store.EquippedClassSkill)==index;
   bool guarantee=general?!store.HasGeneralStarter:!store.HasSkillStarter;
   string name=(general?TacticalPrototypeRules.GeneralSkillNames:TacticalPrototypeRules.ClassSkillNames)[index];
   DrawUiPanel(rect,selected);Text(name+"  "+(level>0?level+" / 3":"未拥有"),new Rect(rect.x+14,rect.y+12,rect.width-28,28),20,selected?PhaseBlue:Paper);
   string description=general?(index==0?"固定方向屏障 · 持续4秒\n冷却 "+(10-Mathf.Max(0,level-1))+" 秒":"1级投出与召回，2级命中沉默\n3级范围沉默；墙体与实体盾阻挡"):ClassSkillTuning.Describe(index,Mathf.Max(1,level));
   Text(description,new Rect(rect.x+14,rect.y+49,rect.width-28,general?51:102),16,Muted);
   float y=rect.yMax-53,w=(rect.width-36)/2;bool enabled=GUI.enabled;
   int cost=level==0?TacticalPrototypeRules.SkillBasePrice(index,general):level<3?TacticalPrototypeRules.SkillUpgradePrice(level):0;
   GUI.enabled=enabled&&(guarantee||(level<3&&store.Profile.credits>=cost));
   string purchase=guarantee?"领取保障":level==0?"购买 / "+cost:level<3?"强化 / "+cost:"已满级";
   if(Button(purchase,new Rect(rect.x+12,y,w,37),guarantee))ExpeditionAction(()=>{
    if(guarantee){if(general)store.ClaimGeneralStarter(index);else store.ClaimSkillStarter(index);}
    else if(level==0)store.BuySkill(Guid.NewGuid().ToString("N"),index,general);
    else store.UpgradeSkill(Guid.NewGuid().ToString("N"),index,general);
   });
   GUI.enabled=enabled&&level>0&&!selected;
   if(Button(selected?"已装配":"装配",new Rect(rect.x+24+w,y,w,37),selected))ExpeditionAction(()=>{if(general)store.EquipGeneralSkill(index);else store.EquipClassSkill(index);});
   GUI.enabled=enabled;
  }
  void ExpeditionProcurementUI()
  {
   var store=expeditionStore;float width=(viewWidth-128)/3;bool enabled=GUI.enabled;
   for(int i=0;i<3;i++)
   {
    GUI.enabled=enabled;int type=i;float x=48+i*(width+16);int price=TacticalPrototypeRules.ConsumablePrice(i);
    DrawUiPanel(new Rect(x,278,width,234),true);
    Text(TacticalPrototypeRules.ConsumableNames[i],new Rect(x+18,295,width-36,35),27,Paper);
    Text(ConsumableDescription(i),new Rect(x+18,343,width-36,47),18,Muted);
    Text(price+" 信用点 / 个    库存 "+store.ConsumableStock(i),new Rect(x+18,399,width-36,30),20,PhaseBlue);
    GUI.enabled=enabled&&store.Profile.credits>=price;
    if(Button(store.Profile.credits>=price?"购买 1 个":"信用点不足",new Rect(x+18,450,width-36,42),true))ExpeditionAction(()=>store.BuyConsumable(Guid.NewGuid().ToString("N"),type));
   }
   GUI.enabled=enabled;
   Text("出发携带 · 两槽不同类型 · 库存包含带入关卡的道具",new Rect(48,533,viewWidth-96,31),22,Paper);
   var loadout=store.Profile.classLoadouts[store.Profile.activeClass];float slotWidth=(viewWidth-112)/2;
   for(int i=0;i<2;i++)
   {
    GUI.enabled=enabled;int slot=i,type=loadout.itemTypes[i],count=loadout.itemCounts[i];float x=48+i*(slotWidth+16);
    DrawUiPanel(new Rect(x,579,slotWidth,189));
    Text("道具槽 "+(i+1)+" / "+(type<0?"空":TacticalPrototypeRules.ConsumableNames[type]),new Rect(x+16,592,slotWidth-32,31),21,Paper);
    float buttonWidth=(slotWidth-50)/4;
    for(int k=-1;k<3;k++)
    {
     int choice=k;GUI.enabled=enabled&&choice!=type&&(choice<0||loadout.itemTypes[1-slot]!=choice);
     if(Button(choice<0?"空槽":TacticalPrototypeRules.ConsumableNames[choice],new Rect(x+16+(k+1)*(buttonWidth+6),637,buttonWidth,36),choice==type))
      ExpeditionAction(()=>store.SetConsumableSlot(slot,choice,choice<0?0:1));
    }
    GUI.enabled=enabled&&type>=0&&count>0;
    if(Button("－",new Rect(x+16,697,42,38)))ExpeditionAction(()=>store.SetConsumableSlot(slot,count<=1?-1:type,Mathf.Max(0,count-1)));
    GUI.enabled=enabled;int actual=type<0?0:Mathf.Min(count,store.ConsumableStock(type));
    Text("预设 "+count+" / 预计带入 "+actual,new Rect(x+75,703,slotWidth-145,30),18,Muted,TextAnchor.MiddleCenter);
    GUI.enabled=enabled&&type>=0&&count<TacticalPrototypeRules.ConsumableLimit(type);
    if(Button("＋",new Rect(x+slotWidth-58,697,42,38)))ExpeditionAction(()=>store.SetConsumableSlot(slot,type,count+1));
   }
   GUI.enabled=enabled;
   Text("失败重试或退出返还本轮道具；成功仅扣实际消耗。空库存也可部署，基础生命与规定弹药免费。",new Rect(48,787,viewWidth-96,39),17,Muted);
  }
  string ConsumableDescription(int type)
  {
   if(type==0)return "恢复 "+TacticalPrototypeRules.MedkitHeal.ToString("0")+" 生命 · 上限2个";
   if(type==1)return "双枪各补 "+TacticalPrototypeRules.AmmoMagazineCount+" 匣备弹 · 上限1个";
   return "获得 "+TacticalPrototypeRules.ArmorAmount.ToString("0")+" 临时护甲 · 未耗尽不叠加";
  }
  void DrawTacticalInventory()
  {
   ExpeditionChrome("战术背包","行动已暂停 · 配置在出发前冻结 · "+TacticalAttemptSummary());
   float width=(viewWidth-144)/2;
   for(int i=0;i<2;i++)DrawInventoryWeaponCard(i,new Rect(48+i*(width+48),235,width,213));
   var snapshot=expeditionStore.Profile.deployment;
   int[] remaining={expeditionMedkits,expeditionAmmoPacks,expeditionArmorPacks};
   for(int i=0;i<2;i++)
   {
    float x=48+i*(width+48);int type=snapshot.itemTypes[i];
    DrawUiPanel(new Rect(x,479,width,217),true);
    Text(type<0?"空道具槽":TacticalPrototypeRules.ConsumableNames[type]+" × "+remaining[type],new Rect(x+20,497,width-40,40),27,Paper);
    if(type<0){Text("关外可选择两种不同道具",new Rect(x+20,556,width-40,35),18,Muted);continue;}
    Text(ConsumableDescription(type),new Rect(x+20,549,width-40,53),18,Muted);
    bool useful=type==0?player.health<config.playerHealth:type==2?player.TemporaryArmor<=0:false;
    if(type==1)foreach(int id in player.EquippedWeapons)if(player.reserve[id]<ExpeditionReserveLimit(id))useful=true;
    bool enabled=GUI.enabled;GUI.enabled=enabled&&remaining[type]>0&&useful&&!expeditionPending;
    string reason=remaining[type]<=0?"已用完":!useful?(type==0?"生命已满":type==1?"备弹已满":"现有护甲未耗尽"):"使用";
    if(Button(reason,new Rect(x+20,626,width-40,46),true)){if(type==0)UseExpeditionMedkit();else if(type==1)UseExpeditionAmmo();else UseExpeditionArmor();}
    GUI.enabled=enabled;
   }
   Text("当前护甲 "+player.TemporaryArmor.ToString("0")+" · "+ExpeditionObjective,new Rect(50,720,viewWidth-100,47),19,Muted);
   if(Button("返回行动 / "+KeyName(18),new Rect(viewWidth-370,793,320,55),true)){expeditionInventory=false;SetState(RunState.Playing);}
  }
  void TacticalReturnToPreparation()
  {
   if(!AbandonExpedition())return;
   OpenExpeditionBoard();expeditionTab=0;
  }
  void DrawTacticalExpeditionOverlay()
  {
   float w=Mathf.Min(800,viewWidth-120),x=(viewWidth-w)/2;
   Fill(FullScreenGuiRect,new Color(.005f,.015f,.04f,.88f));Fill(new Rect(x,130,w,655),PanelBlue);
   if(expeditionPending)
   {
    Text("结算保存受阻",new Rect(x+34,167,w-68,60),38);
    Text("原出发记录仍保留。保存完成前不会开启新的部署。\n\n"+expeditionNotice,new Rect(x+34,258,w-68,230),22,Muted);
    if(Button("重试保存原结算",new Rect(x+34,558,w-68,60),true))
    {if(expeditionPendingSuccess)FinishExpedition(true);else TacticalReturnToPreparation();}
    return;
   }
   if(ExpeditionAwaitingRetry)
   {
    Text("行动失败",new Rect(x+34,166,w-68,60),42,Amber);
    Text(ExpeditionTitle+" · "+TacticalAttemptSummary(),new Rect(x+34,247,w-68,64),22,PhaseBlue);
    Text("继续本次部署：使用同一出发快照，恢复出发时的生命、弹药与随身道具。\n\n本轮未结算收益不保留；武器、配件与携带配置仍锁定。也可退出准备后重新整备。",new Rect(x+34,336,w-68,156),22,Paper);
    if(Button("继续本次部署 / 恢复出发状态",new Rect(x+34,542,w-68,57),true))RetryExpedition();
    if(Button("退出并返回准备",new Rect(x+34,615,w-68,54)))TacticalReturnToPreparation();
    if(!string.IsNullOrEmpty(expeditionNotice))Text(expeditionNotice,new Rect(x+34,701,w-68,61),17,Amber);
    return;
   }
   if(state==RunState.Paused)
   {
    Text("行动暂停",new Rect(x+34,164,w-68,60),42);
    Text(ExpeditionTitle+" / "+ExpeditionObjective+"\n"+TacticalAttemptSummary(),new Rect(x+34,254,w-68,120),21,Muted);
    if(Button("继续行动",new Rect(x+34,406,w-68,52),true)){expeditionAbandonConfirm=false;SetState(RunState.Playing);}
    if(Button("战术背包",new Rect(x+34,474,w-68,49))){expeditionAbandonConfirm=false;expeditionInventory=true;}
    if(Button("设置",new Rect(x+34,539,w-68,49)))OpenSettings();
    if(Button(expeditionAbandonConfirm?"确认退出 / 恢复出发状态，本轮无收益":"退出并返回准备",new Rect(x+34,632,w-68,52)))
    {if(expeditionAbandonConfirm)TacticalReturnToPreparation();else expeditionAbandonConfirm=true;}
    if(!string.IsNullOrEmpty(expeditionNotice))Text(expeditionNotice,new Rect(x+34,717,w-68,51),17,Amber);
    return;
   }
   var receipt=expeditionReceipt;bool success=receipt!=null&&receipt.success;
   Text(success?(expeditionLevel==11?"封锁解除":"行动完成"):"行动已中止",new Rect(x+34,165,w-68,63),42,success?Paper:Amber);
   Text(ExpeditionTitle+" / "+FormatTime(runSeconds),new Rect(x+34,245,w-68,40),23,PhaseBlue);
   Text(success?"奖励已计入共享信用点，随身道具仅扣本次实际消耗。回到整备页可采购新上架武器、强化技能并选择下一场配置。":receipt!=null?"已恢复出发时的库存与装配，本次无收益。返回准备后可以重新整备。":"尚未保存退出结果。请重试退出保存，原出发记录保留。",new Rect(x+34,310,w-68,118),22,Paper);
   if(receipt!=null)
    Text("信用点 +"+receipt.reward+"（行动 "+receipt.baseReward+" + 首通 "+receipt.firstReward+" + 可选 "+receipt.bonusReward+"）\n道具消耗 医疗 "+receipt.medkitsUsed+" / 弹药 "+receipt.ammoUsed+" / 护甲 "+receipt.armorUsed+" · 尝试 "+receipt.attempts+" 次 · 死亡 "+receipt.deaths+" 次",new Rect(x+34,457,w-68,105),20,Muted);
   if(success&&expeditionLevel<11&&Button("查看下一段行动",new Rect(x+34,594,w-68,54),true))
   {OpenExpeditionBoard();expeditionSelected=expeditionLevel+1;expeditionTab=0;}
   if(Button(receipt==null?"重试退出并返回准备":"返回准备 / 整备与补给",new Rect(x+34,668,w-68,54),!success))
   {if(receipt==null)TacticalReturnToPreparation();else{OpenExpeditionBoard();expeditionTab=3;}}
   if(!string.IsNullOrEmpty(expeditionNotice))Text(expeditionNotice,new Rect(x+34,739,w-68,40),16,Amber);
  }

 }
}






