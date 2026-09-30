using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.AI;
namespace SwapHunter
{
 public sealed partial class DemoGame
 {
  public bool expeditionActive,expeditionBoard,expeditionInventory,expeditionPending,expeditionBonus,expeditionJammer;
  public int expeditionLevel,expeditionSelected,expeditionObjectives,expeditionWave,expeditionMedkits,expeditionAmmoPacks;
  public ExpeditionStore expeditionStore; public ExpeditionMap expeditionMap; public ExpeditionReceipt expeditionReceipt;
  public string expeditionNotice="";
  string expeditionRun=""; bool expeditionPendingSuccess,expeditionAbandonConfirm;
  float expeditionNextWave=-1,expeditionBreachUntil;
  int expeditionTab,expeditionWeaponSlot,expeditionSelectedWeapon;
  Transform classicWorld;
  public string ExpeditionTitle=>ExpeditionCatalog.Levels[Mathf.Clamp(expeditionLevel,0,11)].name;
  public int ExpeditionRequiredEnemies=>CountExpeditionEnemies(false);
  public int ExpeditionOptionalEnemies=>CountExpeditionEnemies(true);
  int CountExpeditionEnemies(bool optional)
  {
   int count=0;foreach(var pending in deployments)if(pending.arena==stage&&pending.optional==optional)count++;
   foreach(var e in enemies)if(e&&e.Alive&&e.arena==stage&&e.expeditionOptional==optional)count++;return count;
  }
  public bool ExpeditionMainComplete=>expeditionMap!=null&&expeditionObjectives>=expeditionMap.objectiveCount&&expeditionWave>=ExpeditionCatalog.Levels[expeditionLevel].waves&&ExpeditionRequiredEnemies==0;
  public string ExpeditionObjective=>expeditionLevel==0&&!tutorialMoved?KeyName(0)+" / "+KeyName(1)+" / "+KeyName(2)+" / "+KeyName(3)+" 前进到断桥边，鼠标观察":expeditionLevel==0&&!tutorialSwapped?"瞄准青色信标 · "+KeyName(6)+" 交换位置 · 保持原朝向":expeditionLevel==0&&!tutorialShot?KeyName(12)+" 射击训练机 · "+KeyName(13)+" 瞄准 · "+KeyName(7)+" 装填":expeditionJammer?"干扰尚未解除 · 徒步接近入口琥珀色终端并按 "+KeyName(8):ExpeditionMainComplete?"主线完成 · 沿回程桥返回入口撤离"+(expeditionBonus?" · 可带数据追兵撤离":" · 可选橙色数据 +40"):
   "授权 "+expeditionObjectives+" / "+(expeditionMap==null?0:expeditionMap.objectiveCount)+" · 必需敌人 "+ExpeditionRequiredEnemies+" · 数据追兵 "+ExpeditionOptionalEnemies;
  int Spec=>expeditionActive&&expeditionStore!=null?expeditionStore.Profile.specialization:-1;
  int Rank=>Spec<0?0:expeditionStore.Profile.ranks[Spec];
  int Attachment=>expeditionActive&&expeditionStore!=null&&player?expeditionStore.Profile.attachments[Mathf.Clamp(player.weapon,0,11)]:0;
  public float ExpeditionDamageMultiplier=>Spec==0&&Time.time<expeditionBreachUntil?1+.08f*(Rank+1):1;
  public float ExpeditionReloadMultiplier=>Attachment==1?1.08f:1;
  public float ExpeditionSpreadMultiplier=>Attachment==1?.85f:Attachment==3?1.12f:1;
  public float ExpeditionMoveMultiplier=>(Spec==1?1-.02f*(Rank+1):Spec==2?1+.03f*(Rank+1):1)*(Attachment==2?.96f:Attachment==3?1.05f:1);
  public float ExpeditionDamageTaken=>Spec==1?1-.05f*(Rank+1):Spec==2?1+.03f*(Rank+1):1;
  public float ExpeditionCooldownMultiplier=>Spec==2?1-.05f*(Rank+1):1;
  void InitializeExpedition()
  {
   try{expeditionStore=new ExpeditionStore(Path.Combine(RunStorage.Root,"Expedition-v4"));expeditionNotice=expeditionStore.Notice;expeditionSelected=expeditionStore.Profile.highestUnlocked;}
   catch(Exception e){expeditionStore=null;expeditionNotice="章节档案无法读取，原文件保留："+e.Message;Debug.LogWarning(expeditionNotice);}
  }
  public void OpenExpeditionBoard()
  {
   if(tutorialCourse)tutorialCourse.End();
   if(campaignActive)return;
   if(expeditionActive&&expeditionReceipt==null)return;
   ClearExpeditionWorld();expeditionActive=false;expeditionInventory=false;expeditionBoard=true;campaignBoard=false;lastSettlement=null;
   SetState(RunState.Menu);
  }
  void ClearExpeditionWorld()
  {
   ClearDeployments();
   if(expeditionMap!=null)
   {
    foreach(var enemy in enemies)if(enemy){enemy.gameObject.SetActive(false);Destroy(enemy.gameObject);}enemies.Clear();
    for(int i=anchors.Count-1;i>=0;i--)if(!anchors[i]||expeditionMap.anchors.Contains(anchors[i])){anchors.RemoveAt(i);anchorOrigins.RemoveAt(i);}
    foreach(Transform child in effects){child.gameObject.SetActive(false);Destroy(child.gameObject);}
    if(expeditionMap.root){expeditionMap.root.gameObject.SetActive(false);Destroy(expeditionMap.root.gameObject);}expeditionMap=null;
   }
   if(classicWorld){classicWorld.gameObject.SetActive(true);world=classicWorld;}
  }
  public bool BeginExpedition(int level)
  {
   if(expeditionStore==null||campaignActive||expeditionPending)return false;
   if(expeditionActive&&expeditionReceipt==null)return false;
   try
   {
    string run=expeditionStore.Begin(level);ClearExpeditionWorld();expeditionRun=run;expeditionLevel=level;expeditionSelected=level;
    expeditionActive=true;expeditionBoard=false;expeditionInventory=false;expeditionReceipt=null;expeditionBonus=false;expeditionJammer=level==10;
    expeditionObjectives=expeditionWave=0;expeditionNextWave=-1;expeditionBreachUntil=0;expeditionAbandonConfirm=false;
    campaignBoard=false;practice=false;lastSettlement=null;stage=0;stageComplete=combatComplete=false;wave=0;nextWaveAt=-1;
    totalKills=totalSwaps=totalShots=totalHits=deaths=0;runSeconds=0;tutorialMoved=tutorialShot=tutorialSwapped=false;
    foreach(var enemy in enemies)if(enemy){enemy.gameObject.SetActive(false);Destroy(enemy.gameObject);}enemies.Clear();
    foreach(Transform child in effects){child.gameObject.SetActive(false);Destroy(child.gameObject);}
    if(!classicWorld)classicWorld=world;classicWorld.gameObject.SetActive(false);
    expeditionMap=ExpeditionWorld.Build(this,level);world=expeditionMap.root;player.ResetAt(expeditionMap.spawn);
    player.SetLoadout(expeditionStore.Profile.primary,expeditionStore.Profile.secondary);
    foreach(int w in player.EquippedWeapons)if(expeditionStore.Profile.attachments[w]==2)player.reserve[w]=Mathf.RoundToInt(player.reserve[w]*1.25f);
    if(level==9)foreach(int w in player.EquippedWeapons)player.reserve[w]=Mathf.RoundToInt(player.reserve[w]*.5f);
    expeditionMedkits=Spec==1?3:2;expeditionAmmoPacks=1;
    SetState(RunState.Playing);SpawnExpeditionWave();if(level==0)EnemyActor.Spawn(EnemyKind.Training,new Vector3(5,0,37),stage);
    Toast(ExpeditionCatalog.Levels[level].brief,9);Record("expedition_start",(level+1)+":"+run);
    return true;
   }
   catch(Exception e)
   {
    expeditionNotice="部署失败："+e.Message;Debug.LogException(e);
    if(expeditionStore!=null&&!string.IsNullOrEmpty(expeditionStore.Profile.activeRun))
    {expeditionActive=true;expeditionRun=expeditionStore.Profile.activeRun;FinishExpedition(false);}
    return false;
   }
  }
  void SpawnExpeditionWave()
  {
   if(expeditionMap==null||expeditionWave>=ExpeditionCatalog.Levels[expeditionLevel].waves)return;
   expeditionWave++;wave=expeditionWave;
   int count=expeditionLevel<3?2:Mathf.Min(expeditionMap.enemySpawns.Count,3+expeditionLevel/4);
   for(int i=0;i<count;i++)
   {
    Vector3 p=expeditionMap.enemySpawns[i%expeditionMap.enemySpawns.Count];
    // The deployment queue retains the enemy budget if this location is temporarily blocked.
    EnemyKind kind=expeditionLevel==1&&i==0?EnemyKind.Shield:expeditionLevel==5?EnemyKind.Assault:i==0&&expeditionLevel>=4?EnemyKind.Sniper:i==1&&expeditionLevel>=3?EnemyKind.Shield:EnemyKind.Assault;
    if(expeditionLevel==11&&expeditionWave==3&&i==count-1){kind=EnemyKind.Elite;p=new Vector3(0,0,55);}
    QueueDeployment(kind,p,stage);
   }
   Record("expedition_wave",(expeditionLevel+1)+":"+expeditionWave);
  }
  void UpdateExpedition()
  {
   if(Pressed(18)&&!expeditionPending)
   {
    if(IsPlaying){expeditionInventory=true;SetState(RunState.Paused);}
    else if(expeditionInventory){expeditionInventory=false;SetState(RunState.Playing);}
   }
   if(!IsPlaying)return;
   runSeconds+=Time.deltaTime;
   if(Pressed(19))UseExpeditionMedkit();
   if(ExpeditionRequiredEnemies==0&&expeditionWave<ExpeditionCatalog.Levels[expeditionLevel].waves)
   {
    if(expeditionNextWave<0){expeditionNextWave=Time.time+5;Toast("增援将在 5 秒后抵达 · 调整位置并装填",3);}
    else if(Time.time>=expeditionNextWave){expeditionNextWave=-1;SpawnExpeditionWave();}
   }
   foreach(var node in expeditionMap.nodes)if(node&&node.kind==ExpeditionNodeKind.Extraction)node.SetExtractionReady(ExpeditionMainComplete);
   var n=FindExpeditionNode();
   if(n&&Time.unscaledTime>toastUntil)
    Toast(KeyName(8)+" · "+n.DisplayLabel+(n.kind==ExpeditionNodeKind.Bonus?" / 两名数据追兵，可带追兵撤离 +40":""),.15f);
  }
  public ExpeditionNode FindExpeditionNode()
  {
   if(!expeditionActive||expeditionMap==null||!player)return null;ExpeditionNode nearest=null;float range=2.8f;
   foreach(var n in expeditionMap.nodes)
   {
    if(!n||n.used&&n.kind!=ExpeditionNodeKind.Extraction)continue;Vector3 delta=n.Point-player.Eye.position;
    if(delta.magnitude>=range||Vector3.Dot(delta.normalized,player.Eye.forward)<.15f)continue;
    if(Physics.Linecast(player.Eye.position,n.Point,out var sight,Layers.WorldMask)&&sight.collider.GetComponentInParent<ExpeditionNode>()!=n)continue;
    nearest=n;range=delta.magnitude;
   }
   return nearest;
  }
  public bool UseExpeditionNode(ExpeditionNode node)
  {
   if(!IsPlaying||node==null||node!=FindExpeditionNode())return false;
   if(node.kind==ExpeditionNodeKind.Extraction)
   {
    if(!ExpeditionMainComplete){Toast("先接通全部授权并击败必需敌人；数据追兵不阻止撤离",3);return false;}
    if(totalSwaps<1){Toast("相位链路尚未校准 · 至少完成一次实际换位",3);return false;}
    return FinishExpedition(true);
   }
    if(node.used)return false;
    if(node.kind==ExpeditionNodeKind.Launch)
    {
     if(!player.Launch()){Toast("站稳后启动大跳装置",1.5f);return false;}
     node.Activate();if(node.lamp)node.lamp.sharedMaterial=Shapes.Mat(3);
     Toast("大跳已启动 · 空中按 "+KeyName(6)+" 换位",3);sound.Play("phase");Record("launch_pad",node.transform.position.ToString());return true;
    }
   if(node.kind==ExpeditionNodeKind.Jammer){expeditionJammer=false;Toast("干扰已解除 · 换位链路恢复",4);}
   else if(node.kind==ExpeditionNodeKind.Bonus)
   {
    expeditionBonus=true;
    foreach(var offset in new[]{new Vector3(-4,0,-4),new Vector3(4,0,-4)})
     QueueDeployment(EnemyKind.Assault,node.transform.position+offset,stage,true);
    Toast("数据已取得 · 两名追兵预警 · 主线完成后可带追兵撤离 +40",4);
   }
   else {expeditionObjectives++;Toast("授权已记录 "+expeditionObjectives+" / "+expeditionMap.objectiveCount,3);if(expeditionObjectives==expeditionMap.objectiveCount)StartCoroutine(OpenExpeditionReturnBridge(expeditionMap));}
   node.Activate();sound.Play("confirm");Record("expedition_node",node.label);return true;
  }
  IEnumerator OpenExpeditionReturnBridge(ExpeditionMap openedMap)
  {
   openedMap.returnBridge.SetActive(true);Physics.SyncTransforms();
   Toast("全部授权接通 · 回程桥展开，可步行返回入口",4);Record("return_bridge_open",(expeditionLevel+1).ToString());
   yield return openedMap.navigation.UpdateNavMesh(openedMap.navigation.navMeshData);
   if(expeditionMap!=openedMap||!openedMap.root)yield break;
   openedMap.returnBridgeReady=true;Record("return_bridge_navigation_ready",(expeditionLevel+1).ToString());
  }
  public void OnExpeditionSwap(SwapTarget target)
  {if(!expeditionActive)return;expeditionBreachUntil=Time.time+2.5f;Record("expedition_swap",target.name);}
  public bool FinishExpedition(bool success)
  {
   if(!expeditionActive||expeditionStore==null)return false;
   if(expeditionReceipt!=null)return true;
   if(expeditionPending)success=expeditionPendingSuccess;
   else
   {
    if(success&&(!ExpeditionMainComplete||totalSwaps<1))return false;
    expeditionPending=true;expeditionPendingSuccess=success;
   }
   try
   {
    expeditionReceipt=expeditionStore.Settle(expeditionRun,success,expeditionBonus,runSeconds);expeditionPending=false;
    expeditionInventory=false;SetState(success?RunState.Victory:RunState.Dead);
    Record("expedition_settlement",JsonUtility.ToJson(expeditionReceipt));return true;
   }
   catch(Exception e){expeditionNotice="结算保存失败，可重试；结果已锁定："+e.Message;SetState(RunState.Paused);return false;}
  }
  public bool UseExpeditionMedkit()
  {
   if(!expeditionActive||expeditionMedkits<=0||player.health<=0||player.health>=config.playerHealth||expeditionReceipt!=null)return false;
   if(!IsPlaying&&!expeditionInventory)return false;
   expeditionMedkits--;player.health=Mathf.Min(config.playerHealth,player.health+40);sound.Play("confirm");Record("medkit",expeditionMedkits.ToString());return true;
  }
  int ExpeditionReserveLimit(int id) => Mathf.RoundToInt(WeaponCatalog.Get(id).reserve * (expeditionStore.Profile.attachments[id] == 2 ? 1.25f : 1));
  public bool UseExpeditionAmmo()
  {
   if(!expeditionInventory||expeditionAmmoPacks<=0||expeditionReceipt!=null)return false;
   bool useful=false;foreach(int w in player.EquippedWeapons)if(player.reserve[w]<ExpeditionReserveLimit(w))useful=true;
   if(!useful)return false;expeditionAmmoPacks--;
   foreach(int w in player.EquippedWeapons)player.reserve[w]=Mathf.Max(player.reserve[w],Mathf.Min(ExpeditionReserveLimit(w),player.reserve[w]+WeaponCatalog.Get(w).magazine*2));
   sound.Play("confirm");Record("ammo_pack",expeditionAmmoPacks.ToString());return true;
  }
 }
}




