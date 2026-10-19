using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;

namespace SwapHunter
{
    // Native observations of the production hand/knife animations. No timeline
    // is assigned by this fixture; every frame advances through normal Update.
    public static class FocusedEquipmentChecks
    {
        public static IEnumerator Run(Action<string,bool,string> add, string output) => new Suite(add, output).Run();

        [Serializable] sealed class Report
        {
            public string scope = "Isolated physical floor, live first-person animation, native camera stack including both hands. Requested timestamps are thresholds only; each frame records its actual elapsed action time. No permanent inventory grants and no visual-quality claim.";
            public bool permanentProfileUnchanged;
            public List<Stage> stages = new List<Stage>();
        }
        [Serializable] sealed class Stage
        {
            public string file, action;
            public int knifeId, frame, handLayer, knifeLayer;
            public float requestedSeconds, actualSeconds, attackSeconds, inspectSeconds, equipSeconds, pickupSeconds;
            public float gripError, hingeSeparation, tipForwardDot, mainFov, equipmentFov;
            public bool ready, attacking, pickingUp, pickupCommitted;
            public Vector3 eye, grip, tip,tipDirection,cameraForward;
        }
        sealed class Suite
        {
            readonly Action<string,bool,string> add;
            readonly string output;
            readonly Report report = new Report();
            readonly Vector3 at = new Vector3(3600,0,0);
            DemoGame G => DemoGame.I;
            PlayerMotor P => G.player;
            MeleeEquipmentV2 M => P.meleeEquipment;
            public Suite(Action<string,bool,string> callback, string directory) { add=callback; output=directory; }
            void C(string id, bool passed, string detail="") => add(id, passed, detail);
            static float Number(float value) => float.IsNaN(value)||float.IsInfinity(value) ? -1 : value;
            void Aim(Vector3 point)
            {
                Vector3 d=point-P.Eye.position;Vector2 recoil=P.MechanicalRecoil;
                P.SetLook(Mathf.Atan2(d.x,d.z)*Mathf.Rad2Deg-recoil.y,
                    -Mathf.Atan2(d.y,new Vector2(d.x,d.z).magnitude)*Mathf.Rad2Deg+recoil.x);
            }
            IEnumerator Reach(Func<float> seconds, Func<bool> active, float requested, float timeout=2)
            {
                float deadline=Time.realtimeSinceStartup+timeout;
                while(active() && seconds()<requested && Time.realtimeSinceStartup<deadline) yield return null;
            }
            IEnumerator FinishAttack()
            {
                float deadline=Time.realtimeSinceStartup+2;
                while(M.AttackActive && Time.realtimeSinceStartup<deadline) yield return null;
                yield return null;
            }
            IEnumerator ReadyAfterEquip()
            {
                float deadline=Time.realtimeSinceStartup+2;
                while(!M.Ready&&Time.realtimeSinceStartup<deadline)yield return null;
                yield return new WaitForSeconds(.12f);
            }
            IEnumerator Shot(string file,string action,float requested,Func<float> actual)
            {
                // Observe the already-evaluated hand bones and the actual overlay
                // camera. StandardRequest excludes OnGUI/HUD/story subtitles.
                yield return new WaitForEndOfFrame();
                var visual=M.HeldVisual;
                var stage=new Stage {
                    file=file, action=action, requestedSeconds=requested, actualSeconds=actual(),
                    knifeId=M.KnifeId,frame=Time.frameCount,attackSeconds=M.AttackSeconds,inspectSeconds=M.InspectSeconds,equipSeconds=M.EquipSeconds,pickupSeconds=M.PickupSeconds,
                    ready=M.Ready,attacking=M.AttackActive,pickingUp=M.PickingUp,pickupCommitted=M.PickupCommitted,
                    eye=P.Eye.position,grip=visual?visual.Grip.position:Vector3.zero,tip=visual?visual.Tip.position:Vector3.zero,tipDirection=visual?(visual.Tip.position-visual.Grip.position).normalized:Vector3.zero,cameraForward=P.Eye.forward,
                    gripError=visual?Number(P.V2KnifeGripError(visual.Grip)):-1,
                    hingeSeparation=visual?Number(visual.HingeSeparation):-1,
                    tipForwardDot=visual?Vector3.Dot((visual.Tip.position-visual.Grip.position).normalized,P.Eye.forward):0,
                    handLayer=P.V2HandsRenderLayer,knifeLayer=visual?visual.gameObject.layer:-1,
                    mainFov=P.cameraView.fieldOfView,equipmentFov=P.weaponCamera.fieldOfView
                };
                Capture(P.cameraView,Path.Combine(output,file));report.stages.Add(stage);
            }
            public IEnumerator Run()
            {
                if(!RunStorage.IsValidation||!G.qaMode||!G.IsPlaying||!M) { C("focused_equipment_fixture",false,"Requires isolated, freshly begun active chapter and player"); yield break; }
                Directory.CreateDirectory(output);
                Vector3 oldFeet=P.transform.position;float oldYaw=P.yaw,oldPitch=P.pitch,oldHealth=P.health,oldScale=Time.timeScale;
                bool oldAI=G.qaSuppressAI,oldInput=G.qaInputEnabled,oldCrouch=P.IsCrouching,oldPlayerEnabled=P.enabled,oldEquipped=M.Equipped;int oldKnife=M.KnifeId;
                var preview=P.GetComponent<SwapEndpointPreview>();bool oldPreview=preview&&preview.enabled;
                string profileBefore=G.expeditionStore!=null?JsonUtility.ToJson(G.expeditionStore.Profile):"";
                var fixture=new GameObject("Focused equipment / independent physical floor");
                try
                {
                    G.qaSuppressAI=true;G.qaInputEnabled=false;Time.timeScale=1;P.enabled=true;P.target=null;if(preview)preview.enabled=false;
                    P.CancelActions();P.ResetAllReloadV2();M.CancelForAction();M.ResetForAttempt();P.TrySetCrouch(false);P.health=G.config.playerHealth;
                    Shapes.Box("Real equipment fixture floor",fixture.transform,at+Vector3.down*.5f,new Vector3(14,1,14),1,true,Layers.World);
                    Shapes.Box("Neutral inspection backdrop",fixture.transform,at+new Vector3(0,1.5f,5),new Vector3(12,3,.2f),1,true,Layers.World);
                    var light=new GameObject("Equipment inspection key");light.transform.SetParent(fixture.transform,false);light.transform.rotation=Quaternion.Euler(35,-25,0);var lamp=light.AddComponent<Light>();lamp.type=LightType.Directional;lamp.intensity=.7f;lamp.color=new Color(1,.95f,.88f);
                    Physics.SyncTransforms();P.SetFeet(at+Vector3.up*.035f);P.verticalSpeed=0;P.SetLook(0,0);P.SetViewActive(true);yield return null;

                    float equipStarted=Time.time;bool equip=M.ReviewEquipKnife(1);C("focused_m9_review_equip",equip,"Temporary visual equipment; no permanent asset grant");if(!equip)yield break;
                    yield return ReadyAfterEquip();
                    yield return Shot("focused-equipment-01-m9-ready.png","M9 static ready; actualSeconds since normal equip began (equipSeconds=-1 means already ended)",.40f,()=>Time.time-equipStarted);
                    C("focused_m9_ready_contact",M.Equipped&&M.Ready&&!M.AttackActive&&P.V2KnifeGripError(M.HeldVisual.Grip)<.035f,"grip="+P.V2KnifeGripError(M.HeldVisual.Grip)+";equipSeconds="+M.EquipSeconds);

                    bool lightAttack=M.BeginAttack(false,false);C("focused_light_started",lightAttack);
                    yield return Reach(()=>M.AttackSeconds,()=>M.AttackActive,.14f);
                    yield return Shot("focused-equipment-02-m9-light.png","M9 light cross-cut",.14f,()=>M.AttackSeconds);
                    C("focused_light_live_phase",lightAttack&&M.AttackActive&&M.AttackSeconds>=.10f&&M.AttackSeconds<=.34f&&P.V2KnifeGripError(M.HeldVisual.Grip)<.035f,"actualSeconds="+M.AttackSeconds+";grip="+P.V2KnifeGripError(M.HeldVisual.Grip));
                    yield return FinishAttack();C("focused_light_returns_ready",!M.AttackActive&&M.Ready);

                    bool heavyAttack=M.BeginAttack(true,false);C("focused_heavy_started",heavyAttack);
                    yield return Reach(()=>M.AttackSeconds,()=>M.AttackActive,.33f);
                    yield return Shot("focused-equipment-03-m9-thrust.png","M9 heavy forward thrust",.33f,()=>M.AttackSeconds);
                    float forward=Vector3.Dot((M.HeldVisual.Tip.position-M.HeldVisual.Grip.position).normalized,P.Eye.forward);
                    C("focused_heavy_forward_contact",heavyAttack&&M.AttackActive&&M.AttackSeconds>=.26f&&M.AttackSeconds<=.70f&&forward>.85f&&P.V2KnifeGripError(M.HeldVisual.Grip)<.035f,"actualSeconds="+M.AttackSeconds+";tipForwardDot="+forward+";grip="+P.V2KnifeGripError(M.HeldVisual.Grip));
                    yield return FinishAttack();

                    M.Inspect();yield return Reach(()=>M.InspectSeconds,()=>M.InspectSeconds>=0,.45f);
                    yield return Shot("focused-equipment-04-m9-inspect.png","M9 live inspect",.45f,()=>M.InspectSeconds);
                    bool inspecting=M.InspectSeconds>=0;M.CancelForAction();yield return null;
                    yield return Shot("focused-equipment-05-inspect-cancel.png","Inspect canceled back to ready",-1,()=>M.InspectSeconds);
                    C("focused_inspect_cancel_ready",inspecting&&M.InspectSeconds<0&&!M.AttackActive&&M.Ready&&M.Equipped&&P.V2KnifeGripError(M.HeldVisual.Grip)<.035f,"inspectSeconds="+M.InspectSeconds+";ready="+M.Ready);

                    bool butterfly=M.ReviewEquipKnife(3);C("focused_butterfly_review_equip",butterfly);if(!butterfly)yield break;
                    yield return ReadyAfterEquip();M.Inspect();yield return Reach(()=>M.InspectSeconds,()=>M.InspectSeconds>=0,.45f);
                    yield return Shot("focused-equipment-06-butterfly-inspect.png","Butterfly live hinge inspection",.45f,()=>M.InspectSeconds);
                    C("focused_butterfly_hinges_connected",M.InspectSeconds>=0&&M.HeldVisual.HingeSeparation<=.005f&&P.V2KnifeGripError(M.HeldVisual.Grip)<.035f,"actualSeconds="+M.InspectSeconds+";hingeError="+M.HeldVisual.HingeSeparation+";grip="+P.V2KnifeGripError(M.HeldVisual.Grip));
                    M.CancelForAction();M.Unequip();P.SetFeet(at+Vector3.up*.035f);P.SetLook(0,0);yield return null;

                    var pickup=KnifePickupV2.Create(fixture.transform,1,at+new Vector3(.22f,.06f,.22f),Quaternion.Euler(90,0,12));
                    Aim(pickup.Point);Physics.SyncTransforms();yield return null;Vector3 original=pickup.Point;int worldInstance=pickup.Visual.GetInstanceID();bool started=M.TryPickup(pickup);
                    C("focused_ground_pickup_started",started,P.LastV2PickupDiagnostic);if(!started)yield break;
                    yield return Reach(()=>M.PickupSeconds,()=>M.PickingUp,.18f);
                    yield return Shot("focused-equipment-07-ground-approach.png","Ground pickup before grasp",.18f,()=>M.PickupSeconds);
                    C("focused_ground_precontact_world",!M.PickupCommitted&&pickup.Visual&&Vector3.Distance(pickup.Point,original)<.001f,"actualSeconds="+M.PickupSeconds+";worldInstance="+worldInstance);
                    float deadline=Time.realtimeSinceStartup+2;while(M.PickingUp&&!M.PickupCommitted&&Time.realtimeSinceStartup<deadline)yield return null;
                    yield return Shot("focused-equipment-08-ground-grasp.png","Ground pickup actual grip commit",.35f,()=>M.PickupSeconds);
                    C("focused_ground_same_object_contact",M.PickupCommitted&&M.HeldVisual&&M.HeldVisual.GetInstanceID()==worldInstance&&M.LastPickupGripError<.035f&&M.LastPickupWorldTransferError<.001f,"actualSeconds="+M.PickupSeconds+";grip="+M.LastPickupGripError+";worldTransfer="+M.LastPickupWorldTransferError);
                    C("focused_ground_shared_projection",P.V2HandsRenderLayer==Layers.Actor&&M.HeldVisual.gameObject.layer==Layers.Actor&&Mathf.Abs(P.cameraView.fieldOfView-P.weaponCamera.fieldOfView)<.1f,"hands="+P.V2HandsRenderLayer+";knife="+M.HeldVisual.gameObject.layer+";Fov="+P.cameraView.fieldOfView+"/"+P.weaponCamera.fieldOfView);
                    deadline=Time.realtimeSinceStartup+2;while(M.PickingUp&&Time.realtimeSinceStartup<deadline)yield return null;
                    C("focused_ground_owned_once_ready",M.KnifeId==1&&M.Ready&&!pickup.Available&&M.AttemptPickups.Length==1,"attemptCount="+M.AttemptPickups.Length);
                    report.permanentProfileUnchanged=profileBefore==(G.expeditionStore!=null?JsonUtility.ToJson(G.expeditionStore.Profile):"");C("focused_equipment_no_permanent_grant",report.permanentProfileUnchanged);
                    C("focused_equipment_eight_native_frames",report.stages.Count==8,"Actual animation timestamps are recorded in focused-equipment.json; images still require visual review.");
                }
                finally
                {
                    File.WriteAllText(Path.Combine(output,"focused-equipment.json"),JsonUtility.ToJson(report,true));
                    M.CancelForAction();M.ResetForAttempt();M.ReviewEquipKnife(oldKnife);if(!oldEquipped)M.Unequip();P.SetPickupBody(0);P.SetPickupProjectionV2(false);P.TrySetCrouch(oldCrouch);P.SetFeet(oldFeet);P.SetLook(oldYaw,oldPitch);P.health=oldHealth;P.enabled=oldPlayerEnabled;P.target=null;if(preview)preview.enabled=oldPreview;G.qaSuppressAI=oldAI;G.qaInputEnabled=oldInput;Time.timeScale=oldScale;
                    fixture.SetActive(false);UnityEngine.Object.Destroy(fixture);
                }
            }
            static void Capture(Camera camera,string path)
            {
                var target=new RenderTexture(1600,900,24,RenderTextureFormat.ARGB32);Texture2D pixels=null;var previous=RenderTexture.active;
                try{target.Create();RenderPipeline.SubmitRenderRequest(camera,new RenderPipeline.StandardRequest{destination=target});RenderTexture.active=target;pixels=new Texture2D(1600,900,TextureFormat.RGBA32,false);pixels.ReadPixels(new Rect(0,0,1600,900),0,0);pixels.Apply();File.WriteAllBytes(path,pixels.EncodeToPNG());}
                finally{RenderTexture.active=previous;target.Release();UnityEngine.Object.Destroy(target);if(pixels)UnityEngine.Object.Destroy(pixels);}
            }
        }
    }
}
