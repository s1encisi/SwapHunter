using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;

namespace SwapHunter
{
    // Native frames of normal production actions. The fixture removes one round
    // before each Reload() call; it never assigns reload timers, poses or stages.
    public static class OctoberWeaponMotionChecks
    {
        public static IEnumerator Run(Action<string,bool,string> add,string output)=>new Suite(add,output).Run();

        [Serializable] sealed class Frame
        {
            public int weaponId,frame,ammo,reserve;
            public string name,mechanism,source,file,action,reloadStage,presentationStage;
            public float requestedSeconds,actualSeconds,reloadElapsed,reloadTotal,reloadProgress;
            public float rightPalmError,supportPalmError,reloadPalmError,reloadGripWeight,wristError;
            public bool reloadPending,reloadCommitted,handRigReady;
            public Vector3 eye,muzzle,rightGrip,rightPalm,leftPalm,reloadContact;
        }
        [Serializable] sealed class Report
        {
            public string scope="Twelve actual firearm resources; three native camera-stack frames per weapon: normal SwitchWeapon preparation, held ready, and a real Reload middle phase. TimeScale=1; normal Update/LateUpdate; no animation timers or transforms assigned. One round removed before Reload is an explicit ammo fixture. AI and user input are paused only during this observation. Metrics are diagnostic, not proof of visual quality.";
            public string contactNotes="Right palm should stay at the grip. The left hand intentionally leaves SupportGrip during reload, so supportPalmError is not a pass/fail test there. reloadPalmError is meaningful when reloadGripWeight is near one. Images require actual visual inspection.";
            public bool permanentProfileUnchanged;
            public List<Frame> frames=new List<Frame>();
        }
        sealed class Suite
        {
            readonly Action<string,bool,string> add;readonly string output;readonly Report report=new Report();
            DemoGame G=>DemoGame.I;PlayerMotor P=>G.player;
            public Suite(Action<string,bool,string> callback,string directory){add=callback;output=directory;}
            void Check(string name,bool passed,string detail="")=>add(name,passed,detail);
            static float Finite(float value)=>float.IsNaN(value)||float.IsInfinity(value)?-1:value;
            IEnumerator Until(Func<bool> ready,float timeout)
            {float until=Time.realtimeSinceStartup+timeout;while(!ready()&&Time.realtimeSinceStartup<until)yield return null;}
            IEnumerator Shot(int id,string action,float requested,float began)
            {
                yield return new WaitForEndOfFrame();
                string file="october-firearm-"+id.ToString("00")+"-"+action+".png";
                var frame=new Frame{weaponId=id,name=WeaponCatalog.Get(id).name,mechanism=WeaponCatalog.Get(id).mechanism.ToString(),source=P.CurrentWeaponVisualSource,file=file,action=action,frame=Time.frameCount,
                    requestedSeconds=requested,actualSeconds=action=="reload"?P.ReloadV2Elapsed:Time.time-began,ammo=P.ammo[id],reserve=P.reserve[id],
                    reloadStage=P.ReloadV2Stage,presentationStage=P.ReloadStageTag,reloadElapsed=P.ReloadV2Elapsed,reloadTotal=P.ReloadV2Total,reloadProgress=P.ReloadProgress,
                    reloadPending=P.ReloadV2Pending,reloadCommitted=P.ReloadV2Committed,handRigReady=P.Cc0HandRigReady,
                    rightPalmError=Finite(P.RightGripError),supportPalmError=Finite(P.SupportHandGripError),reloadPalmError=Finite(P.ReloadGripError),reloadGripWeight=P.ReloadGripWeight,wristError=Finite(P.ReloadWristError),
                    eye=P.Eye.position,muzzle=P.CurrentMuzzleWorld,rightGrip=P.RightGripTargetWorld,rightPalm=P.RightHandContactWorld,leftPalm=P.ReloadHandContactWorld,reloadContact=P.ReloadGripTargetWorld};
                Capture(P.cameraView,Path.Combine(output,file));report.frames.Add(frame);
                // This validates the captured action identity; geometry/pose quality
                // is determined by inspecting the corresponding native image.
                bool expected=P.weapon==id&&P.Cc0HandRigReady&&P.CurrentWeaponVisualSource==WeaponVisualFactory.SourceKey(id);
                if(action=="reload")expected&=frame.reloadPending&&frame.reloadElapsed>0&&frame.reloadElapsed<frame.reloadTotal;
                Check("firearm_"+id+"_"+action+"_native_frame",expected,"file="+file+";actualSeconds="+frame.actualSeconds+";stage="+frame.reloadStage+"/"+frame.presentationStage+";rightPalm="+frame.rightPalmError+";supportPalm="+frame.supportPalmError+";reloadPalm="+frame.reloadPalmError+";reloadContactWeight="+frame.reloadGripWeight+" (contact metrics are diagnostics)");
            }
            public IEnumerator Run()
            {
                if(!RunStorage.IsValidation||!G||!G.qaMode||!G.IsPlaying||!P)
                {Check("firearm_native_fixture",false,"Requires isolated validation and a freshly begun chapter.");yield break;}
                Directory.CreateDirectory(output);
                bool oldAI=G.qaSuppressAI,oldInput=G.qaInputEnabled,oldPlayerEnabled=P.enabled,oldCrouch=P.IsCrouching;
                float oldScale=Time.timeScale,oldYaw=P.yaw,oldPitch=P.pitch;int oldWeapon=P.weapon;
                int[] oldLoadout=(int[])P.EquippedWeapons.Clone(),oldAmmo=(int[])P.ammo.Clone(),oldReserve=(int[])P.reserve.Clone();
                bool oldKnife=P.meleeEquipment&&P.meleeEquipment.Equipped;
                string profileBefore=G.expeditionStore!=null?JsonUtility.ToJson(G.expeditionStore.Profile):"";
                var preview=P.GetComponent<SwapEndpointPreview>();bool oldPreview=preview&&preview.enabled;
                try
                {
                    G.qaSuppressAI=true;G.qaInputEnabled=false;Time.timeScale=1;P.enabled=true;
                    if(G.StoryModal)G.CloseStoryModal();if(preview)preview.enabled=false;
                    P.CancelActions();P.ResetAllReloadV2();P.UnequipV2Handheld();P.TrySetCrouch(false);P.SetViewActive(true);P.SetLook(0,0);
                    int requestedWeapon=-1;var args=System.Environment.GetCommandLineArgs();int filter=System.Array.IndexOf(args,"-weaponReviewId");if(filter>=0&&(!int.TryParse(args[filter+1],out requestedWeapon)||!WeaponCatalog.Valid(requestedWeapon)))throw new System.ArgumentException("Invalid weapon review id");
                    for(int id=0;id<WeaponCatalog.Count;id++)
                    {
                        if(requestedWeapon>=0&&id!=requestedWeapon)continue;
                        int other=id==0?1:0;
                        // SetLoadout is the existing runtime fixture entry; the
                        // actual observed transition is a normal SwitchWeapon call.
                        P.SetLoadout(other,id);P.UnequipV2Handheld();yield return new WaitForSeconds(.35f);
                        P.SwitchWeapon(id);float switchedAt=Time.time;
                        float preparation=WeaponMotionV2.Get(id).readySeconds*.35f;
                        yield return Until(()=>Time.time-switchedAt>=preparation,1);
                        yield return Shot(id,"switch",preparation,switchedAt);
                        yield return Until(()=>Time.time-switchedAt>=WeaponMotionV2.Get(id).totalSeconds+.16f,2);
                        yield return Shot(id,"held",WeaponMotionV2.Get(id).totalSeconds+.16f,switchedAt);

                        P.ammo[id]=Mathf.Max(0,P.Capacity(id)-1); // Explicit single-round-short fixture.
                        bool started=P.Reload();float reloadStarted=Time.time;
                        Check("firearm_"+id+"_real_reload_started",started,"mechanism="+WeaponCatalog.Get(id).mechanism+";ammoFixture="+P.ammo[id]+"/"+P.Capacity(id)+";timer remains production-owned");
                        if(!started)continue;
                        float midpoint=id==1?.46f:P.ReloadV2Total*.58f;
                        yield return Until(()=>P.ReloadV2Elapsed>=midpoint||!P.ReloadV2Pending,8);
                        yield return Shot(id,"reload",midpoint,reloadStarted);
                        yield return Until(()=>!P.ReloadV2Pending,8);
                        Check("firearm_"+id+"_reload_finished",!P.ReloadV2Pending,"elapsed="+P.ReloadV2Elapsed+";total="+P.ReloadV2Total+";inserted="+P.ReloadV2Inserted);
                    }
                    report.permanentProfileUnchanged=profileBefore==(G.expeditionStore!=null?JsonUtility.ToJson(G.expeditionStore.Profile):"");
                    Check("firearm_requested_native_frames",report.frames.Count==(requestedWeapon>=0?1:WeaponCatalog.Count)*3,"captured="+report.frames.Count+";source/phase checks do not replace visual review");
                    Check("firearm_fixture_no_permanent_profile_change",report.permanentProfileUnchanged);
                }
                finally
                {
                    File.WriteAllText(Path.Combine(output,"october-firearm-motion.json"),JsonUtility.ToJson(report,true));
                    P.CancelActions();P.ResetAllReloadV2();P.SetLoadout(oldLoadout[0],oldLoadout[1]);P.SwitchWeapon(oldWeapon);
                    Array.Copy(oldAmmo,P.ammo,oldAmmo.Length);Array.Copy(oldReserve,P.reserve,oldReserve.Length);
                    if(oldKnife&&P.meleeEquipment)P.meleeEquipment.Equip();else P.UnequipV2Handheld();
                    P.TrySetCrouch(oldCrouch);P.SetLook(oldYaw,oldPitch);P.enabled=oldPlayerEnabled;
                    if(preview)preview.enabled=oldPreview;G.qaSuppressAI=oldAI;G.qaInputEnabled=oldInput;Time.timeScale=oldScale;
                }
            }
            static void Capture(Camera camera,string path)
            {
                var target=new RenderTexture(1600,900,24,RenderTextureFormat.ARGB32);Texture2D pixels=null;var previous=RenderTexture.active;
                try
                {
                    target.Create();var request=new RenderPipeline.StandardRequest{destination=target};
                    if(!RenderPipeline.SupportsRenderRequest(camera,request))throw new InvalidOperationException("Native firearm capture requires camera-stack render request support.");
                    RenderPipeline.SubmitRenderRequest(camera,request);RenderTexture.active=target;
                    pixels=new Texture2D(target.width,target.height,TextureFormat.RGBA32,false);pixels.ReadPixels(new Rect(0,0,target.width,target.height),0,0);pixels.Apply();File.WriteAllBytes(path,pixels.EncodeToPNG());
                }
                finally{RenderTexture.active=previous;target.Release();UnityEngine.Object.Destroy(target);if(pixels)UnityEngine.Object.Destroy(pixels);}
            }
        }
    }
}
