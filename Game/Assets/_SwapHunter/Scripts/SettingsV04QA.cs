using System;
using System.IO;
using UnityEngine;
namespace SwapHunter
{
    // Isolated migration/persistence checks; never saves over the player's settings file.
    public static class SettingsV04QA
    {
        public static void Run(DemoGame g, Action<string, bool, string> check)
        {
            if(!RunStorage.IsValidation){check("settings_v4_validation_guard",false,"Requires isolated validation storage");return;}
            try
            {
                var oldRaw=JsonUtility.FromJson<GameOptions>("{\"version\":2,\"sensitivity\":1.27,\"quality\":0,\"resolution\":2,\"effectsVolume\":0.31,\"feedbackVolume\":0.27}");oldRaw.Validate();
                check("settings_v4_missing_new_fields_receive_defaults",Mathf.Approximately(oldRaw.sensitivity,1.27f)&&Mathf.Approximately(oldRaw.musicVolume,.45f)&&Mathf.Approximately(oldRaw.uiScale,1)&&oldRaw.muteWhenUnfocused&&oldRaw.actions.Length==GameOptions.DefaultBindings().Length,"Actual legacy JSON omits all new v4 fields");
                var old = new GameOptions { version = 2, resolution = 2, quality = 0, effectsVolume = .31f, feedbackVolume = .27f };
                var legacy = new ActionBinding[17]; Array.Copy(old.actions, legacy, 17); old.actions = legacy;
                old.actions[0] = new ActionBinding("K:UpArrow", "M:3");
                old.actions[8] = new ActionBinding("", "K:Enter");
                check("settings_v4_fixture_uses_valid_custom_keys",BindingInput.Valid("K:UpArrow")&&BindingInput.Valid("M:3")&&BindingInput.Valid("K:Enter"),"Verified against installed InputSystem.Key enum");
                string[] before = new string[17];
                for (int i=0;i<17;i++) before[i]=old.actions[i].primary+"|"+old.actions[i].secondary;
                // Go through the actual persisted JSON representation of a previous version.
                var migrated=JsonUtility.FromJson<GameOptions>(JsonUtility.ToJson(old)); migrated.Validate();
                bool unchanged=true;string differences="";
                for(int i=0;i<17;i++) unchanged &= before[i]==migrated.actions[i].primary+"|"+migrated.actions[i].secondary;
                check("settings_v4_migration_preserves_all_17_bindings",unchanged&&migrated.actions.Length==GameOptions.DefaultBindings().Length,"length="+migrated.actions.Length+differences);
                check("settings_v4_appends_new_actions",migrated.actions[17].primary=="K:LeftCtrl"&&migrated.actions[18].primary=="K:Tab"&&migrated.actions[19].primary=="K:F","crouch/inventory/consumable");
                check("settings_v4_legacy_display_and_audio_migrate",migrated.version==GameOptions.CurrentVersion&&migrated.resolutionWidth==1920&&migrated.resolutionHeight==1080&&Mathf.Approximately(migrated.renderScale,.7f)&&Mathf.Approximately(migrated.ambientVolume,.31f)&&Mathf.Approximately(migrated.uiVolume,.27f),"legacy resolution 2, quality 0, independent channels");
                string migratedJson=JsonUtility.ToJson(migrated);migrated.Validate();
                check("settings_v4_migration_idempotent",migratedJson==JsonUtility.ToJson(migrated),"second Validate preserves state");

                var conflict=new GameOptions { version=2,actions=new ActionBinding[17] };
                var defaults=GameOptions.DefaultBindings();Array.Copy(defaults,conflict.actions,17);
                conflict.actions[0]=new ActionBinding("K:F","K:LeftCtrl");conflict.actions[1].secondary="K:Tab";
                conflict.Validate();
                check("settings_v4_new_defaults_do_not_steal_custom_bindings",conflict.actions[0].primary=="K:F"&&conflict.actions[0].secondary=="K:LeftCtrl"&&conflict.actions[1].secondary=="K:Tab"&&conflict.actions[17].primary==""&&conflict.actions[18].primary==""&&conflict.actions[19].primary=="","new actions remain unbound when their defaults are already used");
                conflict.Bind(19,0,"K:B",false);
                check("settings_v4_unbound_new_action_can_be_assigned",conflict.actions[19].primary=="K:B"&&conflict.actions[0].primary=="K:F","custom F stays untouched");

                var saved=new GameOptions { uiScale=.88f,toggleCrouch=true,muteWhenUnfocused=false,dynamicResolution=true,displayIndex=2,resolutionWidth=2560,resolutionHeight=1440,refreshRateHz=144,renderScale=.93f,dynamicResolutionMin=.62f,musicVolume=.17f,ambientVolume=.29f,uiVolume=.38f,toggleAim=true,toggleSprint=true };
                saved.actions[17]=new ActionBinding("K:C","M:4");saved.actions[18]=new ActionBinding("K:I");saved.actions[19]=new ActionBinding("K:B");
                string path=Path.Combine(RunStorage.Root,"qa-settings-v4-"+Guid.NewGuid().ToString("N")+".json");
                SettingsStore.SaveToPath(saved,path);var loaded=SettingsStore.LoadFromPath(path);
                check("settings_v4_new_parameters_persist",JsonUtility.ToJson(saved)==JsonUtility.ToJson(loaded),path);
                check("settings_v4_clone_has_independent_bindings",!ReferenceEquals(saved.actions,loaded.actions)&&!ReferenceEquals(saved.actions[17],loaded.actions[17]),"editing a draft cannot mutate saved bindings");
                loaded.actions[17].primary="K:Z";
                check("settings_v4_loaded_binding_edit_is_isolated",saved.actions[17].primary=="K:C","loaded record edited independently");

                var invalid=new GameOptions { uiScale=.1f,renderScale=.1f,dynamicResolutionMin=9,musicVolume=float.NaN,ambientVolume=-2,uiVolume=9,displayIndex=-7,resolutionWidth=20,resolutionHeight=40,refreshRateHz=-1 };
                invalid.actions[19]=new ActionBinding("K:NotAKey","M:99");invalid.Validate();
                check("settings_v4_ui_scale_clamped",Mathf.Approximately(invalid.uiScale,.8f),"UI stays within the supported canvas range");
                check("settings_v4_invalid_render_bounds_clamped",Mathf.Approximately(invalid.renderScale,.5f)&&Mathf.Approximately(invalid.dynamicResolutionMin,.5f),"dynamic minimum must not exceed maximum");
                check("settings_v4_invalid_audio_and_display_clamped",!float.IsNaN(invalid.musicVolume)&&invalid.ambientVolume==0&&invalid.uiVolume==1&&invalid.displayIndex==0&&invalid.resolutionWidth==640&&invalid.resolutionHeight==480&&invalid.refreshRateHz==0,"invalid persisted input repaired");
                check("settings_v4_invalid_new_key_repaired",invalid.actions[19].primary=="K:F"&&invalid.actions[19].secondary=="","invalid primary restored and invalid secondary cleared");

                string legacyText=JsonUtility.ToJson(old);File.WriteAllText(SettingsStore.LegacyFilePath,legacyText);
                var loadedLegacy=SettingsStore.Load();SettingsStore.Save(loadedLegacy);
                check("settings_v4_file_migration_preserves_legacy",SettingsStore.FilePath!=SettingsStore.LegacyFilePath&&File.Exists(SettingsStore.FilePath)&&File.ReadAllText(SettingsStore.LegacyFilePath)==legacyText&&loadedLegacy.actions[0].primary=="K:UpArrow","New settings-v4.json; old settings-v2.json untouched");
                File.WriteAllText(SettingsStore.FilePath,"{broken-current-settings");
                var recoveredSettings=SettingsStore.Load();
                check("settings_v4_corrupt_primary_uses_valid_legacy",recoveredSettings.actions[0].primary=="K:UpArrow"&&SettingsStore.LastWarning.Contains("旧版配置")&&File.ReadAllText(SettingsStore.LegacyFilePath)==legacyText,"Fallback keeps legacy custom bindings and original file");
                check("settings_v4_corrupt_primary_preserved",Directory.GetFiles(RunStorage.Root,Path.GetFileName(SettingsStore.FilePath)+".backup-*").Length>0,"Damaged current settings archived");
                SettingsStore.Save(recoveredSettings);SettingsStore.Load();
                var previousOptions=g.options;g.options=previousOptions.Clone();
                try
                {
                    var pipeline=UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline as UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset;
                    g.options.quality=0;g.ApplySettings();
                    check("settings_v4_low_quality_reaches_renderer",pipeline&&pipeline.msaaSampleCount==1&&Mathf.Approximately(pipeline.shadowDistance,25)&&Shapes.EffectBudget==60,"Live URP and effect budget");
                    g.options.quality=2;g.ApplySettings();
                    check("settings_v4_high_quality_reaches_renderer",pipeline&&pipeline.msaaSampleCount==4&&Mathf.Approximately(pipeline.shadowDistance,80)&&Shapes.EffectBudget==120,"Live URP and effect budget");
                }
                finally{g.options=previousOptions;g.ApplySettings();}
                string liveBefore=JsonUtility.ToJson(g.options);
                g.OpenSettings();g.SelectSettingsTab(5);
                g.PendingOptions.toggleCrouch=!g.options.toggleCrouch;
                g.PendingOptions.toggleAim=!g.options.toggleAim;
                g.PendingOptions.actions[17].primary="K:Z";
                check("settings_v4_draft_changes_do_not_apply_early",liveBefore==JsonUtility.ToJson(g.options),"toggle and binding changes remain in draft");
                g.CancelSettings();
                check("settings_v4_cancel_preserves_active_settings",liveBefore==JsonUtility.ToJson(g.options)&&g.PendingOptions==null,"cancel discards draft without saving");
            }
            catch(Exception e)
            {
                g.CancelSettings();check("settings_v4_exception",false,e.ToString());
            }
        }
    }
}




