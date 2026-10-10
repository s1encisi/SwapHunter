using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
namespace SwapHunter
{
 [Serializable] public sealed class ExpeditionLevel
 {
  public int id,reward,weaponUnlock;
  // Retained only for source compatibility with historical QA; v0.6 uses encounter groups.
  public int waves;
  public string name,lesson,brief,ending;
  public ExpeditionLevel(int i,string n,string l,string b,string e,int w,int waves)
  {id=i;name=n;lesson=l;brief=b;ending=e;weaponUnlock=w;this.waves=waves;reward=80+i*15;}
 }
 public static class ExpeditionCatalog
 {
  public static readonly ExpeditionLevel[] Levels={
   new ExpeditionLevel(0,"相位校准","夺位 · 双向换位","在安全区完成一次换位，再解决侧台守卫，从前方出口离开。地面侧路与楼梯始终可用。","相位校准完成。下一段盾线需要先选择自己的原站位。",4,0),
   new ExpeditionLevel(1,"盾线缺口","朝向 · 双场盾线","解决两名指定盾卫，穿过货检闸口。第二场先观察侧向突击兵，换位保留双方朝向。","盾线突破。三连发步枪已解锁。",2,0),
   new ExpeditionLevel(2,"垂直通路","上行路线 · 稳定落脚","取得上层通行件并从上层出口离开。楼梯、低通道和大跳提供不同路径，无需清空三层守卫。","上层通路接通，精确射手步枪已解锁。",6,0),
   new ExpeditionLevel(3,"首次撤离","凭据 · 何时脱战","取得凭据后中央捷径开放，侧入口出现截击预警。可留下普通敌人，从对侧撤离。","首次带回凭据。风险目标只在成功撤离后兑现。",5,0),
   new ExpeditionLevel(4,"交叉火网","双塔 · 局部通路","两塔形成不同射线。每个火控节点分别打开一段通路，两塔始终有楼梯可达。","货运通道开放，重型步枪已解锁。",3,0),
   new ExpeditionLevel(5,"危险转移","真实手雷 · 破坏封锁","摧毁两处封锁装置后离开。枪械、爆炸与近战均可破坏；手雷留在世界，换位不重置引信。","封锁装置清除，榴弹发射器已解锁。",9,0),
   new ExpeditionLevel(6,"三岛继电","搬运锚点 · 接入锁存","站上接入座，与任务锚点换位完成供能。三座顺序自由；锚点可移走，维护复位保留进度。","三岛接入完成，栓动狙击枪已解锁。",7,0),
   new ExpeditionLevel(7,"回线仓区","任意两匣 · 分支选择","A仓窄廊、B仓长线、C仓折返。任意两匣即可撤离，第三匣成功带出+40。主线不依赖回刀。","仓区数据回收，支援轻机枪已解锁。",8,0),
   new ExpeditionLevel(8,"高塔回收","顶层核心 · 分层下撤","从顶层携核心向地面撤离。楼梯永久连通，回头不重复增援，上层活敌无需全清。","核心坐标确认，蓄能步枪已解锁。",10,0),
   new ExpeditionLevel(9,"持续封锁","有限预算 · 主动断援","关闭三个控制站取消对应未落地增援，已部署敌人不会消失。备弹减半、弹匣保留，仅一处场地补给。","后续增援切断，相位标记枪已解锁。",11,0),
   new ExpeditionLevel(10,"反相设施","局部干扰 · 外环取核","取核心权限后沿外环撤离。两个干扰区独立可关；仅Q两端入场受限，无需关闭全部场区。","核心权限取得，封锁指挥官就在前方。",-1,0),
   new ExpeditionLevel(11,"核心突破","双高台授权 · Boss争位","取得双高台授权和地面核心，击败指挥官，从对侧撤离。随从同时至多两名，Boss死后撤销未部署随从。","核心回收成功，十二段空间行动完成。",-1,0)
  };
  public static readonly string[] Specializations={"突入","生存","机动"};
  public static readonly string[] Attachments={"标准组件","补偿器","扩容备弹","轻量握把"};
  public static readonly string[] AttachmentRules={"无额外收益或代价","散布 −15%，装填 +8%","备用弹药 +25%，移动 −4%","移动 +5%，散布 +12%"};
  public static int RankCost(int rank)=>120+rank*100;
  public static int ReplayReward(int level)=>level==0?5:30;
 }
 [Serializable] public sealed class ExpeditionReceipt
 {
  public string runId;public int level,reward;public bool success,first,bonus;public float seconds;
  public int baseReward,firstReward,bonusReward,medkitsUsed,ammoUsed,armorUsed,attempts,deaths;
  public bool historical;public int sourceVersion;
 }
 [Serializable] public sealed class ClassLoadout
 {
  public int classId,primary,secondary,medkitCarry;public int[] attachments,itemTypes,itemCounts;
  public string classSkillId="",tacticalSkillId="",starterSkillId="";
  public bool starterWeaponClaimed,starterSkillClaimed;
 }
 [Serializable] public sealed class TacticalDeployment
 {
  // No initializers: JsonUtility's empty inline object must differ from a real snapshot.
  public string runId,classSkillId,tacticalSkillId;
  public int level,classId,primary,secondary,medkits,ammoPacks,armorPacks,inventoryMedkits,attempts,deaths;
  public int classSkillLevel,generalSkillLevel,baseReward,firstReward,bonusReward;
  public int[] attachments,itemTypes,itemCounts,inventoryConsumables,carriedConsumables;
  public bool hasMobility,hasShield;
 }
 [Serializable] public sealed class TacticalPurchaseReceipt
 {
  public string requestId,kind;public int productId,quantity,unitPrice,totalCost,balanceAfter,stockAfter,skillLevelAfter,rulesVersion;
 }
 [Serializable] public sealed class ExpeditionProfile
 {
  public int version=4,credits,highestUnlocked,primary=0,secondary=1,specialization,knifeRank;
  public int[] ranks=new int[3],attachments=new int[12];
  public bool[] cleared=new bool[12],weapons=new bool[12];
  public float[] bestSeconds=new float[12];
  public string activeRun="";public int activeLevel;
  public List<ExpeditionReceipt> receipts=new List<ExpeditionReceipt>();
  // v6 is a new profile, never an automatic read/import of v4 or the T01 v5 file.
  public int activeClass,medkitStock,mobilityLevel,shieldLevel;
  public int[] classSkillLevels,generalLevels,consumables;
  public bool sharedCarbineClaimed,sharedTacticalClaimed;
  public string starterGeneralId="";
  public ClassLoadout[] classLoadouts;
  public TacticalDeployment deployment;
  public List<TacticalPurchaseReceipt> purchases;
  public ExpeditionProfile(){weapons[0]=weapons[1]=true;}
 }
 public sealed class ExpeditionStore
 {
  readonly string file;bool recoveredBackup;
  public ExpeditionProfile Profile{get;private set;}
  public string Notice{get;private set;}="";
  public string SavePath=>file;
  public bool IsTactical{get;private set;}
  ClassLoadout Loadout=>Profile.classLoadouts[Profile.activeClass];
  public int ActiveClass=>IsTactical?Profile.activeClass:0;
  public int Primary=>IsTactical?(Profile.deployment!=null?Profile.deployment.primary:Loadout.primary):Profile.primary;
  public int Secondary=>IsTactical?(Profile.deployment!=null?Profile.deployment.secondary:Loadout.secondary):Profile.secondary;
  public int EquippedClassSkill=>IsTactical?TacticalPrototypeRules.SkillIndex(Profile.deployment!=null?Profile.deployment.classSkillId:Loadout.classSkillId):-1;
  public int EquippedGeneralSkill=>IsTactical?TacticalPrototypeRules.SkillIndex(Profile.deployment!=null?Profile.deployment.tacticalSkillId:Loadout.tacticalSkillId,true):-1;
  public int EquippedClassSkillId=>EquippedClassSkill;
  public int EquippedGeneralSkillId=>EquippedGeneralSkill;
  public int EquippedClassSkillLevel=>!IsTactical?0:Profile.deployment!=null?Profile.deployment.classSkillLevel:SkillLevel(EquippedClassSkill);
  public int EquippedGeneralSkillLevel=>!IsTactical?0:Profile.deployment!=null?Profile.deployment.generalSkillLevel:SkillLevel(EquippedGeneralSkill,true);
  public int MedkitStock=>ConsumableStock(0);
  public int MedkitCarry=>ConsumableCarry(0);
  public bool HasMobility=>IsTactical&&EquippedClassSkill==TacticalPrototypeRules.MobilitySkillIndex&&EquippedClassSkillLevel>0;
  public bool HasShield=>!IsTactical||(EquippedGeneralSkill==TacticalPrototypeRules.ShieldGeneralIndex&&EquippedGeneralSkillLevel>0);
  public bool HasKnife=>!IsTactical||(EquippedGeneralSkill==TacticalPrototypeRules.KnifeGeneralIndex&&EquippedGeneralSkillLevel>0);
  public int KnifeLevel=>IsTactical?(HasKnife?EquippedGeneralSkillLevel:0):Profile.knifeRank;
  public bool HasWeaponStarter=>IsTactical&&Profile.sharedCarbineClaimed&&Loadout.starterWeaponClaimed;
  public bool HasSkillStarter=>IsTactical&&Loadout.starterSkillClaimed;
  public bool HasGeneralStarter=>IsTactical&&Profile.sharedTacticalClaimed;
  public bool HasCurrentClassStarter=>HasWeaponStarter&&HasSkillStarter&&HasGeneralStarter;
  public bool HasAssaultStarter=>IsTactical&&Profile.sharedCarbineClaimed&&Profile.sharedTacticalClaimed&&Profile.classLoadouts[0].starterWeaponClaimed&&Profile.classLoadouts[0].starterSkillClaimed;

  public ExpeditionStore(string folder):this(folder,false){}
  public ExpeditionStore(string folder,bool tactical)
  {
   IsTactical=tactical;Directory.CreateDirectory(folder);file=Path.Combine(folder,tactical?"expedition-v6.json":"expedition-v4.json");
   if(File.Exists(file)||File.Exists(file+".bak"))
   {
    try{Profile=Read(file);}catch(Exception e) when(e is IOException||e is ArgumentException||e is InvalidDataException)
    {Profile=Read(file+".bak");recoveredBackup=true;Notice="已从备份恢复，最近一次操作可能回退。";}
   }
   else{Profile=tactical?NewTacticalProfile():new ExpeditionProfile();Persist(Profile);}
   if(!string.IsNullOrEmpty(Profile.activeRun))
   {
    Settle(Profile.activeRun,false,false,0);
    Notice+=tactical?" 上次未结算行动已结束，库存恢复至采购后；采购保留，本关未提交收益不保留。":" 上次中断的行动已结束，永久解锁保留。";
   }
  }
  public static ExpeditionProfile NewTacticalProfile()
  {
   var p=new ExpeditionProfile{version=6,weapons=new bool[WeaponCatalog.Count],classLoadouts=new ClassLoadout[4],classSkillLevels=new int[8],generalLevels=new int[2],consumables=new int[3],purchases=new List<TacticalPurchaseReceipt>()};
   for(int i=0;i<4;i++)p.classLoadouts[i]=new ClassLoadout{classId=i,primary=0,secondary=TacticalPrototypeRules.StarterWeapon(i),attachments=new int[12],itemTypes=new[]{-1,-1},itemCounts=new int[2]};
   return p;
  }
  ExpeditionProfile Read(string path)
  {
   string json=File.ReadAllText(path);
   if(!json.Contains("\"version\"")||!json.Contains("\"weapons\"")||!json.Contains("\"receipts\""))throw new InvalidDataException("章节存档字段缺失");
   var p=JsonUtility.FromJson<ExpeditionProfile>(json);NormalizeEmptyDeployment(p);Validate(p);
   if(p.version!=(IsTactical?6:4))throw new InvalidDataException("存档版本与当前模式不符；未导入、迁移或覆盖原档");
   return p;
  }
  static bool Empty(int[] values)=>values==null||values.Length==0;
  static void NormalizeEmptyDeployment(ExpeditionProfile p)
  {
   var d=p?.deployment;
   if(d!=null&&string.IsNullOrEmpty(p.activeRun)&&string.IsNullOrEmpty(d.runId)&&string.IsNullOrEmpty(d.classSkillId)&&string.IsNullOrEmpty(d.tacticalSkillId)&&d.level==0&&d.classId==0&&d.primary==0&&d.secondary==0
    &&d.medkits==0&&d.ammoPacks==0&&d.armorPacks==0&&d.inventoryMedkits==0&&d.attempts==0&&d.deaths==0&&d.classSkillLevel==0&&d.generalSkillLevel==0&&d.baseReward==0&&d.firstReward==0&&d.bonusReward==0
    &&!d.hasMobility&&!d.hasShield&&Empty(d.attachments)&&Empty(d.itemTypes)&&Empty(d.itemCounts)&&Empty(d.inventoryConsumables)&&Empty(d.carriedConsumables))p.deployment=null;
  }
  static bool ValidAttachments(int[] values)
  {if(values==null||values.Length!=12)return false;foreach(int n in values)if(n<0||n>=ExpeditionCatalog.Attachments.Length)return false;return true;}
  static bool ValidArray(int[] values,int length,int minimum,int maximum)
  {if(values==null||values.Length!=length)return false;foreach(int n in values)if(n<minimum||n>maximum)return false;return true;}
  static bool ValidItems(int[] types,int[] counts)
  {
   if(types==null||counts==null||types.Length!=2||counts.Length!=2)return false;
   for(int i=0;i<2;i++)
   {
    if(types[i]==-1){if(counts[i]!=0)return false;}
    else if(!TacticalPrototypeRules.ValidConsumable(types[i])||counts[i]<1||counts[i]>TacticalPrototypeRules.ConsumableLimit(types[i]))return false;
   }
   return types[0]<0||types[1]<0||types[0]!=types[1];
  }
  static int ItemCarry(ClassLoadout loadout,int type)
  {for(int i=0;i<2;i++)if(loadout.itemTypes[i]==type)return loadout.itemCounts[i];return 0;}
  static void SyncCompatibilityFields(ExpeditionProfile p)
  {
   if(p.version!=6)return;
   var l=p.classLoadouts[p.activeClass];p.primary=l.primary;p.secondary=l.secondary;p.attachments=(int[])l.attachments.Clone();
   p.medkitStock=p.consumables[0];p.mobilityLevel=p.classSkillLevels[1];p.shieldLevel=p.generalLevels[0];
   foreach(var preset in p.classLoadouts)preset.medkitCarry=ItemCarry(preset,0);
  }
  public static void Validate(ExpeditionProfile p)
  {
   if(p==null||(p.version!=4&&p.version!=6)||p.credits<0||p.highestUnlocked<0||p.highestUnlocked>11||p.ranks==null||p.ranks.Length!=3||!ValidAttachments(p.attachments)||p.weapons==null||p.weapons.Length!=12||p.cleared==null||p.cleared.Length!=12||p.bestSeconds==null||p.bestSeconds.Length!=12||p.receipts==null||p.specialization<0||p.specialization>2)throw new InvalidDataException("章节存档不完整，保留原文件");
   if(p.version==4&&(!WeaponCatalog.Valid(p.primary)||!WeaponCatalog.Valid(p.secondary)||p.primary==p.secondary||!p.weapons[p.primary]||!p.weapons[p.secondary]||!p.weapons[0]||!p.weapons[1]))throw new InvalidDataException("武器装配无效");
   if(p.knifeRank<0||p.knifeRank>3)throw new InvalidDataException("飞刀研究等级无效");
   foreach(int r in p.ranks)if(r<0||r>3)throw new InvalidDataException("专精等级无效");
   foreach(float t in p.bestSeconds)if(float.IsNaN(t)||float.IsInfinity(t)||t<0)throw new InvalidDataException("时间无效");
   if(!string.IsNullOrEmpty(p.activeRun)&&(p.activeLevel<0||p.activeLevel>p.highestUnlocked))throw new InvalidDataException("活动关卡无效");
   var ids=new HashSet<string>();foreach(var r in p.receipts)
   {
    if(r==null||string.IsNullOrEmpty(r.runId)||!ids.Add(r.runId)||r.level<0||r.level>11||r.reward<0)throw new InvalidDataException("结算收据无效");
    if(p.version==6&&r.historical&&(r.sourceVersion!=4&&r.sourceVersion!=5))throw new InvalidDataException("历史结算来源版本无效");
    if(p.version==6&&!r.historical&&(r.baseReward<0||r.firstReward<0||r.bonusReward<0||r.reward!=(long)r.baseReward+r.firstReward+r.bonusReward||r.medkitsUsed<0||r.medkitsUsed>2||r.ammoUsed<0||r.ammoUsed>1||r.armorUsed<0||r.armorUsed>1||r.attempts<1||r.deaths<0||r.deaths>r.attempts||(!r.success&&(r.reward!=0||r.medkitsUsed!=0||r.ammoUsed!=0||r.armorUsed!=0||r.first||r.bonus))))throw new InvalidDataException("战术结算收据无效");
   }
   if(p.version==6)ValidateTactical(p,ids);
  }
  static void ValidateTactical(ExpeditionProfile p,HashSet<string> settledRuns)
  {
   if(!TacticalPrototypeRules.ValidClass(p.activeClass)||p.classLoadouts==null||p.classLoadouts.Length!=4||!ValidArray(p.classSkillLevels,8,0,3)||!ValidArray(p.generalLevels,2,0,3)||!ValidArray(p.consumables,3,0,int.MaxValue)||p.purchases==null)throw new InvalidDataException("四职业战术档案不完整");
   for(int i=0;i<4;i++)
   {
    var l=p.classLoadouts[i];
    if(l==null||l.classId!=i||!TacticalPrototypeRules.ClassCanUseWeapon(i,l.primary)||!TacticalPrototypeRules.ClassCanUseWeapon(i,l.secondary)||l.primary==l.secondary||!ValidAttachments(l.attachments)||!ValidItems(l.itemTypes,l.itemCounts))throw new InvalidDataException("职业预设不合法");
    if(l.starterWeaponClaimed&&(!p.weapons[TacticalPrototypeRules.StarterWeapon(i)]||!p.weapons[l.primary]||!p.weapons[l.secondary]))throw new InvalidDataException("武器保障或职业预设与拥有状态不符");
    int skill=TacticalPrototypeRules.SkillIndex(l.classSkillId),general=TacticalPrototypeRules.SkillIndex(l.tacticalSkillId,true),starter=TacticalPrototypeRules.SkillIndex(l.starterSkillId);
    if(!string.IsNullOrEmpty(l.classSkillId)&&(!TacticalPrototypeRules.ClassCanUseSkill(i,skill)||p.classSkillLevels[skill]<1))throw new InvalidDataException("职业技能装配不合法");
    if(!string.IsNullOrEmpty(l.tacticalSkillId)&&(general<0||p.generalLevels[general]<1))throw new InvalidDataException("通用技能装配不合法");
    if(l.starterSkillClaimed&&(!TacticalPrototypeRules.ClassCanUseSkill(i,starter)||p.classSkillLevels[starter]<1))throw new InvalidDataException("职业技能保障记录无效");
   }
   int generalStarter=TacticalPrototypeRules.SkillIndex(p.starterGeneralId,true);
   if((p.sharedCarbineClaimed&&!p.weapons[0])||(p.sharedTacticalClaimed&&(generalStarter<0||p.generalLevels[generalStarter]<1)))throw new InvalidDataException("共享保障记录无效");
   var purchaseIds=new HashSet<string>();
   foreach(var r in p.purchases)
   {
    if(r==null||string.IsNullOrWhiteSpace(r.requestId)||!purchaseIds.Add(r.requestId)||r.quantity<1||r.unitPrice<1||r.totalCost!=(long)r.quantity*r.unitPrice||r.balanceAfter<0||r.stockAfter<0||r.rulesVersion<1)throw new InvalidDataException("采购回执不完整");
    bool product=r.kind=="weapon"?WeaponCatalog.Valid(r.productId):r.kind=="consumable"?TacticalPrototypeRules.ValidConsumable(r.productId):(r.kind=="class-base"||r.kind=="class-upgrade")?TacticalPrototypeRules.ValidSkill(r.productId):(r.kind=="general-base"||r.kind=="general-upgrade")&&TacticalPrototypeRules.ValidSkill(r.productId,true);
    if(!product||(r.kind!="consumable"&&r.quantity!=1))throw new InvalidDataException("采购商品记录无效");
   }
   var d=p.deployment;
   if(string.IsNullOrEmpty(p.activeRun)){if(d!=null)throw new InvalidDataException("非行动状态含有出发快照");return;}
   if(d==null||d.runId!=p.activeRun||settledRuns.Contains(d.runId)||d.level!=p.activeLevel||d.classId!=p.activeClass||d.attempts<1||d.deaths<0||d.deaths>d.attempts||d.baseReward<0||d.firstReward<0||d.bonusReward<0||!ValidAttachments(d.attachments)||!ValidItems(d.itemTypes,d.itemCounts)||!ValidArray(d.inventoryConsumables,3,0,int.MaxValue)||!ValidArray(d.carriedConsumables,3,0,2)||!TacticalPrototypeRules.ClassCanUseWeapon(d.classId,d.primary)||!TacticalPrototypeRules.ClassCanUseWeapon(d.classId,d.secondary)||d.primary==d.secondary||!p.weapons[d.primary]||!p.weapons[d.secondary])throw new InvalidDataException("出发快照无效，保留原文件");
   int cs=TacticalPrototypeRules.SkillIndex(d.classSkillId),gs=TacticalPrototypeRules.SkillIndex(d.tacticalSkillId,true);
   if(!TacticalPrototypeRules.ClassCanUseSkill(d.classId,cs)||gs<0||d.classSkillLevel<1||d.classSkillLevel>3||d.generalSkillLevel<1||d.generalSkillLevel>3||p.classSkillLevels[cs]!=d.classSkillLevel||p.generalLevels[gs]!=d.generalSkillLevel)throw new InvalidDataException("出发技能快照无效");
   for(int type=0;type<3;type++)
   {
    int desired=0;for(int slot=0;slot<2;slot++)if(d.itemTypes[slot]==type)desired=d.itemCounts[slot];
    if(d.inventoryConsumables[type]!=p.consumables[type]||d.carriedConsumables[type]!=Math.Min(desired,d.inventoryConsumables[type]))throw new InvalidDataException("出发库存快照无效");
   }
   if(d.medkits!=d.carriedConsumables[0]||d.ammoPacks!=d.carriedConsumables[1]||d.armorPacks!=d.carriedConsumables[2]||d.inventoryMedkits!=d.inventoryConsumables[0]||d.hasMobility!=(cs==1)||d.hasShield!=(gs==0))throw new InvalidDataException("出发快照字段不一致");
  }
  ExpeditionProfile Copy()
  {var p=JsonUtility.FromJson<ExpeditionProfile>(JsonUtility.ToJson(Profile));NormalizeEmptyDeployment(p);return p;}
  void Persist(ExpeditionProfile p)
  {
   SyncCompatibilityFields(p);Validate(p);if(p.version!=(IsTactical?6:4))throw new InvalidDataException("拒绝跨模式保存");string temp=file+".tmp";
   using(var s=new FileStream(temp,FileMode.Create,FileAccess.Write,FileShare.None))using(var w=new StreamWriter(s)){w.Write(JsonUtility.ToJson(p,true));w.Flush();s.Flush(true);}
   if(File.Exists(file))
   {
    if(recoveredBackup){File.Copy(file,file+".corrupt-"+Guid.NewGuid().ToString("N"));File.Replace(temp,file,null);}
    else File.Replace(temp,file,file+".bak");
   }
   else File.Move(temp,file);
   recoveredBackup=false;
  }
  void Commit(ExpeditionProfile p){Persist(p);Profile=p;}
  void RequireIdle(){if(!string.IsNullOrEmpty(Profile.activeRun))throw new InvalidOperationException("行动中不能更改职业、配装、技能、携带或采购");}
  void RequireTactical(){if(!IsTactical)throw new InvalidOperationException("此操作仅用于独立四职业战术档");}
  void RequireLegacy(){if(IsTactical)throw new InvalidOperationException("四职业模式不使用旧专精或旧飞刀研究；未扣费");}
  public int SkillLevel(int index,bool general=false)
  {return IsTactical&&TacticalPrototypeRules.ValidSkill(index,general)?(general?Profile.generalLevels[index]:Profile.classSkillLevels[index]):0;}
  public int ConsumableStock(int type)
  {if(!TacticalPrototypeRules.ValidConsumable(type))throw new ArgumentOutOfRangeException(nameof(type));return IsTactical?Profile.consumables[type]:0;}
  public int ConsumableCarry(int type)
  {if(!TacticalPrototypeRules.ValidConsumable(type))throw new ArgumentOutOfRangeException(nameof(type));return IsTactical?ItemCarry(Loadout,type):0;}
  public int AttachmentFor(int weapon)
  {
   if(!WeaponCatalog.Valid(weapon))throw new ArgumentOutOfRangeException(nameof(weapon));
   return IsTactical?(Profile.deployment!=null?Profile.deployment.attachments[weapon]:Loadout.attachments[weapon]):Profile.attachments[weapon];
  }
  public bool CanEquip(int weapon)=>WeaponCatalog.Valid(weapon)&&Profile.weapons[weapon]&&(!IsTactical||TacticalPrototypeRules.ClassCanUseWeapon(Profile.activeClass,weapon));
  public string WeaponAvailability(int weapon)
  {
   if(!WeaponCatalog.Valid(weapon))return "武器不存在";
   if(Profile.weapons[weapon])return CanEquip(weapon)?"已拥有 · 可装备":"已拥有 · 当前职业不可用";
   if(!IsTactical)return "完成章节后解锁";
   if(!TacticalPrototypeRules.WeaponListed(weapon,Profile.cleared))return "尚未上架";
   return TacticalPrototypeRules.ClassCanUseWeapon(Profile.activeClass,weapon)?"已上架 · "+TacticalPrototypeRules.WeaponPrice(weapon)+" 信用点":"已上架 · 当前职业不可用";
  }
  public void SelectClass(int classId)
  {
   RequireTactical();RequireIdle();if(!TacticalPrototypeRules.ValidClass(classId))throw new ArgumentOutOfRangeException(nameof(classId));
   if(Profile.activeClass==classId)return;var p=Copy();p.activeClass=classId;
   var l=p.classLoadouts[classId];
   if(string.IsNullOrEmpty(l.tacticalSkillId))for(int i=0;i<2;i++)if(p.generalLevels[i]>0){l.tacticalSkillId=TacticalPrototypeRules.GeneralSkillIds[i];break;}
   Commit(p);
  }
  public void ClaimWeaponStarter()
  {
   RequireTactical();RequireIdle();if(HasWeaponStarter)return;var p=Copy();var l=p.classLoadouts[p.activeClass];
   p.weapons[0]=true;p.sharedCarbineClaimed=true;p.weapons[TacticalPrototypeRules.StarterWeapon(p.activeClass)]=true;l.starterWeaponClaimed=true;Commit(p);
  }
  public void ClaimSkillStarter(int skillIndex)
  {
   RequireTactical();RequireIdle();if(!TacticalPrototypeRules.ClassCanUseSkill(Profile.activeClass,skillIndex))throw new InvalidOperationException("只能领取当前职业的入门技能");
   string id=TacticalPrototypeRules.SkillIds[skillIndex];
   if(Loadout.starterSkillClaimed){if(Loadout.starterSkillId!=id)throw new InvalidOperationException("该职业的技能保障已领取，另一技能可独立购买");return;}
   var p=Copy();var l=p.classLoadouts[p.activeClass];p.classSkillLevels[skillIndex]=Math.Max(1,p.classSkillLevels[skillIndex]);l.starterSkillClaimed=true;l.starterSkillId=id;l.classSkillId=id;Commit(p);
  }
  public void ClaimGeneralStarter(int generalIndex)
  {
   RequireTactical();RequireIdle();if(!TacticalPrototypeRules.ValidSkill(generalIndex,true))throw new ArgumentOutOfRangeException(nameof(generalIndex));
   string id=TacticalPrototypeRules.GeneralSkillIds[generalIndex];
   if(Profile.sharedTacticalClaimed){if(Profile.starterGeneralId!=id)throw new InvalidOperationException("全档通用技能保障已领取，另一技能可独立购买");return;}
   var p=Copy();p.generalLevels[generalIndex]=Math.Max(1,p.generalLevels[generalIndex]);p.sharedTacticalClaimed=true;p.starterGeneralId=id;p.classLoadouts[p.activeClass].tacticalSkillId=id;Commit(p);
  }
  public void ClaimAssaultStarter()
  {
   RequireTactical();RequireIdle();SelectClass(0);ClaimWeaponStarter();
   if(!HasSkillStarter)ClaimSkillStarter(1);if(!HasGeneralStarter)ClaimGeneralStarter(0);
  }
  TacticalPurchaseReceipt Purchase(string requestId,string kind,int product,int quantity,Func<ExpeditionProfile,int> price,Action<ExpeditionProfile> grant)
  {
   RequireTactical();if(string.IsNullOrWhiteSpace(requestId))throw new ArgumentException("采购请求编号不能为空",nameof(requestId));
   if(quantity<1)throw new ArgumentOutOfRangeException(nameof(quantity));
   foreach(var old in Profile.purchases)if(old.requestId==requestId)
   {if(old.kind!=kind||old.productId!=product||old.quantity!=quantity)throw new InvalidOperationException("同一采购编号不能更改商品或数量");return old;}
   RequireIdle();var p=Copy();int unit=price(p),cost=checked(unit*quantity);
   if(unit<1)throw new InvalidOperationException("商品价格未配置");
   if(p.credits<cost)throw new InvalidOperationException("信用点不足；可使用保障配置完成主线获得信用点");
   p.credits-=cost;grant(p);
   var receipt=new TacticalPurchaseReceipt{requestId=requestId,kind=kind,productId=product,quantity=quantity,unitPrice=unit,totalCost=cost,balanceAfter=p.credits,stockAfter=kind=="consumable"?p.consumables[product]:0,rulesVersion=TacticalPrototypeRules.Version};
   if(kind.StartsWith("class-",StringComparison.Ordinal))receipt.skillLevelAfter=p.classSkillLevels[product];
   if(kind.StartsWith("general-",StringComparison.Ordinal))receipt.skillLevelAfter=p.generalLevels[product];
   p.purchases.Add(receipt);Commit(p);return receipt;
  }
  public TacticalPurchaseReceipt BuyWeapon(string requestId,int weapon)
  {
   if(!WeaponCatalog.Valid(weapon))throw new ArgumentOutOfRangeException(nameof(weapon));
   return Purchase(requestId,"weapon",weapon,1,p=>{if(p.weapons[weapon])throw new InvalidOperationException("武器已拥有");if(!TacticalPrototypeRules.WeaponListed(weapon,p.cleared))throw new InvalidOperationException("武器尚未上架");return TacticalPrototypeRules.WeaponPrice(weapon);},p=>p.weapons[weapon]=true);
  }
  public TacticalPurchaseReceipt BuySkill(string requestId,int skillIndex,bool general=false)
  {
   if(!TacticalPrototypeRules.ValidSkill(skillIndex,general))throw new ArgumentOutOfRangeException(nameof(skillIndex));
   return Purchase(requestId,general?"general-base":"class-base",skillIndex,1,p=>{if((general?p.generalLevels:p.classSkillLevels)[skillIndex]>0)throw new InvalidOperationException("技能已拥有");return TacticalPrototypeRules.SkillBasePrice(skillIndex,general);},p=>(general?p.generalLevels:p.classSkillLevels)[skillIndex]=1);
  }
  public TacticalPurchaseReceipt UpgradeSkill(string requestId,int skillIndex,bool general=false)
  {
   if(!TacticalPrototypeRules.ValidSkill(skillIndex,general))throw new ArgumentOutOfRangeException(nameof(skillIndex));
   return Purchase(requestId,general?"general-upgrade":"class-upgrade",skillIndex,1,p=>TacticalPrototypeRules.SkillUpgradePrice((general?p.generalLevels:p.classSkillLevels)[skillIndex]),p=>(general?p.generalLevels:p.classSkillLevels)[skillIndex]++);
  }
  public TacticalPurchaseReceipt BuyConsumable(string requestId,int type,int quantity=1)
  {
   if(!TacticalPrototypeRules.ValidConsumable(type))throw new ArgumentOutOfRangeException(nameof(type));
   return Purchase(requestId,"consumable",type,quantity,p=>TacticalPrototypeRules.ConsumablePrice(type),p=>p.consumables[type]=checked(p.consumables[type]+quantity));
  }
  public TacticalPurchaseReceipt BuyMedkit(string requestId,int quantity=1)=>BuyConsumable(requestId,0,quantity);
  public void EquipClassSkill(int index)
  {
   RequireTactical();RequireIdle();if(!TacticalPrototypeRules.ClassCanUseSkill(Profile.activeClass,index)||SkillLevel(index)<1)throw new InvalidOperationException("职业技能尚未拥有或不属于当前职业");
   var p=Copy();p.classLoadouts[p.activeClass].classSkillId=TacticalPrototypeRules.SkillIds[index];Commit(p);
  }
  public void EquipGeneralSkill(int index)
  {
   RequireTactical();RequireIdle();if(!TacticalPrototypeRules.ValidSkill(index,true)||SkillLevel(index,true)<1)throw new InvalidOperationException("通用技能尚未拥有");
   var p=Copy();p.classLoadouts[p.activeClass].tacticalSkillId=TacticalPrototypeRules.GeneralSkillIds[index];Commit(p);
  }
  public void SetConsumableSlot(int slot,int type,int count)
  {
   RequireTactical();RequireIdle();if(slot<0||slot>1)throw new ArgumentOutOfRangeException(nameof(slot));
   if(type==-1){if(count!=0)throw new InvalidOperationException("空槽数量必须为0");}
   else
   {
    if(!TacticalPrototypeRules.ValidConsumable(type)||count<0||count>TacticalPrototypeRules.ConsumableLimit(type))throw new InvalidOperationException("道具类型或携带量无效");
    if(count==0)type=-1;else if(Loadout.itemTypes[1-slot]==type)throw new InvalidOperationException("两个道具槽不能选择相同类型");
   }
   var p=Copy();var l=p.classLoadouts[p.activeClass];l.itemTypes[slot]=type;l.itemCounts[slot]=count;Commit(p);
  }
  public void SetMedkitCarry(int quantity)
  {
   RequireTactical();RequireIdle();if(quantity<0||quantity>2)throw new ArgumentOutOfRangeException(nameof(quantity));
   int slot=Array.IndexOf(Loadout.itemTypes,0);
   if(quantity==0){if(slot>=0)SetConsumableSlot(slot,-1,0);return;}
   if(slot<0)slot=Array.IndexOf(Loadout.itemTypes,-1);
   if(slot<0)throw new InvalidOperationException("两个道具槽已占用，请先空出一槽");
   SetConsumableSlot(slot,0,quantity);
  }
  public string Begin(int level)
  {
   RequireIdle();if(level<0||level>Profile.highestUnlocked)throw new InvalidOperationException("关卡尚未解锁");
   if(IsTactical&&(!CanEquip(Primary)||!CanEquip(Secondary)||Primary==Secondary||!TacticalPrototypeRules.ClassCanUseSkill(Profile.activeClass,EquippedClassSkill)||EquippedClassSkillLevel<1||EquippedGeneralSkill<0||EquippedGeneralSkillLevel<1))throw new InvalidOperationException("请配置两把已拥有的职业武器、一项职业技能和一项通用技能");
   var p=Copy();p.activeRun=Guid.NewGuid().ToString("N");p.activeLevel=level;
   if(IsTactical)
   {
    var l=p.classLoadouts[p.activeClass];var carried=new int[3];for(int type=0;type<3;type++)carried[type]=Math.Min(ItemCarry(l,type),p.consumables[type]);
    p.deployment=new TacticalDeployment{runId=p.activeRun,level=level,classId=p.activeClass,primary=l.primary,secondary=l.secondary,attachments=(int[])l.attachments.Clone(),classSkillId=l.classSkillId,tacticalSkillId=l.tacticalSkillId,classSkillLevel=EquippedClassSkillLevel,generalSkillLevel=EquippedGeneralSkillLevel,itemTypes=(int[])l.itemTypes.Clone(),itemCounts=(int[])l.itemCounts.Clone(),inventoryConsumables=(int[])p.consumables.Clone(),carriedConsumables=carried,medkits=carried[0],ammoPacks=carried[1],armorPacks=carried[2],inventoryMedkits=p.consumables[0],attempts=1,deaths=0,baseReward=TacticalPrototypeRules.BaseReward(level),firstReward=p.cleared[level]?0:TacticalPrototypeRules.FirstClearExtra(level),bonusReward=TacticalPrototypeRules.OptionalReward,hasMobility=HasMobility,hasShield=HasShield};
   }
   Commit(p);return p.activeRun;
  }
  void RequireDeployment(string run)
  {RequireTactical();if(string.IsNullOrEmpty(run)||Profile.activeRun!=run||Profile.deployment==null||Profile.deployment.runId!=run)throw new InvalidOperationException("行动与出发快照不匹配");}
  public TacticalDeployment Retry(string run)
  {RequireDeployment(run);var p=Copy();p.deployment.attempts=checked(p.deployment.attempts+1);Commit(p);return Profile.deployment;}
  public void RecordDeath(string run)
  {RequireDeployment(run);var p=Copy();p.deployment.deaths=checked(p.deployment.deaths+1);Commit(p);}
  public ExpeditionReceipt Settle(string run,bool success,bool bonus,float seconds,int medkitsUsed=0,int ammoUsed=0,int armorUsed=0)
  {
   foreach(var old in Profile.receipts)if(old.runId==run)return old;
   if(string.IsNullOrEmpty(run)||Profile.activeRun!=run)throw new InvalidOperationException("结算行动不匹配");
   if(float.IsNaN(seconds)||float.IsInfinity(seconds)||seconds<0)throw new InvalidOperationException("结算时间无效");
   var p=Copy();int level=p.activeLevel;bool first=success&&!p.cleared[level];
   var receipt=new ExpeditionReceipt{runId=run,level=level,success=success,first=first,bonus=success&&bonus,seconds=seconds};
   if(IsTactical)
   {
    var d=p.deployment;int[] used={medkitsUsed,ammoUsed,armorUsed};if(d==null)throw new InvalidOperationException("缺少出发快照");
    for(int i=0;i<3;i++){if(used[i]<0||used[i]>d.carriedConsumables[i])throw new InvalidOperationException("实际道具消耗与出发快照不符");p.consumables[i]=d.inventoryConsumables[i]-(success?used[i]:0);}
    receipt.baseReward=success?d.baseReward:0;receipt.firstReward=first?d.firstReward:0;receipt.bonusReward=success&&bonus?d.bonusReward:0;
    receipt.reward=checked(receipt.baseReward+receipt.firstReward+receipt.bonusReward);receipt.medkitsUsed=success?medkitsUsed:0;receipt.ammoUsed=success?ammoUsed:0;receipt.armorUsed=success?armorUsed:0;receipt.attempts=d.attempts;receipt.deaths=d.deaths;p.deployment=null;
   }
   else receipt.reward=success?(first?ExpeditionCatalog.Levels[level].reward:ExpeditionCatalog.ReplayReward(level))+(bonus?40:0):0;
   if(success)
   {
    p.cleared[level]=true;p.highestUnlocked=Math.Max(p.highestUnlocked,Math.Min(11,level+1));
    if(!IsTactical){int unlock=ExpeditionCatalog.Levels[level].weaponUnlock;if(unlock>=0)p.weapons[unlock]=true;}
    if(p.bestSeconds[level]==0||seconds<p.bestSeconds[level])p.bestSeconds[level]=Math.Max(.01f,seconds);
   }
   p.credits=checked(p.credits+receipt.reward);p.activeRun="";p.receipts.Add(receipt);Commit(p);return receipt;
  }
  public void Equip(int slot,int weapon)
  {
   RequireIdle();if(slot<0||slot>1||!CanEquip(weapon))throw new InvalidOperationException("武器尚未拥有或不属于当前职业");var p=Copy();
   if(IsTactical){var l=p.classLoadouts[p.activeClass];if(slot==0){if(l.secondary==weapon)l.secondary=l.primary;l.primary=weapon;}else{if(l.primary==weapon)l.primary=l.secondary;l.secondary=weapon;}}
   else if(slot==0){if(p.secondary==weapon)p.secondary=p.primary;p.primary=weapon;}else{if(p.primary==weapon)p.primary=p.secondary;p.secondary=weapon;}
   Commit(p);
  }
  public void SetAttachment(int weapon,int attachment)
  {
   RequireIdle();if(!CanEquip(weapon)||attachment<0||attachment>=ExpeditionCatalog.Attachments.Length)throw new InvalidOperationException("装配无效");var p=Copy();
   if(IsTactical)p.classLoadouts[p.activeClass].attachments[weapon]=attachment;else p.attachments[weapon]=attachment;Commit(p);
  }
  public void SelectSpecialization(int spec)
  {RequireLegacy();RequireIdle();if(spec<0||spec>2)throw new ArgumentOutOfRangeException(nameof(spec));var p=Copy();p.specialization=spec;Commit(p);}
  public static int KnifeUpgradeCost(int rank)=>new[]{80,140,220}[Mathf.Clamp(rank,0,2)];
  public void UpgradeKnife()
  {
   RequireLegacy();RequireIdle();if(Profile.knifeRank>=3)throw new InvalidOperationException("飞刀研究已完成");int cost=KnifeUpgradeCost(Profile.knifeRank);
   if(Profile.credits<cost)throw new InvalidOperationException("研究点不足");var p=Copy();p.credits-=cost;p.knifeRank++;Commit(p);
  }
  public void Upgrade(int spec)
  {
   RequireLegacy();RequireIdle();if(spec<0||spec>2||Profile.ranks[spec]>=3)throw new InvalidOperationException("专精已满级");int cost=ExpeditionCatalog.RankCost(Profile.ranks[spec]);
   if(Profile.credits<cost)throw new InvalidOperationException("研究点不足");var p=Copy();p.credits-=cost;p.ranks[spec]++;Commit(p);
  }
 }
}