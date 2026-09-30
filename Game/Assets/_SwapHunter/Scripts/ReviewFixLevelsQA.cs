using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
namespace SwapHunter
{
 public sealed partial class ReviewFixQA
 {
  Keyboard levelKeys;
  IEnumerator LevelChecks()
  {
   if(Array.IndexOf(Environment.GetCommandLineArgs(),"-reviewLevelPursuit")>=0){yield return LevelPursuitCheck();yield break;}
   var previousStore=G.expeditionStore;var previousKeys=G.qaKeyboard;bool previousInput=G.qaInputEnabled,previousSuppression=G.qaSuppressAI;
   levelKeys=InputSystem.AddDevice<Keyboard>();G.qaKeyboard=levelKeys;G.qaInputEnabled=true;G.qaSuppressAI=true;
   try
   {
    Check("level_fixture_scope",true,"Isolated profile, suppressed AI, lethal enemy damage and setup positioning isolate route/gate checks; return bridges are crossed with W driving the real PlayerMotor. Not a human playthrough.");
    if(G.expeditionActive&&G.expeditionReceipt==null)G.FinishExpedition(false);
    G.campaignActive=false;G.OpenExpeditionBoard();
    var store=new ExpeditionStore(Path.Combine(output,"level-review-profile-"+Guid.NewGuid().ToString("N")));
    G.expeditionStore=store;store.Profile.highestUnlocked=11;
    Check("level01_deploy",G.BeginExpedition(0),"Normal first chapter deployment in isolated profile.");yield return null;
    var map=G.expeditionMap;
    Check("return_bridge_initially_absent",!map.returnBridge.activeSelf&&!Physics.Raycast(new Vector3(0,3,22),Vector3.down,5,Layers.WorldMask),"Gap stays physical until all authorizations are active.");
    Check("closed_bridge_not_navigable",!CompleteLevelPath(new Vector3(0,0,28),new Vector3(0,0,14)),"No initial NavMesh path crosses the physical void.");
    var training=G.enemies.Find(e=>e&&e.Alive&&e.kind==EnemyKind.Training);
    P.SetFeet(new Vector3(5,.04f,16));P.verticalSpeed=0;P.cooldown=0;Aim(training.AimPoint);yield return null;
    Vector3 entrance=P.transform.position;int swaps=P.swapCount;
    Check("enemy_first_crossing_valid",P.RequestSwap(training),P.ValidateSwap(training));yield return new WaitForSeconds(.25f);
    Check("enemy_first_crossing_exchanges_both_ends",P.swapCount==swaps+1&&P.transform.position.z>30&&Vector3.Distance(training.transform.position,entrance)<.2f,"player="+P.transform.position+"; training="+training.transform.position);
    training.Damage(10000);yield return null;
    Check("entrance_enemy_killed_anchors_still_across",!training.Alive&&map.anchors.TrueForAll(a=>a.transform.position.z>27),"The historical risky branch has no remaining target on the entrance island.");
    var objective=map.nodes.Find(n=>n.kind==ExpeditionNodeKind.Objective);
    yield return UseLevelNode(objective,"level01_authorization");yield return WaitForReturnNavigation();
    Check("objective_number_and_completion_feedback",objective.objectiveIndex==1&&objective.worldLabel.text.Contains("[01]")&&objective.worldLabel.text.Contains("已完成")&&objective.lamp.sharedMaterial==Shapes.Mat(10),objective.worldLabel.text);
    Check("return_bridge_nav_path",CompleteLevelPath(new Vector3(0,0,28),new Vector3(0,0,14)),"Asynchronously updated NavMesh crosses the former gap.");
    P.SetFeet(new Vector3(0,.04f,28));P.verticalSpeed=0;Aim(new Vector3(0,1.6f,14));yield return Capture("levels-01-return-bridge-open");
    yield return WalkLevelTo(new Vector3(0,0,14),"return_bridge_physical_walk");
    yield return WalkLevelTo(new Vector3(0,0,6),"return_walk_to_entrance");
    yield return WalkLevelTo(new Vector3(-4,0,6),"return_walk_to_extraction");
    var extraction=map.nodes.Find(n=>n.kind==ExpeditionNodeKind.Extraction);Aim(extraction.Point);yield return null;
    Check("extraction_ready_label",extraction.worldLabel.text.Contains("可撤离"),extraction.worldLabel.text);
    Check("enemy_first_route_extracts",G.UseExpeditionNode(extraction)&&G.expeditionReceipt.success,"Extraction after physical return; no SetFeet across the bridge.");
    Check("calibration_first_reward_unchanged",G.expeditionReceipt!=null&&G.expeditionReceipt.reward==80,"first="+(G.expeditionReceipt==null?-1:G.expeditionReceipt.reward));

    // Keep a legacy replay receipt in the fixture and prove the new rule is prospective.
    string legacyId="legacy-replay-"+Guid.NewGuid().ToString("N");
    store.Profile.receipts.Add(new ExpeditionReceipt{runId=legacyId,level=0,reward=30,success=true,seconds=12});store.Profile.credits+=30;
    string replay=store.Begin(0);var replayReceipt=store.Settle(replay,true,false,12);int credits=store.Profile.credits;
    Check("calibration_replay_is_five",replayReceipt.reward==5&&!replayReceipt.first,"reward="+replayReceipt.reward);
    var old=store.Settle(legacyId,true,false,12);var reloaded=new ExpeditionStore(Path.GetDirectoryName(store.SavePath));
    Check("legacy_receipt_and_growth_preserved",old.reward==30&&store.Profile.credits==credits&&reloaded.Profile.credits==credits&&reloaded.Profile.receipts.Exists(r=>r.runId==legacyId&&r.reward==30),"Legacy receipt remains 30; persisted credits="+credits);
    var otherFirst=store.Settle(store.Begin(1),true,false,15);var otherReplay=store.Settle(store.Begin(1),true,false,15);
    Check("other_chapter_rewards_unchanged",otherFirst.reward==95&&otherReplay.reward==30,"chapter2 first="+otherFirst.reward+" replay="+otherReplay.reward);

    yield return BeginLevelFixture(3);
    Check("mandatory_pending_budget_counted",G.ExpeditionRequiredEnemies==G.PendingDeployments&&G.ExpeditionRequiredEnemies>0,"Initial mandatory deployment budget="+G.ExpeditionRequiredEnemies);
    ClearActors();G.expeditionWave=ExpeditionCatalog.Levels[3].waves;
    var required=EnemyActor.Spawn(EnemyKind.Training,new Vector3(-9,0,35),G.stage);
    foreach(var n in G.expeditionMap.nodes)if(n.kind==ExpeditionNodeKind.Objective)yield return UseLevelNode(n,"optional_setup_authorization_"+n.objectiveIndex);
    yield return WaitForReturnNavigation();
    Check("required_enemy_still_blocks_extraction",G.ExpeditionRequiredEnemies==1&&!G.ExpeditionMainComplete,"Required training actor isolates the mandatory-enemy gate.");
    var bonus=G.expeditionMap.nodes.Find(n=>n.kind==ExpeditionNodeKind.Bonus);int warnings=G.DeploymentWarnings;
    yield return UseLevelNode(bonus,"optional_data_interaction");
    Check("optional_pending_budget_warned",G.ExpeditionOptionalEnemies==2&&G.PendingDeployments==2&&G.DeploymentWarnings>=warnings+2,"optional="+G.ExpeditionOptionalEnemies+" warnings="+(G.DeploymentWarnings-warnings));
    Check("optional_does_not_bypass_required_enemy",!G.ExpeditionMainComplete,"Live mandatory enemy continues to block extraction.");
    required.Damage(10000);yield return null;
    Check("optional_pending_does_not_block_main",G.ExpeditionMainComplete&&G.ExpeditionOptionalEnemies==2,"All mandatory enemies cleared; optional queue remains.");
    yield return new WaitForSeconds(4);
    int liveOptional=0;foreach(var e in G.enemies)if(e&&e.Alive&&e.expeditionOptional)liveOptional++;
    Check("optional_flag_survives_deployment",liveOptional==2&&G.ExpeditionRequiredEnemies==0&&G.ExpeditionMainComplete,"live optional="+liveOptional);
    G.totalSwaps=1;extraction=G.expeditionMap.nodes.Find(n=>n.kind==ExpeditionNodeKind.Extraction);
    P.SetFeet(extraction.transform.position+new Vector3(0,.04f,2));Aim(extraction.Point);yield return null;yield return Capture("levels-02-live-data-pursuers-extraction");
    Check("extract_with_live_optional_enemies",G.UseExpeditionNode(extraction)&&G.expeditionReceipt.success&&G.expeditionReceipt.bonus&&liveOptional==2,"Fixture positions player at extraction to isolate the optional-enemy gate; live pursuers are retained.");

    yield return BeginLevelFixture(6);ClearActors();G.expeditionWave=ExpeditionCatalog.Levels[6].waves;
    int activated=0;foreach(var n in G.expeditionMap.nodes)if(n.kind==ExpeditionNodeKind.Objective)
    {
     yield return UseLevelNode(n,"relay_authorization_"+n.objectiveIndex);activated++;
     if(activated<G.expeditionMap.objectiveCount)Check("relay_bridge_waits_for_all_"+activated,!G.expeditionMap.returnBridge.activeSelf,"Partial authorization retains original disconnected islands.");
    }
    yield return WaitForReturnNavigation();
    Check("relay_islands_connect_to_extraction",CompleteLevelPath(new Vector3(13,0,53),new Vector3(-4,0,6))&&CompleteLevelPath(new Vector3(-12,0,65),new Vector3(-4,0,6)),"Both far islands receive a complete NavMesh route to the entrance after authorization.");
    P.SetFeet(new Vector3(-14,.04f,60));P.verticalSpeed=0;
    yield return WalkLevelTo(new Vector3(-14,0,44),"relay_return_bridge_physical_walk");yield return Capture("levels-03-relay-return-route");
    G.FinishExpedition(false);

    yield return BossGroundDeploymentCheck();

    foreach(int chapter in new[]{8,9,10})
    {
     yield return BeginLevelFixture(chapter);ClearActors();G.expeditionWave=ExpeditionCatalog.Levels[chapter].waves;
     bool reserves=true,magazines=true;foreach(int id in P.EquippedWeapons)
     {var w=WeaponCatalog.Get(id);reserves&=P.reserve[id]==Mathf.RoundToInt(w.reserve*(chapter==9?.5f:1));magazines&=P.ammo[id]==w.magazine;}
     Check("starting_reserve_chapter_"+(chapter+1),reserves&&magazines,"Only chapter10 starts with half reserve; magazines unchanged.");
     if(chapter==9)
     {
      int id=P.EquippedWeapons[0],before=P.reserve[id];G.expeditionInventory=true;G.SetState(RunState.Paused);
      bool used=G.UseExpeditionAmmo();int expected=Mathf.Min(WeaponCatalog.Get(id).reserve,before+WeaponCatalog.Get(id).magazine*2);
      Check("chapter10_ammo_recovery_preserved",used&&P.reserve[id]==expected&&G.expeditionMap.root.GetComponentsInChildren<SupplyPickup>().Length==1,"Ammo pack remains two magazines; original one field supply remains.");
      G.expeditionInventory=false;G.SetState(RunState.Playing);yield return Capture("levels-04-chapter10-reserve");
     }
     G.FinishExpedition(false);
    }
   }
   finally
   {
    InputSystem.QueueStateEvent(levelKeys,new KeyboardState());InputSystem.RemoveDevice(levelKeys);G.qaKeyboard=previousKeys;G.qaInputEnabled=previousInput;G.qaSuppressAI=previousSuppression;
    if(G.expeditionActive&&G.expeditionReceipt==null)G.FinishExpedition(false);G.OpenExpeditionBoard();G.expeditionStore=previousStore;
   }
  }
  [Serializable] sealed class LevelPursuitResult
  {
   public string kind="Chapter4 optional-data extraction with real AI and W/Shift movement; isolated main-goal setup before capture, not human balance research";
   public int chapter=4,seed=704,enemyShots,optionalAlive,optionalPending,medkitsUsed,shieldsUsed,shieldBlocks;
   public float startHP,endHP,seconds,wallSeconds,plannedPathDistance,travelledDistance,minimumY;
   public bool mainReadyAtStart,dataTaken,success,aliveAtEnd;
   public string failure="",startPosition,endPosition;
  }
  IEnumerator LevelPursuitCheck()
  {
   var previousStore=G.expeditionStore;var previousKeys=G.qaKeyboard;bool previousInput=G.qaInputEnabled,previousSuppression=G.qaSuppressAI;var previousRandom=UnityEngine.Random.state;
   var result=new LevelPursuitResult();float began=0,wallBegan=0;bool recorded=false;
   levelKeys=InputSystem.AddDevice<Keyboard>();G.qaKeyboard=levelKeys;G.qaInputEnabled=true;G.qaSuppressAI=true;UnityEngine.Random.InitState(result.seed);
   try
   {
    Check("pursuit_scope",true,"Chapter4 main authorizations, defeated mandatory enemies and pre-data position use isolated setup. From data pickup onward: real AI, no SetFeet, no clear/damage enemies, no HP overrides, normal shield cooldown and finite medkits. Observe the deployment for 5 seconds, then sprint along a complete NavMesh path.");
    if(G.expeditionActive&&G.expeditionReceipt==null)G.FinishExpedition(false);
    G.campaignActive=false;G.OpenExpeditionBoard();G.expeditionStore=new ExpeditionStore(Path.Combine(output,"pursuit-profile-"+Guid.NewGuid().ToString("N")));G.expeditionStore.Profile.highestUnlocked=3;
    if(!G.BeginExpedition(3)){Check("pursuit_deploy",false,G.expeditionNotice);yield break;}
    ClearActors();G.expeditionWave=ExpeditionCatalog.Levels[3].waves;yield return null;
    // Complete one ordinary exchange in setup; leave its relocated beacon away from the return path.
    P.SetFeet(new Vector3(8,.04f,14));P.verticalSpeed=0;Aim(G.expeditionMap.anchors[0].AimPoint);
    Check("pursuit_setup_actual_swap",P.RequestSwap(G.expeditionMap.anchors[0]),"Ordinary Q transaction before the measured extraction segment.");yield return new WaitForSeconds(.25f);
    foreach(var node in G.expeditionMap.nodes)if(node.kind==ExpeditionNodeKind.Objective)yield return UseLevelNode(node,"pursuit_setup_authorization_"+node.objectiveIndex);
    yield return WaitForReturnNavigation();
    var bonus=G.expeditionMap.nodes.Find(n=>n.kind==ExpeditionNodeKind.Bonus);var extraction=G.expeditionMap.nodes.Find(n=>n.kind==ExpeditionNodeKind.Extraction);
    P.SetFeet(bonus.transform.position+new Vector3(0,.04f,-2));P.verticalSpeed=0;Aim(bonus.Point);yield return null;
    Vector3 finish=extraction.transform.position+new Vector3(0,.04f,2);var path=new NavMeshPath();
    bool pathReady=NavMesh.CalculatePath(P.transform.position,finish,NavMesh.AllAreas,path)&&path.status==NavMeshPathStatus.PathComplete;
    Check("pursuit_complete_return_path",pathReady,"Real fourth-chapter cover, stairs and newly opened bridge.");
    if(!pathReady)yield break;
    for(int i=1;i<path.corners.Length;i++)result.plannedPathDistance+=Vector3.Distance(path.corners[i-1],path.corners[i]);
    result.mainReadyAtStart=G.ExpeditionMainComplete;result.startHP=P.health;result.startPosition=P.transform.position.ToString("F3");result.minimumY=P.transform.position.y;
    G.qaSuppressAI=false;began=Time.time;wallBegan=Time.realtimeSinceStartup;recorded=true;
    yield return Capture("pursuit-01-before-data");Aim(bonus.Point);
    result.dataTaken=G.UseExpeditionNode(bonus);G.Record("review_pursuit_start","hp="+result.startHP+"; path="+result.plannedPathDistance+"; seed="+result.seed);
    if(!result.dataTaken){result.failure="Data interaction rejected";Check("pursuit_data_taken",false,result.failure);yield break;}
    Check("pursuit_data_taken",true,"Optional reinforcements use the normal warned deployment queue.");
    float waitUntil=Time.time+5;Vector3 previous=P.transform.position;
    while(Time.time<waitUntil&&G.IsPlaying)
    {
     PursuitDefend(result);yield return null;result.travelledDistance+=Vector3.Distance(previous,P.transform.position);previous=P.transform.position;
    }
    yield return Capture("pursuit-02-real-reinforcements");
    bool bridgeCaptured=false;float deadline=Time.realtimeSinceStartup+45;
    foreach(var corner in path.corners)
    {
     while(Vector3.ProjectOnPlane(corner-P.transform.position,Vector3.up).magnitude>.45f&&G.IsPlaying&&Time.realtimeSinceStartup<deadline)
     {
      PursuitDefend(result);Vector3 direction=corner-P.transform.position;P.SetLook(Mathf.Atan2(direction.x,direction.z)*Mathf.Rad2Deg,0);
      InputSystem.QueueStateEvent(levelKeys,new KeyboardState(Key.W,Key.LeftShift));yield return null;
      result.travelledDistance+=Vector3.Distance(previous,P.transform.position);previous=P.transform.position;result.minimumY=Mathf.Min(result.minimumY,previous.y);
      if(!bridgeCaptured&&P.transform.position.z<28.5f){bridgeCaptured=true;yield return Capture("pursuit-03-crossing-return-bridge");}
     }
     if(!G.IsPlaying||Time.realtimeSinceStartup>=deadline)break;
    }
    InputSystem.QueueStateEvent(levelKeys,new KeyboardState());yield return new WaitForSeconds(.25f);
    result.travelledDistance+=Vector3.Distance(previous,P.transform.position);
    if(G.IsPlaying&&Vector3.ProjectOnPlane(finish-P.transform.position,Vector3.up).magnitude<1)
    {Aim(extraction.Point);yield return Capture("pursuit-04-entrance-with-pursuers");result.success=G.UseExpeditionNode(extraction)&&G.expeditionReceipt!=null&&G.expeditionReceipt.success&&G.expeditionReceipt.bonus;}
    else result.failure=G.IsPlaying?"Return route did not finish within 45 seconds":"Player died before extraction";
    if(!result.success&&string.IsNullOrEmpty(result.failure))result.failure="Extraction interaction rejected";
    UpdatePursuitResult(result,began,wallBegan);
    Check("pursuit_real_ai_fired",result.enemyShots>0,"shots="+result.enemyShots+"; optional alive="+result.optionalAlive);
    Check("pursuit_physical_return_with_live_enemies",result.success&&result.optionalAlive>0&&result.minimumY>-.25f,"success="+result.success+"; HP "+result.startHP+" -> "+result.endHP+"; seconds="+result.seconds+"; distance="+result.travelledDistance+"; enemies="+result.optionalAlive+"; failure="+result.failure);
    yield return Capture("pursuit-05-result");
   }
   finally
   {
    if(recorded){UpdatePursuitResult(result,began,wallBegan);File.WriteAllText(Path.Combine(output,"level-pursuit-report.json"),JsonUtility.ToJson(result,true));G.Record("review_pursuit_end",JsonUtility.ToJson(result));}
    InputSystem.QueueStateEvent(levelKeys,new KeyboardState());InputSystem.RemoveDevice(levelKeys);G.qaKeyboard=previousKeys;G.qaInputEnabled=previousInput;G.qaSuppressAI=previousSuppression;UnityEngine.Random.state=previousRandom;
    if(G.expeditionActive&&G.expeditionReceipt==null)G.FinishExpedition(false);G.OpenExpeditionBoard();G.expeditionStore=previousStore;
   }
  }
  void PursuitDefend(LevelPursuitResult result)
  {
   if(P.health<=55&&G.UseExpeditionMedkit()){result.medkitsUsed++;G.Record("review_pursuit_medkit","hp="+P.health+"; remaining="+G.expeditionMedkits);}
   if(P.health>70||P.tactics.ShieldCooldownLeft>0||P.tactics.ShieldActive)return;
   EnemyActor nearest=null;float range=24;
   foreach(var e in G.enemies)if(e&&e.Alive&&e.expeditionOptional&&!Physics.Linecast(P.Eye.position,e.AimPoint,Layers.WorldMask))
   {float distance=Vector3.Distance(P.transform.position,e.transform.position);if(distance<range){range=distance;nearest=e;}}
   if(nearest){Aim(nearest.AimPoint);if(P.tactics.ActivateShield()){result.shieldsUsed++;G.Record("review_pursuit_shield","normal cooldown; target="+nearest.transform.position);}}
  }
  void UpdatePursuitResult(LevelPursuitResult result,float began,float wallBegan)
  {
   result.endHP=P.health;result.seconds=Time.time-began;result.wallSeconds=Time.realtimeSinceStartup-wallBegan;result.endPosition=P.transform.position.ToString("F3");result.aliveAtEnd=P.health>0;
   result.enemyShots=result.optionalAlive=0;foreach(var e in G.enemies)if(e&&e.expeditionOptional){result.enemyShots+=e.shotsFired;if(e.Alive)result.optionalAlive++;}
   result.optionalPending=G.PendingDeployments;result.shieldBlocks=P.tactics.ShieldBlocks;
  }
  IEnumerator BossGroundDeploymentCheck()
  {
   yield return BeginLevelFixture(11);ClearActors();G.expeditionWave=2;
   P.SetFeet(new Vector3(0,.04f,47));P.verticalSpeed=0;P.SetLook(0,0);
   float began=Time.time,until=Time.time+12;int warnings=G.DeploymentWarnings;EnemyActor commander=null;
   while(!commander&&Time.time<until)
   {yield return null;commander=G.enemies.Find(e=>e&&e.Alive&&e.kind==EnemyKind.Elite);}
   bool grounded=commander&&Mathf.Abs(commander.transform.position.y)<.2f;
   bool connected=commander&&CompleteLevelPath(commander.transform.position,new Vector3(0,0,47));
   Check("chapter12_director_boss_ground_deployment",commander&&commander.boss&&grounded&&connected&&G.expeditionWave==3&&G.DeploymentWarnings>warnings&&Time.time-began>=7,
    "Setup clears earlier waves and sets wave=2; normal 5-second director plus warned deployment actually spawns third-wave Boss. feet="+(commander?commander.transform.position.ToString():"missing")+"; full ground path="+connected+"; seconds="+(Time.time-began)+"; warnings="+(G.DeploymentWarnings-warnings));
   if(commander){Aim(commander.AimPoint);yield return Capture("levels-05-director-boss-ground");}
   G.FinishExpedition(false);
  }
  IEnumerator BeginLevelFixture(int level)
  {if(G.expeditionActive&&G.expeditionReceipt==null)G.FinishExpedition(false);G.OpenExpeditionBoard();Check("fixture_deploy_"+(level+1),G.BeginExpedition(level),"Isolated profile.");yield return null;}
  IEnumerator UseLevelNode(ExpeditionNode node,string name)
  {P.SetFeet(node.transform.position+new Vector3(0,.04f,-2));P.verticalSpeed=0;Aim(node.Point);yield return null;Check(name,G.UseExpeditionNode(node),node.DisplayLabel);yield return null;}
  IEnumerator WaitForReturnNavigation()
  {float until=Time.realtimeSinceStartup+15;while(!G.expeditionMap.returnBridgeReady&&Time.realtimeSinceStartup<until)yield return null;Check("return_navigation_ready_"+(G.expeditionLevel+1),G.expeditionMap.returnBridgeReady,"NavMeshSurface.UpdateNavMesh completed.");}
  bool CompleteLevelPath(Vector3 from,Vector3 to)
  {var path=new NavMeshPath();return NavMesh.CalculatePath(from,to,NavMesh.AllAreas,path)&&path.status==NavMeshPathStatus.PathComplete;}
  IEnumerator WalkLevelTo(Vector3 destination,string name)
  {
   Vector3 start=P.transform.position;float minimumY=start.y,until=Time.realtimeSinceStartup+Vector3.Distance(start,destination)/Mathf.Max(1,G.config.walkSpeed)+5;
   while(Vector3.ProjectOnPlane(destination-P.transform.position,Vector3.up).magnitude>.3f&&Time.realtimeSinceStartup<until&&G.IsPlaying)
   {var d=destination-P.transform.position;P.SetLook(Mathf.Atan2(d.x,d.z)*Mathf.Rad2Deg,0);InputSystem.QueueStateEvent(levelKeys,new KeyboardState(Key.W));yield return null;minimumY=Mathf.Min(minimumY,P.transform.position.y);}
   InputSystem.QueueStateEvent(levelKeys,new KeyboardState());yield return new WaitForSeconds(.2f);
   Check(name,Vector3.ProjectOnPlane(destination-P.transform.position,Vector3.up).magnitude<.7f&&minimumY>-.25f&&P.health>0,"W/controller movement from "+start+" to "+P.transform.position+"; minimum y="+minimumY);
  }
 }
}
