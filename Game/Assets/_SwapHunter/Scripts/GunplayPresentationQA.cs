using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace SwapHunter
{
    // Opt-in presentation fixtures. Actual Reload/Bolt logic runs at normal time;
    // setup ammunition and actor placement are declared fixtures, not played combat.
    public sealed class GunplayPresentationQA : MonoBehaviour
    {
        [Serializable] public sealed class Check { public string name, detail; public bool passed; }
        [Serializable] public sealed class Frame
        {
            public string file, stage; public int index, ammo, reserve, discarded;
            public float gameSeconds, realSeconds, progress, remaining, separation, measuredSeparation, gripWeight, gripError, wristError, hingeAngle;
            public bool magazineMeshInFrustum;
        }
        [Serializable] public sealed class ReloadRun
        {
            public int weapon, initialAmmo, initialReserve, finalAmmo, finalReserve, expectedTransfer, contactSamples, distinctOldMagazines, maxDiscardCounter, phaseImages;
            public string name, mode, mechanism, hingeName;
            public bool began, finished, earlyAmmoChange, meshPresent, meshSeenInFrustum, contactErrorsValid=true, independentOldMagazineSeen, chamberStageSeen;
            public float duration, seconds, weaponMotion, maxSeparation, maxMeasuredSeparation, finalSeparation, finalMeasuredSeparation, finalMagazineAngle,
                maxGripError, maxWristError, maxHingeAngle, finalHingeAngle, chargingHandleTravel, chargingHandleAngle, observedCaptureFps;
            public List<string> stages=new List<string>(); public List<Frame> frames=new List<Frame>();
        }
        [Serializable] public sealed class ProjectileRun
        {
            public string profile; public int spawned, renderedFrames; public float requestedSpeed, requestedDamage, coreWidth, tailWidth, tailLength;
            public bool sourcePreserved=true, gameplayValuesPreserved=true, noActiveCollider=true, narrowGeometry=true, meshSeenInFrustum;
            public List<Frame> frames=new List<Frame>();
        }
        [Serializable] public sealed class Report
        {
            public string version, kind="Native real-time presentation fixtures. Ammo is set once before each reload; AI is suppressed in the static inspection scene. No per-frame pose, health, ammo or time-scale resets. PNGs are evidence for human review, not automated visual approval.";
            public bool completed, passed, batchMode, visualCaptureSkipped, visualApprovalPerformed=false;
            public string meshCheck="Active enabled mesh renderer intersecting weapon-camera frustum; this does not prove lack of occlusion, attractiveness or realism.";
            public string sequenceTiming="Requested 30 samples/sec without Time.captureFramerate or timeScale changes; frames retain actual game/realtime timestamps and may be slower under PNG readback.";
            public List<Check> checks=new List<Check>(); public List<ReloadRun> reloads=new List<ReloadRun>();
            public List<ProjectileRun> projectiles=new List<ProjectileRun>(); public List<string> errors=new List<string>();
        }
        sealed class FeedPose
        {
            public Transform root, magazine, hinge, handle;
            public Vector3 magazinePoint, handlePoint; public Quaternion magazineRotation, hingeRotation, handleRotation;
            public FeedPose(Transform active,int id)
            {
                root=active; if(!root)return;
                magazine=ImportedModels.Find(root,"magazine");
                hinge=ImportedModels.Find(root,id==5?"break_action":id==9?"cylinder_hinge":"feed_cover");
                handle=ImportedModels.Find(root,"charging_handle");
                if(magazine){magazinePoint=root.InverseTransformPoint(magazine.position);magazineRotation=Quaternion.Inverse(root.rotation)*magazine.rotation;}
                if(hinge)hingeRotation=hinge.localRotation;
                if(handle){handlePoint=handle.localPosition;handleRotation=handle.localRotation;}
            }
            public float Separation=>root&&magazine?root.TransformVector(root.InverseTransformPoint(magazine.position)-magazinePoint).magnitude:0;
            public float MagazineAngle=>root&&magazine?Quaternion.Angle(magazineRotation,Quaternion.Inverse(root.rotation)*magazine.rotation):0;
            public float HingeAngle=>hinge?Quaternion.Angle(hingeRotation,hinge.localRotation):0;
            public float HandleTravel=>handle?Vector3.Distance(handle.localPosition,handlePoint):0;
            public float HandleAngle=>handle?Quaternion.Angle(handle.localRotation,handleRotation):0;
        }
        readonly Report report=new Report(); string output; float started, oldMotion; bool done;
        DemoGame G=>DemoGame.I; PlayerMotor P=>G.player;
        void Awake(){started=Time.realtimeSinceStartup;Application.logMessageReceived+=OnLog;}
        void OnLog(string message,string stack,LogType type)
        {if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)report.errors.Add(type+": "+message+"\n"+stack);}
        void Update(){if(!done&&Time.realtimeSinceStartup-started>420){report.errors.Add("420 second watchdog; incomplete evidence retained");Finish(false);}}
        void OnDestroy(){Application.logMessageReceived-=OnLog;}
        void CheckThat(string name,bool passed,string detail)
        {report.checks.Add(new Check{name=name,passed=passed,detail=detail});Debug.Log("GUNPLAY_QA "+name+" "+passed+" "+detail);Save();}
        void Save(){if(!string.IsNullOrEmpty(output))File.WriteAllText(Path.Combine(output,"gunplay-presentation-report.json"),JsonUtility.ToJson(report,true));}
        void Finish(bool completed)
        {
            if(done)return;done=true;report.completed=completed;report.passed=completed&&report.errors.Count==0&&report.checks.Count>0&&report.checks.TrueForAll(c=>c.passed);
            if(G&&G.options!=null)G.options.weaponMotion=oldMotion;Save();Application.Quit(report.passed?0:1);
        }
        Transform ActiveWeapon(int id)
        {
            string suffix=" / weapon_"+id.ToString("00");
            foreach(var t in P.GetComponentsInChildren<Transform>(false))if(t.name.EndsWith(suffix,StringComparison.Ordinal))return t;
            return null;
        }
        static bool MeshPresent(Transform group,Camera camera=null)
        {
            if(!group)return false;Plane[] planes=camera?GeometryUtility.CalculateFrustumPlanes(camera):null;
            foreach(var renderer in group.GetComponentsInChildren<Renderer>(false))
            {
                var filter=renderer.GetComponent<MeshFilter>();var skin=renderer as SkinnedMeshRenderer;
                bool mesh=filter&&filter.sharedMesh&&filter.sharedMesh.vertexCount>0||skin&&skin.sharedMesh&&skin.sharedMesh.vertexCount>0;
                if(mesh&&renderer.enabled&&renderer.gameObject.activeInHierarchy&&(planes==null||GeometryUtility.TestPlanesAABB(planes,renderer.bounds)))return true;
            }
            return false;
        }
        IEnumerator SetupWeapon(int id,float motion)
        {
            P.SetLoadout(id,id==0?1:0);G.options.weaponMotion=motion;
            yield return new WaitForSeconds(.16f);
        }
        IEnumerator Start()
        {
            var args=Environment.GetCommandLineArgs();int at=Array.IndexOf(args,"-qaOutput");
            output=at>=0&&at+1<args.Length?Path.GetFullPath(args[at+1]):RunStorage.Root;Directory.CreateDirectory(output);
            while(!G||!G.player||G.state==RunState.Loading)yield return null;
            report.version=Application.version;report.batchMode=Application.isBatchMode;report.visualCaptureSkipped=Application.isBatchMode;oldMotion=G.options.weaponMotion;
            CheckThat("isolated_mode",RunStorage.IsValidation&&G.qaMode,"Root must register -swapHunterGunplayQA as an isolated QA mode");
            if(!RunStorage.IsValidation||!G.qaMode){Finish(false);yield break;}
            if(!Application.isBatchMode)while(!UnityEngine.Rendering.SplashScreen.isFinished)yield return null;
            G.StartRun(false);G.qaSuppressAI=true;G.qaInputEnabled=false;
            // One static scene/camera placement before sampling, not a pose override during reloads.
            P.SetFeet(new Vector3(0,.035f,3));P.SetLook(0,0);yield return new WaitForSeconds(.2f);
            for(int id=0;id<WeaponCatalog.Count;id++)
            {
                yield return ReloadCase(id,false,.65f,id<2);
                yield return ReloadCase(id,true,0,false);
                yield return CancelCase(id,true);
                yield return CancelCase(id,false);
            }
            yield return ProjectileCase("ordinary",EnemyKind.Assault,38,6,.018f,.012f,.38f,false);
            yield return ProjectileCase("sniper",EnemyKind.Sniper,90,30,.020f,.014f,.62f,false);
            yield return ProjectileCase("omni",EnemyKind.Elite,23,5,.030f,.020f,.58f,true);
            G.options.weaponMotion=oldMotion;P.CancelActions();
            if(!Application.isBatchMode)
            {
                float deadline=Time.realtimeSinceStartup+5;
                while(MissingCaptures()>0&&Time.realtimeSinceStartup<deadline)yield return null;
                CheckThat("png_capture_files_present",MissingCaptures()==0,"All recorded PNG paths must exist; this is capture completeness, not visual approval");
                foreach(var run in report.reloads)if(run.mode=="empty-normal")
                    CheckThat("weapon-"+run.weapon.ToString("00")+"_phase_capture_count",run.phaseImages>=6&&run.phaseImages<=10,
                        "Actual captured phase samples="+run.phaseImages+"; continuous capture mean FPS="+run.observedCaptureFps.ToString("F1")+" (actual timestamps retained)");
            }
            P.CancelActions();G.SetState(RunState.Menu);yield return null;yield return null;yield return new WaitForSecondsRealtime(.2f);Finish(true);
        }

        IEnumerator ReloadCase(int id,bool tactical,float motion,bool continuous)
        {
            yield return SetupWeapon(id,motion);
            var pose=new FeedPose(ActiveWeapon(id),id);int capacity=P.Capacity(id);
            var run=new ReloadRun{weapon=id,name=P.CurrentWeaponName,mode=tactical?"tactical-motion-zero":"empty-normal",mechanism=WeaponCatalog.Get(id).mechanism.ToString(),weaponMotion=motion,
                initialAmmo=tactical?Mathf.Max(1,capacity/2):0,initialReserve=capacity*2,hingeName=id==5?"break_action":id==9?"cylinder_hinge":"feed_cover"};
            report.reloads.Add(run);string tag="weapon-"+id.ToString("00")+"-"+run.mode;
            string directory=Path.Combine(output,tag);if(!Application.isBatchMode)Directory.CreateDirectory(directory);
            run.meshPresent=MeshPresent(pose.magazine);
            CheckThat(tag+"_magazine_mesh",pose.root&&pose.magazine&&run.meshPresent,"Authored magazine group must contain an active mesh, not a generated marker");
            if(id==5||id==9)CheckThat(tag+"_independent_hinge",pose.hinge,"Required authored pivot: "+run.hingeName);
            P.ammo[id]=run.initialAmmo;P.reserve[id]=run.initialReserve;
            run.expectedTransfer=Mathf.Min(capacity-run.initialAmmo,run.initialReserve);run.began=P.Reload();run.duration=P.reloadLeft;
            CheckThat(tag+"_reload_started",run.began&&run.duration>0,"Actual public Reload; ammo setup occurs once before the call");
            if(!run.began){run.finalAmmo=P.ammo[id];run.finalReserve=P.reserve[id];Save();yield break;}
            float gameStart=Time.time,realStart=Time.realtimeSinceStartup,nextFrame=gameStart;
            float finishedAt=-1;int phaseIndex=0;bool zeroMotionImage=false;float[] phases={0,.12f,.26f,.40f,.56f,.72f,.88f,1};
            var discardedIds=new HashSet<int>();
            while(Time.realtimeSinceStartup-realStart<run.duration+4)
            {
                yield return null;
                float progress=P.reloadLeft>0?P.ReloadProgress:1;
                bool phaseDue=!tactical&&phaseIndex<phases.Length&&progress>=phases[phaseIndex];
                bool clipDue=continuous&&Time.time>=nextFrame;
                bool zeroDue=tactical&&!zeroMotionImage&&progress>=.5f;
                bool capture=!Application.isBatchMode&&(phaseDue||clipDue||zeroDue);
                if(capture)yield return new WaitForEndOfFrame();
                progress=P.reloadLeft>0?P.ReloadProgress:1;
                string stage=P.ReloadStageTag;if(!run.stages.Contains(stage))run.stages.Add(stage);run.chamberStageSeen|=stage=="chamber";
                if(P.reloadLeft>0&&(P.ammo[id]!=run.initialAmmo||P.reserve[id]!=run.initialReserve))run.earlyAmmoChange=true;
                run.maxSeparation=Mathf.Max(run.maxSeparation,P.MagazineSeparation);run.maxMeasuredSeparation=Mathf.Max(run.maxMeasuredSeparation,pose.Separation);
                run.maxHingeAngle=Mathf.Max(run.maxHingeAngle,pose.HingeAngle);run.chargingHandleTravel=Mathf.Max(run.chargingHandleTravel,pose.HandleTravel);run.chargingHandleAngle=Mathf.Max(run.chargingHandleAngle,pose.HandleAngle);
                run.meshSeenInFrustum|=MeshPresent(pose.magazine,P.weaponCamera);run.maxDiscardCounter=Mathf.Max(run.maxDiscardCounter,P.DiscardedMagazineCount);
                if(P.reloadLeft>0&&P.ReloadGripWeight>=.95f)
                {
                    run.contactSamples++;float grip=P.ReloadGripError,wrist=P.ReloadWristError;
                    run.contactErrorsValid&=grip>=0&&wrist>=0&&!float.IsNaN(grip)&&!float.IsNaN(wrist);
                    run.maxGripError=Mathf.Max(run.maxGripError,grip);run.maxWristError=Mathf.Max(run.maxWristError,wrist);
                }
                foreach(var old in FindObjectsByType<DiscardedMagazineVisual>(FindObjectsSortMode.None))
                {
                    if(old.weaponId!=id||!pose.magazine||old.sourceMagazineId!=pose.magazine.GetInstanceID())continue;discardedIds.Add(old.GetInstanceID());bool activeCollider=false;
                    foreach(var collider in old.GetComponentsInChildren<Collider>(true))activeCollider|=collider.enabled;
                    run.independentOldMagazineSeen|=old.transform.parent==G.effects&&!old.transform.IsChildOf(P.transform)&&old.sourceMagazineId!=0&&MeshPresent(old.transform)&&!activeCollider;
                }
                if(capture)
                {
                    string file=continuous?"frame-"+run.frames.Count.ToString("D4")+".png":tactical?"motion-zero-core-action.png":"phase-"+phaseIndex.ToString("D2")+".png";
                    ScreenCapture.CaptureScreenshot(Path.Combine(directory,file));
                    run.frames.Add(new Frame{file=tag+"/"+file,index=run.frames.Count,gameSeconds=Time.time-gameStart,realSeconds=Time.realtimeSinceStartup-realStart,progress=progress,
                        remaining=P.reloadLeft,stage=stage,ammo=P.ammo[id],reserve=P.reserve[id],discarded=P.DiscardedMagazineCount,separation=P.MagazineSeparation,measuredSeparation=pose.Separation,
                        gripWeight=P.ReloadGripWeight,gripError=P.ReloadGripError,wristError=P.ReloadWristError,hingeAngle=pose.HingeAngle,magazineMeshInFrustum=MeshPresent(pose.magazine,P.weaponCamera)});
                    if(phaseDue)run.phaseImages++;if(zeroDue)zeroMotionImage=true;nextFrame=Time.time+1f/30;
                }
                if(phaseDue)while(phaseIndex<phases.Length&&progress>=phases[phaseIndex])phaseIndex++;
                if(P.reloadLeft<=0){if(finishedAt<0)finishedAt=Time.time;if(Time.time-finishedAt>=.2f){run.finished=true;break;}}
            }
            run.seconds=Time.time-gameStart;run.finalAmmo=P.ammo[id];run.finalReserve=P.reserve[id];run.finalSeparation=P.MagazineSeparation;run.finalMeasuredSeparation=pose.Separation;run.finalMagazineAngle=pose.MagazineAngle;run.finalHingeAngle=pose.HingeAngle;run.distinctOldMagazines=discardedIds.Count;
            if(run.frames.Count>1)run.observedCaptureFps=(run.frames.Count-1)/Mathf.Max(.001f,run.frames[run.frames.Count-1].realSeconds-run.frames[0].realSeconds);
            CheckThat(tag+"_supply_only_at_completion",run.finished&&!run.earlyAmmoChange&&run.finalAmmo==run.initialAmmo+run.expectedTransfer&&run.finalReserve==run.initialReserve-run.expectedTransfer,
                "ammo="+run.initialAmmo+"->"+run.finalAmmo+"; reserve="+run.initialReserve+"->"+run.finalReserve+"; earlyChange="+run.earlyAmmoChange);
            CheckThat(tag+"_feed_returns_to_rest",run.finished&&run.finalSeparation<.005f&&run.finalMeasuredSeparation<.005f&&run.finalMagazineAngle<1&&run.finalHingeAngle<1,
                "property="+run.finalSeparation+"; measured="+run.finalMeasuredSeparation+"; magazineAngle="+run.finalMagazineAngle+"; hinge="+run.finalHingeAngle);
            CheckThat(tag+"_magazine_mesh_enters_frustum",run.meshSeenInFrustum,"Structural frustum evidence only; human must inspect occlusion and presentation");
            if(id==5||id==9)CheckThat(tag+"_hinge_opens",pose.hinge&&run.maxHingeAngle>15,"Independent local pivot peak="+run.maxHingeAngle+" degrees; no box-mag drop requirement");
            else
            {
                CheckThat(tag+"_magazine_leaves_well",run.maxSeparation>=.18f&&run.maxMeasuredSeparation>=.18f,"propertyPeak="+run.maxSeparation+"; measured relative-to-weapon peak="+run.maxMeasuredSeparation+"m; weaponMotion="+motion);
                CheckThat(tag+(tactical?"_tactical_stows_magazine":"_old_magazine_is_independent"),tactical?run.maxDiscardCounter==0&&run.distinctOldMagazines==0:
                    run.maxDiscardCounter==1&&run.distinctOldMagazines==1&&run.independentOldMagazineSeen,
                    "counter="+run.maxDiscardCounter+"; distinct objects="+run.distinctOldMagazines+"; independent mesh="+run.independentOldMagazineSeen);
                if(tactical)CheckThat(tag+"_no_chamber_pull",!run.chamberStageSeen&&run.chargingHandleTravel<.01f&&run.chargingHandleAngle<1,
                    "chamberStage="+run.chamberStageSeen+"; handleTravel="+run.chargingHandleTravel+"; handleAngle="+run.chargingHandleAngle);
            }
            CheckThat(tag+"_contact_grip_and_wrist",run.contactSamples>0&&run.contactErrorsValid&&run.maxGripError<.025f&&run.maxWristError<.025f,
                "Contact weight>=.95; samples="+run.contactSamples+"; grip="+run.maxGripError+"m; wrist="+run.maxWristError+"m; validErrors="+run.contactErrorsValid);
            Save();
        }
        int MissingCaptures()
        {
            int missing=0;
            foreach(var reload in report.reloads)foreach(var frame in reload.frames)if(!File.Exists(Path.Combine(output,frame.file)))missing++;
            foreach(var projectile in report.projectiles)foreach(var frame in projectile.frames)if(!File.Exists(Path.Combine(output,frame.file)))missing++;
            return missing;
        }

        IEnumerator CancelCase(int id,bool switchWeapon)
        {
            yield return SetupWeapon(id,.65f);var pose=new FeedPose(ActiveWeapon(id),id);int reserve=P.Capacity(id)*2;
            P.ammo[id]=0;P.reserve[id]=reserve;bool began=P.Reload();float duration=P.reloadLeft,start=Time.time;
            string tag="weapon-"+id.ToString("00")+(switchWeapon?"-switch-cancel":"-action-cancel");
            while(P.reloadLeft>0&&P.ReloadProgress<.5f&&Time.time-start<duration+1)yield return null;
            bool activeAtCancel=P.reloadLeft>0;int beforeAmmo=P.ammo[id],beforeReserve=P.reserve[id];
            if(switchWeapon)P.SwitchWeapon(id==0?1:0);else P.CancelActions();
            yield return new WaitForSeconds(.12f);
            bool restored=pose.magazine&&pose.Separation<.005f&&pose.MagazineAngle<1&&pose.HingeAngle<1;
            bool oldStillPresent=false;foreach(var old in FindObjectsByType<DiscardedMagazineVisual>(FindObjectsSortMode.None))if(old.weaponId==id)oldStillPresent=true;
            CheckThat(tag+"_restores_feed",began&&activeAtCancel&&restored&&!oldStillPresent,"separation="+pose.Separation+"; hinge="+pose.HingeAngle+"; leftover discard="+oldStillPresent);
            // Wait beyond the original completion time to catch a delayed second award.
            while(Time.time-start<duration+.25f)yield return null;
            CheckThat(tag+"_no_delayed_ammo",P.ammo[id]==0&&P.reserve[id]==reserve&&beforeAmmo==0&&beforeReserve==reserve,
                "ammo="+P.ammo[id]+"; reserve="+P.reserve[id]+"; expected=0/"+reserve);
            if(switchWeapon){P.SwitchWeapon(id);yield return new WaitForSeconds(.12f);}
            CheckThat(tag+"_rest_stays_stable",pose.magazine&&pose.Separation<.005f&&pose.MagazineAngle<1&&pose.HingeAngle<1,"Returned supply-part transforms remain at their original pose");
        }

        IEnumerator ProjectileCase(string profile,EnemyKind kind,float speed,float damage,float width,float tailWidth,float tailLength,bool omni)
        {
            P.CancelActions();P.SetLoadout(0,1);G.options.weaponMotion=.65f;P.SetLook(0,0);
            foreach(var bolt in FindObjectsByType<Bolt>(FindObjectsSortMode.None)){bolt.gameObject.SetActive(false);Destroy(bolt.gameObject);}
            var owner=EnemyActor.Spawn(kind,new Vector3(6,0,13),G.stage);
            CheckThat("projectile_"+profile+"_source",owner,"Real EnemyActor source fixture");if(!owner)yield break;
            if(omni)
            {
                var boss=owner.EnableBoss();owner.Damage(BossEncounter.ShieldMaximum);owner.Damage(361);
                bool begun=boss.TryBeginSkill(BossSkill.OmniFire);float until=Time.time+2;
                while(begun&&boss.SkillStep==BossSkillStep.Telegraph&&Time.time<until){boss.Tick(Time.deltaTime);yield return null;}
                CheckThat("projectile_omni_style_state",begun&&boss.SkillStep==BossSkillStep.Execute,"Explicit Boss phase/skill fixture; first real volley executes, then source AI remains suppressed");
            }
            var result=new ProjectileRun{profile=profile,requestedSpeed=speed,requestedDamage=damage};report.projectiles.Add(result);
            string directory=Path.Combine(output,"projectile-"+profile);if(!Application.isBatchMode)Directory.CreateDirectory(directory);
            float start=Time.time,realStart=Time.realtimeSinceStartup,nextShot=start,nextImage=start;var active=new List<Bolt>();
            while(Time.time-start<1.4f&&G.IsPlaying)
            {
                if(Time.time>=nextShot)
                {
                    nextShot+=.2f;
                    var b=Bolt.Spawn(owner,P.Eye.position+P.Eye.forward*7-P.Eye.right*3,P.Eye.right,speed,damage);active.Add(b);result.spawned++;
                    result.sourcePreserved&=b.owner==owner;result.gameplayValuesPreserved&=Mathf.Abs(b.velocity.magnitude-speed)<.001f&&b.damage==damage&&b.life==3;
                }
                yield return null;bool capture=!Application.isBatchMode&&Time.time>=nextImage;if(capture)yield return new WaitForEndOfFrame();
                foreach(var bolt in active)
                {
                    if(!bolt)continue;var core=bolt.transform.Find("Tracer core");var tail=bolt.GetComponentInChildren<LineRenderer>();
                    foreach(var collider in bolt.GetComponentsInChildren<Collider>(true))result.noActiveCollider&=!collider.enabled;
                    result.narrowGeometry&=core&&tail&&Mathf.Abs(core.localScale.x-width)<.0001f&&Mathf.Abs(core.localScale.y-width)<.0001f&&Mathf.Abs(tail.startWidth-tailWidth)<.0001f&&tail.GetPosition(1).magnitude<=tailLength+.001f;
                    if(core)result.coreWidth=Mathf.Max(result.coreWidth,core.localScale.x);
                    if(tail){result.tailWidth=Mathf.Max(result.tailWidth,tail.startWidth);result.tailLength=Mathf.Max(result.tailLength,tail.GetPosition(1).magnitude);}
                    result.meshSeenInFrustum|=MeshPresent(core,P.cameraView);
                }
                if(capture)
                {
                    string file="frame-"+result.renderedFrames.ToString("D4")+".png";ScreenCapture.CaptureScreenshot(Path.Combine(directory,file));
                    result.frames.Add(new Frame{index=result.renderedFrames,file="projectile-"+profile+"/"+file,gameSeconds=Time.time-start,realSeconds=Time.realtimeSinceStartup-realStart});result.renderedFrames++;nextImage=Time.time+1f/30;
                }
            }
            CheckThat("projectile_"+profile+"_source_and_gameplay",result.spawned>0&&result.sourcePreserved&&result.gameplayValuesPreserved&&result.noActiveCollider,
                "source retained; speed="+speed+"; damage="+damage+"; life=3; activeCollider=false; samples="+result.spawned);
            CheckThat("projectile_"+profile+"_narrow_visual",result.narrowGeometry&&result.meshSeenInFrustum,"coreWidth="+result.coreWidth+"; tailWidth="+result.tailWidth+"; maxTail="+result.tailLength+"; geometry/frustum only, not visual approval");
            owner.gameObject.SetActive(false);Destroy(owner.gameObject);Save();
        }
    }
}
