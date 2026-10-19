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
  public int expeditionLevel,expeditionSelected,expeditionObjectives,expeditionWave,expeditionMedkits,expeditionAmmoPacks,expeditionArmorPacks;
  public ExpeditionStore expeditionStore; public ExpeditionMap expeditionMap; public ExpeditionReceipt expeditionReceipt;
  public string expeditionNotice="";
  string expeditionRun=""; bool expeditionPendingSuccess,expeditionAbandonConfirm;
  float expeditionBreachUntil;
  bool expeditionAwaitingRetry, expeditionDeathPending;
  public bool ExpeditionAwaitingRetry=>expeditionAwaitingRetry;
  bool TacticalExpedition=>expeditionStore!=null&&expeditionStore.IsTactical;
  int ActiveExpeditionAttachment(int id)=>TacticalExpedition&&expeditionStore.Profile.deployment!=null?expeditionStore.Profile.deployment.attachments[id]:expeditionStore.AttachmentFor(id);
  int expeditionTab,expeditionWeaponSlot,expeditionSelectedWeapon;
  internal int ExpeditionTabForReview=>expeditionTab;
  internal int ExpeditionWeaponForReview=>expeditionSelectedWeapon;
  Transform classicWorld;
  public string ExpeditionTitle=>ExpeditionCatalog.Levels[Mathf.Clamp(expeditionLevel,0,11)].name;
  public MissionRuntime mission {get;private set;}
  public int ExpeditionRequiredEnemies=>mission==null?0:mission.RequiredRemaining;
  public int ExpeditionOptionalEnemies=>mission==null?0:mission.LivingNonRequired;
  public int ExpeditionUntriggeredEnemies=>mission==null?0:mission.UntriggeredBudget;
  public int ExpeditionWarningEnemies=>mission==null?0:mission.WarningBudget;
  public int ExpeditionCancelledEnemies=>mission==null?0:mission.CancelledBudget;
  public bool ExpeditionMainComplete=>mission!=null&&mission.CanExtract;
  public string ExpeditionObjective=>mission==null?"行动未部署":mission.Status;
  public bool MissionObjectiveComplete(string id)=>mission!=null&&mission.ObjectiveComplete(id);
  public bool CompleteMissionObjective(string id)=>mission!=null&&mission.CompleteObjective(id);
  int Spec=>expeditionActive&&expeditionStore!=null&&!TacticalExpedition?expeditionStore.Profile.specialization:-1;
  int Rank=>Spec<0?0:expeditionStore.Profile.ranks[Spec];
  int Attachment=>expeditionActive&&expeditionStore!=null&&player?ActiveExpeditionAttachment(Mathf.Clamp(player.weapon,0,11)):0;
  public float ExpeditionDamageMultiplier=>Spec==0&&Time.time<expeditionBreachUntil?1+.08f*(Rank+1):1;
  public float ExpeditionReloadMultiplier=>Attachment==1?1.08f:1;
  public float ExpeditionSpreadMultiplier=>Attachment==1?.85f:Attachment==3?1.12f:1;
  public float ExpeditionMoveMultiplier=>(Spec==1?1-.02f*(Rank+1):Spec==2?1+.03f*(Rank+1):1)*(Attachment==2?.96f:Attachment==3?1.05f:1);
  public float ExpeditionDamageTaken=>Spec==1?1-.05f*(Rank+1):Spec==2?1+.03f*(Rank+1):1;
  public float ExpeditionCooldownMultiplier=>Spec==2?1-.05f*(Rank+1):1;
  void InitializeExpedition()
  {
   try{bool tactical=RunStorage.IsTacticalDemo||RunStorage.IsTacticalValidation||(!RunStorage.IsValidation&&!RunStorage.IsDemonstration);expeditionStore=new ExpeditionStore(Path.Combine(RunStorage.Root,tactical?"Expedition-Tactical-v6":"Expedition-v4"),tactical);expeditionNotice=expeditionStore.Notice;expeditionSelected=expeditionStore.Profile.highestUnlocked;}
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
   ClearStoryAttempt();
   if(mission!=null){mission.Stop("world_clear");mission=null;}
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
   if(expeditionStore==null||campaignActive||expeditionPending||TrialSessionActive||TrialAccountReadBlocked)return false;
   if(expeditionActive&&expeditionReceipt==null)return false;
   try { string run=expeditionStore.Begin(level); return DeployExpeditionAttempt(level,run); }
   catch(Exception e){expeditionNotice="无法部署："+e.Message;return false;}
  }
  bool DeployExpeditionAttempt(int level,string run)
  {
   try
   {
    ClearExpeditionWorld();expeditionRun=run;expeditionLevel=level;expeditionSelected=level;
    expeditionActive=true;expeditionBoard=false;expeditionInventory=false;expeditionReceipt=null;expeditionBonus=false;expeditionJammer=false;
    expeditionObjectives=expeditionWave=0;expeditionBreachUntil=0;expeditionAbandonConfirm=false;
    expeditionAwaitingRetry=expeditionDeathPending=false;expeditionNotice="";
    campaignBoard=false;practice=false;lastSettlement=null;stage=0;stageComplete=combatComplete=false;wave=0;nextWaveAt=-1;
    totalKills=totalSwaps=totalShots=totalHits=0;deaths=TacticalExpedition?expeditionStore.Profile.deployment.deaths:0;
    runSeconds=0;tutorialMoved=tutorialShot=tutorialSwapped=false;
    foreach(var enemy in enemies)if(enemy){enemy.gameObject.SetActive(false);Destroy(enemy.gameObject);}enemies.Clear();
    foreach(Transform child in effects){child.gameObject.SetActive(false);Destroy(child.gameObject);}
    if(!classicWorld)classicWorld=world;classicWorld.gameObject.SetActive(false);
    expeditionMap=ExpeditionWorld.Build(this,level);world=expeditionMap.root;player.ResetAt(expeditionMap.spawn);
    var deployment=expeditionStore.Profile.deployment;
    player.SetLoadout(TacticalExpedition?deployment.primary:expeditionStore.Primary,TacticalExpedition?deployment.secondary:expeditionStore.Secondary);
    foreach(int w in player.EquippedWeapons)if(ActiveExpeditionAttachment(w)==2)player.reserve[w]=Mathf.RoundToInt(player.reserve[w]*1.25f);
    // V2: each control region has authored finite supplies; no arbitrary half-reserve penalty.
    expeditionMedkits=TacticalExpedition?deployment.carriedConsumables[0]:Spec==1?3:2;expeditionAmmoPacks=TacticalExpedition?deployment.carriedConsumables[1]:1;expeditionArmorPacks=TacticalExpedition?deployment.carriedConsumables[2]:0;
    MissionCatalog.Validate();
    var layout=expeditionMap.root.GetComponent<MissionLayout>();
    if(!layout)throw new InvalidOperationException("章节未绑定 MissionLayout");
    mission=new MissionRuntime(this,layout,MissionCatalog.Get(level));
    SetState(RunState.Playing);BeginAttemptMetrics();BeginStoryAttempt();mission.Tick(0);
    Record("d2_configuration","rules="+TacticalPrototypeRules.Version+";extendedMark="+DamageResolution.ExtendedPlayerMarking+";relay="+FireControlRelay.FeatureEnabled+";hazardChoices="+EnemyHazardAwareness.DecisionsEnabled+";facility="+D2MaintenanceFacility.FeatureEnabled+";relayRange=13/14;connect=.8;deviceHP=48");
    Toast(mission.Definition.instruction,9);Record("expedition_start",(level+1)+":"+run);
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
  void HandleExpeditionDeath()
  {
   if(!TacticalExpedition){FinishExpedition(false);return;}
   if(expeditionAwaitingRetry||expeditionReceipt!=null)return;
   EndAttemptMetrics("death");
   expeditionAwaitingRetry=true;expeditionDeathPending=true;expeditionInventory=false;
   if(mission!=null)mission.Stop("death_retry");
   SetState(RunState.Dead);SaveExpeditionDeath();
  }
  bool SaveExpeditionDeath()
  {
   if(!expeditionDeathPending)return true;
   try{expeditionStore.RecordDeath(expeditionRun);expeditionDeathPending=false;expeditionNotice="";return true;}
   catch(Exception e){expeditionNotice="死亡记录保存失败，重试或退出时将再次保存："+e.Message;return false;}
  }
  public bool RetryExpedition()
  {
   if(!TacticalExpedition||!expeditionAwaitingRetry||expeditionPending||!SaveExpeditionDeath())return false;
   try{var deployment=expeditionStore.Retry(expeditionRun);return DeployExpeditionAttempt(deployment.level,deployment.runId);}
   catch(Exception e){expeditionNotice="无法恢复出发快照："+e.Message;return false;}
  }
  public bool AbandonExpedition()
  {
   if(!SaveExpeditionDeath())return false;
   return FinishExpedition(false);
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
   // ConsumableUseV2 owns F/T timing and commits through the same use methods.
   if(mission!=null)mission.Tick(Time.deltaTime);
   foreach(var node in expeditionMap.nodes)if(node&&node.kind==ExpeditionNodeKind.Extraction)node.SetExtractionReady(ExpeditionMainComplete);

  }
  public ExpeditionNode FindExpeditionNode()
  {
   if(InteractionV2.Active){InteractionV2.Instance.RefreshFocus();return InteractionV2.Instance.Focus as ExpeditionNode;}
   if(!expeditionActive||expeditionMap==null||!player)return null;ExpeditionNode nearest=null;float range=2.8f;
   foreach(var n in expeditionMap.nodes)
   {
    if(!n||n.used&&!n.repeatable&&n.kind!=ExpeditionNodeKind.Extraction)continue;Vector3 delta=n.Point-player.Eye.position;
    if(delta.magnitude>=range||Vector3.Dot(delta.normalized,player.Eye.forward)<.15f)continue;
    if(Physics.Linecast(player.Eye.position,n.Point,out var sight,Layers.WorldMask)&&sight.collider.GetComponentInParent<ExpeditionNode>()!=n)continue;
    nearest=n;range=delta.magnitude;
   }
   return nearest;
  }
  public bool UseExpeditionNode(ExpeditionNode node)
  {
   if(!IsPlaying||player.SwapActionsLocked||node==null||node!=FindExpeditionNode())return false;
   if(node.useAction!=null)return node.useAction();
   if(node.kind==ExpeditionNodeKind.Extraction)
   {
    if(!ExpeditionMainComplete){Toast("先完成本关任务和指定威胁；普通活敌与未触发预算无需全清",3);return false;}
    if(expeditionLevel==0&&totalSwaps<1){Toast("相位链路尚未校准 · 至少完成一次实际换位",3);return false;}
    return FinishExpedition(true);
   }
    if(node.used)return false;
    if(node.kind==ExpeditionNodeKind.Launch)
    {
     if(!player.Launch()){Toast("站稳后启动大跳装置",1.5f);return false;}
     node.Activate();if(node.lamp)node.lamp.sharedMaterial=Shapes.Mat(3);
     Toast("大跳已启动 · 空中按 "+KeyName(6)+" 换位",3);sound.Play("phase");Record("launch_pad",node.transform.position.ToString());return true;
    }
   if(node.kind==ExpeditionNodeKind.Bonus)
   {
    if(expeditionBonus)return false;
    expeditionBonus=true;node.Activate();
    Toast("额外数据已携带 · 成功撤离 +40 · "+MissionCatalog.Get(expeditionLevel).bonusRisk,4);
    sound.Play("confirm");Record("mission_bonus",expeditionLevel+":"+node.label);return true;
   }
   var layout=expeditionMap.root.GetComponent<MissionLayout>();
   if(layout.TryObjective(node,out var id)&&CompleteMissionObjective(id))
   {sound.Play("confirm");return true;}
   return false;
  }
  public void OnExpeditionSwap(SwapTarget target)
  {
   if(!expeditionActive)return;expeditionBreachUntil=Time.time+2.5f;
   Record("expedition_swap",target.name);
  }
  void RecordExpeditionResources(ExpeditionReceipt receipt)
  {
   bool hasPlayer=player;
   int[] equipped=hasPlayer?player.EquippedWeapons:new int[0];
   var weapons=new WeaponResourceTelemetry[equipped.Length];
   for(int slot=0;slot<equipped.Length;slot++)
   {
    int id=equipped[slot];
    weapons[slot]=new WeaponResourceTelemetry{slot=slot,weaponId=id,magazine=player.ammo[id],reserve=player.reserve[id]};
   }
   var snapshot=new ExpeditionResourceTelemetry{
    runId=receipt.runId,level=receipt.level,success=receipt.success,committed=true,
    hasPlayer=hasPlayer,health=hasPlayer?player.health:0,
    medkits=expeditionMedkits,ammoPacks=expeditionAmmoPacks,armorPacks=expeditionArmorPacks,temporaryArmor=hasPlayer?player.TemporaryArmor:0,weapons=weapons
   };
   RecordResourceSnapshot("expedition_resources","Committed receipt "+receipt.runId+"; level="+receipt.level+"; success="+receipt.success,snapshot);
  }
  public bool FinishExpedition(bool success)
  {
   if(!expeditionActive||expeditionStore==null)return false;
   if(expeditionReceipt!=null)return true;
   if(expeditionPending)success=expeditionPendingSuccess;
   else
   {
    if(success&&!ExpeditionMainComplete)return false;
    expeditionPending=true;expeditionPendingSuccess=success;
    Record("mission_exit",expeditionLevel+" success="+success+" health="+player.health+" medkits="+expeditionMedkits+" ammoPacks="+expeditionAmmoPacks+" feet="+player.transform.position);
    if(mission!=null)mission.Stop(success?"extracted":"failed");
   }
   try
   {
    expeditionReceipt=expeditionStore.Settle(expeditionRun,success,expeditionBonus,runSeconds,TacticalExpedition&&success?expeditionStore.Profile.deployment.carriedConsumables[0]-expeditionMedkits:0,TacticalExpedition&&success?expeditionStore.Profile.deployment.carriedConsumables[1]-expeditionAmmoPacks:0,TacticalExpedition&&success?expeditionStore.Profile.deployment.carriedConsumables[2]-expeditionArmorPacks:0,player.meleeEquipment?player.meleeEquipment.AttemptPickups:null,player.meleeEquipment?player.meleeEquipment.KnifeId:0);expeditionPending=false;
    expeditionInventory=false;expeditionAwaitingRetry=false;SetState(success?RunState.Victory:RunState.Dead);
    Record("expedition_settlement",JsonUtility.ToJson(expeditionReceipt));
    // mission_exit is intent; only a successfully persisted receipt reaches this snapshot.
    RecordExpeditionResources(expeditionReceipt);
    EndAttemptMetrics(success?"success":"abandon");RecordAttemptSettlement(expeditionReceipt);StorySettlement(expeditionReceipt);return true;
   }
   catch(Exception e){expeditionNotice="结算保存失败，可重试；结果已锁定："+e.Message;SetState(RunState.Paused);return false;}
  }
  public bool UseExpeditionMedkit()
  {
   if(expeditionPending||!expeditionActive||expeditionMedkits<=0||player.health<=0||player.health>=config.playerHealth||expeditionReceipt!=null)return false;
   if(!IsPlaying&&!expeditionInventory)return false;
   float before=player.health;expeditionMedkits--;player.health=Mathf.Min(config.playerHealth,player.health+(TacticalExpedition?TacticalPrototypeRules.MedkitHeal:40));RecordAttemptConsumable(0,player.health-before);sound.Play("confirm");Record("medkit",expeditionMedkits.ToString());return true;
  }
  internal int ExpeditionReserveLimit(int id) => Mathf.RoundToInt(EffectiveWeaponStats.Read(id,config).Reserve * (ActiveExpeditionAttachment(id) == 2 ? 1.25f : 1));
  public bool UseExpeditionAmmo()
  {
   if(expeditionPending||(!expeditionInventory&&!IsPlaying)||expeditionAmmoPacks<=0||expeditionReceipt!=null)return false;
   bool useful=false;foreach(int w in player.EquippedWeapons)if(player.reserve[w]<ExpeditionReserveLimit(w))useful=true;
   if(!useful)return false;expeditionAmmoPacks--;
   int granted=0;foreach(int w in player.EquippedWeapons){int before=player.reserve[w];player.reserve[w]=Mathf.Max(before,Mathf.Min(ExpeditionReserveLimit(w),before+EffectiveWeaponStats.Read(w,config).AmmoPackRounds));granted+=player.reserve[w]-before;}
   RecordAttemptConsumable(1,granted);
   sound.Play("confirm");Record("ammo_pack",expeditionAmmoPacks.ToString());return true;
  }
  public bool UseExpeditionArmor()
  {
   if(!TacticalExpedition||!expeditionActive||expeditionPending||expeditionArmorPacks<=0||expeditionReceipt!=null||!player||player.health<=0)return false;
   if(!IsPlaying&&!expeditionInventory)return false;
   if(!player.TryApplyArmor(TacticalPrototypeRules.ArmorAmount)){Toast("现有护甲尚未耗尽，无需使用应急护甲",2);return false;}
   expeditionArmorPacks--;RecordAttemptConsumable(2,0);sound.Play("confirm");Record("armor_pack",expeditionArmorPacks.ToString());return true;
  }

 }
}




