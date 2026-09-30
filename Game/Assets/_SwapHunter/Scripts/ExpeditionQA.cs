using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.AI;
namespace SwapHunter
{
 public sealed class ExpeditionQA:MonoBehaviour
 {
  [Serializable] public sealed class Check{public string name,detail;public bool passed;}
  [Serializable] public sealed class Report{public string version=Application.version;public string kind="Native scripted integration with controlled positioning and accelerated wave waits; not human playtesting";public bool passed;public List<Check> checks=new List<Check>();}
  readonly Report report=new Report();string output;float started;bool finished;
  DemoGame G=>DemoGame.I;PlayerMotor P=>G.player;
  void Awake(){started=Time.realtimeSinceStartup;Application.logMessageReceived+=OnLog;}
  void Update(){if(!finished&&Time.realtimeSinceStartup-started>480){Add("watchdog",false,"Exceeded 480 seconds");Finish();}}
  void OnLog(string message,string stack,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)Add("runtime_error",false,message);}
  void Add(string name,bool pass,string detail=""){report.checks.Add(new Check{name=name,passed=pass,detail=detail});Debug.Log("EXPEDITION_QA "+name+" "+pass+" "+detail);}
  void Aim(Vector3 point){Vector3 d=(point-P.Eye.position).normalized;P.SetLook(Mathf.Atan2(d.x,d.z)*Mathf.Rad2Deg,-Mathf.Asin(d.y)*Mathf.Rad2Deg);}
  IEnumerator Capture(string name){yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(output,name+".png"));yield return null;yield return null;}
  IEnumerator UseNode(ExpeditionNode n,string label)
  {
   P.SetFeet(n.transform.position+new Vector3(0,.035f,-2.05f));P.verticalSpeed=0;Aim(n.Point);yield return new WaitForSeconds(.15f);
   Add(label+"_reachable",G.FindExpeditionNode()==n,n.label);
   Add(label+"_interaction",G.UseExpeditionNode(n),n.label);
  }
  IEnumerator Start()
  {
   var args=Environment.GetCommandLineArgs();int ix=Array.IndexOf(args,"-qaOutput");output=ix>=0&&ix+1<args.Length?args[ix+1]:RunStorage.Root;Directory.CreateDirectory(output);
   while(!UnityEngine.Rendering.SplashScreen.isFinished)yield return null;yield return new WaitForSecondsRealtime(.5f);
   Add("isolated_profile",RunStorage.IsValidation&&G.expeditionStore.SavePath.StartsWith(RunStorage.Root),G.expeditionStore.SavePath);
   DataChecks();SettingsV04QA.Run(G,Add);
   G.qaSuppressAI=true;G.options=new GameOptions();G.SyncLegacyBindings();G.ApplySettings();
   if(Array.IndexOf(args,"-qaWeaponsOnly")>=0)
   {
    report.kind="Targeted native weapons, actions, settings and visual regression; chapter/routes coverage is reported separately; not human playtesting";
    G.StartRun(true);yield return new WaitForSeconds(.25f);
    yield return WeaponMechanicsQA.Run(G,Add);yield return WeaponVisualQA.Run(G,output,Add);Finish();yield break;
   }
   G.expeditionBoard=false;G.SetState(RunState.Menu);yield return Capture("00-home");
   G.OpenSettings();for(int tab=0;tab<6;tab++){G.SelectSettingsTab(tab);yield return Capture("00-settings-"+tab);}
   G.SelectSettingsTab(1);G.SetBindingScrollForReview(900);yield return Capture("00-settings-bindings-bottom");G.CancelSettings();
   G.OpenExpeditionBoard();yield return Capture("00-action-map");
   G.SetExpeditionTabForReview(1);yield return Capture("00-weapons");
   G.SetExpeditionTabForReview(2);yield return Capture("00-specialization");G.SetExpeditionTabForReview(0);
   if(Array.IndexOf(args,"-qaUiOnly")>=0)
   {
    G.BeginExpedition(0);yield return new WaitForSeconds(.3f);G.expeditionInventory=true;G.SetState(RunState.Paused);yield return Capture("00-backpack-native");
    G.expeditionInventory=false;G.SetState(RunState.Playing);G.FinishExpedition(false);yield return Capture("00-failed-settlement-native");
    G.OpenExpeditionBoard();G.options.uiScale=.8f;yield return Capture("00-ui-scale80-native");G.options.uiScale=1;
    G.expeditionBoard=false;G.StartRun(true);G.qaSuppressAI=true;P.SetLook(0,0);
    foreach(float scale in new[]{1f,.8f})
    {
     G.options.uiScale=scale;yield return new WaitForEndOfFrame();yield return null;
     foreach(float y in new[]{.1f,.5f,.9f})
     {
      Vector3 world=P.cameraView.ViewportToWorldPoint(new Vector3(.35f,y,12));Vector3 expected=P.cameraView.WorldToScreenPoint(world);
      Vector2 actual=G.HudMarkerScreenPosition(world);float error=Vector2.Distance(actual,new Vector2(expected.x,Screen.height-expected.y));
      Add("ui_marker_projection_"+scale+"_"+y,error<.1f,"pixel error="+error);
     }
     Rect mask=G.HudScreenMaskBounds;Add("ui_mask_full_viewport_"+scale,Mathf.Abs(mask.xMin)<.1f&&Mathf.Abs(mask.yMin)<.1f&&Mathf.Abs(mask.width-Screen.width)<.1f&&Mathf.Abs(mask.height-Screen.height)<.1f,mask.ToString());
     yield return Capture("00-hud-scale-"+Mathf.RoundToInt(scale*100));
    }
    G.options.uiScale=1;
    Add("native_ui_only_capture",true,Screen.width+"x"+Screen.height+"; menu/settings/loadout/specialization/backpack/settlement layout fixtures");Finish();yield break;
   }
   G.expeditionStore.SetAttachment(0,2);
   for(int level=0;level<12;level++)
   {
    string key="level_"+(level+1).ToString("00");
    Add(key+"_deploy",G.BeginExpedition(level),G.expeditionNotice);yield return new WaitForSeconds(.25f);
    if(G.expeditionMap==null){Add(key+"_map",false,"Missing map");break;}
    Add(key+"_navmesh",NavMesh.SamplePosition(G.expeditionMap.spawn,out var spawn,.5f,NavMesh.AllAreas),G.expeditionMap.spawn.ToString());
    Add(key+"_gap",!Physics.Raycast(new Vector3(0,3,22),Vector3.down,5,Layers.WorldMask),"No walkable floor across phase gap");
    foreach(var a in G.expeditionMap.anchors)
    {
     Vector3 feet=a.transform.position;
     bool clearance=!Physics.CheckCapsule(feet+Vector3.up*.38f,feet+Vector3.up*1.45f,.285f,Layers.WorldMask,QueryTriggerInteraction.Ignore);
     bool floor=Physics.Raycast(feet+Vector3.up*.15f,Vector3.down,.35f,Layers.WorldMask);
     Add(key+"_anchor_standing_clearance",clearance&&floor,feet.ToString());
    }
    Add(key+"_early_settlement_rejected",!G.FinishExpedition(true),"Main goals incomplete");
    if(level==10)
    {
     P.SetFeet(new Vector3(0,.04f,14));P.cooldown=0;yield return new WaitForSeconds(.1f);
     Add(key+"_jammer_blocks",P.ValidateSwap(G.expeditionMap.anchors[0]).Contains("干扰"));
     yield return UseNode(G.expeditionMap.nodes.Find(n=>n.kind==ExpeditionNodeKind.Jammer),key+"_jammer");
    }
    P.SetFeet(new Vector3(0,.04f,14));P.verticalSpeed=0;P.cooldown=0;
    var anchor=G.expeditionMap.anchors[0];Aim(anchor.AimPoint);yield return new WaitForSeconds(.15f);
    Vector3 before=P.transform.position,other=anchor.transform.position;
    Add(key+"_swap_valid",P.ValidateSwap(anchor)=="可换位",P.ValidateSwap(anchor));
    Add(key+"_swap_requested",P.RequestSwap(anchor));yield return new WaitForSeconds(.2f);
    Add(key+"_bidirectional_swap",Vector3.Distance(P.transform.position,other)<.15f&&Vector3.Distance(anchor.transform.position,before)<.15f,"before="+before+", expected="+other+", player="+P.transform.position+", anchor="+anchor.transform.position);
    Add(key+"_no_free_teleport",G.totalSwaps==1);
    if(level==0)
    {
     int expanded=P.reserve[0];P.reserve[1]=0;G.expeditionInventory=true;G.SetState(RunState.Paused);
     Add("ammo_pack_preserves_expanded_reserve",G.UseExpeditionAmmo()&&P.reserve[0]>=expanded&&P.reserve[1]>0);
     yield return Capture("00-backpack");G.expeditionInventory=false;G.SetState(RunState.Playing);
    }
    P.SetLook(0,0);yield return Capture(key+"-entry");
    foreach(var n in G.expeditionMap.nodes)
    {
     if(n.kind!=ExpeditionNodeKind.Objective)continue;
     Add(key+"_node_ground_"+n.label,Physics.Raycast(n.transform.position+Vector3.up*.2f,Vector3.down,.5f,Layers.WorldMask),n.transform.position.ToString());
     Vector3 approach=n.transform.position+Vector3.back*2.05f;
     bool approachNav=NavMesh.SamplePosition(approach,out var navPoint,.6f,NavMesh.AllAreas)&&Vector3.Distance(navPoint.position,approach)<.6f;
     var path=new NavMeshPath();bool route=NavMesh.CalculatePath(P.transform.position,approach,NavMesh.AllAreas,path)&&path.status==NavMeshPathStatus.PathComplete;
     foreach(var routeAnchor in G.expeditionMap.anchors)
     {
      if(NavMesh.CalculatePath(routeAnchor.transform.position,approach,NavMesh.AllAreas,path)&&path.status==NavMeshPathStatus.PathComplete){route=true;break;}
     }
     Add(key+"_node_approach_"+n.label,approachNav,approach.ToString());
     Add(key+"_node_route_"+n.label,route,"Walkable path from player landing or a phase platform");
     yield return UseNode(n,key+"_"+n.label);
    }
    if(level==3||level==11)yield return UseNode(G.expeditionMap.nodes.Find(n=>n.kind==ExpeditionNodeKind.Bonus),key+"_bonus");
    int guard=0;
    while((G.AliveEnemies>0||G.expeditionWave<ExpeditionCatalog.Levels[level].waves)&&guard++<50)
    {
     foreach(var e in G.enemies.ToArray())if(e&&e.Alive)e.Damage(100000);
     Time.timeScale=5;yield return new WaitForSeconds(5.2f);Time.timeScale=1;
    }
    Add(key+"_main_complete",G.ExpeditionMainComplete,"objectives="+G.expeditionObjectives+", waves="+G.expeditionWave);
    P.health=50;int kits=G.expeditionMedkits;Add(key+"_medkit",G.UseExpeditionMedkit()&&G.expeditionMedkits==kits-1&&P.health==90);
    yield return UseNode(G.expeditionMap.nodes.Find(n=>n.kind==ExpeditionNodeKind.Extraction),key+"_extract");
    Add(key+"_saved",G.expeditionReceipt!=null&&G.expeditionReceipt.success&&G.expeditionStore.Profile.cleared[level]);
    if(level==3||level==11)yield return Capture(key+"-settlement");
    G.OpenExpeditionBoard();yield return null;
   }
   int count=0;foreach(bool owned in G.expeditionStore.Profile.weapons)if(owned)count++;
   Add("all_twelve_weapons_unlock",count==12,count.ToString());
   // Weapon/action integration runs in an isolated classic fixture after chapter worlds are restored.
   G.expeditionBoard=false;G.StartRun(true);yield return new WaitForSeconds(.25f);
   yield return WeaponMechanicsQA.Run(G,Add);
   yield return WeaponVisualQA.Run(G,output,Add);
   Finish();
  }
  void DataChecks()
  {
   try
   {
    string folder=Path.Combine(output,"data-fixture");var s=new ExpeditionStore(folder);
    bool locked=false;try{s.Begin(2);}catch(InvalidOperationException){locked=true;}Add("locked_level_rejected",locked);
    string run=s.Begin(0);bool equipmentLocked=false;try{s.Equip(0,1);}catch(InvalidOperationException){equipmentLocked=true;}Add("equipment_locked_during_run",equipmentLocked);
    var r=s.Settle(run,true,false,30);int credits=s.Profile.credits;
    Add("first_reward",r.first&&credits==80);s.Settle(run,true,true,20);Add("receipt_idempotent",s.Profile.credits==credits);
    s.Equip(0,4);Add("reward_weapon_equippable",s.Profile.primary==4);
    s.SetAttachment(4,1);Add("attachment_persisted",new ExpeditionStore(folder).Profile.attachments[4]==1);
    s.Begin(1);var recovered=new ExpeditionStore(folder);Add("interrupted_run_closed",string.IsNullOrEmpty(recovered.Profile.activeRun)&&recovered.Profile.credits==credits);
    var recoveredAgain=new ExpeditionStore(folder);Add("interruption_once",recoveredAgain.Profile.receipts.Count==recovered.Profile.receipts.Count);
    var malformed=JsonUtility.FromJson<ExpeditionProfile>(JsonUtility.ToJson(s.Profile));malformed.primary=99;bool rejected=false;try{ExpeditionStore.Validate(malformed);}catch(InvalidDataException){rejected=true;}Add("invalid_loadout_rejected",rejected);
    string backupPath=recoveredAgain.SavePath;
    string original=File.ReadAllText(backupPath);int oldSpec=recoveredAgain.Profile.specialization;bool saveRejected=false;
    using(var lockedFile=new FileStream(backupPath,FileMode.Open,FileAccess.Read,FileShare.None))
    {try{recoveredAgain.SelectSpecialization((oldSpec+1)%3);}catch(IOException){saveRejected=true;}}
    Add("failed_write_keeps_memory_and_disk",saveRejected&&recoveredAgain.Profile.specialization==oldSpec&&File.ReadAllText(backupPath)==original);
    recoveredAgain.SelectSpecialization(0); // Ensure backup is an idle profile, so the next mutation is the first recovered write.
    File.WriteAllText(backupPath,"broken");
    var backup=new ExpeditionStore(folder);Add("backup_recovery",backup.Notice.Contains("备份"),backup.Notice);
    string goodBackup=File.ReadAllText(backupPath+".bak");backup.SelectSpecialization(1);
    Add("backup_preserved_after_recovery",File.ReadAllText(backupPath+".bak")==goodBackup);
    Add("corrupt_primary_preserved",Directory.GetFiles(folder,"*.corrupt-*").Length>0);
   }
   catch(Exception e){Add("data_exception",false,e.ToString());}
  }
  void Finish()
  {
   if(finished)return;finished=true;Time.timeScale=1;report.passed=report.checks.TrueForAll(c=>c.passed);
   File.WriteAllText(Path.Combine(output,"expedition-report.json"),JsonUtility.ToJson(report,true));
   Debug.Log("EXPEDITION_QA_RESULT="+report.passed+";checks="+report.checks.Count);Application.Quit(report.passed?0:1);
  }
  void OnDestroy(){Application.logMessageReceived-=OnLog;}
 }
}












