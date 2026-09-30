using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace SwapHunter
{
    public sealed class DemoQA : MonoBehaviour
    {
        [Serializable] public sealed class Check { public string name; public bool passed; public string detail; }
        [Serializable] public sealed class Report
        {
            public string version = Application.version, kind = "Scripted native-player integration checks; not a human playtest";
            public string graphicsDevice, graphicsApi;
            public int width, height;
            public List<Check> checks = new List<Check>();
            public float frameP50Ms, frameP95Ms;
            public bool passed;
        }
        public Report report = new Report();
        string output; float started; bool finished;
        int originalCheckpoint; GameOptions originalOptions;
        DemoGame G => DemoGame.I;
        PlayerMotor P => G.player;
        void Awake() { started = Time.realtimeSinceStartup; Application.logMessageReceived += LogError; }
        void Update() { if (!finished && Time.realtimeSinceStartup - started > 180) { CheckThat("watchdog", false, "Integration run exceeded 180 seconds"); Finish(); } }
        void LogError(string message, string stack, LogType type)
        {
            if (type == LogType.Exception || type == LogType.Error || type == LogType.Assert)
                report.checks.Add(new Check { name = "runtime_error", passed = false, detail = message });
        }
        IEnumerator Start()
        {
            string[] args = Environment.GetCommandLineArgs(); int index = Array.IndexOf(args, "-qaOutput");
            output = index >= 0 && index + 1 < args.Length ? args[index + 1] : RunStorage.Root; Directory.CreateDirectory(output);
            originalCheckpoint = RunStorage.Checkpoint; originalOptions = G.options.Clone(); G.options = new GameOptions(); G.SyncLegacyBindings(); G.ApplySettings();
            report.graphicsDevice = SystemInfo.graphicsDeviceName; report.graphicsApi = SystemInfo.graphicsDeviceType.ToString(); report.width = Screen.width; report.height = Screen.height;
            while (!UnityEngine.Rendering.SplashScreen.isFinished) yield return null;
            yield return new WaitForSecondsRealtime(1);
            CheckThat("menu_boot", G.state == RunState.Menu && P != null, "Native game initialized");
            CheckThat("validation_preferences_isolated", RunStorage.IsValidation && Path.GetFullPath(SettingsStore.FilePath).StartsWith(Path.GetFullPath(output).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase), SettingsStore.FilePath);
            int playerCheckpoint = PlayerPrefs.GetInt("SH.checkpoint", -9999), testCheckpoint = RunStorage.Checkpoint;
            RunStorage.Checkpoint = 3;
            CheckThat("validation_checkpoint_isolated", RunStorage.Checkpoint == 3 && PlayerPrefs.GetInt("SH.checkpoint", -9999) == playerCheckpoint, "Test checkpoint changes do not write the player's checkpoint");
            RunStorage.Checkpoint = testCheckpoint;
            yield return Capture("01-menu"); G.StartRun(); G.qaSuppressAI = true; yield return new WaitForSeconds(.4f);
            CheckThat("tutorial_spawn", G.stage == 0 && G.AliveEnemies == 1 && P.Grounded, P.transform.position.ToString());
            var keyboard = InputSystem.AddDevice<Keyboard>(); var mouse = InputSystem.AddDevice<Mouse>();
            G.qaKeyboard = keyboard; G.qaMouse = mouse; InputSystem.EnableDevice(keyboard); InputSystem.EnableDevice(mouse);
            keyboard.MakeCurrent(); mouse.MakeCurrent(); G.qaInputEnabled = true;
            Vector3 beforeMove = P.transform.position;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(G.bindings[0])); yield return new WaitForSeconds(.7f);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;
            CheckThat("keyboard_movement", Vector3.Distance(beforeMove, P.transform.position) > 2.5f, P.transform.position.ToString());
            float groundY = P.transform.position.y;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(G.bindings[4])); yield return null; yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return new WaitForSeconds(.18f);
            CheckThat("jump_and_airborne_status", P.transform.position.y > groundY + .5f && !P.Grounded, P.transform.position.ToString());
            yield return new WaitForSeconds(.6f);
            float oldYaw = P.yaw;
            InputSystem.QueueStateEvent(mouse, new MouseState { delta = new Vector2(60, 0) }); yield return null; yield return null;
            CheckThat("mouse_look", Mathf.Abs(P.yaw - oldYaw) > 1, P.yaw.ToString("F2"));
            EnemyActor target = G.enemies[0]; Aim(target.AimPoint);
            int initialAmmo = P.ammo[0]; float initialEnemyHealth = target.health;
            InputSystem.QueueStateEvent(mouse, new MouseState { buttons = 1 }); yield return null; yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState()); yield return null;
            CheckThat("mouse_fire_and_damage", P.ammo[0] < initialAmmo && target.health < initialEnemyHealth, "ammo=" + P.ammo[0] + ", hp=" + target.health);
            G.qaInputEnabled = false; InputSystem.RemoveDevice(keyboard); InputSystem.RemoveDevice(mouse); G.qaKeyboard = null; G.qaMouse = null;
            yield return Capture("02-tutorial");
            P.ammo[0] = 20; int oldReserve = P.reserve[0]; CheckThat("reload_start", P.Reload(), "Partial magazine starts reload");
            yield return new WaitForSeconds(.2f); CheckThat("reload_no_early_ammo", P.ammo[0] == 20 && P.reserve[0] == oldReserve, "Ammo committed at completion");
            P.cooldown = 0; Vector3 oldPlayer = P.transform.position, oldTarget = target.transform.position; float yaw = P.yaw, enemyYaw = target.transform.eulerAngles.y, hp = P.health;
            string reason = P.ValidateSwap(target); CheckThat("swap_available", reason == "可换位", reason);
            P.RequestSwap(target); yield return new WaitForSeconds(.18f);
            CheckThat("swap_exchange", Near(P.transform.position, oldTarget) && Near(target.transform.position, oldPlayer), "Both feet positions exchanged");
            CheckThat("swap_state_preserved", P.health == hp && P.ammo[0] == 20 && P.reloadLeft > 0 && Mathf.Abs(Mathf.DeltaAngle(P.yaw, yaw)) < .1f && Mathf.Abs(Mathf.DeltaAngle(target.transform.eulerAngles.y, enemyYaw)) < .1f, "Health, magazine, reload and headings preserved");
            CheckThat("swap_cooldown", P.cooldown > G.CampaignCooldown-.5f && !P.RequestSwap(target), P.cooldown.ToString("F2"));
            CheckThat("navigation_recovered", target.agent.isOnNavMesh, "Agent remains on navigation");
            yield return new WaitForSeconds(1.5f); CheckThat("reload_completes", P.ammo[0] == 24 && P.reserve[0] == oldReserve - 4, "Reload survives swap");
            P.ammo[0] = 10; oldReserve = P.reserve[0]; P.Reload(); yield return new WaitForSeconds(.2f); P.SwitchWeapon(1);
            CheckThat("switch_cancels_reload_without_ammo", P.reloadLeft == 0 && P.ammo[0] == 10 && P.reserve[0] == oldReserve, "No free ammunition"); P.SwitchWeapon(0);
            P.cooldown = 0; P.SetFeet(new Vector3(0, .03f, 36));
            CheckThat("swap_range_rejection", P.ValidateSwap(target).Contains("22") && !P.RequestSwap(target) && P.cooldown == 0, P.ValidateSwap(target));
            P.SetFeet(new Vector3(0, 3, 8)); P.verticalSpeed = 0;
            CheckThat("swap_airborne_available", P.ValidateSwap(target) == "可换位", P.ValidateSwap(target));
            P.SetFeet(new Vector3(0, .03f, 8)); target.MoveTo(new Vector3(0, 0, 13)); P.verticalSpeed = 0; Aim(target.AimPoint); yield return new WaitForSeconds(.2f);
            GameObject wall = Fixture(new Vector3(0, 1.5f, 10.5f), new Vector3(2, 3, .3f));
            CheckThat("swap_wall_rejection", P.ValidateSwap(target) == "视线被挡" && !P.RequestSwap(target) && P.cooldown == 0, P.ValidateSwap(target)); RemoveFixture(wall); yield return null;
            GameObject ceiling = Fixture(target.transform.position + Vector3.up * 2, new Vector3(2, .6f, 2));
            CheckThat("swap_occupied_destination", P.ValidateSwap(target) == "落点受阻" && !P.RequestSwap(target), P.ValidateSwap(target)); RemoveFixture(ceiling); yield return null;
            P.SetFeet(new Vector3(3, .03f, 5)); EnemyActor large = EnemyActor.Spawn(EnemyKind.Elite, new Vector3(3, 0, 16), 0); large.suppressAI = true;
            ceiling = Fixture(P.transform.position + Vector3.up * 2.1f, new Vector3(2, .28f, 2));
            CheckThat("swap_checks_both_body_sizes", P.ValidateSwap(large) == "落点受阻", P.ValidateSwap(large)); RemoveFixture(ceiling); large.gameObject.SetActive(false); Destroy(large.gameObject); yield return null;
            P.SetFeet(new Vector3(0, .03f, 8)); P.cooldown = 0; oldPlayer = P.transform.position;
            P.RequestSwap(target); target.Damage(1000); yield return new WaitForSeconds(.2f);
            CheckThat("swap_target_dies_during_windup", Near(P.transform.position, oldPlayer) && P.cooldown == 0 && !P.preparing, "No one-sided teleport or cooldown");
            G.EnterStage(1, true); yield return WaitDeployments(); yield return new WaitForSeconds(.4f);
            var entrySniper = G.enemies.Find(e => e.kind == EnemyKind.Sniper); Aim(entrySniper.AimPoint); yield return null;
            CheckThat("high_ground_readable_from_entry", P.FindTarget() == entrySniper && P.ValidateSwap(entrySniper) == "可换位", P.ValidateSwap(entrySniper));
            target = G.enemies.Find(e => e.kind == EnemyKind.Sniper); P.SetFeet(new Vector3(4, .03f, 35)); Aim(target.AimPoint); yield return new WaitForSeconds(.2f);
            Route("cargo_stair_route", new Vector3(0, 0, 32), new Vector3(10, 4, 55));
            Route("warehouse_stair_route", new Vector3(0, 0, 76), new Vector3(-11, 4, 100));
            Route("tower_high_route", new Vector3(0, 0, 120), new Vector3(-12, 7, 149));
            reason = P.ValidateSwap(target); CheckThat("high_platform_swap_available", reason == "可换位", reason);
            oldTarget = target.transform.position; oldPlayer = P.transform.position; enemyYaw = target.transform.eulerAngles.y;
            P.RequestSwap(target); yield return new WaitForSeconds(.2f);
            CheckThat("high_platform_exchange", Near(P.transform.position, oldTarget) && Near(target.transform.position, oldPlayer), P.transform.position.ToString());
            foreach (var enemy in G.enemies) enemy.suppressAI = enemy != target;
            G.qaSuppressAI = false; yield return new WaitForSeconds(1.2f);
            CheckThat("enemy_recovers_and_turns", target.stunLeft == 0 && target.agent.isOnNavMesh && !target.agent.isStopped && Mathf.Abs(Mathf.DeltaAngle(target.transform.eulerAngles.y, enemyYaw)) > 5, "Post-swap AI resumes at finite turn speed");
            G.qaSuppressAI = true; yield return Capture("03-high-ground-swap");
            G.EnterStage(0, true); yield return new WaitForSeconds(.4f); P.SetFeet(new Vector3(0, .03f, 10)); target = G.enemies[0]; Aim(target.AimPoint);
            P.SwitchWeapon(1); yield return new WaitForSeconds(.3f); initialEnemyHealth = target.health; initialAmmo = P.ammo[1]; P.Fire(); yield return new WaitForSeconds(.15f);
            CheckThat("shotgun_damage_and_ammo", target.health <= initialEnemyHealth - 48 && P.ammo[1] == initialAmmo - 1, "health=" + target.health + ", ammo=" + P.ammo[1]);
            G.EnterStage(2, true); yield return WaitDeployments(); yield return new WaitForSeconds(.4f);
            target = G.enemies.Find(e => e.kind == EnemyKind.Shield && Vector3.Distance(e.transform.position,new Vector3(2,0,85))<3); P.SetFeet(new Vector3(2, .03f, 81)); Aim(target.AimPoint); yield return new WaitForSeconds(.3f);
            initialEnemyHealth = target.health; bool shieldShot = P.Fire(); yield return new WaitForSeconds(.2f);
            CheckThat("shield_blocks_front_bullet", shieldShot && target.health == initialEnemyHealth, "Shield health=" + target.health + "; fired=" + shieldShot);
            CheckThat("shield_is_swappable", P.FindTarget() == target && P.ValidateSwap(target) == "可换位", P.ValidateSwap(target));
            var shield = target.GetComponentInChildren<BoxCollider>();
            Vector3 shieldAtDestination = shield.transform.TransformPoint(shield.center) + P.transform.position - target.transform.position;
            var shieldBlock = Fixture(shieldAtDestination, new Vector3(.8f, .8f, .1f));
            CheckThat("shield_shape_destination_rejection", P.ValidateSwap(target) == "落点受阻", P.ValidateSwap(target)); RemoveFixture(shieldBlock); yield return null;
            oldTarget = target.transform.position; P.RequestSwap(target); yield return new WaitForSeconds(.2f);
            CheckThat("shield_exchange", Near(P.transform.position, oldTarget), P.transform.position.ToString()); yield return Capture("04-transfer-hall");
            G.EnterStage(1, true); yield return WaitDeployments(); yield return new WaitForSeconds(.4f); P.SetFeet(new Vector3(0, .03f, 35));
            target = EnemyActor.Spawn(EnemyKind.Sniper, new Vector3(0, 0, 40), 1); target.suppressAI = true; Aim(target.AimPoint);
            hp = P.health; var grenade = GrenadeActor.Spawn(P.transform.position + Vector3.up * .15f, P.transform.position + Vector3.up * .15f, .6f);
            P.RequestSwap(target); yield return new WaitForSeconds(.18f);
            CheckThat("grenade_fuse_survives_swap", grenade && grenade.remaining < .6f && grenade.remaining > 0, grenade ? grenade.remaining.ToString("F2") : "missing");
            yield return new WaitForSeconds(.7f);
            CheckThat("real_grenade_reverse_kill", target && !target.Alive && P.health == hp, "Explosion at old position kills swapped enemy");
            G.EnterStage(2, true); yield return WaitDeployments(); yield return new WaitForSeconds(.4f); P.SetFeet(new Vector3(0, .03f, 84));
            target = G.enemies.Find(e => e.kind == EnemyKind.Assault && Vector3.Distance(e.transform.position,new Vector3(0,0,94))<3);
            foreach (var enemy in G.enemies) enemy.suppressAI = enemy != target;
            target.agent.speed = 0; int previousDifficulty = G.difficulty; G.difficulty = 0; G.qaSuppressAI = false;
            yield return new WaitForSeconds(3);
            var grenadeCover = Fixture(new Vector3(0, 1.1f, 89), new Vector3(3, 2.2f, .35f));
            float throwDeadline = Time.time + 5;
            while (target.grenadesThrown == 0 && Time.time < throwDeadline && G.IsPlaying) yield return null;
            CheckThat("grenadier_uses_last_visible_position", target.grenadesThrown > 0 && G.IsPlaying, "throws=" + target.grenadesThrown);
            G.qaSuppressAI = true; P.SetFeet(new Vector3(0, .03f, 76));
            var lobbed = G.effects.GetComponentInChildren<GrenadeActor>(); yield return new WaitForSeconds(1.7f);
            CheckThat("grenade_clears_cover", lobbed && lobbed.transform.position.z < 88, lobbed ? lobbed.transform.position.ToString() : "missing");
            RemoveFixture(grenadeCover); G.difficulty = previousDifficulty;
            G.EnterStage(1, true); yield return WaitDeployments(); yield return new WaitForSeconds(.4f); P.SetFeet(new Vector3(0, .03f, 35));
            foreach (var enemy in G.enemies) enemy.suppressAI = true;
            target = EnemyActor.Spawn(EnemyKind.Assault, new Vector3(0, 0, 40), 1); G.qaSuppressAI = false;
            hp = P.health; yield return new WaitForSeconds(5);
            bool blocked = Physics.Linecast(target.AimPoint, P.transform.position + Vector3.up * 1.1f, out var blocker, Layers.WorldMask);
            CheckThat("enemy_projectiles_damage_player", target.shotsFired > 0 && P.health > 0 && P.health < hp, "health=" + P.health + "; shots=" + target.shotsFired + "; player=" + P.transform.position + "; enemy=" + target.transform.position + "; blocker=" + (blocked ? blocker.collider.name : "none")); G.qaSuppressAI = true;
            P.cooldown = 3; G.SetState(RunState.Paused); float cooldown = P.cooldown;
            yield return new WaitForSecondsRealtime(.4f); CheckThat("pause_freezes_cooldown", P.cooldown == cooldown, P.cooldown.ToString("F2")); yield return Capture("05-pause");
            G.settingsOpen = true; yield return Capture("06-settings"); G.settingsOpen = false;
            float sensitivity = G.sensitivity, field = G.fov; G.sensitivity = 1.37f; G.fov = 67; G.SaveSettings(); G.sensitivity = .4f; G.LoadSettings();
            CheckThat("settings_persist", Mathf.Abs(G.sensitivity - 1.37f) < .01f && G.fov == 67, "Local settings round-trip"); G.sensitivity = sensitivity; G.fov = field; G.SaveSettings();
            G.SetState(RunState.Playing); G.HandleFocusLoss(); CheckThat("focus_loss_pauses", G.state == RunState.Paused, G.state.ToString()); G.SetState(RunState.Playing);
            GrenadeActor.Spawn(P.transform.position + Vector3.up, P.transform.position, 10); P.Hurt(1000, P.transform.position);
            CheckThat("death_screen", G.state == RunState.Dead, G.state.ToString()); G.Retry(); yield return new WaitForSeconds(.4f);
            CheckThat("retry_clears_transients", P.health == 100 && P.cooldown == 0 && P.ammo[0] == 24 && G.effects.GetComponentsInChildren<GrenadeActor>().Length == 0, "Health, ammo, cooldown and grenades reset");
            // Fixture damage accelerates objective progression; combat is exercised above.
            G.StartRun(); G.qaSuppressAI = true; yield return new WaitForSeconds(.4f);
            G.tutorialMoved = G.tutorialShot = G.tutorialSwapped = true; yield return UseTerminal(0, 0);
            CheckThat("tutorial_gate_opens", G.stageComplete && G.gates[0].open, "Tutorial prerequisites then interaction");
            P.SetFeet(new Vector3(0, .03f, 31)); yield return new WaitForSeconds(.25f);
            CheckThat("enter_cargo_checkpoint", G.stage == 1 && !G.gates[0].open, "Previous shutter closed");
            yield return ClearWaves(); CheckThat("cargo_two_waves", G.wave == 2 && G.combatComplete, "wave=" + G.wave);
            yield return UseTerminal(1, 0); P.SetFeet(new Vector3(0, .03f, 75)); yield return new WaitForSeconds(.25f);
            CheckThat("enter_warehouse_checkpoint", G.stage == 2, G.stage.ToString());
            int alive = G.AliveEnemies; yield return UseTerminal(2, 0); yield return UseTerminal(2, 1);
            CheckThat("relay_objective_without_kill_all", G.stageComplete && G.AliveEnemies == alive && alive > 0, "Alive guards=" + G.AliveEnemies);
            P.SetFeet(new Vector3(0, .03f, 119)); yield return new WaitForSeconds(.25f);
            CheckThat("enter_tower_checkpoint", G.stage == 3, G.stage.ToString()); yield return Capture("07-control-tower"); yield return ClearWaves();
            CheckThat("tower_three_waves_complete", G.wave == 3 && G.combatComplete, "wave=" + G.wave);
            yield return UseTerminal(3, 0); CheckThat("core_acquired", G.hasCore && G.gates[3].open, "Core unlocks extraction");
            P.SetFeet(new Vector3(0, .03f, 165)); yield return new WaitForSeconds(.25f);
            yield return UseTerminal(4, 0); CheckThat("campaign_victory", G.state == RunState.Victory, G.state.ToString()); yield return Capture("08-victory");
            G.StartRun(true); G.qaSuppressAI = true; yield return WaitDeployments(); yield return new WaitForSeconds(.4f);
            CheckThat("practice_mode", G.practice && G.stage == 1 && G.AliveEnemies > 0, "Repeatable combat entry");
            G.EnterStage(3, true); yield return WaitDeployments(); G.qaSuppressAI = false; var samples = new List<float>();
            for (int i = 0; i < 180; i++) { yield return null; samples.Add(Time.unscaledDeltaTime * 1000); }
            samples.Sort(); report.frameP50Ms = samples[samples.Count / 2]; report.frameP95Ms = samples[Mathf.FloorToInt((samples.Count - 1) * .95f)];
            G.qaSuppressAI = true; CheckThat("runtime_rendering", SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null, report.graphicsDevice); yield return V02Checks(); yield return ShieldFeedbackChecks(); Finish();
        }
        IEnumerator ShieldFeedbackChecks()
        {
            foreach (var kind in new[] { EnemyKind.Shield, EnemyKind.Elite })
            {
                G.StartRun(); G.EnterStage(0, true); G.qaSuppressAI = true;
                var foe = EnemyActor.Spawn(kind, new Vector3(3, 0, 16), 0); foe.suppressAI = true;
                CheckThat("shield_collider_identity_" + kind, !foe.IsShieldCollider(foe.GetComponent<CapsuleCollider>()) && foe.IsShieldCollider(foe.GetComponentInChildren<BoxCollider>()), "Body and shield are identified by collider reference, never GameObject name");
                P.SetFeet(new Vector3(3, .03f, 12)); Aim(foe.AimPoint); yield return new WaitForSeconds(.4f);
                float before = foe.health; bool fired = P.Fire(); yield return new WaitForSeconds(.12f);
                CheckThat("shield_front_feedback_" + kind, fired && foe.health == before && P.blockFlash > 0 && P.hitFlash == 0 && P.AimingAtShield, "Front plate blocks with distinct feedback");
                P.Hurt(1, P.transform.position + Vector3.right * 3);
                yield return Capture("shield-front-" + kind);
                P.SetFeet(new Vector3(-1, .03f, 16)); Aim(foe.AimPoint); yield return new WaitForSeconds(.5f);
                before = foe.health; fired = P.Fire(); yield return null;
                CheckThat("shield_side_damage_" + kind, fired && foe.health < before && P.hitFlash > 0 && P.blockFlash == 0 && !P.AimingAtShield, "Side body shot damages and clears block feedback");
                P.SetFeet(new Vector3(3, .03f, 20)); Aim(foe.AimPoint); yield return new WaitForSeconds(.5f);
                before = foe.health; fired = P.Fire(); yield return null;
                CheckThat("shield_back_damage_" + kind, fired && foe.health < before && P.hitFlash > 0 && !P.AimingAtShield, "Back body shot damages");
                yield return Capture("shield-back-" + kind);
                P.SetFeet(new Vector3(3, .03f, 12)); Aim(foe.AimPoint); yield return new WaitForSeconds(.5f);
                P.Fire(); P.cooldown = 0; yield return new WaitForSeconds(.05f);
                CheckThat("shield_swap_clears_block_" + kind, P.RequestSwap(foe), P.ValidateSwap(foe));
                yield return new WaitForSeconds(.18f);
                CheckThat("shield_swap_block_hint_reset_" + kind, P.blockFlash == 0, "Previous block message does not follow successful swap");
                Aim(foe.AimPoint); before = foe.health; fired = P.Fire(); yield return null;
                CheckThat("shield_swap_then_turn_damage_" + kind, fired && foe.health < before, "Swap, turn and shoot body actually damages");
                int killsBefore = G.totalKills;
                for (int shot = 0; foe.Alive && shot < 20; shot++) { yield return new WaitForSeconds(.15f); Aim(foe.AimPoint); P.Fire(); }
                CheckThat("shield_body_can_be_killed_" + kind, !foe.Alive && G.totalKills == killsBefore + 1, "Normal body shots produce a real kill and update run statistics");
            }
            G.SetState(RunState.Menu);
        }
        IEnumerator V02Checks()
        {
            G.SetState(RunState.Menu); G.options = new GameOptions(); G.SyncLegacyBindings(); G.ApplySettings();
            CheckThat("v02_model_library", G.config.models && G.config.models.carbine && G.config.models.playerBody && G.config.models.assault && G.config.models.cargo, "Serialized glTF-imported asset references exist");
            CheckThat("v02_imported_player_meshes", P.GetComponentsInChildren<MeshFilter>(true).Length > 50, "Player body, arms and both weapons instantiated");
            CheckThat("v02_world_models", Array.FindAll(G.world.GetComponentsInChildren<Transform>(true), t => t.name.Contains("[Three.js]")).Length > 100, "Imported environment modules are used in the live world");
            G.OpenSettings(); float applied = G.sensitivity; G.PendingOptions.sensitivity = 2.2f; G.CancelSettings();
            CheckThat("v02_cancel_discards_draft", G.sensitivity == applied, G.sensitivity.ToString("F2"));
            G.OpenSettings(); G.PendingOptions.sensitivity = 1.23f; G.PendingOptions.verticalSensitivity = .9f; G.PendingOptions.aimSensitivity = .6f;
            G.PendingOptions.crosshairGap = 8; G.PendingOptions.viewModelFov = 74; G.PendingOptions.volume = .35f; G.PendingOptions.vSync = false; G.PendingOptions.frameLimit = 2;
            G.PendingOptions.Bind(6, 0, "M:3", true); G.PendingOptions.Bind(7, 1, "K:T", true); G.ApplyDraft(); G.CancelSettings();
            var loaded = SettingsStore.Load();
            CheckThat("v02_settings_roundtrip", Mathf.Abs(loaded.sensitivity - 1.23f) < .001f && loaded.aimSensitivity == .6f && loaded.actions[6].primary == "M:3" && loaded.actions[7].secondary == "K:T" && loaded.crosshairGap == 8, "Numeric options, mouse and secondary bindings persisted");
            CheckThat("v02_options_apply_to_runtime", Mathf.Abs(AudioListener.volume - .35f) < .001f && QualitySettings.vSyncCount == 0 && Application.targetFrameRate == 144 && P.weaponCamera.fieldOfView == 74, "Audio, FPS and equipment FOV actually changed");
            var keys = new GameOptions(); bool refused = false;
            try { keys.Bind(6, 0, "K:E", false); } catch (InvalidOperationException) { refused = true; }
            CheckThat("v02_conflict_requires_choice", refused && keys.actions[6].primary == "K:Q", "Conflict did not silently overwrite");
            keys.Bind(6, 0, "K:E", true);
            CheckThat("v02_conflict_swap", keys.actions[6].primary == "K:E" && keys.actions[8].primary == "K:Q", "Both actions keep a usable binding");
            keys = new GameOptions(); keys.Bind(6, 1, "K:E", true);
            CheckThat("v02_secondary_conflict_no_duplicate", keys.actions[6].secondary == "K:E" && keys.actions[8].primary == "", "Explicit replacement of an empty secondary slot");
            keys.sensitivity = float.NaN; keys.fov = 10000; keys.Validate();
            CheckThat("v02_invalid_values_sanitized", !float.IsNaN(keys.sensitivity) && keys.fov == 85 && !BindingInput.Valid("K:9999"), "Malformed numeric/key values cannot enter controls");
            var keyboard = InputSystem.AddDevice<Keyboard>(); var mouse = InputSystem.AddDevice<Mouse>();
            G.qaKeyboard = keyboard; G.qaMouse = mouse; G.qaInputEnabled = true;
            InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Back)); yield return null; yield return null;
            CheckThat("v02_mouse_side_binding", G.Held(6), "Mouse back button resolves to swap");
            InputSystem.QueueStateEvent(mouse, new MouseState()); InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.T)); yield return null; yield return null;
            CheckThat("v02_secondary_key_binding", G.Held(7), "Secondary reload key is active");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;
            G.qaInputEnabled = false; G.options = new GameOptions(); G.SyncLegacyBindings(); G.ApplySettings();
            G.OpenSettings();
            for (int tab = 0; tab < 5; tab++) { G.SelectSettingsTab(tab); yield return Capture("v02-settings-" + tab); }
            G.CancelSettings(); G.StartRun(); G.qaSuppressAI = true; G.qaInputEnabled = true; yield return new WaitForSeconds(.3f);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W)); yield return new WaitForSeconds(.08f);
            float earlySpeed = P.CurrentSpeed; yield return new WaitForSeconds(.45f);
            CheckThat("v02_acceleration", earlySpeed > .3f && earlySpeed < G.config.walkSpeed && Mathf.Abs(P.CurrentSpeed - G.config.walkSpeed) < .2f, "early=" + earlySpeed + "; settled=" + P.CurrentSpeed);
            float movingSpread = P.CurrentSpreadDegrees;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.S)); yield return new WaitForSeconds(.075f);
            CheckThat("v02_counter_strafe_braking", P.CurrentSpeed < 1.6f, "speed=" + P.CurrentSpeed);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return new WaitForSeconds(.22f);
            CheckThat("v02_stop_restores_accuracy", P.CurrentSpeed < .05f && movingSpread > P.CurrentSpreadDegrees + 1, "moving=" + movingSpread + "; stopped=" + P.CurrentSpreadDegrees);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W, Key.LeftAlt)); yield return new WaitForSeconds(.45f);
            CheckThat("v02_slow_walk", Mathf.Abs(P.CurrentSpeed - G.config.slowSpeed) < .2f, P.CurrentSpeed.ToString("F2"));
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return new WaitForSeconds(.2f);
            G.options.toggleAim = true;
            InputSystem.QueueStateEvent(mouse, new MouseState { buttons = 2 }); yield return null; yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState()); yield return new WaitForSeconds(.3f);
            CheckThat("v02_toggle_aim", P.IsAiming && Mathf.Abs(P.cameraView.fieldOfView - G.fov * .84f) < 1, P.cameraView.fieldOfView.ToString("F2"));
            CheckThat("v02_iron_sight_alignment", P.AimAlignmentError < .025f, P.AimAlignmentError.ToString("F4"));
            InputSystem.QueueStateEvent(mouse, new MouseState { buttons = 2 }); yield return null; yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState()); yield return new WaitForSeconds(.15f);
            CheckThat("v02_toggle_aim_off", !P.IsAiming, "Second press returns to hip fire");
            G.qaInputEnabled = false; G.options.shake = 0; G.options.weaponMotion = 0; P.ResetAt(new Vector3(0,.03f,2)); P.SetLook(90,0); yield return new WaitForSeconds(.3f);
            P.Fire(); yield return null;
            CheckThat("v02_recoil_not_disabled_by_comfort", P.MechanicalRecoil.x > .3f && G.shake == 0, P.MechanicalRecoil.ToString());
            yield return new WaitForSeconds(.7f);
            CheckThat("v02_recoil_recovers", P.MechanicalRecoil.magnitude < .03f && P.ShotBloom < .01f, P.MechanicalRecoil.ToString());
            for (int i = 0; i < 8; i++) { P.Fire(); yield return new WaitForSeconds(.14f); }
            CheckThat("v02_sustained_fire_tradeoff", P.ShotBloom > .15f && P.MechanicalRecoil.x > 2, "bloom=" + P.ShotBloom + "; recoil=" + P.MechanicalRecoil);
            float heat = P.ShotBloom; yield return new WaitForSeconds(.25f);
            CheckThat("v02_accuracy_recovery", P.ShotBloom < heat, "before=" + heat + "; after=" + P.ShotBloom);
            G.options = new GameOptions(); G.SyncLegacyBindings(); G.ApplySettings(); P.ResetAt(new Vector3(0,.03f,5)); P.SetLook(0,0); yield return new WaitForSeconds(.4f);
            G.options.actions[12] = new ActionBinding("M:3"); G.qaInputEnabled = true; int ammo = P.ammo[0];
            InputSystem.QueueStateEvent(mouse, new MouseState()); yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Back)); yield return null; yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState()); yield return null;
            CheckThat("v02_rebound_fire_executes", P.ammo[0] < ammo, "Fire action follows configured mouse button");
            G.qaInputEnabled = false; InputSystem.RemoveDevice(keyboard); InputSystem.RemoveDevice(mouse); G.qaKeyboard = null; G.qaMouse = null;
            G.options = new GameOptions(); G.SyncLegacyBindings(); G.ApplySettings();
            G.EnterStage(1, true); yield return WaitDeployments(); G.qaSuppressAI = true; yield return new WaitForSeconds(.3f);
            yield return Capture("v02-gameplay");
            P.SetLook(0,78); yield return new WaitForSeconds(.25f); yield return Capture("v02-player-body");
            G.SetState(RunState.Menu); yield return Capture("v02-menu");
        }

        IEnumerator WaitDeployments()
        {
            float end=Time.time+10;while(G.PendingDeployments>0&&Time.time<end)yield return null;
            CheckThat("warned_deployments_complete",G.PendingDeployments==0,"pending="+G.PendingDeployments);
        }
        IEnumerator ClearWaves()
        {
            bool eliteObserved = false; float timeout = Time.time + 25;
            while (!G.combatComplete && Time.time < timeout)
            {
                foreach (var enemy in G.enemies.ToArray()) if (enemy && enemy.Alive) { if (enemy.kind == EnemyKind.Elite) eliteObserved = true; enemy.Damage(1000); }
                yield return new WaitForSeconds(.15f);
            }
            if (G.stage == 3) CheckThat("elite_present", eliteObserved, "Final wave contains the commander");
        }
        IEnumerator UseTerminal(int stage, int id)
        {
            Terminal terminal = G.terminals.Find(t => t.stage == stage && t.id == id);
            P.SetFeet(terminal.transform.position + new Vector3(0, .03f, -2)); P.verticalSpeed = 0; Aim(terminal.Point); yield return new WaitForSeconds(.15f);
            CheckThat("interact_" + stage + "_" + id, G.Interact(), terminal.label); yield return new WaitForSecondsRealtime(.15f);
        }
        void Aim(Vector3 point) { Vector3 d = (point - P.Eye.position).normalized; P.SetLook(Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg, -Mathf.Asin(d.y) * Mathf.Rad2Deg); }
        static bool Near(Vector3 a, Vector3 b) => Vector3.Distance(a, b) < .3f;
        GameObject Fixture(Vector3 center, Vector3 size) { GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = "QA obstruction"; go.layer = Layers.World; go.transform.position = center; go.transform.localScale = size; Physics.SyncTransforms(); return go; }
        void RemoveFixture(GameObject fixture) { fixture.GetComponent<Collider>().enabled = false; Destroy(fixture); Physics.SyncTransforms(); }
        void Route(string name, Vector3 from, Vector3 to)
        {
            bool a = NavMesh.SamplePosition(from, out var start, 1, NavMesh.AllAreas), b = NavMesh.SamplePosition(to, out var end, 1, NavMesh.AllAreas);
            NavMeshPath path = new NavMeshPath(); bool complete = a && b && NavMesh.CalculatePath(start.position, end.position, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete;
            CheckThat(name, complete, path.status.ToString());
        }
        IEnumerator Capture(string name) { yield return new WaitForEndOfFrame(); ScreenCapture.CaptureScreenshot(Path.Combine(output, name + ".png")); yield return new WaitForSecondsRealtime(.25f); }
        void CheckThat(string name, bool passed, string detail) { report.checks.Add(new Check { name = name, passed = passed, detail = detail }); Debug.Log("QA " + name + " " + passed + " " + detail); }
        void Finish()
        {
            if (finished) return; finished = true; if (originalOptions != null) { G.options = originalOptions; G.SyncLegacyBindings(); G.SaveSettings(); } RunStorage.Checkpoint = originalCheckpoint; 
            report.passed = report.checks.TrueForAll(c => c.passed); File.WriteAllText(Path.Combine(output, "qa-report.json"), JsonUtility.ToJson(report, true));
            Debug.Log("SWAPHUNTER_QA=" + report.passed); Application.Quit(report.passed ? 0 : 1);
        }
        void OnDestroy() { Application.logMessageReceived -= LogError; }
    }
}





