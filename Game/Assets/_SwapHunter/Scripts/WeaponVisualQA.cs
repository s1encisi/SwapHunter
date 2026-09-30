using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
namespace SwapHunter
{
    // Native weapon presentation evidence. No model transforms or sight tolerances are altered here.
    public static class WeaponVisualQA
    {
        [Serializable] public sealed class Entry
        {
            public int id,width,height;
            public string name,resourcePath,resourceName,instanceName,hipImage,adsImage,reloadImage;
            public bool resourceExists,resourceInstantiated,frontSightExists,rearSightExists,muzzleExists,adsActive,reloadActive;
            public float aimAlignmentError,rearAlignmentError=-1,reloadDuration,reloadRemainingAtCapture;
            public Vector3 frontViewport,rearViewport,muzzleViewport,adsLocalPosition,adsLocalEuler;
            public bool adsPoseCalibrated;
        }
        [Serializable] public sealed class ReloadFrame
        {
            public int index;public string image;public float elapsedGame,elapsedRealtime,remaining,phase,localRise;
            public Vector3 magazineLocal,magazineWorld,leftHandWorld,gripTargetWorld,handContactWorld;public bool reloading,gripRigValid;
            public float gripWeight,gripError,wristError;
        }
        [Serializable] public sealed class ReloadSequence
        {
            public string kind="Native real-time reload samples; no frozen poses, retiming, or interpolated frames.";
            public int weapon=4,activeFrames,grippingFrames,observedActiveFrames,observedGripFrames;
            public string sampling="One photographed reload plus one separate unretimed reload sampled every Update without PNG readback. Ammo setup only before each reload; image frames and logic frames remain separate.";
            public List<ReloadFrame> logicFrames=new List<ReloadFrame>();
            public float duration,minLocalRise,maxLocalRise,maxGripError,maxWristError,gripTolerance=.015f;
            public string gripDefinition="Authored cylinder top at magazine-local (0,.043,0); physical glove palm underside at hand-local (0,-.0275,.04). No generated marker is substituted for contact.";
            public bool passed;
            public List<ReloadFrame> frames=new List<ReloadFrame>();
        }
        [Serializable] public sealed class Report
        {
            public string kind="Native rendered weapon views; hip, actual right-button ADS, and actual reload midpoint. Scripted visual fixtures, not human visual approval.";
            public string sourcePolicy="Weapons 02..11 must have Resources/Weapons/weapon_XX and instantiate that resource; fallback imported base guns are not accepted.";
            public float aimTolerance=.025f;
            public bool complete,passed,topMagazineSequencePassed;
            public List<Entry> weapons=new List<Entry>();
        }
        public static IEnumerator Run(DemoGame g,string output,Action<string,bool,string> check)
        {
            if(!g.qaMode||!RunStorage.IsValidation||g.expeditionActive&&g.expeditionReceipt==null||g.campaignActive)
            {check("weapon_visual_isolation",false,"Run only in isolated validation with no active mission.");yield break;}
            string folder=Path.Combine(output,"weapon-visuals");Directory.CreateDirectory(folder);
            var report=new Report();var oldOptions=g.options;var oldKeys=g.qaKeyboard;var oldMouse=g.qaMouse;
            bool oldInput=g.qaInputEnabled,oldSuppress=g.qaSuppressAI;int oldCheckpoint=RunStorage.Checkpoint;
            var keys=InputSystem.AddDevice<Keyboard>();var mouse=InputSystem.AddDevice<Mouse>();var p=g.player;
            int[] oldLoad=(int[])p.EquippedWeapons.Clone(),oldAmmo=(int[])p.ammo.Clone(),oldReserve=(int[])p.reserve.Clone();int oldWeapon=p.weapon;float oldHealth=p.health;
            try
            {
                if(g.expeditionActive)g.OpenExpeditionBoard();
                g.options=new GameOptions{quality=2,renderScale=1,resolutionWidth=1920,resolutionHeight=1080,windowMode=0,dynamicResolution=false,weaponMotion=.65f};
                g.SyncLegacyBindings();g.ApplySettings(true);g.qaKeyboard=keys;g.qaMouse=mouse;g.qaInputEnabled=true;g.qaSuppressAI=true;
                g.StartRun(true);yield return new WaitForSecondsRealtime(.6f);
                p.SetFeet(new Vector3(0,.035f,34));p.verticalSpeed=0;p.SetLook(0,0);
                for(int id=0;id<WeaponCatalog.Count;id++)
                {
                    InputSystem.QueueStateEvent(mouse,new MouseState());
                    p.SetLoadout(id,id==0?1:0);p.SetLook(0,0);yield return new WaitForSeconds(.4f);
                    var entry=new Entry{id=id,name=p.CurrentWeaponName,width=Screen.width,height=Screen.height,resourcePath=id<2?"ModelLibrary legacy "+id:"Weapons/weapon_"+id.ToString("00")};
                    Transform active=null;
                    foreach(var t in p.GetComponentsInChildren<Transform>(false))
                        if(t.name==p.CurrentWeaponName+" / weapon_"+id.ToString("00")){active=t;break;}
                    GameObject resource=id==0?g.config.models.carbine:id==1?g.config.models.shotgun:Resources.Load<GameObject>(entry.resourcePath);
                    entry.resourceExists=resource;entry.resourceName=resource?resource.name:"MISSING";
                    entry.instanceName=active&&active.childCount>0?active.GetChild(0).name:"MISSING";
                    entry.resourceInstantiated=active&&resource&&active.childCount>0&&entry.instanceName.StartsWith(resource.name,StringComparison.Ordinal);
                    var front=active?ImportedModels.Find(active,"AimFront"):null;
                    var rear=active?ImportedModels.Find(active,"AimRear"):null;
                    var muzzle=active?ImportedModels.Find(active,"Muzzle"):null;
                    entry.frontSightExists=front;entry.rearSightExists=rear;entry.muzzleExists=muzzle;
                    check("weapon_visual_authored_resource_"+id,entry.resourceExists&&entry.resourceInstantiated,
                        "resource="+entry.resourceName+"; actual child="+entry.instanceName+"; fallback not accepted");
                    check("weapon_visual_sight_and_muzzle_"+id,front&&muzzle,"front="+entry.frontSightExists+"; rear="+entry.rearSightExists+"; muzzle="+entry.muzzleExists);
                    entry.hipImage="weapon-"+id.ToString("00")+"-hip.png";yield return Capture(folder,entry.hipImage,check);
                    InputSystem.QueueStateEvent(mouse,new MouseState{buttons=2});yield return new WaitForSeconds(.5f);
                    entry.adsActive=p.IsAiming;entry.aimAlignmentError=p.AimAlignmentError;entry.adsLocalPosition=p.CurrentAdsLocalPosition;entry.adsLocalEuler=p.CurrentAdsLocalEuler;entry.adsPoseCalibrated=p.CurrentAdsCalibrated;
                    if(front)entry.frontViewport=p.weaponCamera.WorldToViewportPoint(front.position);
                    if(rear){entry.rearViewport=p.weaponCamera.WorldToViewportPoint(rear.position);entry.rearAlignmentError=Vector2.Distance(new Vector2(entry.rearViewport.x,entry.rearViewport.y),Vector2.one*.5f);}
                    if(muzzle)entry.muzzleViewport=p.weaponCamera.WorldToViewportPoint(muzzle.position);
                    check("weapon_visual_real_ads_"+id,p.IsAiming,"Actual virtual right mouse button held.");
                    check("weapon_visual_ads_alignment_"+id,entry.adsActive&&entry.frontSightExists&&entry.frontViewport.z>0&&entry.aimAlignmentError<report.aimTolerance,
                        "error="+entry.aimAlignmentError.ToString("F5")+"; front="+entry.frontViewport.ToString("F4")+"; rear="+entry.rearViewport.ToString("F4")+"; tolerance="+report.aimTolerance);
                    check("weapon_visual_rear_alignment_"+id,entry.adsActive&&entry.rearSightExists&&entry.rearViewport.z>0&&entry.rearAlignmentError<report.aimTolerance,
                        "rear error="+entry.rearAlignmentError.ToString("F5")+"; viewport="+entry.rearViewport.ToString("F4")+"; same tolerance="+report.aimTolerance);
                    check("weapon_visual_authored_ads_pose_"+id,entry.adsPoseCalibrated,"Whole-rig pose from unchanged authored sights; position="+entry.adsLocalPosition.ToString("F4")+"; euler="+entry.adsLocalEuler.ToString("F4"));
                    entry.adsImage="weapon-"+id.ToString("00")+"-ads.png";yield return Capture(folder,entry.adsImage,check);
                    InputSystem.QueueStateEvent(mouse,new MouseState());yield return new WaitForSeconds(.12f);
                    p.ammo[id]=Mathf.Max(0,p.Capacity(id)-1);p.reserve[id]=p.Capacity(id)*2;
                    bool started=p.Reload();entry.reloadDuration=p.reloadLeft;
                    if(started)yield return new WaitForSeconds(entry.reloadDuration*.48f);
                    entry.reloadActive=p.reloadLeft>0;entry.reloadRemainingAtCapture=p.reloadLeft;
                    check("weapon_visual_reload_midpoint_"+id,started&&entry.reloadActive&&!p.IsAiming,"remaining="+p.reloadLeft.ToString("F3")+"; duration="+entry.reloadDuration.ToString("F3")+"; ADS="+p.IsAiming);
                    entry.reloadImage="weapon-"+id.ToString("00")+"-reload.png";yield return Capture(folder,entry.reloadImage,check);
                    if(id==4)yield return CaptureTopMagazineReload(g,active,folder,check,value=>report.topMagazineSequencePassed=value);
                    report.weapons.Add(entry);
                    File.WriteAllText(Path.Combine(folder,"weapon-visual-report.json"),JsonUtility.ToJson(report,true));
                }
                report.complete=report.weapons.Count==WeaponCatalog.Count;
                report.passed=report.complete&&report.topMagazineSequencePassed&&report.weapons.TrueForAll(e=>e.resourceExists&&e.resourceInstantiated&&e.adsActive&&e.frontSightExists&&e.muzzleExists&&e.frontViewport.z>0&&e.aimAlignmentError<report.aimTolerance&&e.rearSightExists&&e.rearViewport.z>0&&e.rearAlignmentError<report.aimTolerance&&e.adsPoseCalibrated&&e.reloadActive&&File.Exists(Path.Combine(folder,e.hipImage))&&File.Exists(Path.Combine(folder,e.adsImage))&&File.Exists(Path.Combine(folder,e.reloadImage)));
            }
            finally
            {
                InputSystem.RemoveDevice(keys);InputSystem.RemoveDevice(mouse);
                g.qaKeyboard=oldKeys;g.qaMouse=oldMouse;g.qaInputEnabled=oldInput;g.qaSuppressAI=oldSuppress;
                g.options=oldOptions;g.SyncLegacyBindings();g.ApplySettings(true);RunStorage.Checkpoint=oldCheckpoint;
                p.SetLoadout(oldLoad[0],oldLoad[1]);p.SwitchWeapon(oldWeapon);Array.Copy(oldAmmo,p.ammo,oldAmmo.Length);Array.Copy(oldReserve,p.reserve,oldReserve.Length);p.health=oldHealth;
                p.CancelActions();g.SetState(RunState.Menu);
                File.WriteAllText(Path.Combine(folder,"weapon-visual-report.json"),JsonUtility.ToJson(report,true));
            }
        }
        static IEnumerator CaptureTopMagazineReload(DemoGame g,Transform root,string folder,Action<string,bool,string> check,Action<bool> finished)
        {
            var p=g.player;var magazine=root?ImportedModels.Find(root,"magazine"):null;
            if(!magazine){check("smg_reload_sequence_magazine",false,"Missing authored magazine.");yield break;}
            // Let the previous midpoint demonstration finish before recording a second complete reload.
            while(p.reloadLeft>0)yield return null;
            yield return new WaitForSeconds(.12f);
            Vector3 rest=magazine.localPosition;var hand=ImportedModels.Find(root.parent,"hand_L");
            p.ammo[4]=Mathf.Max(0,p.Capacity(4)-1);p.reserve[4]=p.Capacity(4)*2;
            if(!p.Reload()){check("smg_reload_sequence_start",false,"Second real reload rejected.");yield break;}
            var sequence=new ReloadSequence{duration=p.reloadLeft};
            string directory=Path.Combine(folder,"weapon-04-reload-sequence");Directory.CreateDirectory(directory);
            float start=Time.time,realStart=Time.realtimeSinceStartup,next=start;
            while(Time.time-start<=sequence.duration+.12f)
            {
                if(Time.time<next){yield return null;continue;}
                yield return new WaitForEndOfFrame();
                float elapsed=Time.time-start;int index=sequence.frames.Count;
                string name="frame-"+index.ToString("00")+"-t"+Mathf.RoundToInt(elapsed*1000).ToString("0000")+"ms.png";
                ScreenCapture.CaptureScreenshot(Path.Combine(directory,name));
                float rise=magazine.localPosition.y-rest.y;
                var frame=new ReloadFrame{index=index,image=name,elapsedGame=elapsed,elapsedRealtime=Time.realtimeSinceStartup-realStart,
                    remaining=p.reloadLeft,phase=Mathf.Clamp01(1-p.reloadLeft/sequence.duration),localRise=rise,
                    magazineLocal=magazine.localPosition,magazineWorld=magazine.position,leftHandWorld=hand?hand.position:Vector3.zero,reloading=p.reloadLeft>0,
                    gripTargetWorld=p.ReloadGripTargetWorld,handContactWorld=p.ReloadHandContactWorld,gripRigValid=p.AuthoredReloadGripValid,
                    gripWeight=p.ReloadGripWeight,gripError=p.ReloadGripError,wristError=p.ReloadWristError};
                sequence.frames.Add(frame);if(frame.reloading)sequence.activeFrames++;
                if(frame.reloading&&frame.gripWeight>=.95f)
                {
                    sequence.grippingFrames++;
                    sequence.maxGripError=Mathf.Max(sequence.maxGripError,frame.gripRigValid?frame.gripError:1000);
                    sequence.maxWristError=Mathf.Max(sequence.maxWristError,frame.gripRigValid?frame.wristError:1000);
                }
                sequence.minLocalRise=Mathf.Min(sequence.minLocalRise,rise);sequence.maxLocalRise=Mathf.Max(sequence.maxLocalRise,rise);
                next=start+sequence.frames.Count*(sequence.duration/16f);
                yield return null;
            }
            float deadline=Time.realtimeSinceStartup+3;
            while(Time.realtimeSinceStartup<deadline)
            {
                bool all=true;foreach(var frame in sequence.frames)if(!File.Exists(Path.Combine(directory,frame.image))){all=false;break;}
                if(all)break;yield return null;
            }
            bool images=true;float upwardWithdrawal=0;foreach(var frame in sequence.frames){images&=File.Exists(Path.Combine(directory,frame.image));if(frame.phase>=.20f&&frame.phase<=.40f)upwardWithdrawal=Mathf.Max(upwardWithdrawal,frame.localRise);}
            bool reseated=sequence.frames.Count>0&&Mathf.Abs(sequence.frames[sequence.frames.Count-1].localRise)<.005f;
            // PNG encoding stalls are not animation frame coverage. Preserve the
            // photographed sequence, then measure a second real reload without IO.
            p.ammo[4]=Mathf.Max(0,p.Capacity(4)-1);p.reserve[4]=p.Capacity(4)*2;
            bool observedStarted=p.Reload();float observedAt=Time.time,observedWall=Time.realtimeSinceStartup;
            while(observedStarted&&p.reloadLeft>0&&Time.realtimeSinceStartup-observedWall<sequence.duration+3)
            {
                yield return null;if(p.reloadLeft<=0)break;sequence.observedActiveFrames++;
                var observed=new ReloadFrame{index=sequence.logicFrames.Count,elapsedGame=Time.time-observedAt,elapsedRealtime=Time.realtimeSinceStartup-observedWall,
                    remaining=p.reloadLeft,phase=p.ReloadProgress,localRise=magazine.localPosition.y-rest.y,reloading=true,gripWeight=p.ReloadGripWeight,
                    gripRigValid=p.AuthoredReloadGripValid,gripError=p.ReloadGripError,wristError=p.ReloadWristError,
                    magazineLocal=magazine.localPosition,magazineWorld=magazine.position,gripTargetWorld=p.ReloadGripTargetWorld,handContactWorld=p.ReloadHandContactWorld};
                sequence.logicFrames.Add(observed);
                if(observed.phase>=.20f&&observed.phase<=.40f)upwardWithdrawal=Mathf.Max(upwardWithdrawal,observed.localRise);
                if(observed.gripWeight>=.95f){sequence.observedGripFrames++;sequence.maxGripError=Mathf.Max(sequence.maxGripError,observed.gripRigValid?observed.gripError:1000);sequence.maxWristError=Mathf.Max(sequence.maxWristError,observed.gripRigValid?observed.wristError:1000);}
            }
            reseated&=p.reloadLeft<=0&&Mathf.Abs(magazine.localPosition.y-rest.y)<.005f;
            sequence.passed=images&&sequence.frames.Count>=8&&sequence.observedActiveFrames>=12&&upwardWithdrawal>.18f&&reseated&&sequence.observedGripFrames>=6&&sequence.maxGripError<=sequence.gripTolerance&&sequence.maxWristError<=sequence.gripTolerance;
            check("smg_reload_actual_palm_contact",sequence.observedGripFrames>=6&&sequence.maxGripError<=sequence.gripTolerance,"Actual rigid palm-to-cylinder surface error max="+sequence.maxGripError.ToString("F5")+"m; held frames="+sequence.grippingFrames+"; tolerance="+sequence.gripTolerance);
            check("smg_reload_wrist_connected",sequence.observedGripFrames>=6&&sequence.maxWristError<=sequence.gripTolerance,"Actual authored sleeve endpoint to glove wrist error max="+sequence.maxWristError.ToString("F5")+"m");
            check("smg_reload_real_time_samples",images&&sequence.frames.Count>=8&&sequence.observedActiveFrames>=12,"Photographed active/total="+sequence.activeFrames+"/"+sequence.frames.Count+"; no-IO observed active="+sequence.observedActiveFrames+"; duration="+sequence.duration);
            check("smg_reload_exits_upwards",upwardWithdrawal>.18f&&reseated,"Upward clearance before stowing="+upwardWithdrawal.ToString("F5")+"; reseated="+reseated+"; total min/max="+sequence.minLocalRise.ToString("F5")+"/"+sequence.maxLocalRise.ToString("F5"));
            File.WriteAllText(Path.Combine(directory,"timeline.json"),JsonUtility.ToJson(sequence,true));finished(sequence.passed);
        }
        static IEnumerator Capture(string folder,string name,Action<string,bool,string> check)
        {
            yield return new WaitForEndOfFrame();string path=Path.Combine(folder,name);ScreenCapture.CaptureScreenshot(path);
            float until=Time.realtimeSinceStartup+2;
            yield return null;yield return null;
            while(!File.Exists(path)&&Time.realtimeSinceStartup<until)yield return null;
            check("weapon_visual_frame_"+name,File.Exists(path),path);
        }
    }
}
