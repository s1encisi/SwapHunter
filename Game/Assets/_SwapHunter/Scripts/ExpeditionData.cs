using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
namespace SwapHunter
{
 [Serializable] public sealed class ExpeditionLevel
 {
  public int id,reward,weaponUnlock,waves;
  public string name,lesson,brief,ending;
  public ExpeditionLevel(int i,string n,string l,string b,string e,int w,int waves)
  {id=i;name=n;lesson=l;brief=b;ending=e;weaponUnlock=w;this.waves=waves;reward=80+i*15;}
 }
 public static class ExpeditionCatalog
 {
  public static readonly ExpeditionLevel[] Levels={
   new ExpeditionLevel(0,"相位校准","练习章 · 双向换位","跨越断桥，接通校准台后回程桥开启。换位保持朝向；本章重玩奖励5点，适合练习。","相位链路建立。沿维护通道穿过封锁。",4,0),
   new ExpeditionLevel(1,"盾线缺口","格挡与身体 · 绕后","利用掩体与换位绕到盾卫侧背，再读取通行授权。","第一份通行授权取得，三连发步枪已解锁。",2,1),
   new ExpeditionLevel(2,"垂直通路","高低差 · 蹲伏与攀爬","沿坡道、楼梯抵达上层。低通道需要蹲伏，标记短台可翻越。","上层电网重启。观察高度优势与撤离路线。",6,1),
   new ExpeditionLevel(3,"首次撤离","风险目标 · 带回奖励","接通双终端开启回程桥。橙色数据引来追兵；必需敌人清除后可带追兵返回入口撤离。","第一章完成。装配、行动与撤离构成完整循环。",5,1),
   new ExpeditionLevel(4,"交叉火网","掩体交换 · 射线管理","两座哨塔覆盖主通道。用换位改变射线关系，接近控制台。","防线视野被切断，重型步枪已解锁。",3,2),
   new ExpeditionLevel(5,"危险转移","手雷时机 · 位置利用","掷弹兵封锁掩体。换位不重置手雷引信，先确认落点，再转移危险。","危险可以转移，代价不能消失。榴弹发射器已解锁。",9,2),
   new ExpeditionLevel(6,"双端继电","离散平台 · 路线规划","利用信标接通三个分离平台；全部授权接通后，岛间与入口回程桥一起开启。","外部线路恢复，栓动狙击枪已解锁。",7,1),
   new ExpeditionLevel(7,"断路仓区","短视线 · 近距脱困","穿过低通道与夹层。先找出口方向，再决定突入。","第二章完成，封锁中心已暴露。",8,2),
   new ExpeditionLevel(8,"高塔回收","多层回程 · 补给管理","从三个高度回收数据。全部授权后入口回程桥开启；用楼梯或信标下到地面。","核心坐标确认，蓄能步枪已解锁。",10,2),
   new ExpeditionLevel(9,"持续封锁","长战斗 · 资源调度","起始备弹减半，弹匣、场地补给和弹药包保留。接通三段授权，抵挡必需增援后撤离。","相位标记枪已解锁；标记不会允许穿墙换位。",11,3),
   new ExpeditionLevel(10,"反相设施","解除干扰 · 窗口选择","先徒步关闭入口干扰源，再利用信标跨越封锁断口。","干扰源关闭，核心室就在前方。",-1,2),
   new ExpeditionLevel(11,"核心突破","综合战术 · 多阶段撤离","接通核心终端，击败封锁指挥官，返回入口完成撤离。","核心回收成功，货运港脱离封锁。十二段行动完成，可重玩挑战不同配装与路线。",-1,3)
  };
  public static readonly string[] Specializations={"突入","生存","机动"};
  public static readonly string[] Attachments={"标准组件","补偿器","扩容备弹","轻量握把"};
  public static readonly string[] AttachmentRules={"无额外收益或代价","散布 −15%，装填 +8%","备用弹药 +25%，移动 −4%","移动 +5%，散布 +12%"};
  public static int RankCost(int rank)=>120+rank*100;
  public static int ReplayReward(int level)=>level==0?5:30;
 }
 [Serializable] public sealed class ExpeditionReceipt
 {public string runId;public int level,reward;public bool success,first,bonus;public float seconds;}
 [Serializable] public sealed class ExpeditionProfile
 {
  public int version=4,credits,highestUnlocked,primary=0,secondary=1,specialization,knifeRank;
  public int[] ranks=new int[3],attachments=new int[12];
  public bool[] cleared=new bool[12],weapons=new bool[12];
  public float[] bestSeconds=new float[12];
  public string activeRun="";public int activeLevel;
  public List<ExpeditionReceipt> receipts=new List<ExpeditionReceipt>();
  public ExpeditionProfile(){weapons[0]=weapons[1]=true;}
 }
 public sealed class ExpeditionStore
 {
  readonly string file; bool recoveredBackup;
  public ExpeditionProfile Profile{get;private set;}
  public string Notice{get;private set;}="";
  public string SavePath=>file;
  public ExpeditionStore(string folder)
  {
   Directory.CreateDirectory(folder);file=Path.Combine(folder,"expedition-v4.json");
   if(File.Exists(file)||File.Exists(file+".bak"))
   {
    try{Profile=Read(file);}catch(Exception e) when(e is IOException||e is ArgumentException||e is InvalidDataException)
    {Profile=Read(file+".bak");recoveredBackup=true;Notice="已从备份恢复，最近一次操作可能回退。";}
   }
   else{Profile=new ExpeditionProfile();Persist(Profile);}
   if(!string.IsNullOrEmpty(Profile.activeRun))
   {Settle(Profile.activeRun,false,false,0);Notice+=" 上次中断的行动已结束，永久解锁保留。";}
  }
  static ExpeditionProfile Read(string path)
  {
   string json=File.ReadAllText(path);
   if(!json.Contains("\"version\"")||!json.Contains("\"weapons\"")||!json.Contains("\"receipts\""))throw new InvalidDataException("章节存档字段缺失");
   var p=JsonUtility.FromJson<ExpeditionProfile>(json);Validate(p);return p;
  }
  public static void Validate(ExpeditionProfile p)
  {
   if(p==null||p.version!=4||p.credits<0||p.highestUnlocked<0||p.highestUnlocked>11||p.ranks==null||p.ranks.Length!=3||p.attachments==null||p.attachments.Length!=12||p.weapons==null||p.weapons.Length!=12||p.cleared==null||p.cleared.Length!=12||p.bestSeconds==null||p.bestSeconds.Length!=12||p.receipts==null||p.specialization<0||p.specialization>2)throw new InvalidDataException("章节存档不完整，保留原文件");
   if(p.primary<0||p.primary>11||p.secondary<0||p.secondary>11||p.primary==p.secondary||!p.weapons[p.primary]||!p.weapons[p.secondary]||!p.weapons[0]||!p.weapons[1])throw new InvalidDataException("武器装配无效");
   if(p.knifeRank<0||p.knifeRank>3)throw new InvalidDataException("飞刀研究等级无效");
   foreach(int r in p.ranks)if(r<0||r>3)throw new InvalidDataException("专精等级无效");
   foreach(int a in p.attachments)if(a<0||a>3)throw new InvalidDataException("配件无效");
   foreach(float t in p.bestSeconds)if(float.IsNaN(t)||float.IsInfinity(t)||t<0)throw new InvalidDataException("时间无效");
   if(!string.IsNullOrEmpty(p.activeRun)&&(p.activeLevel<0||p.activeLevel>p.highestUnlocked))throw new InvalidDataException("活动关卡无效");
   var ids=new HashSet<string>();foreach(var r in p.receipts)if(r==null||string.IsNullOrEmpty(r.runId)||!ids.Add(r.runId)||r.level<0||r.level>11||r.reward<0)throw new InvalidDataException("结算收据无效");
  }
  ExpeditionProfile Copy()=>JsonUtility.FromJson<ExpeditionProfile>(JsonUtility.ToJson(Profile));
  void Persist(ExpeditionProfile p)
  {
   Validate(p);string temp=file+".tmp";
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
  void RequireIdle(){if(!string.IsNullOrEmpty(Profile.activeRun))throw new InvalidOperationException("行动中不能更改永久装配");}
  public string Begin(int level)
  {
   RequireIdle();if(level<0||level>Profile.highestUnlocked)throw new InvalidOperationException("关卡尚未解锁");
   var p=Copy();p.activeRun=Guid.NewGuid().ToString("N");p.activeLevel=level;Commit(p);return p.activeRun;
  }
  public ExpeditionReceipt Settle(string run,bool success,bool bonus,float seconds)
  {
   foreach(var old in Profile.receipts)if(old.runId==run)return old;
   if(string.IsNullOrEmpty(run)||Profile.activeRun!=run)throw new InvalidOperationException("结算行动不匹配");
   if(float.IsNaN(seconds)||float.IsInfinity(seconds)||seconds<0)throw new InvalidOperationException("结算时间无效");
   var p=Copy();int level=p.activeLevel;bool first=success&&!p.cleared[level];
   int reward=success?(first?ExpeditionCatalog.Levels[level].reward:ExpeditionCatalog.ReplayReward(level))+(bonus?40:0):0;
   var receipt=new ExpeditionReceipt{runId=run,level=level,success=success,first=first,bonus=success&&bonus,reward=reward,seconds=seconds};
   if(success)
   {
    p.cleared[level]=true;p.highestUnlocked=Mathf.Max(p.highestUnlocked,Mathf.Min(11,level+1));
    int unlock=ExpeditionCatalog.Levels[level].weaponUnlock;if(unlock>=0)p.weapons[unlock]=true;
    if(p.bestSeconds[level]==0||seconds<p.bestSeconds[level])p.bestSeconds[level]=Mathf.Max(.01f,seconds);
   }
   p.credits=checked(p.credits+reward);p.activeRun="";p.receipts.Add(receipt);Commit(p);return receipt;
  }
  public void Equip(int slot,int weapon)
  {
   RequireIdle();if(slot<0||slot>1||weapon<0||weapon>11||!Profile.weapons[weapon])throw new InvalidOperationException("武器尚未解锁");
   var p=Copy();if(slot==0){if(p.secondary==weapon)p.secondary=p.primary;p.primary=weapon;}else{if(p.primary==weapon)p.primary=p.secondary;p.secondary=weapon;}Commit(p);
  }
  public void SetAttachment(int weapon,int attachment)
  {RequireIdle();if(weapon<0||weapon>11||attachment<0||attachment>3||!Profile.weapons[weapon])throw new InvalidOperationException("装配无效");var p=Copy();p.attachments[weapon]=attachment;Commit(p);}
  public void SelectSpecialization(int spec)
  {RequireIdle();if(spec<0||spec>2)throw new ArgumentOutOfRangeException(nameof(spec));var p=Copy();p.specialization=spec;Commit(p);}
  public static int KnifeUpgradeCost(int rank)=>new[]{80,140,220}[Mathf.Clamp(rank,0,2)];
  public void UpgradeKnife()
  {
   RequireIdle();if(Profile.knifeRank>=3)throw new InvalidOperationException("飞刀研究已完成");
   int cost=KnifeUpgradeCost(Profile.knifeRank);if(Profile.credits<cost)throw new InvalidOperationException("研究点不足");
   var p=Copy();p.credits-=cost;p.knifeRank++;Commit(p);
  }
  public void Upgrade(int spec)
  {
   RequireIdle();if(spec<0||spec>2||Profile.ranks[spec]>=3)throw new InvalidOperationException("专精已满级");int cost=ExpeditionCatalog.RankCost(Profile.ranks[spec]);
   if(Profile.credits<cost)throw new InvalidOperationException("研究点不足");var p=Copy();p.credits-=cost;p.ranks[spec]++;Commit(p);
  }
 }
}


