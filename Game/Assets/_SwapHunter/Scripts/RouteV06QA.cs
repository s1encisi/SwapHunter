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
    // No player-position assignment, SetFeet or agent Warp. This is a controlled
    // clear-enemy fallback route fixture, not a human/combat/performance test.
    public sealed class RouteV06QA : MonoBehaviour
    {
        [Serializable] public sealed class Check { public string name, detail; public bool passed; }
        [Serializable] public sealed class Chapter
        {
            public int level, fixtureKills, swaps, checkpointsWalked, checkpointsSkipped;
            public bool passed;
            public float metersWalked, seconds;
            public string failure = "", lastStep = "", lastPosition = "", lastCorners = "";
            public int untriggeredBeforeExit, warningBeforeExit, livingBeforeExit, cancelledAfterExit;
        }
        [Serializable] public sealed class Report
        {
            public string version, startedUtc = DateTime.UtcNow.ToString("o");
            public string scope = "清敌/耗尽跳板的保底路线夹具，非真人/真实战斗通关。虚拟 Keyboard/Mouse 驱动真实 PlayerMotor；仅 Begin 正常出生和真实 RequestSwap 改变位置。第10/11关保留活敌，第12关只夹具击败 Boss。";
            public string status = "running", loadout = "", storage = "";
            public bool passed, allTwelveCovered;
            public int[] selectedLevels;
            public List<Check> checks = new List<Check>();
            public List<Chapter> chapters = new List<Chapter>();
        }
        readonly Report report = new Report();
        readonly List<int> levels = new List<int>();
        DemoGame G => DemoGame.I;
        PlayerMotor P => G.player;
        Keyboard keys, previousKeys;
        Mouse mouse, previousMouse;
        GameOptions previousOptions;
        ExpeditionStore previousStore;
        bool previousInput, previousSuppression, inputInstalled, finished, routing, ok;
        string output, fatalError;
        Chapter current;
        float startedRealtime, chapterStart, watchdogSeconds = 60;
        const float EyeHeight = 1.62f;
        void Awake() { report.version = Application.version;  startedRealtime = Time.realtimeSinceStartup; Application.logMessageReceived += OnLog; }
        void OnDestroy() { Application.logMessageReceived -= OnLog; RestoreInput(); }
        void OnLog(string message, string stack, LogType type)
        {
            if (!finished && (type == LogType.Error || type == LogType.Exception || type == LogType.Assert))
                fatalError = message + "\n" + stack;
        }
        void Update()
        {
            if (finished) return;
            if (fatalError != null) { Add("runtime_error", false, fatalError); Finish(); return; }
            if (Time.realtimeSinceStartup - startedRealtime > watchdogSeconds)
            { Add("watchdog", false, "Exceeded " + watchdogSeconds + "s; " + Diagnostic()); Finish(); return; }
            if (routing && current != null) { current.lastPosition = P.transform.position.ToString("F3"); MaintainFixture(); }
        }
        void Add(string name, bool passed, string detail = "")
        {
            report.checks.Add(new Check { name = name, passed = passed, detail = detail });
            Debug.Log("ROUTE_V06_QA " + name + " " + passed + " " + detail);
        }
        string Key => current == null ? "setup" : "chapter_" + current.level.ToString("00");
        string Diagnostic() => current == null ? "setup" : "step=" + current.lastStep + ";position=" + current.lastPosition + ";corners=" + current.lastCorners;
        void Fail(string step, string detail)
        {
            if (!ok) return;
            ok = false; Release();
            if (current != null) { current.failure = detail; current.lastStep = step; }
            Add(Key + "_" + step + "_failed", false, detail + "; " + Diagnostic());
        }
        void Step(string name) { if (current != null) current.lastStep = name; G.Record("route_fixture_step", Key + ":" + name); }
        bool ParseLevels(string[] args)
        {
            int index = Array.IndexOf(args, "-v06Levels");
            if (index < 0) for (int level = 0; level < 12; level++) levels.Add(level);
            else
            {
                if (index + 1 >= args.Length) { Add("levels_argument", false, "-v06Levels 缺少 1-based 列表，例如 2,6,7"); return false; }
                var seen = new HashSet<int>();
                foreach (string token in args[index + 1].Split(','))
                {
                    if (!int.TryParse(token.Trim(), out int human) || human < 1 || human > 12)
                    { Add("levels_argument", false, "非法章节：" + token); return false; }
                    if (seen.Add(human)) levels.Add(human - 1);
                }
            }
            report.selectedLevels = levels.ConvertAll(level => level + 1).ToArray();
            watchdogSeconds = 240 * levels.Count + 40;
            return levels.Count > 0;
        }
        bool Prepare()
        {
            if (!G || !P || !G.qaMode || !RunStorage.IsValidation)
            { Add("isolated_validation_guard", false, "Requires qaMode and RunStorage.IsValidation."); return false; }
            if (G.expeditionActive && G.expeditionReceipt == null)
            { Add("live_run_guard", false, "Refusing to replace an unfinished player run."); return false; }
            previousStore = G.expeditionStore;
            try
            {
                var store = new ExpeditionStore(Path.Combine(RunStorage.Root, "route-v06-profile"));
                // Data fixture unlock only; never reported as gameplay completion.
                for (int level = 0; level < 12; level++)
                { string id = store.Begin(level); store.Settle(id, true, false, 60); }
                store.Equip(0, 8); store.Equip(1, 7);
                store.SetAttachment(8, 2); store.SetAttachment(7, 2); store.SelectSpecialization(1);
                for (int i = 0; i < 3; i++) store.Upgrade(1);
                G.expeditionStore = store; report.storage = store.SavePath;
                Add("fixture_profile_isolated", store.SavePath.StartsWith(RunStorage.Root, StringComparison.OrdinalIgnoreCase), store.SavePath);
                Add("fixture_no_recall_research", store.Profile.knifeRank == 0);
            }
            catch (Exception error) { Add("fixture_profile_setup", false, error.ToString()); return false; }
            previousKeys = G.qaKeyboard; previousMouse = G.qaMouse; previousOptions = G.options;
            previousInput = G.qaInputEnabled; previousSuppression = G.qaSuppressAI;
            keys = InputSystem.AddDevice<Keyboard>(); mouse = InputSystem.AddDevice<Mouse>(); inputInstalled = true;
            G.options = new GameOptions { shake = 0, weaponMotion = 0, frameLimit = 0 };
            G.SyncLegacyBindings(); G.ApplySettings(false);
            G.qaKeyboard = keys; G.qaMouse = mouse; G.qaInputEnabled = true; G.qaSuppressAI = true;
            report.loadout = "生存专精3阶；主武器轻机枪(id8)+扩容备弹，副武器栓狙(id7)+扩容；默认moveSpeedScale=1；普通W，不冲刺/慢走/大跳；飞刀研究0。";
            Add("input_devices_installed", G.KeyboardDevice == keys && G.MouseDevice == mouse, report.loadout);
            return true;
        }

        IEnumerator Start()
        {
            string[] args = Environment.GetCommandLineArgs(); int outputIndex = Array.IndexOf(args, "-qaOutput");
            output = outputIndex >= 0 && outputIndex + 1 < args.Length ? args[outputIndex + 1] : RunStorage.Root;
            Directory.CreateDirectory(output);
            if (!ParseLevels(args)) { Finish(); yield break; }
            // Batch/nographics may never report SplashScreen.isFinished.
            float readyUntil = Time.realtimeSinceStartup + 8;
            while ((!G || !G.player || G.expeditionStore == null) && Time.realtimeSinceStartup < readyUntil) yield return null;
            if (!Prepare()) { Finish(); yield break; }
            try
            {
                foreach (int level in levels)
                {
                    current = new Chapter { level = level + 1 };
                    int chapterFirstCheck = report.checks.Count;
                    report.chapters.Add(current); ok = true; routing = false;
                    if (G.expeditionActive) G.OpenExpeditionBoard();
                    if (!G.BeginExpedition(level)) { Fail("begin", G.expeditionNotice); SaveProgress(); continue; }
                    chapterStart = Time.realtimeSinceStartup; routing = true;
                    Release(); yield return new WaitForSecondsRealtime(.2f);
                    Add(Key + "_normal_spawn", Vector3.Distance(P.transform.position, G.expeditionMap.spawn) < .45f, P.transform.position.ToString("F3"));
                    int launches = 0;
                    foreach (var node in G.expeditionMap.nodes)
                        if (node && node.kind == ExpeditionNodeKind.Launch) { node.used = true; launches++; }
                    Add(Key + "_launches_exhausted_fixture", true, "marked used=" + launches + "; no launch replenishment");
                    Add(Key + "_slow_loadout", P.weapon == 8 && G.expeditionStore.Profile.specialization == 1
                        && G.expeditionStore.Profile.ranks[1] == 3 && G.options.moveSpeedScale == 1,
                        "normal-W speed=" + (G.config.walkSpeed * G.options.moveSpeedScale * G.ExpeditionMoveMultiplier * P.CurrentWeaponDefinition.movement).ToString("F3"));
                    if (level == 0)
                    {
                        yield return CalibrationSwap();
                        if (ok) yield return ReachRegion("combat", "combat_region");
                        if (ok) yield return WaitRequired("combat", 12);
                    }
                    else if (level == 1)
                    {
                        yield return WaitRequired("first", 12);
                        if (ok) yield return ReachRegion("second", "second_region");
                        if (ok) yield return WaitRequired("second", 12);
                    }
                    else if (level == 6) yield return RelaySequence();
                    else
                    {
                        foreach (var objective in G.mission.Definition.objectives)
                        {
                            if (!ok) break;
                            if (level == 7 && objective.id == "c") continue;
                            var node = ObjectiveNode(objective.id);
                            if (!node) { Fail("objective_" + objective.id, "Bound objective node is missing."); break; }
                            if (node.GetComponent<DamageableObjective>()) yield return DestroyByMelee(node);
                            else yield return VisitNode(node, "objective_" + objective.id);
                        }
                    }
                    if (ok && level == 7)
                    {
                        Add(Key + "_two_crates_only", G.mission.CompletedObjectives == 2 && !G.expeditionBonus && !G.MissionObjectiveComplete("c"));
                        Add(Key + "_third_warehouse_not_activated", !G.mission.GroupTriggered("warehouse-c") && !G.mission.RegionVisited("c"));
                    }
                    if (ok) yield return WaitMainReady(12);
                    if (ok) yield return WalkCheckpoints(level);
                    if (ok && level == 7) Add(Key + "_third_warehouse_still_not_activated", !G.mission.GroupTriggered("warehouse-c") && !G.mission.RegionVisited("c"));
                    if (ok && (level == 9 || level == 10)) Add(Key + "_no_fixture_kills", current.fixtureKills == 0, "living=" + G.mission.LivingNonRequired);
                    if (ok && level == 10)
                    {
                        bool active = true; int count = 0;
                        foreach (var field in G.expeditionMap.root.GetComponentsInChildren<PhaseInterferenceField>()) { count++; active &= field.Active; }
                        Add(Key + "_did_not_require_field_shutdown", active && count == 2 && !G.MissionObjectiveComplete("field-left") && !G.MissionObjectiveComplete("field-right"));
                    }
                    if (ok)
                    {
                        current.untriggeredBeforeExit = G.mission.UntriggeredBudget; current.warningBeforeExit = G.mission.WarningBudget;
                        current.livingBeforeExit = G.mission.LivingNonRequired;
                        yield return VisitNode(G.expeditionMap.nodes.Find(node => node && node.kind == ExpeditionNodeKind.Extraction), "extract");
                    }
                    bool settled = G.expeditionReceipt != null && G.expeditionReceipt.success;
                    bool chapterAssertionsPassed = true;
                    for (int check = chapterFirstCheck; check < report.checks.Count; check++)
                        if (!report.checks[check].passed) chapterAssertionsPassed = false;
                    current.passed = ok && settled && chapterAssertionsPassed;
                    if (!chapterAssertionsPassed && string.IsNullOrEmpty(current.failure)) current.failure = "本章断言失败，请查看 checks 中的实际失败项。";
                    if (G.mission != null) current.cancelledAfterExit = G.mission.CancelledBudget;
                    current.seconds = Time.realtimeSinceStartup - chapterStart;
                    Add(Key + "_completed", current.passed, "real walk=" + current.metersWalked.ToString("F2") + "m; real Q=" + current.swaps + "; fixture kills=" + current.fixtureKills + "; " + current.failure);
                    Release(); routing = false;
                    if (!settled && G.expeditionActive && G.expeditionReceipt == null) G.FinishExpedition(false);
                    G.OpenExpeditionBoard(); SaveProgress(); yield return null;
                }
            }
            finally { routing = false; RestoreInput(); }
            Finish();
        }

        void MaintainFixture()
        {
            if (!G || !G.IsPlaying || G.mission == null || current == null) return;
            int level = current.level - 1;
            if (level == 9 || level == 10) return;
            foreach (var member in G.mission.Members)
            {
                if (member.state != EncounterBudgetState.Deployed || !member.actor || !member.actor.Alive) continue;
                if (level == 11 && member.role.kind != EnemyKind.Elite) continue;
                G.Record("route_fixture_clear_deployed", member.Key + ";kind=" + member.role.kind);
                member.actor.Damage(100000); current.fixtureKills++;
            }
        }
        ExpeditionNode ObjectiveNode(string id)
        {
            var layout = G.expeditionMap.root.GetComponent<MissionLayout>();
            foreach (var node in G.expeditionMap.nodes)
                if (node && layout.TryObjective(node, out string bound) && bound == id) return node;
            return null;
        }
        IEnumerator WaitRequired(string group, float timeout)
        {
            Step("wait_deployed_" + group);
            float until = Time.realtimeSinceStartup + timeout;
            bool arrived = false, defeated = false;
            while (ok && Time.realtimeSinceStartup < until)
            {
                arrived = false; defeated = true;
                foreach (var member in G.mission.Members)
                {
                    if (member.group.id != group || !member.role.required) continue;
                    arrived |= member.state == EncounterBudgetState.Deployed;
                    if (!member.Defeated) defeated = false;
                }
                if (arrived && defeated) break;
                Release(); yield return null;
            }
            if (!arrived || !defeated) Fail("wait_deployed_" + group, "Required budget never deployed/was not defeated; " + BudgetStatus());
            else Add(Key + "_" + group + "_actually_deployed", true, "Only real deployed actor was cleared; no pending budget fabricated.");
            yield return new WaitForSecondsRealtime(.15f);
        }
        IEnumerator WaitMainReady(float timeout)
        {
            Step("wait_main_ready");
            float until = Time.realtimeSinceStartup + timeout;
            while (ok && !G.ExpeditionMainComplete && Time.realtimeSinceStartup < until) { Release(); yield return null; }
            if (!G.ExpeditionMainComplete) Fail("main_ready", G.ExpeditionObjective + "; " + BudgetStatus());
            else Add(Key + "_main_ready_from_actual_actions", true, BudgetStatus());
            yield return new WaitForSecondsRealtime(.15f);
        }
        string BudgetStatus()
        {
            if (G.mission == null) return "no mission";
            return "untriggered=" + G.mission.UntriggeredBudget + ";warning=" + G.mission.WarningBudget
                + ";deployed=" + G.mission.DeployedBudget + ";cancelled=" + G.mission.CancelledBudget + ";required=" + G.mission.RequiredRemaining;
        }
        IEnumerator CalibrationSwap()
        {
            Step("calibration_swap"); PhaseAnchor choice = null;
            foreach (var anchor in G.expeditionMap.anchors)
                if (anchor && P.ValidateSwap(anchor).IsValid) { choice = anchor; break; }
            if (!choice) { Fail("calibration_swap", "No legal calibration anchor from normal spawn."); yield break; }
            yield return RealSwap(choice, "calibration_swap");
        }
        IEnumerator RealSwap(PhaseAnchor anchor, string step)
        {
            Step(step); Release();
            float until = Time.realtimeSinceStartup + 8;
            while (P.cooldown > .001f && Time.realtimeSinceStartup < until) { Release(); yield return null; }
            if (P.cooldown > .001f) { Fail(step, "Swap cooldown did not finish."); yield break; }
            yield return AimAt(anchor.AimPoint);
            if (!ok) yield break;
            var reason = P.ValidateSwap(anchor);
            if (!reason.IsValid) { Fail(step, "Real swap validation rejected: " + reason); yield break; }
            Vector3 before = P.transform.position, destination = anchor.transform.position;
            int swaps = G.totalSwaps;
            if (!P.RequestSwap(anchor)) { Fail(step, "RequestSwap rejected after valid preview."); yield break; }
            yield return new WaitForSecondsRealtime(.24f);
            bool done = G.totalSwaps == swaps + 1 && Vector3.Distance(P.transform.position, destination) < .25f
                && Vector3.Distance(anchor.transform.position, before) < .2f;
            Add(Key + "_" + step + "_transaction", done, "source=" + before.ToString("F3") + ";destination=" + destination.ToString("F3")
                + ";player=" + P.transform.position.ToString("F3") + ";anchor=" + anchor.transform.position.ToString("F3"));
            if (!done) Fail(step, "Actual swap did not reach both endpoints.");
            else current.swaps++;
            Release();
        }
        IEnumerator RelaySequence()
        {
            var anchors = G.expeditionMap.anchors.ToArray();
            if (anchors.Length != 3) { Fail("relay", "Expected the authored three task anchors."); yield break; }
            var origins = new Vector3[anchors.Length];
            for (int i = 0; i < anchors.Length; i++) origins[i] = anchors[i].transform.position;
            string[] ids = { "a", "b", "c" }; int[] target = { 1, 2, 0 };
            for (int i = 0; i < ids.Length && ok; i++)
            {
                var node = ObjectiveNode(ids[i]);
                var socket = node ? node.GetComponent<PhaseAnchorSocket>() : null;
                if (!socket) { Fail("relay_" + ids[i], "Missing authored socket."); yield break; }
                yield return ReachPoint(socket.transform.position, "socket_stand_" + ids[i], .2f);
                if (!ok) yield break;
                Add(Key + "_socket_standing_" + ids[i], Vector3.Distance(P.transform.position, socket.transform.position) < socket.Radius * .65f,
                    "real feet=" + P.transform.position.ToString("F3"));
                yield return RealSwap(anchors[target[i]], "relay_swap_" + ids[i]);
                if (!ok) yield break;
                float until = Time.realtimeSinceStartup + 2;
                while (!socket.Connected && Time.realtimeSinceStartup < until) yield return null;
                bool connected = socket.Connected && G.MissionObjectiveComplete(ids[i]);
                Add(Key + "_socket_latched_" + ids[i], connected);
                if (!connected) { Fail("relay_" + ids[i], "Actual anchor placement did not connect socket."); yield break; }
                yield return new WaitForSecondsRealtime(.15f);
            }
            var maintenance = G.expeditionMap.nodes.Find(node => node && node.GetComponent<AnchorMaintenanceReset>());
            int completed = G.mission.CompletedObjectives, credits = G.expeditionStore.Profile.credits;
            yield return ReachNode(maintenance, "maintenance_approach");
            if (!ok) yield break;
            Release(); yield return AimAt(maintenance.Point); yield return new WaitForSecondsRealtime(.12f);
            Vector3 playerBefore = P.transform.position;
            bool reset = G.UseExpeditionNode(maintenance);
            yield return null;
            bool positions = true;
            for (int i = 0; i < anchors.Length; i++) positions &= Vector3.Distance(anchors[i].transform.position, origins[i]) < .04f;
            bool preserved = G.mission.CompletedObjectives == completed && G.expeditionStore.Profile.credits == credits
                && Vector3.Distance(P.transform.position, playerBefore) < .1f;
            Add(Key + "_maintenance_actual_interaction", reset && positions && preserved,
                "reset=" + reset + ";anchorsAtOrigins=" + positions + ";progress/credits/playerPreserved=" + preserved);
            if (!reset || !positions || !preserved) Fail("maintenance", maintenance.GetComponent<AnchorMaintenanceReset>().LastFailure);
        }
        IEnumerator ReachRegion(string id, string step)
        {
            var layout = G.expeditionMap.root.GetComponent<MissionLayout>();
            if (!layout.TryRegion(id, out var bounds)) { Fail(step, "Missing region " + id); yield break; }
            var goals = new List<Vector3>(); Vector3 p = P.transform.position;
            float x = Mathf.Clamp(p.x, bounds.min.x + 1, bounds.max.x - 1);
            float z = Mathf.Clamp(p.z, bounds.min.z + 1, bounds.max.z - 1);
            foreach (var sample in new[] { new Vector3(x, p.y, z), new Vector3(bounds.center.x, p.y, bounds.center.z) })
                if (NavMesh.SamplePosition(sample, out var nav, 1, NavMesh.AllAreas) && bounds.Contains(nav.position + Vector3.up * .2f) && StandingRoom(nav.position))
                    goals.Add(nav.position);
            yield return Reach(goals, step);
            if (ok)
            {
                yield return null;
                Add(Key + "_" + step + "_arrival_trigger", G.mission.RegionVisited(id), P.transform.position.ToString("F3"));
                if (!G.mission.RegionVisited(id)) Fail(step, "Body reached region but runtime did not latch arrival.");
            }
        }
        IEnumerator DestroyByMelee(ExpeditionNode node)
        {
            var target = node.GetComponent<DamageableObjective>(); string step = "melee_" + target.Id;
            foreach (int weapon in P.EquippedWeapons) { P.ammo[weapon] = 0; P.reserve[weapon] = 0; }
            yield return ReachNode(node, step + "_approach");
            if (!ok) yield break;
            Release(); yield return AimAt(target.AimPoint); yield return new WaitForSecondsRealtime(.12f);
            float initial = target.Health; int hits = P.combatTools.MeleeHits;
            float until = Time.realtimeSinceStartup + 12;
            while (ok && !target.Destroyed && Time.realtimeSinceStartup < until)
            {
                // A pickup cannot silently turn this into a firearm fallback.
                foreach (int weapon in P.EquippedWeapons) { P.ammo[weapon] = 0; P.reserve[weapon] = 0; }
                yield return AimAt(target.AimPoint);
                InputSystem.QueueStateEvent(keys, new KeyboardState(UnityEngine.InputSystem.Key.V)); yield return null;
                Release(); yield return new WaitForSecondsRealtime(.82f);
            }
            bool done = target.Destroyed && P.combatTools.MeleeHits > hits && G.MissionObjectiveComplete(target.Id);
            Add(Key + "_" + step + "_actual_input", done, "initial HP=" + initial + ";remaining=" + target.Health
                + ";actual melee hits=" + (P.combatTools.MeleeHits - hits) + ";no ammo");
            if (!done) Fail(step, "Actual V melee did not destroy reachable task prop.");
            yield return new WaitForSecondsRealtime(.15f);
        }
        IEnumerator VisitNode(ExpeditionNode node, string step)
        {
            yield return ReachNode(node, step);
            if (!ok) yield break;
            Release(); yield return AimAt(node.Point); yield return new WaitForSecondsRealtime(.12f);
            if (!ok) yield break;
            bool found = G.FindExpeditionNode() == node;
            bool used = found && G.UseExpeditionNode(node);
            Add(Key + "_" + step + "_actual_interaction", found && used, "real feet=" + P.transform.position.ToString("F3") + ";node=" + node.label);
            if (!found || !used) Fail(step, "Physically reached endpoint but actual interaction rejected.");
            SaveProgress();
            yield return new WaitForSecondsRealtime(.15f);
        }
        IEnumerator ReachNode(ExpeditionNode node, string step)
        {
            if (!node) { Fail(step, "Missing scene node."); yield break; }
            yield return Reach(InteractionPoints(node), step);
        }
        List<Vector3> InteractionPoints(ExpeditionNode node)
        {
            var result = new List<Vector3>();
            foreach (float range in new[] { 1.65f, 1.95f })
                for (int i = 0; i < 24; i++)
                {
                    float angle = i * Mathf.PI / 12;
                    Vector3 sample = node.transform.position + new Vector3(Mathf.Sin(angle) * range, 0, Mathf.Cos(angle) * range);
                    if (!NavMesh.SamplePosition(sample, out var nav, .35f, NavMesh.AllAreas) || Mathf.Abs(nav.position.y - node.transform.position.y) > .3f) continue;
                    Vector3 feet = nav.position;
                    if (!StandingRoom(feet)) continue;
                    if (Physics.Linecast(feet + Vector3.up * EyeHeight, node.Point, out var sight, Layers.WorldMask, QueryTriggerInteraction.Ignore)
                        && sight.collider.GetComponentInParent<ExpeditionNode>() != node) continue;
                    result.Add(feet);
                }
            return result;
        }
        IEnumerator WalkCheckpoints(int level)
        {
            for (int i = 0; i < G.expeditionMap.walkingCheckpoints.Count && ok; i++)
            {
                Vector3 point = G.expeditionMap.walkingCheckpoints[i];
                if (level == 7)
                {
                    var layout = G.expeditionMap.root.GetComponent<MissionLayout>();
                    if (layout.TryRegion("c", out var third) && third.Contains(point + Vector3.up * .2f))
                    {
                        current.checkpointsSkipped++;
                        Add(Key + "_checkpoint_" + i + "_scope", true, "未采样：" + point + " 位于第三仓，保持任意两匣/未进第三仓夹具条件。");
                        continue;
                    }
                }
                yield return ReachPoint(point, "checkpoint_" + i, .25f);
                if (ok)
                {
                    current.checkpointsWalked++;
                    Add(Key + "_checkpoint_" + i + "_body_reached", true, "authored=" + point.ToString("F3") + ";real=" + P.transform.position.ToString("F3"));
                }
            }
        }

        IEnumerator ReachPoint(Vector3 point, string step, float tolerance)
        {
            var goals = new List<Vector3>();
            if (NavMesh.SamplePosition(point, out var nav, .35f, NavMesh.AllAreas) && Mathf.Abs(nav.position.y - point.y) < .3f && StandingRoom(nav.position))
                goals.Add(nav.position);
            yield return Reach(goals, step, tolerance);
        }
        NavMeshPath FindPath(Vector3 start, Vector3 goal)
        {
            if (!NavMesh.SamplePosition(start, out var from, .6f, NavMesh.AllAreas) || Mathf.Abs(from.position.y - start.y) > .55f) return null;
            if (!NavMesh.SamplePosition(goal, out var to, .35f, NavMesh.AllAreas) || Mathf.Abs(to.position.y - goal.y) > .35f) return null;
            var path = new NavMeshPath();
            return NavMesh.CalculatePath(from.position, to.position, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete ? path : null;
        }
        static float PathLength(NavMeshPath path)
        { float distance = 0; for (int i = 1; i < path.corners.Length; i++) distance += Vector3.Distance(path.corners[i - 1], path.corners[i]); return distance; }
        IEnumerator Reach(List<Vector3> goals, string step, float tolerance = .24f)
        {
            Step(step);
            if (!ok) yield break;
            if (goals.Count == 0) { Fail(step, "No standing/visible interaction approach at the authored floor."); yield break; }
            NavMeshPath best = null; float length = float.PositiveInfinity;
            float until = Time.realtimeSinceStartup + 2.5f;
            do
            {
                foreach (var goal in goals)
                {
                    var path = FindPath(P.transform.position, goal);
                    if (path == null) continue;
                    float candidate = PathLength(path);
                    if (candidate < length) { length = candidate; best = path; }
                }
                if (best != null) break;
                Release(); yield return null;
            } while (Time.realtimeSinceStartup < until);
            if (best == null) { Fail(step, "No complete walking path; no Q/teleport fallback hides missing permanent routes."); yield break; }
            current.lastCorners = string.Join(" -> ", Array.ConvertAll(best.corners, corner => corner.ToString("F3")));
            Add(Key + "_" + step + "_nav_path", true, "length=" + length.ToString("F2") + ";corners=" + current.lastCorners);
            yield return Walk(best, step, tolerance);
        }
        IEnumerator Walk(NavMeshPath path, string step, float tolerance)
        {
            var route = new List<Vector3>(path.corners);
            if (route.Count == 0) { Fail(step, "Complete NavMesh path contained no corners."); yield break; }
            float routeUntil = Time.realtimeSinceStartup + Mathf.Min(100, 12 + PathLength(path) / 1.25f);
            int detours = 0;
            for (int cornerIndex = 0; cornerIndex < route.Count && ok; cornerIndex++)
            {
                Vector3 corner = route[cornerIndex];
                float progressAt = Time.realtimeSinceStartup;
                Vector3 progressPoint = P.transform.position, previous = P.transform.position;
                while (FlatDistance(P.transform.position, corner) > tolerance || Mathf.Abs(P.transform.position.y - corner.y) > .42f)
                {
                    if (!G.IsPlaying || P.health <= 0) { Fail(step, "Game stopped/death during physical walking."); yield break; }
                    if (Time.realtimeSinceStartup > routeUntil || Time.realtimeSinceStartup - chapterStart > 230)
                    { Fail(step, "Physical route timeout at corner=" + corner + ";actual=" + P.transform.position); yield break; }
                    Vector3 delta = corner - P.transform.position;
                    QueueLook(new Vector3(delta.x, 0, delta.z));
                    InputSystem.QueueStateEvent(keys, new KeyboardState(UnityEngine.InputSystem.Key.W)); yield return null;
                    Vector3 now = P.transform.position;
                    current.metersWalked += Vector3.Distance(previous, now); previous = now;
                    if (Time.realtimeSinceStartup - progressAt < 1.25f) continue;
                    if (Vector3.Distance(now, progressPoint) < .1f)
                    {
                        if (detours < 4 && TryActorDetour(corner, out var detour))
                        {
                            detours++; Release();
                            route.InsertRange(cornerIndex, detour); corner = route[cornerIndex];
                            Add(Key + "_" + step + "_actor_detour", true, "actual=" + now + ";newCorner=" + corner);
                        }
                        else { Fail(step, "Controller stuck despite NavMesh path;corner=" + corner + ";actual=" + now); yield break; }
                    }
                    progressAt = Time.realtimeSinceStartup; progressPoint = now;
                }
                Release(); yield return new WaitForSecondsRealtime(.1f);
            }
            Add(Key + "_" + step + "_physical_walk", ok, "real endpoint=" + P.transform.position.ToString("F3") + ";cumulative=" + current.metersWalked.ToString("F2"));
        }
        bool TryActorDetour(Vector3 corner, out List<Vector3> detour)
        {
            detour = null;
            Vector3 forward = Vector3.ProjectOnPlane(corner - P.transform.position, Vector3.up).normalized;
            bool blockedByActor = false;
            // At controller contact the sweep sphere may already overlap the
            // obstacle and SphereCast can omit it; inspect the forward contact.
            foreach (var shape in Physics.OverlapSphere(P.transform.position + Vector3.up * .9f + forward * .4f,
                .4f, 1 << Layers.Actor, QueryTriggerInteraction.Ignore))
                if (shape.transform != P.transform && !shape.transform.IsChildOf(P.transform)) { blockedByActor = true; break; }
            foreach (var hit in Physics.SphereCastAll(P.transform.position + Vector3.up * .9f, .34f, forward, 1.25f, 1 << Layers.Actor, QueryTriggerInteraction.Ignore))
                if (hit.collider.transform != P.transform && !hit.collider.transform.IsChildOf(P.transform)) { blockedByActor = true; break; }
            if (!blockedByActor) return false; // Never hide faulty stair/wall geometry.
            Vector3 side = Vector3.Cross(Vector3.up, forward);
            foreach (float width in new[] { 1.2f, 1.8f })
                foreach (int sign in new[] { -1, 1 })
                {
                    Vector3 candidate = P.transform.position + side * width * sign + forward * .5f;
                    if (!NavMesh.SamplePosition(candidate, out var nav, .4f, NavMesh.AllAreas) || Mathf.Abs(nav.position.y - candidate.y) > .35f || !StandingRoom(nav.position)) continue;
                    var path = FindPath(P.transform.position, nav.position);
                    if (path == null || PathLength(path) > 4) continue;
                    var points = new List<Vector3>();
                    for (int i = 1; i < path.corners.Length; i++) points.Add(path.corners[i]);
                    if (points.Count == 0) continue;
                    detour = points; return true;
                }
            return false;
        }
        bool StandingRoom(Vector3 feet)
        {
            if (!Physics.Raycast(feet + Vector3.up * .18f, Vector3.down, .5f, Layers.WorldMask, QueryTriggerInteraction.Ignore)) return false;
            foreach (var shape in Physics.OverlapCapsule(feet + Vector3.up * .38f, feet + Vector3.up * 1.47f, .285f, Layers.CombatMask, QueryTriggerInteraction.Ignore))
            {
                if (shape.transform == P.transform || shape.transform.IsChildOf(P.transform)) continue;
                return false;
            }
            return true;
        }
        static float FlatDistance(Vector3 a, Vector3 b) => new Vector2(a.x - b.x, a.z - b.z).magnitude;
        void QueueLook(Vector3 direction)
        {
            if (direction.sqrMagnitude < .000001f) return;
            direction.Normalize(); Vector3 currentLook = P.Eye.forward;
            float yaw = Mathf.Atan2(currentLook.x, currentLook.z) * Mathf.Rad2Deg;
            float pitch = -Mathf.Asin(Mathf.Clamp(currentLook.y, -1, 1)) * Mathf.Rad2Deg;
            float targetYaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
            float targetPitch = -Mathf.Asin(Mathf.Clamp(direction.y, -1, 1)) * Mathf.Rad2Deg;
            float scale = G.sensitivity * .04f;
            var delta = new Vector2(Mathf.DeltaAngle(yaw, targetYaw) / scale,
                (targetPitch - pitch) / (scale * G.options.verticalSensitivity * (G.invertY ? 1 : -1)));
            InputSystem.QueueStateEvent(mouse, new MouseState { delta = delta });
        }
        IEnumerator AimAt(Vector3 point)
        {
            Release();
            float until = Time.realtimeSinceStartup + 1.5f;
            do
            {
                QueueLook(point - P.Eye.position); yield return null;
                if (Vector3.Angle(P.Eye.forward, point - P.Eye.position) < .6f) { Release(); yield break; }
            } while (Time.realtimeSinceStartup < until);
            Fail("aim", "Virtual mouse could not align view;actualForward=" + P.Eye.forward + ";target=" + point);
        }
        void Release()
        {
            if (keys != null && keys.added) InputSystem.QueueStateEvent(keys, new KeyboardState());
            if (mouse != null && mouse.added) InputSystem.QueueStateEvent(mouse, new MouseState());
        }
        void RestoreInput()
        {
            if (!inputInstalled) return;
            Release(); inputInstalled = false;
            if (keys != null && keys.added) InputSystem.RemoveDevice(keys);
            if (mouse != null && mouse.added) InputSystem.RemoveDevice(mouse);
            if (G)
            {
                G.qaKeyboard = previousKeys; G.qaMouse = previousMouse;
                G.qaInputEnabled = previousInput; G.qaSuppressAI = previousSuppression;
                if (previousOptions != null) { G.options = previousOptions; G.SyncLegacyBindings(); G.ApplySettings(false); }
                if (previousStore != null) G.expeditionStore = previousStore;
            }
        }
        void SaveProgress()
        { if (output != null) File.WriteAllText(Path.Combine(output, "route-v06-report.json"), JsonUtility.ToJson(report, true)); }
        void Finish()
        {
            if (finished) return;
            finished = true; routing = false; Release();
            if (current != null && !current.passed && current.seconds == 0) current.seconds = Time.realtimeSinceStartup - chapterStart;
            report.passed = report.chapters.Count == levels.Count && levels.Count > 0
                && report.chapters.TrueForAll(chapter => chapter.passed) && report.checks.TrueForAll(check => check.passed);
            report.allTwelveCovered = report.passed && levels.Count == 12;
            report.status = report.passed ? "completed" : "failed";
            SaveProgress(); RestoreInput();
            Debug.Log("ROUTE_V06_QA_RESULT=" + report.passed + ";allTwelve=" + report.allTwelveCovered + ";chapters=" + report.chapters.Count);
            Application.Quit(report.passed ? 0 : 1);
        }
    }
}

