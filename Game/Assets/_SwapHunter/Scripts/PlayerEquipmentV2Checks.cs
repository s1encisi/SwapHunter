using System;using System.Collections;using System.IO;using UnityEngine;using UnityEngine.InputSystem;using UnityEngine.InputSystem.LowLevel;
namespace SwapHunter
{
 public static class PlayerEquipmentV2Checks
 {
  public static IEnumerator Run(Action<string,bool,string> add)=>new Suite(add).Run();
  sealed class Suite
  {
   readonly Action<string,bool,string> add;DemoGame G=>DemoGame.I;PlayerMotor P=>G.player;
   public Suite(Action<string,bool,string> add){this.add=add;}void C(string id,bool ok,string detail="")=>add(id,ok,detail);
   void Aim(Vector3 point){var d=point-P.Eye.position;P.SetLook(Mathf.Atan2(d.x,d.z)*Mathf.Rad2Deg,-Mathf.Atan2(d.y,new Vector2(d.x,d.z).magnitude)*Mathf.Rad2Deg);}
   string Output{get{var a=Environment.GetCommandLineArgs();int i=Array.IndexOf(a,"-qaOutput");return i>=0&&i+1<a.Length?a[i+1]:RunStorage.Root;}}
   void Shot(string name){Directory.CreateDirectory(Output);var image=ScreenCapture.CaptureScreenshotAsTexture();File.WriteAllBytes(Path.Combine(Output,name),image.EncodeToPNG());UnityEngine.Object.Destroy(image);}
   IEnumerator InteractionAndSupply(Transform fixture,Vector3 at)
   {
    bool oldInput=G.qaInputEnabled;var oldKeys=G.qaKeyboard;var oldMouse=G.qaMouse;var oldBindings=G.options.actions;int kits=G.expeditionMedkits;
    var keys=InputSystem.AddDevice<Keyboard>();var mouse=InputSystem.AddDevice<Mouse>();G.qaKeyboard=keys;G.qaMouse=mouse;G.qaInputEnabled=true;G.options.actions=GameOptions.DefaultBindings();
    void KeyInput(params Key[] input){InputSystem.QueueStateEvent(keys,new KeyboardState(input));InputSystem.QueueStateEvent(mouse,new MouseState());}
    var target=new GameObject("Bound main-objective hold fixture");target.transform.SetParent(fixture,false);target.transform.position=at+new Vector3(-.30f,.10f,.65f);var node=target.AddComponent<ExpeditionNode>();node.kind=ExpeditionNodeKind.Objective;node.label="测试主任务终端";node.objectiveIndex=99;int commits=0;
    node.useAction=()=>{commits++;node.used=true;return true;};G.expeditionMap.nodes.Add(node);G.expeditionMap.root.GetComponent<MissionLayout>().BindObjective(node,"qa-v2-main-hold");
    try
    {
     P.meleeEquipment.Unequip();P.CancelConsumableV2();P.ResetAllReloadV2();P.SetFeet(at);P.SetLook(0,0);Aim(node.Point);Physics.SyncTransforms();KeyInput();yield return null;
     KeyInput(Key.E);yield return new WaitForSeconds(.25f);KeyInput();yield return null;
     C("main_hold_short_press_no_commit",commits==0);
     KeyInput(Key.E);yield return new WaitForSeconds(.66f);C("main_hold_commits_once",commits==1,"count="+commits);yield return new WaitForSeconds(.15f);C("main_hold_no_repeat_while_held",commits==1);KeyInput();yield return null;
     P.health=G.config.playerHealth-50;float hp=P.health;G.expeditionMedkits=1;
     KeyInput(Key.F);yield return new WaitForSeconds(.30f);bool began=P.V2UsingConsumable;
     KeyInput();InputSystem.QueueStateEvent(mouse,new MouseState{buttons=1});yield return null;KeyInput();yield return null;
     C("medical_cancel_no_effect_no_charge",began&&!P.V2UsingConsumable&&G.expeditionMedkits==1&&Mathf.Abs(P.health-hp)<.01f);
     KeyInput(Key.F);yield return new WaitForSeconds(1.3f);KeyInput();yield return null;
     C("medical_complete_single_transaction",G.expeditionMedkits==0&&Mathf.Abs(P.health-Mathf.Min(G.config.playerHealth,hp+40))<.01f,"health="+P.health+";kits="+G.expeditionMedkits);
     P.health=G.config.playerHealth;G.expeditionMedkits=1;KeyInput();yield return null;KeyInput(Key.F);yield return null;
     C("medical_full_health_refuses",!P.V2UsingConsumable&&G.expeditionMedkits==1);KeyInput();yield return null;
    }
    finally
    {
     P.CancelConsumableV2();if(InteractionV2.Instance)InteractionV2.Instance.Cancel();G.expeditionMap.nodes.Remove(node);target.SetActive(false);UnityEngine.Object.Destroy(target);
     G.expeditionMedkits=kits;G.qaInputEnabled=oldInput;G.qaKeyboard=oldKeys;G.qaMouse=oldMouse;G.options.actions=oldBindings;InputSystem.RemoveDevice(keys);InputSystem.RemoveDevice(mouse);
    }
   }
   public IEnumerator Run()
   {
    if(!RunStorage.IsValidation||!G.qaMode||!G.IsPlaying||!P.meleeEquipment){C("equipment_v2_fixture",false,"isolated active player required");yield break;}
    var feet=P.transform.position;float yaw=P.yaw,pitch=P.pitch,health=P.health;var equipped=(int[])P.EquippedWeapons.Clone();var ammo=(int[])P.ammo.Clone();var reserve=(int[])P.reserve.Clone();bool crouched=P.IsCrouching;
    var fixture=new GameObject("V2 focused equipment fixture");Vector3 at=new Vector3(400,0,400);Shapes.Box("equipment check floor",fixture.transform,at+Vector3.down*.5f,new Vector3(12,1,12),1,true,Layers.World);
    try
    {
     P.CancelActions();P.SetLoadout(0,1);P.SetFeet(at);P.SetLook(0,0);P.health=G.config.playerHealth;
     yield return new WaitForSeconds(.45f);yield return new WaitForEndOfFrame();
     C("carbine_right_palm_contact",P.RightGripError<.035f,"error="+P.RightGripError.ToString("F5"));
     C("carbine_support_palm_contact",P.SupportHandGripError<.035f,"error="+P.SupportHandGripError.ToString("F5"));
     C("carbine_trigger_finger_contact",P.V2TriggerFingerError<.025f,"error="+P.V2TriggerFingerError.ToString("F5"));Shot("equipment-carbine-grip.png");
     P.ammo[0]=0;P.reserve[0]=60;C("reload_v2_begin",P.BeginReloadV2());P.TickReloadV2(P.reloadLeft*.70f);
     C("reload_before_latch_no_ammo",P.ammo[0]==0&&P.reserve[0]==60&&!P.ReloadV2Committed,"progress="+P.ReloadProgress);
     float elapsed=P.ReloadV2Elapsed;P.SwitchWeapon(1);P.SwitchWeapon(0);
     C("reload_switch_preserves_pre_latch",Mathf.Abs(P.ReloadV2Elapsed-elapsed)<.001f&&P.ammo[0]==0,"elapsed="+elapsed);
     P.TickReloadV2(P.reloadLeft*.2f);int latched=P.ammo[0],remaining=P.reserve[0];
     C("reload_latch_once_before_ready",latched>0&&remaining==60-latched&&P.ReloadV2Committed&&!P.ReloadV2MechanicalReady,"ammo="+latched+";reserve="+remaining);
     P.SuspendReloadV2();P.ResumeReloadV2();P.TickReloadV2(P.reloadLeft+.1f);
     C("reload_post_latch_resume_no_duplicate",P.ammo[0]==latched&&P.reserve[0]==remaining&&P.ReloadV2MechanicalReady);
     P.ammo[0]=0;P.reserve[0]=60;P.BeginReloadV2();float before=P.reloadLeft;
     C("reload_fast_changes_time_only",P.AccelerateReloadV2(.5f)&&P.ammo[0]==0&&P.reserve[0]==60&&Mathf.Abs(P.reloadLeft-before*.5f)<.001f);
     P.TickReloadV2(P.reloadLeft+.01f);C("reload_fast_single_commit",P.ammo[0]+P.reserve[0]==60);
     P.SetLoadout(1,0);P.ammo[1]=0;P.reserve[1]=12;P.BeginReloadV2();float factor=P.reloadLeft/P.ReloadV2Total;P.TickReloadV2(.64f*factor);
     C("pump_before_shell_no_grant",P.ammo[1]==0);P.TickReloadV2(.02f*factor);C("pump_first_shell_commit",P.ammo[1]==1&&P.reserve[1]==11);
     P.EnsureReloadV2Ready();P.TickReloadV2(P.reloadLeft+.01f);C("pump_interrupt_keeps_one_shell",P.ammo[1]==1&&P.reserve[1]==11&&P.ReloadV2MechanicalReady);
     P.BeginReloadV2();P.TickReloadV2(P.reloadLeft+.01f);C("pump_complete_conserves_rounds",P.ammo[1]+P.reserve[1]==12&&P.ReloadV2MechanicalReady);
     P.SetLoadout(0,1);P.SetLook(0,0);P.TrySetCrouch(false);P.meleeEquipment.ResetForAttempt();
     // Test pickup from a real surface with the exact production IK and ownership boundary.
     var knife=KnifePickupV2.Create(fixture.transform,1,at+new Vector3(.22f,.06f,.22f),Quaternion.Euler(90,0,12));Aim(knife.Point);Physics.SyncTransforms();yield return null;
     string reason;C("ground_pickup_reachable",knife.CanBegin(P,out reason),reason+";"+P.LastV2PickupDiagnostic);bool started=P.meleeEquipment.TryPickup(knife);C("ground_pickup_started",started);Vector3 world=knife.Point;
     if(started)
     {
      yield return new WaitForSeconds(.18f);C("pickup_precontact_stays_world",knife.Visual&&!P.meleeEquipment.PickupCommitted&&Vector3.Distance(knife.Point,world)<.001f);
      P.meleeEquipment.CancelPickup();C("pickup_precontact_cancel_no_grant",knife.Available&&P.meleeEquipment.AttemptPickups.Length==0);
      P.SetLook(0,0);Aim(knife.Point);P.meleeEquipment.TryPickup(knife);
      while(P.meleeEquipment.PickingUp&&!P.meleeEquipment.PickupCommitted)yield return null;
      C("pickup_real_hand_contact",P.meleeEquipment.PickupCommitted&&P.meleeEquipment.LastPickupGripError<.035f,"error="+P.meleeEquipment.LastPickupGripError.ToString("F5"));
      C("pickup_same_world_pose",P.meleeEquipment.PickupCommitted&&P.meleeEquipment.LastPickupWorldTransferError<.001f,"transfer="+P.meleeEquipment.LastPickupWorldTransferError.ToString("F5"));
      yield return new WaitForEndOfFrame();
      C("pickup_shared_world_projection",P.V2HandsRenderLayer==Layers.Actor&&P.meleeEquipment.HeldVisual.gameObject.layer==Layers.Actor&&Mathf.Abs(P.cameraView.fieldOfView-P.weaponCamera.fieldOfView)<.1f,"mainFov="+P.cameraView.fieldOfView+";equipmentFov="+P.weaponCamera.fieldOfView+";handLayer="+P.V2HandsRenderLayer+";eye="+P.Eye.position);
      Shot("equipment-ground-grab.png");
      while(P.meleeEquipment.PickingUp&&P.meleeEquipment.PickupSeconds<.705f)yield return null;
      C("pickup_ready_at_seventy",P.meleeEquipment.Ready,"seconds="+P.meleeEquipment.PickupSeconds);
      P.meleeEquipment.CancelPickup();C("pickup_aftercontact_cancel_owned_once",!knife.Available&&P.meleeEquipment.KnifeId==1&&P.meleeEquipment.AttemptPickups.Length==1&&!knife.Reserve(P));
      P.SetLook(0,0);yield return null;yield return new WaitForEndOfFrame();Shot("equipment-m9-first-person.png");C("held_m9_grip_contact",P.V2KnifeGripError(P.meleeEquipment.HeldVisual.Grip)<.035f,"error="+P.V2KnifeGripError(P.meleeEquipment.HeldVisual.Grip).ToString("F5"));
     }
     P.meleeEquipment.Unequip();P.TrySetCrouch(true);yield return new WaitForSeconds(.1f);
     var crouch=KnifePickupV2.Create(fixture.transform,0,at+new Vector3(.20f,.06f,.20f),Quaternion.Euler(90,0,0));Aim(crouch.Point);C("crouching_ground_reach",crouch.CanBegin(P,out reason),reason+";"+P.LastV2PickupDiagnostic);
     P.TrySetCrouch(false);P.SetLook(0,0);yield return new WaitForSeconds(.2f);
     Shapes.Box("physical waist-height table",fixture.transform,at+new Vector3(.22f,.5f,.65f),new Vector3(.5f,1,.5f),1,true,Layers.World);Physics.SyncTransforms();
     var table=KnifePickupV2.Create(fixture.transform,3,at+new Vector3(.22f,1.05f,.42f),Quaternion.Euler(90,0,0),true);Aim(table.Point);
     C("table_far_pose_rejected",!table.CanBegin(P,out reason),reason+";"+P.LastV2PickupDiagnostic);
     // A folded handle is 15 cm beyond this knife's root. Approach the actual grip;
     // the capsule remains 3 cm short of the table's front face after this 5 cm step.
     P.SetFeet(P.transform.position+Vector3.forward*.05f);Physics.SyncTransforms();yield return null;Aim(table.Point);
     C("table_pickup_reachable",table.CanBegin(P,out reason),reason+";"+P.LastV2PickupDiagnostic);
     if(P.meleeEquipment.TryPickup(table)){while(P.meleeEquipment.PickingUp)yield return null;C("table_butterfly_real_commit",P.meleeEquipment.KnifeId==3&&P.meleeEquipment.LastPickupGripError<.035f,"error="+P.meleeEquipment.LastPickupGripError);}
     else C("table_butterfly_real_commit",false,"pickup not started");
     P.SetLook(0,0);yield return null;yield return new WaitForEndOfFrame();Shot("equipment-butterfly-open.png");
     P.meleeEquipment.Inspect();yield return new WaitForSeconds(.30f);yield return new WaitForEndOfFrame();Shot("equipment-butterfly-hinge.png");
     P.meleeEquipment.Unequip();
     var claw=KnifePickupV2.Create(fixture.transform,2,at+new Vector3(.22f,1.05f,.49f),Quaternion.Euler(90,0,0));Aim(claw.Point);
     if(P.meleeEquipment.TryPickup(claw)){while(P.meleeEquipment.PickingUp)yield return null;P.SetLook(0,0);yield return null;yield return new WaitForEndOfFrame();Shot("equipment-claw-ring-grip.png");C("claw_palm_contact",P.V2KnifeGripError(P.meleeEquipment.HeldVisual.Grip)<.035f,"palm="+P.V2KnifeGripError(P.meleeEquipment.HeldVisual.Grip)+";ring="+P.V2TriggerFingerError);}
     else C("claw_palm_contact",false,P.LastV2PickupDiagnostic);
     yield return InteractionAndSupply(fixture.transform,at);
    }
    finally
    {
     P.meleeEquipment.ResetForAttempt();P.SetLoadout(equipped[0],equipped[1]);Array.Copy(ammo,P.ammo,ammo.Length);Array.Copy(reserve,P.reserve,reserve.Length);P.health=health;P.TrySetCrouch(crouched);P.SetFeet(feet);P.SetLook(yaw,pitch);P.SetPickupBody(0);fixture.SetActive(false);UnityEngine.Object.Destroy(fixture);
    }
   }
  }
 }
}
