using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Rendering;

namespace SwapHunter
{
    // Production methods and normal frame progression; screenshots remain human
    // review evidence, never an automated claim of visual or audio quality.
    public sealed class OctoberRefinementQA : MonoBehaviour
    {
        [Serializable] public sealed class Check { public string name, detail; public bool passed; }
        [Serializable] public sealed class Frame { public string file, kind; public int knifeId; public float inspectSeconds, gripError; }
        [Serializable] public sealed class Report
        {
            public string scope="October refinement: isolated seeded account, actual deployment/input/actions, storage contracts, native screenshots. No human playthrough or subjective visual/audio pass is implied.";
            public string group; public bool completed, passed;
            public bool pointerOpeningAttempted,pointerOpenedDropdown;
            public string pointerOpeningEvidence="Not attempted";
            public List<Check> checks=new List<Check>(); public List<Frame> frames=new List<Frame>();
        }
        readonly Report report=new Report(); string output; bool done; float started;
        Keyboard keys; Mouse mouse; GameOptions originalOptions;
        DemoGame G=>DemoGame.I; PlayerMotor P=>G.player;
        const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        void Awake()
        {
            var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"-qaOutput");output=i>=0?Path.GetFullPath(args[i+1]):RunStorage.Root;
            i=Array.IndexOf(args,"-octoberGroup");report.group=i>=0?args[i+1]:"all";Directory.CreateDirectory(output);
            started=Time.realtimeSinceStartup;Application.logMessageReceived+=OnLog;
        }
        void OnLog(string text,string stack,LogType kind){if(!done&&(kind==LogType.Error||kind==LogType.Exception||kind==LogType.Assert))C("runtime_error",false,text+"\n"+stack);}
        void C(string name,bool passed,string detail=""){report.checks.Add(new Check{name=name,passed=passed,detail=detail});Debug.Log("OCTOBER_QA "+name+" "+passed+" "+detail);}
        bool Group(string value)=>report.group=="all"||report.group==value;
        void Field(string name,object value){var field=typeof(DemoGame).GetField(name,Private);if(field==null)throw new MissingFieldException(name);field.SetValue(G,value);}
        void Call(string name){var method=typeof(DemoGame).GetMethod(name,Private);if(method==null)throw new MissingMethodException(name);method.Invoke(G,null);}
        ExpeditionStore Seed(string name)
        {
            string directory=Path.Combine(RunStorage.Root,"october-"+name);Directory.CreateDirectory(directory);
            string file=Path.Combine(directory,"expedition-v6.json");
            // This directory is created for this explicit isolated fixture only.
            File.WriteAllText(file,JsonUtility.ToJson(TacticalDemoProfile.CreateFullPreview()));
            return new ExpeditionStore(directory,true);
        }
        void Board()
        {
            G.CancelSettings();if(G.StoryModal)G.CloseStoryModal();
            if(G.expeditionActive&&G.expeditionReceipt==null)G.AbandonExpedition();
            G.OpenExpeditionBoard();
        }
        IEnumerator Start()
        {
            if(!Application.isBatchMode)while(!SplashScreen.isFinished)yield return null;
            yield return null;C("isolated_validation",RunStorage.IsValidation&&G.qaMode);
            if(!RunStorage.IsValidation||!G.qaMode){Finish(false);yield break;}
            C("refined_weapon_audio_loaded",G.sound.RefinedWeaponClipCount==84,"distinct shot variants and mechanical events="+G.sound.RefinedWeaponClipCount);
            originalOptions=G.options;G.options=originalOptions.Clone();G.options.actions=GameOptions.DefaultBindings();KeyHints.Invalidate();
            keys=InputSystem.AddDevice<Keyboard>();mouse=InputSystem.AddDevice<Mouse>();G.qaKeyboard=keys;G.qaMouse=mouse;G.qaInputEnabled=true;G.qaSuppressAI=true;
            Board();G.expeditionStore=Seed("runtime");
            if(Group("story"))yield return Guard(CatalogAndStory());
            if(report.group=="storykeys")yield return Guard(StoryKeys());
            if(Group("ui"))yield return Guard(Interface());
            if(Group("equipment"))yield return Guard(Knives());
            if(Group("items"))yield return Guard(ItemsAndTeaching());
            if(report.group=="maps")yield return Guard(MapV2GeometryChecks.Run(C,output,new[]{0,11}));
            if(report.group=="firearms"){Board();C("firearms_deployed",G.BeginExpedition(0));yield return Guard(OctoberWeaponMotionChecks.Run(C,output));}
            if(report.group=="motion")
            {Board();C("motion_deployed",G.BeginExpedition(0));yield return Guard(FocusedEquipmentChecks.Run(C,output));}
            Finish(true);
        }
        IEnumerator Tap(Key key)
        {
            InputSystem.QueueStateEvent(keys,new KeyboardState(key));yield return null;yield return null;
            InputSystem.QueueStateEvent(keys,new KeyboardState());yield return null;yield return null;
        }
        IEnumerator Until(Func<bool> predicate,float seconds)
        {float deadline=Time.realtimeSinceStartup+seconds;while(!predicate()&&Time.realtimeSinceStartup<deadline)yield return null;}
        IEnumerator Shot(string file,bool native=false)
        {
            yield return new WaitForEndOfFrame();string path=Path.Combine(output,file);
            if(native)CaptureNative(P.cameraView,path);else ScreenCapture.CaptureScreenshot(path);
            var melee=P?P.meleeEquipment:null;
            report.frames.Add(new Frame{file=file,kind=native?"native camera; visual review required":"full UI; visual review required",knifeId=melee?melee.KnifeId:-1,inspectSeconds=melee?melee.InspectSeconds:-1,gripError=melee&&melee.HeldVisual?P.V2KnifeGripError(melee.HeldVisual.Grip):-1});
            yield return null;
        }
        IEnumerator CatalogAndStory()
        {
            C("twelve_canonical_chapters",ExpeditionCatalog.Levels.Length==12&&StoryContent.Chapters.Length==12&&StoryArchiveContent.Chapters.Length==12);
            int characters=0;for(int chapter=0;chapter<12;chapter++)
            {
                var definition=MissionCatalog.Get(chapter);int mini=0;string expected="B"+(chapter+1).ToString("00");
                foreach(var group in definition.groups)if(group.miniBossId==expected)mini++;
                int length=0;foreach(var paragraph in StoryArchiveContent.Chapters[chapter])length+=paragraph.text.Length;
                characters+=length;C("chapter_"+(chapter+1)+"_archive_and_miniboss",mini==1&&StoryArchiveContent.Chapters[chapter].Length>=3&&length>=200,"miniboss="+expected+";matches="+mini+";archiveChars="+length);
            }
            int backgroundCharacters=0;foreach(var paragraph in StoryArchiveContent.Background)backgroundCharacters+=paragraph.text.Length;
            C("archive_full_timeline",StoryArchiveContent.Background.Length>=4&&characters+backgroundCharacters>3500,"chapter characters="+characters+";background characters="+backgroundCharacters+";complete archive="+(characters+backgroundCharacters));
            Board();C("final_chapter_deployed",G.BeginExpedition(11));yield return new WaitForSeconds(.3f);
            var focus=G.expeditionMap.root.GetComponent<FocusedArenaDefinition>();
            C("final_arena_authored_structure",focus&&focus.chapter==12&&focus.cameras.Length>=6&&G.expeditionMap.walkingCheckpoints.Count>=8,"Focused chapter 12 preparation/observation/combat and permanent route checkpoints.");
            C("final_three_objectives",G.mission.Definition.objectives.Length==3&&G.mission.Definition.requiredObjectives==3);
            P.SetFeet(focus?focus.observation:G.expeditionMap.spawn);P.SetLook(0,0);yield return Shot("october-final-observation.png",true);Board();
        }
        IEnumerator Interface()
        {
            Board();G.expeditionBoard=false;yield return Shot("october-main-menu.png");
            G.OpenSettings();G.SelectSettingsTab(5);yield return null;
            float vw=(float)typeof(DemoGame).GetField("viewWidth",Private).GetValue(G);float sx=Mathf.Max(48,(vw-1380)/2),sw=Mathf.Min(1380,vw-96),scx=sx+222,scol=(sw-222-36)/2;
            Rect difficultyField=new Rect(scx+scol+36,256,scol,42);
            G.PendingOptions.difficulty=1;
            // Send mouse position and button states through the real Input System.
            // Some player configurations do not forward synthetic device events
            // to IMGUI; keep that limitation explicit instead of claiming a click.
            var uiMatrix=(Matrix4x4)typeof(DemoGame).GetField("lastUiMatrix",Private).GetValue(G);
            Vector3 guiScreen=uiMatrix.MultiplyPoint3x4(new Vector3(difficultyField.center.x,difficultyField.center.y,0));
            Vector2 pointer=new Vector2(guiScreen.x,Screen.height-guiScreen.y);
            report.pointerOpeningAttempted=true;
            InputSystem.QueueStateEvent(mouse,new MouseState{position=pointer});yield return null;yield return null;
            InputSystem.QueueStateEvent(mouse,new MouseState{position=pointer,buttons=1});yield return null;yield return null;
            InputSystem.QueueStateEvent(mouse,new MouseState{position=pointer});yield return null;yield return null;yield return null;
            report.pointerOpenedDropdown=G.SettingsDropdownOpen;
            report.pointerOpeningEvidence=report.pointerOpenedDropdown
                ?"The production difficulty dropdown opened after MouseState pointer/down/up events, with no dropdown state assignment."
                :"Synthetic MouseState events did not open IMGUI. Pointer opening remains unverified; the screenshot uses an explicit fixture-opened dropdown and only keyboard selection is verified.";
            Debug.Log("OCTOBER_QA_POINTER "+report.pointerOpeningEvidence);
            if(!report.pointerOpenedDropdown)
            {
                Field("settingsDropdownId","难度");Field("settingsDropdownChoices",new[]{"轻松","标准","硬核"});
                Field("settingsDropdownIndex",1);Field("settingsDropdownAnchor",difficultyField);Call("PositionSettingsDropdown");
            }
            yield return Shot("october-settings-dropdown.png");C("settings_dropdown_open",G.SettingsDropdownOpen,report.pointerOpeningEvidence);
            yield return Tap(Key.DownArrow);yield return Tap(Key.Enter);yield return null;
            C("settings_dropdown_keyboard_selection",!G.SettingsDropdownOpen&&G.PendingOptions.difficulty==2,"Down/Enter chooses the third entry through actual input. "+report.pointerOpeningEvidence);
            G.CancelSettings();
            Board();G.SetExpeditionTabForReview(1);Field("expeditionWeaponSlot",0);
            yield return Until(()=>{var preview=G.GetComponent<WeaponModelPreview>();return preview&&preview.SelectedTexture&&preview.IconCount==WeaponCatalog.Count;},12);
            var weapons=G.GetComponent<WeaponModelPreview>();
            C("armory_primary_capture_ready",weapons&&weapons.SelectedTexture&&weapons.IconCount==WeaponCatalog.Count,"Selected render and all weapon icons must exist before capture.");
            yield return null;yield return null;yield return Shot("october-armory-primary.png");
            Field("expeditionWeaponSlot",2);
            yield return Until(()=>{var preview=G.GetComponent<KnifeArmoryPreviewV2>();return preview&&preview.SelectedTexture&&Array.TrueForAll(preview.Icons,icon=>icon!=null);},12);
            var knives=G.GetComponent<KnifeArmoryPreviewV2>();
            C("armory_melee_capture_ready",knives&&knives.SelectedTexture&&Array.TrueForAll(knives.Icons,icon=>icon!=null),"Selected render and all six knife icons must exist before capture.");
            yield return null;yield return null;yield return Shot("october-armory-melee.png");
            var builtClass=typeof(ClassModelPreview).GetField("built",Private);
            for(int role=0;role<4;role++)
            {
                G.expeditionStore.SelectClass(role);G.SetExpeditionTabForReview(2);
                yield return Until(()=>{var preview=G.GetComponent<ClassModelPreview>();return preview&&preview.Texture&&builtClass!=null&&(int)builtClass.GetValue(preview)==role;},8);
                yield return new WaitForSecondsRealtime(.4f);yield return null;yield return null;yield return null;
                var rolePreview=G.GetComponent<ClassModelPreview>();
                C("class_"+role+"_capture_ready",rolePreview&&rolePreview.Texture&&builtClass!=null&&(int)builtClass.GetValue(rolePreview)==role,"Actual built class matches selection; waited for rendered portrait refresh.");
                yield return Shot("october-class-"+role+".png");
            }
            G.expeditionStore.SelectClass(0);G.OpenStoryArchive();Field("storyArchiveChapter",11);yield return Shot("october-story-complete-archive.png");G.CloseStoryModal();
            Board();C("bindings_chapter_deployed",G.BeginExpedition(0));yield return null;
            var probe=new GameObject("October live world binding probe");var label=probe.AddComponent<TextMesh>();label.text="{key:6} 换位 / {key:7} 换弹";KeyHintWorldLabel.Attach(label);
            G.SetState(RunState.Paused);G.OpenSettings();G.PendingOptions.Bind(6,0,"K:F6",true);G.PendingOptions.Bind(7,0,"K:F7",true);G.ApplyDraft();yield return null;yield return null;
            C("custom_binding_live_ui_and_world",G.KeyName(6)=="F6"&&KeyHints.Resolve("{key:6} / {key:7}")=="F6 / F7"&&label.text.Contains("F6")&&label.text.Contains("F7"),label.text);
            G.OpenSettings();G.PendingOptions.Bind(6,0,"",true);G.PendingOptions.Bind(6,1,"K:F8",true);G.ApplyDraft();yield return null;yield return null;
            C("secondary_only_binding_live",G.KeyName(6)=="F8"&&KeyHints.Resolve("{key:6}")=="F8"&&label.text.Contains("F8"),label.text);
            Destroy(probe);G.CancelSettings();Board();G.options.actions=GameOptions.DefaultBindings();KeyHints.Invalidate();
            C("final_hud_chapter_started",G.BeginExpedition(0));yield return new WaitForSeconds(.6f);
            yield return Shot("october-chapter1-final-hud.png");Board();
        }
        IEnumerator StoryKeys()
        {
            Board();
            var legacy=G.options.Clone();Array.Resize(ref legacy.actions,29);
            legacy.actions[6]=new ActionBinding("K:F6","K:F7");legacy.Validate();
            C("storykeys_old29_migration",legacy.actions.Length==31&&legacy.actions[6].primary=="K:F6"&&legacy.actions[6].secondary=="K:F7"&&legacy.actions[29].primary=="K:F9"&&legacy.actions[30].primary=="K:F10","Existing custom primary/secondary are retained; radio actions receive their defaults.");
            var occupied=G.options.Clone();Array.Resize(ref occupied.actions,29);occupied.actions[6]=new ActionBinding("K:F9");occupied.Validate();
            C("storykeys_migration_preserves_occupied_default",occupied.actions[6].primary=="K:F9"&&string.IsNullOrEmpty(occupied.actions[29].primary),"A previously claimed F9 remains with the player's action; the added radio action stays unbound until chosen.");
            G.options=legacy;KeyHints.Invalidate();G.OpenSettings();
            G.PendingOptions.Bind(29,0,"K:F4",true);G.PendingOptions.Bind(30,0,"K:F5",true);G.PendingOptions.Bind(16,0,"K:F3",true);G.ApplyDraft();G.CancelSettings();
            yield return null;
            C("storykeys_display_and_saved_bindings",G.KeyName(29)=="F4"&&G.KeyName(30)=="F5"&&G.KeyName(16)=="F3"&&KeyHints.Resolve("{key:29} / {key:30} / {key:16}")=="F4 / F5 / F3","Production settings ApplyDraft and dynamic labels use the same actions.");
            var saved=SettingsStore.LoadFromPath(SettingsStore.FilePath);
            C("storykeys_bindings_persisted",saved.actions.Length==31&&saved.actions[29].primary=="K:F4"&&saved.actions[30].primary=="K:F5"&&saved.actions[16].primary=="K:F3");
            C("storykeys_real_chapter_started",G.BeginExpedition(0));yield return Until(()=>G.StoryRadioActive,4);
            var radioField=typeof(DemoGame).GetField("storyRadio",Private);var before=(StoryLine)radioField.GetValue(G);
            C("storykeys_live_radio_present",before!=null&&G.StoryRadioActive);
            yield return Shot("october-storykeys-live-hud.png");
            yield return Tap(Key.F4);var after=(StoryLine)radioField.GetValue(G);
            C("storykeys_rebound_advances_real_radio",before!=null&&!ReferenceEquals(before,after),"F4 changed the current radio line through TickStory; no field was assigned by the fixture.");
            yield return Tap(Key.F5);
            bool archive=(bool)typeof(DemoGame).GetField("storyArchiveOpen",Private).GetValue(G);
            C("storykeys_rebound_opens_archive",archive&&G.StoryModal&&G.state==RunState.Paused,"F5 opened the live attempt transcript and paused the chapter.");
            yield return Shot("october-storykeys-archive.png");
            yield return Tap(Key.F3);
            C("storykeys_rebound_returns_to_play",!G.StoryModal&&G.IsPlaying,"F3 closed the archive and resumed its real return state.");
            // The fixed legacy key is deliberately no longer a second input path.
            yield return Tap(Key.F10);
            C("storykeys_old_archive_shortcut_removed",!G.StoryModal&&G.IsPlaying,"Physical F10 does not open the archive after rebinding the action to F5.");
            Board();
        }
        IEnumerator Knives()
        {
            Board();G.expeditionStore.SelectClass(0);
            int requestedKnife=-1;var args=Environment.GetCommandLineArgs();int filter=Array.IndexOf(args,"-knifeReviewId");if(filter>=0&&(!int.TryParse(args[filter+1],out requestedKnife)||requestedKnife<0||requestedKnife>=6))throw new ArgumentException("Invalid knife review id");
            for(int knife=0;knife<6;knife++)
            {
                if(requestedKnife>=0&&knife!=requestedKnife)continue;
                G.expeditionStore.EquipMeleeKnife(knife);bool begun=G.BeginExpedition(0);C("knife_"+knife+"_deployment_started",begun,G.expeditionNotice);if(!begun)yield break;
                yield return new WaitForSeconds(.4f);var m=P.meleeEquipment;
                C("knife_"+knife+"_snapshot_and_model",G.expeditionStore.Profile.deployment.knifeId==knife&&m.KnifeId==knife&&m.HeldVisual&&m.HeldVisual.KnifeId==knife);
                yield return Tap(Key.Digit3);yield return Until(()=>m.Ready,2);yield return new WaitForSeconds(.15f);
                C("knife_"+knife+"_slot_three_input",m.Equipped&&m.Ready&&m.HeldVisual.gameObject.activeInHierarchy&&m.KnifeId==knife);
                yield return Shot("october-knife-"+knife+"-ready.png",true);
                yield return Tap(Key.H);yield return Until(()=>m.InspectSeconds>=KnifeMotionV3.InspectDuration(knife)*.48f,4);yield return new WaitForEndOfFrame();
                C("knife_"+knife+"_inspect_live",m.InspectSeconds>=.35f&&m.HeldVisual.KnifeId==knife&&P.V2KnifeGripError(m.HeldVisual.Grip)<.04f,"inspectSeconds="+m.InspectSeconds+";gripError="+P.V2KnifeGripError(m.HeldVisual.Grip));
                yield return Shot("october-knife-"+knife+"-inspect.png",true);yield return Until(()=>m.InspectSeconds>=KnifeMotionV3.InspectDuration(knife)*.70f,3);yield return Shot("october-knife-"+knife+"-inspect-detail.png",true);m.CancelForAction();
                yield return Tap(Key.Digit1);yield return new WaitForSeconds(.4f);yield return Tap(Key.V);
                C("knife_"+knife+"_quick_melee_uses_selection",m.AttackActive&&m.HeldVisual.KnifeId==knife,"Quick melee input must animate the selected physical knife.");
                yield return Until(()=>!m.AttackActive,2);
                if(knife==3)
                {
                    P.Hurt(100000,P.Eye.position+Vector3.forward,false,PlayerDamageKind.Environment);yield return null;
                    C("butterfly_real_death_retry_available",G.ExpeditionAwaitingRetry);
                    bool retried=G.RetryExpedition();yield return null;
                    C("butterfly_retry_preserves_deployment",retried&&P.meleeEquipment.KnifeId==3&&P.meleeEquipment.HeldVisual.KnifeId==3&&G.expeditionStore.Profile.deployment.knifeId==3);
                }
                Board();
            }
        }
        void Aim(Vector3 point)
        {Vector3 delta=point-P.Eye.position;var recoil=P.MechanicalRecoil;P.SetLook(Mathf.Atan2(delta.x,delta.z)*Mathf.Rad2Deg-recoil.y,-Mathf.Atan2(delta.y,new Vector2(delta.x,delta.z).magnitude)*Mathf.Rad2Deg+recoil.x);}
        IEnumerator ItemsAndTeaching()
        {
            Board();G.expeditionStore.SelectClass(0);G.expeditionStore.SetConsumableSlot(0,0,3);G.expeditionStore.SetConsumableSlot(1,0,3);
            C("same_type_six_deployed",G.BeginExpedition(0)&&G.expeditionMedkits==6&&G.expeditionStore.Profile.deployment.carriedConsumables[0]==6);
            Board();G.expeditionStore.SetConsumableSlot(0,0,3);G.expeditionStore.SetConsumableSlot(1,1,3);
            C("mixed_three_plus_three_deployed",G.BeginExpedition(0)&&G.expeditionMedkits==3&&G.expeditionAmmoPacks==3);yield return new WaitForSeconds(.5f);
            C("teaching_starts_on_entry",G.HomecomingTeachingActive&&G.HomecomingTeachingCompleted==0);
            Vector3 start=P.transform.position;P.SetLook(0,0);InputSystem.QueueStateEvent(keys,new KeyboardState(Key.W));yield return new WaitForSeconds(1.5f);InputSystem.QueueStateEvent(keys,new KeyboardState());yield return null;
            C("teaching_real_movement",Vector3.Distance(start,P.transform.position)>4&&G.HomecomingTeachingCompleted>=1,"Actual keyboard locomotion; distance="+Vector3.Distance(start,P.transform.position));
            yield return Until(()=>G.enemies.Exists(e=>e&&e.Alive&&e.kind==EnemyKind.Training),5);
            EnemyActor target=G.enemies.Find(e=>e&&e.Alive&&e.kind==EnemyKind.Training);int beforeHits=G.totalHits;
            if(target)for(int i=0;i<12&&G.totalHits==beforeHits;i++){Aim(target.TorsoHitbox?target.TorsoHitbox.bounds.center:target.AimPoint);P.Fire();yield return new WaitForSeconds(.22f);}
            C("teaching_real_shot_hit",G.totalHits>beforeHits&&G.HomecomingTeachingCompleted>=2,"hits="+G.totalHits);
            int beforeAmmo=P.ammo[P.weapon];bool reload=P.Reload();yield return Until(()=>!P.ReloadV2Pending,6);yield return null;
            C("teaching_completed_reload",reload&&P.ammo[P.weapon]>beforeAmmo&&G.HomecomingTeachingCompleted>=3);
            int beforeSwaps=G.totalSwaps;PhaseAnchor anchor=null;foreach(var candidate in G.anchors)if(candidate&&P.ValidateSwap(candidate).IsValid){anchor=candidate;break;}
            bool swapping=anchor&&P.RequestSwap(anchor);yield return Until(()=>G.totalSwaps>beforeSwaps,3);yield return null;
            C("teaching_real_swap",swapping&&G.totalSwaps>beforeSwaps&&G.HomecomingTeachingCompleted>=4);
            yield return Until(()=>P.Grounded,3);bool ability=P.classSkill.ActivateEquipped();yield return null;
            C("teaching_real_class_skill",ability&&!string.IsNullOrEmpty(P.classSkill.LastOutcome)&&G.HomecomingTeachingCompleted>=5,P.classSkill.LastFailure);
            yield return Until(()=>P.classSkill.ActiveSeconds<=0&&!P.TacticalAdvanceActive&&!P.SwapActionsLocked,9);
            var use=P.GetComponent<ConsumableUseV2>();int ammoBefore=G.expeditionAmmoPacks;bool ammoUse=use.Begin(1);yield return Until(()=>!use.Using,3);yield return null;
            C("teaching_real_item_commit",ammoUse&&G.expeditionAmmoPacks==ammoBefore-1&&G.HomecomingTeachingCompleted>=6);
            P.Hurt(12,P.Eye.position+Vector3.forward,false);int medBefore=G.expeditionMedkits;float hp=P.health;bool medUse=use.Begin(0);yield return Until(()=>!use.Using,3);
            C("mixed_loadout_real_medical_commit",medUse&&G.expeditionMedkits==medBefore-1&&P.health>hp);
            yield return Shot("october-chapter1-teaching.png");
            int usedMed=3-G.expeditionMedkits,usedAmmo=3-G.expeditionAmmoPacks;Board();
            var ledger=Seed("settlement-contract");ledger.SetConsumableSlot(0,0,3);ledger.SetConsumableSlot(1,1,3);int stockMed=ledger.ConsumableStock(0),stockAmmo=ledger.ConsumableStock(1);string run=ledger.Begin(0);
            var receipt=ledger.Settle(run,true,false,12,usedMed,usedAmmo);
            C("mixed_consumption_storage_contract",usedMed==1&&usedAmmo==1&&receipt.medkitsUsed==usedMed&&receipt.ammoUsed==usedAmmo&&ledger.ConsumableStock(0)==stockMed-usedMed&&ledger.ConsumableStock(1)==stockAmmo-usedAmmo,"Separate storage contract using observed consumption; no gameplay success or mission completion is claimed.");
            G.expeditionStore.SetConsumableSlot(0,0,3);G.expeditionStore.SetConsumableSlot(1,-1,0);C("teaching_supply_zero_ammo_begin",G.BeginExpedition(0));yield return null;
            C("teaching_supply_is_attempt_only",G.HomecomingTrainingAmmoGrant==1&&G.expeditionAmmoPacks==1&&G.expeditionStore.Profile.deployment.carriedConsumables[1]==0);
            Board();
        }
        IEnumerator Guard(IEnumerator routine)
        {
            while(true){bool more;object current;try{more=routine.MoveNext();current=more?routine.Current:null;}catch(Exception error){C("fixture_exception",false,error.ToString());yield break;}if(!more)yield break;if(current is IEnumerator child)yield return Guard(child);else yield return current;}
        }
        void Update(){if(!done&&Time.realtimeSinceStartup-started>900){C("timeout",false);Finish(false);}}
        void Finish(bool complete)
        {
            if(done)return;done=true;report.completed=complete;report.passed=complete&&report.checks.Count>0&&report.checks.TrueForAll(c=>c.passed);
            File.WriteAllText(Path.Combine(output,"october-refinement-report.json"),JsonUtility.ToJson(report,true));Application.Quit(report.passed?0:1);
        }
        void OnDestroy(){Application.logMessageReceived-=OnLog;if(keys!=null)InputSystem.RemoveDevice(keys);if(mouse!=null)InputSystem.RemoveDevice(mouse);}
        static void CaptureNative(Camera camera,string path)
        {
            var target=new RenderTexture(1600,900,24,RenderTextureFormat.ARGB32);Texture2D pixels=null;var previous=RenderTexture.active;
            try{target.Create();RenderPipeline.SubmitRenderRequest(camera,new RenderPipeline.StandardRequest{destination=target});RenderTexture.active=target;pixels=new Texture2D(1600,900,TextureFormat.RGBA32,false);pixels.ReadPixels(new Rect(0,0,1600,900),0,0);pixels.Apply();File.WriteAllBytes(path,pixels.EncodeToPNG());}
            finally{RenderTexture.active=previous;target.Release();Destroy(target);if(pixels)Destroy(pixels);}
        }
    }
}
