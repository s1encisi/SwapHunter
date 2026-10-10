using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace SwapHunter
{
    // Ideal-route feasibility probe. Only setup places the player; alarm-to-refuge travel uses real input/PlayerMotor.
    public sealed class BossEscapeV06QA : MonoBehaviour
    {
        [Serializable] public sealed class Attempt
        {
            public string id, outcome = "not_started", routeGoal, warning, detail;
            public bool stationaryControl, setupValid, skillStarted, waveExecuted, waveFinished, damagedByWave, reachedGroundedSafeHeight, stayedAboveThreshold, passed;
            public Vector3 requestedStart, actualStart, requestedBossFeet, bossFeet, goal, safeArrivalFeet, finalFeet;
            public Vector3[] navigationCorners;
            public float warningStartedGameTime, initialWarningSeconds = -1, cooldownAtWarning = -1, cooldownAtFirstDamage = -1;
            public float actualExecuteAfterWarning = -1, actualWaveFinishedAfterWarning = -1, actualSafeArrivalAfterWarning = -1, actualFirstMovementAfterWarning = -1, actualFirstDamageAfterWarning = -1;
            public float actualSafeArrivalWallSeconds = -1, healthAtWarning, healthAtEnd, observedDamage, expectedWaveDamage, minimumHeightAfterSafe = 999;
            public float plannedPathLength, advisoryDistanceToSafeHeight, actualMetersMoved, peakActualSpeed, elapsedGameSeconds, maximumObservedDeltaTime;
            public int unusedLaunchPadsAtStart, unusedLaunchPadsAtEnd, swapsAtStart, swapsAtEnd, positionAssignmentsDuringAttempt, samples;
        }
        [Serializable] public sealed class Report
        {
            public string kind = "Native chapter-12 GroundWave escape feasibility: explicit starting positions/source phase, no escorts, pads marked consumed, real mouse/keyboard and PlayerMotor. Not human reaction/playtesting or proof that every possible route was searched.";
            public string fixture = "v06-groundwave-slowest-loadout-v1", version, unityVersion, buildGuid, movement = "Sprint";
            public string loadout = "生存3阶；主武器支援轻机枪8+扩容备弹2，副武器栓狙7+扩容2；moveSpeedScale=1；不换枪、不跳跃、不翻越、不使用大跳或Q。";
            public string planner = "Full-map NavMesh route chosen before warning; advisory distance to first high segment selects among existing permanent refuges. Reaction delay is a reported parameter. Success uses observed grounded feet and native HP damage, never path length alone.";
            public string assumptions = "GroundWave warning is recorded from the actual skill; 12m/s ring, 2.7s execution, native 26 base damage, safe feet at least 1.15m above arena ground. Runtime warning/execution/arrival are recorded independently.";
            public bool batchMode, passed, complete;
            public float reactionDelaySeconds;
            public float configuredWalkSpeed, configuredSprintSpeed, configuredSwapCooldown, moveSpeedScale, movementMultiplier, nominalWalkSpeed, nominalSprintSpeed, safeFeetThreshold = 1.18f;
            public int expectedEscapeAttempts = 8;
            public List<Attempt> attempts = new List<Attempt>();
            public List<string> errors = new List<string>();
        }
        struct Trace
        {
            public int attempt, frame, step;
            public float gameSeconds, wallSeconds, health, speed, cooldown, waveRadius;
            public Vector3 feet;
            public bool grounded, sprinting;
        }
        sealed class Plan { public string name; public Vector3 goal; public Vector3[] corners; public float length, advisorySafeDistance; }
        readonly Report report = new Report();
        readonly Trace[] traces = new Trace[30000];
        int traceCount;
        string output;
        bool done, inputInstalled, attemptActive;
        float began;
        Keyboard keys, previousKeys;
        Mouse mouse, previousMouse;
        GameOptions previousOptions;
        bool previousInput, previousSuppression;
        EnemyActor source;
        DemoGame G => DemoGame.I;
        PlayerMotor P => G.player;
        static readonly Vector3[] Starts = {
            new Vector3(0, .04f, 30), new Vector3(0, .04f, 34),
            new Vector3(0, .04f, 46), new Vector3(0, .04f, 54),
            new Vector3(-18, .04f, 35), new Vector3(18, .04f, 42),
            new Vector3(-5, .04f, 21), new Vector3(5, .04f, 34)
        };
        static readonly string[] StartNames = { "front-center", "between-refuges", "rear-center", "rear-far", "left-flank", "right-flank", "left-ramp-approach", "right-ramp-approach" };

        void Awake()
        {
            began = Time.realtimeSinceStartup;
            var args = Environment.GetCommandLineArgs(); int i = Array.IndexOf(args, "-qaOutput");
            output = i >= 0 && i + 1 < args.Length ? args[i + 1] : RunStorage.Root; Directory.CreateDirectory(output);
            i = Array.IndexOf(args, "-bossEscapeMovement"); if (i >= 0 && i + 1 < args.Length) report.movement = args[i + 1];
            i = Array.IndexOf(args, "-bossEscapeReactionSeconds");
            if (i >= 0 && (i + 1 >= args.Length || !float.TryParse(args[i + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out report.reactionDelaySeconds)
                || report.reactionDelaySeconds < 0 || report.reactionDelaySeconds > 1.5f))
                report.errors.Add("Reaction delay must be 0..1.5 seconds.");
            report.version = Application.version; report.unityVersion = Application.unityVersion; report.buildGuid = Application.buildGUID; report.batchMode = Application.isBatchMode;
            Application.logMessageReceived += Error;
        }
        void Error(string message, string stack, LogType type)
        { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) report.errors.Add(message + "\n" + stack); }
        void Update() { if (!done && Time.realtimeSinceStartup - began > 180) { report.errors.Add("180-second real-time escape watchdog"); Finish(); } }
        IEnumerator Start()
        {
            if (!RunStorage.IsValidation) report.errors.Add("Escape probe requires isolated validation storage.");
            if (report.movement != "Sprint" && report.movement != "Walk") report.errors.Add("-bossEscapeMovement must be Sprint or Walk.");
            if (report.errors.Count > 0) { Finish(); yield break; }
            float ready = Time.realtimeSinceStartup + 15;
            while ((!G || !G.player || G.expeditionStore == null) && Time.realtimeSinceStartup < ready) yield return null;
            if (!G || !G.player || G.expeditionStore == null) { report.errors.Add("Runtime was not ready within 15 seconds."); Finish(); yield break; }
            var store = new ExpeditionStore(Path.Combine(RunStorage.Root, "v06-boss-escape-fixture"));
            for (int level = 0; level < 12; level++) { string run = store.Begin(level); store.Settle(run, true, false, 60); }
            store.Equip(0, 8); store.Equip(1, 7); store.SetAttachment(8, 2); store.SetAttachment(7, 2); store.SelectSpecialization(1);
            for (int rank = 0; rank < 3; rank++) store.Upgrade(1);
            G.expeditionStore = store;
            previousKeys = G.qaKeyboard; previousMouse = G.qaMouse; previousOptions = G.options; previousInput = G.qaInputEnabled; previousSuppression = G.qaSuppressAI;
            keys = InputSystem.AddDevice<Keyboard>(); mouse = InputSystem.AddDevice<Mouse>(); inputInstalled = true;
            G.options = new GameOptions { shake = 0, weaponMotion = 0, frameLimit = 0 }; G.SyncLegacyBindings(); G.ApplySettings(false);
            G.qaKeyboard = keys; G.qaMouse = mouse; G.qaInputEnabled = true; G.qaSuppressAI = true;
            if (!G.BeginExpedition(11)) { report.errors.Add("Chapter-12 fixture failed: " + G.expeditionNotice); Finish(); yield break; }
            G.mission?.Stop("boss escape fixture"); G.ClearDeployments();
            foreach (var enemy in G.enemies) if (enemy) enemy.gameObject.SetActive(false);
            foreach (var node in G.expeditionMap.nodes) if (node && node.kind == ExpeditionNodeKind.Launch) node.used = true;
            report.configuredWalkSpeed = G.config.walkSpeed; report.configuredSprintSpeed = G.config.sprintSpeed;
            report.configuredSwapCooldown = G.config.swapCooldown; report.moveSpeedScale = G.options.moveSpeedScale;
            report.movementMultiplier = G.ModuleMovement * G.ModuleAimMovement * G.ExpeditionMoveMultiplier * P.CurrentWeaponDefinition.movement * G.options.moveSpeedScale;
            report.nominalWalkSpeed = G.config.walkSpeed * report.movementMultiplier;
            report.nominalSprintSpeed = G.config.sprintSpeed * report.movementMultiplier;
            if (P.weapon != 8 || store.Profile.specialization != 1 || store.Profile.ranks[1] != 3 || store.Profile.attachments[8] != 2 || G.options.moveSpeedScale != 1)
            { report.errors.Add("Requested slowest loadout was not actually applied."); Finish(); yield break; }
            yield return RunAttempt("stationary-damage-control", Starts[0], true);
            for (int i = 0; i < Starts.Length; i++) yield return RunAttempt(StartNames[i], Starts[i], false);
            report.complete = true; Finish();
        }
        IEnumerator RunAttempt(string id, Vector3 requested, bool stationary)
        {
            Release(); G.qaSuppressAI = true; attemptActive = false;
            if (source) { source.gameObject.SetActive(false); Destroy(source.gameObject); }
            var result = new Attempt { id = id, requestedStart = requested, stationaryControl = stationary };
            report.attempts.Add(result); int attemptIndex = report.attempts.Count - 1;
            Physics.SyncTransforms();
            if (!NavMesh.SamplePosition(requested, out var startNav, .35f, NavMesh.AllAreas) || Mathf.Abs(startNav.position.y - requested.y) > .25f || !EnemyMotion.ClearVolume(startNav.position, .32f, 1.8f, P.transform))
            { result.outcome = "invalid_ground_start"; result.detail = "Requested point was not a clear authored ground start; no escape teleport was used."; yield break; }
            PlaceFixture(startNav.position + Vector3.up * .025f, result);
            P.SetLoadout(8, 7); P.health = G.config.playerHealth; P.verticalSpeed = 0;
            result.requestedBossFeet = G.expeditionMap.root.GetComponent<MissionLayout>().Spawn("boss", 0).position;
            source = EnemyActor.Spawn(EnemyKind.Elite, result.requestedBossFeet, G.stage);
            if (!source) { result.outcome = "boss_fixture_failed"; yield break; }
            var boss = source.boss ? source.boss : source.EnableBoss();
            source.Damage(BossEncounter.ShieldMaximum + BossEncounter.HealthMaximum * .5f + 1);
            Vector3 face = source.AimPoint - P.Eye.position;
            P.SetLook(Mathf.Atan2(face.x, face.z) * Mathf.Rad2Deg, -Mathf.Atan2(face.y, new Vector2(face.x, face.z).magnitude) * Mathf.Rad2Deg);
            // Settle the initial controller and consume its legitimate post-setup look-ignore frames before the warning.
            yield return new WaitForSeconds(.16f); Release();
            result.actualStart = P.transform.position; result.bossFeet = source.transform.position;
            if (!P.Grounded || P.transform.position.y >= 1.15f) { result.outcome = "start_not_grounded_below_wave"; yield break; }
            Plan plan = stationary ? null : ChoosePlan(P.transform.position);
            if (!stationary && plan == null) { result.outcome = "no_complete_refuge_route"; result.detail = "No complete NavMesh candidate to an existing permanent high point; no successful escape claimed."; yield break; }
            if (plan != null) { result.routeGoal = plan.name; result.goal = plan.goal; result.navigationCorners = plan.corners; result.plannedPathLength = plan.length; result.advisoryDistanceToSafeHeight = plan.advisorySafeDistance; }
            result.setupValid = true; result.unusedLaunchPadsAtStart = UnusedPads(); result.swapsAtStart = P.swapCount;
            P.cooldown = G.CampaignCooldown * G.ExpeditionCooldownMultiplier; // The real maximum cooldown; it then decrements normally.
            result.cooldownAtWarning = P.cooldown; result.healthAtWarning = P.health;
            result.expectedWaveDamage = 26 * G.DamageMultiplier * G.ModuleDamageTaken * G.ExpeditionDamageTaken;
            var oldEffects = new HashSet<int>(); foreach (Transform effect in G.effects) oldEffects.Add(effect.GetInstanceID());
            result.warningStartedGameTime = Time.time; double wallStarted = Time.realtimeSinceStartupAsDouble;
            result.skillStarted = boss.TryBeginSkill(BossSkill.GroundWave); result.warning = boss.Warning; result.initialWarningSeconds = boss.SkillSeconds;
            if (!result.skillStarted) { result.outcome = "native_groundwave_rejected"; yield break; }
            LineRenderer wave = null;
            foreach (Transform effect in G.effects)
                if (!oldEffects.Contains(effect.GetInstanceID()) && effect.TryGetComponent<LineRenderer>(out var line) && line.positionCount > 8) wave = line;
            result.outcome = "running"; attemptActive = true; G.qaSuppressAI = false;
            int corner = 0; Vector3 previous = P.transform.position; float lastTrace = -1, lastProgressAt = Time.time; Vector3 lastProgressPoint = previous;
            while (Time.time - result.warningStartedGameTime < 8)
            {
                float elapsed = Time.time - result.warningStartedGameTime;
                result.maximumObservedDeltaTime = Mathf.Max(result.maximumObservedDeltaTime, Time.deltaTime);
                if (!G.IsPlaying || P.health <= 0) { result.outcome = "game_stopped_or_player_died"; break; }
                if (boss.SkillStep == BossSkillStep.Execute && !result.waveExecuted) { result.waveExecuted = true; result.actualExecuteAfterWarning = elapsed; }
                if (result.waveExecuted && boss.SkillStep == BossSkillStep.Recover && !result.waveFinished)
                { result.waveFinished = true; result.actualWaveFinishedAfterWarning = elapsed; G.qaSuppressAI = true; }
                Vector3 feet = P.transform.position; float moved = Vector3.Distance(feet, previous); result.actualMetersMoved += moved; previous = feet;
                result.peakActualSpeed = Mathf.Max(result.peakActualSpeed, P.CurrentSpeed);
                if (result.actualFirstMovementAfterWarning < 0 && Vector3.ProjectOnPlane(feet - result.actualStart, Vector3.up).magnitude > .05f) result.actualFirstMovementAfterWarning = elapsed;
                if (P.health < result.healthAtWarning - .01f && !result.damagedByWave)
                { result.damagedByWave = true; result.actualFirstDamageAfterWarning = elapsed; result.cooldownAtFirstDamage = P.cooldown; }
                if (P.Grounded && feet.y >= report.safeFeetThreshold && !result.reachedGroundedSafeHeight)
                { result.reachedGroundedSafeHeight = true; result.actualSafeArrivalAfterWarning = elapsed; result.actualSafeArrivalWallSeconds = (float)(Time.realtimeSinceStartupAsDouble - wallStarted); result.safeArrivalFeet = feet; }
                if (result.reachedGroundedSafeHeight) result.minimumHeightAfterSafe = Mathf.Min(result.minimumHeightAfterSafe, feet.y);
                if (elapsed - lastTrace >= .02f || result.damagedByWave && Mathf.Abs(elapsed - result.actualFirstDamageAfterWarning) < .001f)
                {
                    if (traceCount == traces.Length) { report.errors.Add("Trajectory buffer exhausted; no truncated evidence accepted."); break; }
                    traces[traceCount++] = new Trace { attempt = attemptIndex, frame = Time.frameCount, step = (int)boss.SkillStep,
                        gameSeconds = elapsed, wallSeconds = (float)(Time.realtimeSinceStartupAsDouble - wallStarted), feet = feet, health = P.health,
                        speed = P.CurrentSpeed, cooldown = P.cooldown, grounded = P.Grounded, sprinting = P.IsSprinting,
                        waveRadius = wave && boss.SkillStep == BossSkillStep.Execute ? Vector3.Distance(wave.GetPosition(0), boss.DangerCenter) : -1 };
                    result.samples++; lastTrace = elapsed;
                }
                if (result.waveFinished && (stationary || result.reachedGroundedSafeHeight)) break;
                if (stationary || elapsed < report.reactionDelaySeconds || result.reachedGroundedSafeHeight) Release();
                else
                {
                    while (corner < plan.corners.Length && FlatDistance(feet, plan.corners[corner]) < .28f && Mathf.Abs(feet.y - plan.corners[corner].y) < .5f) corner++;
                    if (corner >= plan.corners.Length) { result.detail = "Reached planned route end without grounded safe height."; Release(); }
                    else
                    {
                        Vector3 direction = plan.corners[corner] - feet; direction.y = 0; QueueLook(direction);
                        InputSystem.QueueStateEvent(keys, report.movement == "Sprint" ? new KeyboardState(Key.W, Key.LeftShift) : new KeyboardState(Key.W));
                    }
                    if (Time.time - lastProgressAt >= 1.2f)
                    {
                        if (Vector3.Distance(feet, lastProgressPoint) < .08f) result.detail = "Controller stalled despite the planned NavMesh route; observed at " + feet + ".";
                        lastProgressAt = Time.time; lastProgressPoint = feet;
                    }
                }
                yield return null;
            }
            Release(); G.qaSuppressAI = true; attemptActive = false;
            result.finalFeet = P.transform.position; result.healthAtEnd = P.health; result.observedDamage = result.healthAtWarning - P.health;
            result.elapsedGameSeconds = Time.time - result.warningStartedGameTime; result.swapsAtEnd = P.swapCount; result.unusedLaunchPadsAtEnd = UnusedPads();
            result.stayedAboveThreshold = result.reachedGroundedSafeHeight && result.minimumHeightAfterSafe >= 1.15f;
            bool invariants = result.positionAssignmentsDuringAttempt == 0 && result.swapsAtEnd == result.swapsAtStart && result.unusedLaunchPadsAtStart == 0 && result.unusedLaunchPadsAtEnd == 0;
            result.passed = invariants && result.waveExecuted && result.waveFinished && (stationary
                ? result.damagedByWave && Mathf.Abs(result.observedDamage - result.expectedWaveDamage) < .1f
                : result.reachedGroundedSafeHeight && result.stayedAboveThreshold && !result.damagedByWave);
            if (stationary) result.outcome = result.passed ? "native_wave_damage_control_passed" : "native_wave_damage_control_failed";
            else result.outcome = result.passed ? "escaped_native_wave_on_permanent_height" : result.damagedByWave ? result.reachedGroundedSafeHeight ? "hit_before_late_refuge_arrival" : "hit_without_reaching_refuge" : "no_verified_permanent_height_escape";
            Debug.Log("BOSS_ESCAPE_V06 " + id + " " + result.outcome + ";arrival=" + result.actualSafeArrivalAfterWarning + ";damageAt=" + result.actualFirstDamageAfterWarning + ";damage=" + result.observedDamage + ";feet=" + result.finalFeet);
        }
        void PlaceFixture(Vector3 feet, Attempt result)
        {
            if (attemptActive) { result.positionAssignmentsDuringAttempt++; throw new InvalidOperationException("Player placement is forbidden during the escape attempt."); }
            P.CancelActions(); P.SetFeet(feet);
        }
        Plan ChoosePlan(Vector3 start)
        {
            Plan best = null; int index = 0;
            foreach (var candidate in G.expeditionMap.walkingCheckpoints)
            {
                index++;
                if (candidate.y < report.safeFeetThreshold || !NavMesh.SamplePosition(candidate, out var goal, .45f, NavMesh.AllAreas) || Mathf.Abs(goal.position.y - candidate.y) > .3f) continue;
                var path = new NavMeshPath(); if (!NavMesh.CalculatePath(start, goal.position, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete) continue;
                var corners = path.corners; if (corners.Length == 0) continue;
                float length = 0, advisory = -1;
                for (int i = 1; i < corners.Length; i++)
                {
                    float segment = Vector3.Distance(corners[i - 1], corners[i]);
                    if (advisory < 0 && corners[i].y >= report.safeFeetThreshold)
                        advisory = length + segment * Mathf.Clamp01(Mathf.InverseLerp(corners[i - 1].y, corners[i].y, report.safeFeetThreshold));
                    length += segment;
                }
                if (advisory < 0) continue;
                if (best == null || advisory < best.advisorySafeDistance) best = new Plan { name = "permanent-refuge-" + index, goal = goal.position, corners = corners, length = length, advisorySafeDistance = advisory };
            }
            return best;
        }
        int UnusedPads() { int n = 0; foreach (var node in G.expeditionMap.nodes) if (node && node.kind == ExpeditionNodeKind.Launch && !node.used) n++; return n; }
        static float FlatDistance(Vector3 a, Vector3 b) => new Vector2(a.x - b.x, a.z - b.z).magnitude;
        void QueueLook(Vector3 direction)
        {
            if (direction.sqrMagnitude < .000001f) return; direction.Normalize(); Vector3 current = P.Eye.forward;
            float yaw = Mathf.Atan2(current.x, current.z) * Mathf.Rad2Deg, pitch = -Mathf.Asin(Mathf.Clamp(current.y, -1, 1)) * Mathf.Rad2Deg;
            float targetYaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg, scale = G.sensitivity * .04f;
            InputSystem.QueueStateEvent(mouse, new MouseState { delta = new Vector2(Mathf.DeltaAngle(yaw, targetYaw) / scale, -pitch / (scale * G.options.verticalSensitivity * (G.invertY ? 1 : -1))) });
        }
        void Release()
        {
            if (keys != null && keys.added) InputSystem.QueueStateEvent(keys, new KeyboardState());
            if (mouse != null && mouse.added) InputSystem.QueueStateEvent(mouse, new MouseState());
        }
        void RestoreInput()
        {
            if (!inputInstalled) return; Release(); inputInstalled = false;
            if (keys != null && keys.added) InputSystem.RemoveDevice(keys); if (mouse != null && mouse.added) InputSystem.RemoveDevice(mouse);
            G.qaKeyboard = previousKeys; G.qaMouse = previousMouse; G.qaInputEnabled = previousInput; G.qaSuppressAI = previousSuppression; G.options = previousOptions; G.SyncLegacyBindings();
        }
        void Finish()
        {
            if (done) return; done = true; RestoreInput();
            report.passed = report.complete && report.errors.Count == 0 && report.attempts.Count == report.expectedEscapeAttempts + 1 && report.attempts.TrueForAll(a => a.passed);
            File.WriteAllText(Path.Combine(output, "boss-escape-v06-report.json"), JsonUtility.ToJson(report, true));
            using (var writer = new StreamWriter(Path.Combine(output, "boss-escape-v06-trajectory.csv")))
            {
                writer.WriteLine("attempt,frame,game_seconds,wall_seconds,x,y,z,grounded,sprinting,speed,cooldown,health,skill_step,observed_wave_radius");
                for (int i = 0; i < traceCount; i++)
                {
                    var t = traces[i]; writer.WriteLine(string.Join(",", report.attempts[t.attempt].id, t.frame.ToString(), F(t.gameSeconds), F(t.wallSeconds), F(t.feet.x), F(t.feet.y), F(t.feet.z), t.grounded ? "1" : "0", t.sprinting ? "1" : "0", F(t.speed), F(t.cooldown), F(t.health), ((BossSkillStep)t.step).ToString(), F(t.waveRadius)));
                }
            }
            Debug.Log("BOSS_ESCAPE_V06_QA_RESULT=" + report.passed + ";cases=" + report.attempts.Count); Application.Quit(report.passed ? 0 : 1);
        }
        static string F(float value) => value.ToString("F4", CultureInfo.InvariantCulture);
        void OnDestroy() { Application.logMessageReceived -= Error; if (inputInstalled && G) RestoreInput(); }
    }
}
