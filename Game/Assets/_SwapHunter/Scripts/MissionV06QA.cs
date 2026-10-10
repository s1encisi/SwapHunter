using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.AI;

namespace SwapHunter
{
    // Native integration fixture. Objective injection and controlled positioning
    // isolate orchestration rules; they are not evidence of a human-playable route.
    public sealed class MissionV06QA : MonoBehaviour
    {
        [Serializable] public sealed class Check { public string name, detail; public bool passed; }
        [Serializable] public sealed class Report
        {
            public string version;
            public string scope = "Native mission/deployment integration; controlled objective injection and positioning. Not human traversal, balance or performance evidence.";
            public bool passed;
            public List<Check> checks = new List<Check>();
        }
        readonly Report report = new Report();
        DemoGame G => DemoGame.I;
        PlayerMotor P => G.player;
        string output;
        bool finished;
        float started;
        void Awake() { report.version = Application.version;  started = Time.realtimeSinceStartup; Application.logMessageReceived += OnLog; }
        void OnDestroy() { Application.logMessageReceived -= OnLog; }
        void OnLog(string message, string stack, LogType type)
        { if (!finished && (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)) Add("runtime_error", false, message); }
        void Update() { if (!finished && Time.realtimeSinceStartup - started > 300) { Add("watchdog", false, "300s"); Finish(); } }
        void Add(string name, bool passed, string detail = "")
        {
            report.checks.Add(new Check { name = name, passed = passed, detail = detail });
            Debug.Log("MISSION_V06_QA " + name + " " + passed + " " + detail);
        }
        void Balance(string key)
        {
            var m = G.mission;
            Add(key, m.UntriggeredBudget + m.WarningBudget + m.DeployedBudget + m.CancelledBudget == m.Definition.TotalBudget,
                "untriggered=" + m.UntriggeredBudget + " warning=" + m.WarningBudget + " deployed=" + m.DeployedBudget + " cancelled=" + m.CancelledBudget);
        }
        MissionMember Member(string group, int index = 0)
        { foreach (var member in G.mission.Members) if (member.group.id == group && member.index == index) return member; return null; }
        void Visit(string id)
        {
            var layout = G.expeditionMap.root.GetComponent<MissionLayout>();
            if (!layout.TryRegion(id, out var bounds)) { Add("region_exists_" + id, false); return; }
            Vector3 before = P.transform.position;
            P.SetFeet(bounds.center); P.verticalSpeed = 0;
            G.mission.Tick(0);
            P.SetFeet(before); P.verticalSpeed = 0;
        }
        void Aim(Vector3 point)
        {
            Vector3 direction = (point - P.Eye.position).normalized;
            P.SetLook(Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg, -Mathf.Asin(direction.y) * Mathf.Rad2Deg);
        }
        void KillRequired()
        { foreach (var member in G.mission.Members) if (member.role.required && member.actor && member.actor.Alive) member.actor.Damage(100000); G.mission.Tick(0); }
        void KillEscorts()
        { foreach (var member in G.mission.Members) if (member.group.escort && member.actor && member.actor.Alive) member.actor.Damage(100000); G.mission.Tick(0); }

        IEnumerator Start()
        {
            var args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "-qaOutput");
            output = index >= 0 && index + 1 < args.Length ? args[index + 1] : RunStorage.Root;
            Directory.CreateDirectory(output);
            while (!Application.isBatchMode && !UnityEngine.Rendering.SplashScreen.isFinished) yield return null;
            yield return new WaitForSecondsRealtime(.2f);
            Add("isolated_storage", RunStorage.IsValidation, RunStorage.Root);
            if (!RunStorage.IsValidation) { Finish(); yield break; }
            bool valid = false;
            try { MissionCatalog.Validate(); valid = true; }
            catch (Exception error) { Add("catalog_exception", false, error.ToString()); }
            Add("catalog_resource_configuration_valid", valid);
            if (!valid) { Finish(); yield break; }
            ConfigurationChecks();
            DataFixture();
            G.qaSuppressAI = true;

            for (int level = 0; level < 12; level++)
            {
                string key = "chapter_" + (level + 1).ToString("00");
                if (G.expeditionActive) { G.FinishExpedition(false); G.OpenExpeditionBoard(); yield return null; }
                bool began = G.BeginExpedition(level);
                Add(key + "_begin", began, G.expeditionNotice);
                if (!began || G.mission == null) continue;
                var definition = G.mission.Definition;
                Add(key + "_stable_id", definition.level == level && ExpeditionCatalog.Levels[level].id == level);
                Add(key + "_no_early_exit", !G.FinishExpedition(true));
                Balance(key + "_budget_conserved_start");
                var layout = G.expeditionMap.root.GetComponent<MissionLayout>();
                foreach (var group in definition.groups)
                    for (int i = 0; i < group.roles.Length; i++)
                    {
                        var spawn = layout.Spawn(group.id, i);
                        bool ground = NavMesh.SamplePosition(spawn.position, out var nav, .65f, NavMesh.AllAreas)
                            && Mathf.Abs(nav.position.y - spawn.position.y) < .6f;
                        Add(key + "_spawn_nav_" + group.id + "_" + i, ground, spawn.position.ToString("F2"));
                    }
                float elapsed = G.mission.Elapsed;
                int warnings = G.PendingDeployments;
                G.SetState(RunState.Paused); G.mission.Tick(30);
                yield return new WaitForSecondsRealtime(.06f);
                Add(key + "_pause_freezes_clock", Mathf.Abs(G.mission.Elapsed - elapsed) < .0001f && G.PendingDeployments == warnings);
                G.SetState(RunState.Playing);

                if (level == 0)
                {
                    bool requested = false;
                    foreach (var anchor in G.expeditionMap.anchors)
                    {
                        P.cooldown = 0; Aim(anchor.AimPoint);
                        if (!P.ValidateSwap(anchor).IsValid) continue;
                        requested = P.RequestSwap(anchor); if (requested) break;
                    }
                    yield return new WaitForSeconds(.2f);
                    Add(key + "_real_calibration_swap", requested && G.totalSwaps == 1);
                    Visit("combat");
                    P.SetFeet(G.expeditionMap.spawn); P.verticalSpeed = 0;
                    yield return new WaitForSeconds(4.5f);
                    Add(key + "_combat_deployed_after_q_arrival", Member("combat").actor != null);
                    KillRequired();
                }
                else if (level == 1)
                {
                    yield return new WaitForSeconds(4.5f);
                    Add(key + "_first_guard_blocks", G.ExpeditionRequiredEnemies == 2 && !G.ExpeditionMainComplete);
                    if (Member("first").actor) Member("first").actor.Damage(100000);
                    Visit("second");
                    yield return new WaitForSeconds(4.5f);
                    Add(key + "_second_guard_deployed", Member("second").actor != null);
                    KillRequired();
                    Add(key + "_side_assault_can_survive", Member("second", 1).actor && Member("second", 1).actor.Alive && G.ExpeditionMainComplete);
                }
                else if (level == 4)
                {
                    var point = layout.Spawn("left", 0).position;
                    var blocker = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    blocker.name = "QA temporary tower occupancy"; blocker.layer = Layers.World;
                    blocker.transform.position = point + Vector3.up * 2; blocker.transform.localScale = new Vector3(9, 4, 9);
                    Physics.SyncTransforms();
                    yield return new WaitForSeconds(4.5f);
                    Add(key + "_blocked_tower_retains_budget", Member("left").state == EncounterBudgetState.Warning && !Member("left").actor);
                    blocker.SetActive(false); Destroy(blocker); Physics.SyncTransforms();
                    yield return new WaitForSeconds(5);
                    var sniper = Member("left").actor;
                    Add(key + "_tower_does_not_drop_to_ground", sniper && Mathf.Abs(sniper.transform.position.y - point.y) < .3f, sniper ? sniper.transform.position.ToString() : "not deployed");
                    G.CompleteMissionObjective("a");
                    Add(key + "_one_node_triggers_single_budget", G.mission.GroupTriggered("reinforce") && Member("reinforce").state == EncounterBudgetState.Warning);
                    G.CompleteMissionObjective("b");
                }
                else if (level == 5)
                {
                    yield return FirstGrenadeProbe(key);
                    G.CompleteMissionObjective("a"); G.CompleteMissionObjective("b");
                }
                else if (level == 7)
                {
                    G.CompleteMissionObjective("b"); G.CompleteMissionObjective("a");
                    Add(key + "_any_two_no_q_or_recall_gate", G.ExpeditionMainComplete && G.totalSwaps == 0 && !G.expeditionBonus);
                    Add(key + "_third_branch_budget_untriggered", Member("warehouse-c").state == EncounterBudgetState.Untriggered);
                    G.CompleteMissionObjective("c");
                    Add(key + "_third_crate_latches_bonus", G.expeditionBonus && !G.CompleteMissionObjective("c"));
                }
                else if (level == 8)
                {
                    G.CompleteMissionObjective("a");
                    Visit("bottom");
                    int budget = G.mission.WarningBudget;
                    Visit("bottom");
                    Add(key + "_teleport_arrival_latched", G.mission.GroupTriggered("bottom") && G.mission.WarningBudget == budget);
                    Add(key + "_skipped_middle_not_required", !G.mission.GroupTriggered("middle") && G.ExpeditionMainComplete);
                }
                else if (level == 9)
                {
                    yield return new WaitForSeconds(4.5f);
                    int live = G.ExpeditionOptionalEnemies;
                    G.mission.Tick(16);
                    Add(key + "_left_warning_started", Member("left").state == EncounterBudgetState.Warning);
                    G.CompleteMissionObjective("a");
                    Add(key + "_control_cancels_warning", Member("left").state == EncounterBudgetState.Cancelled && G.mission.CancelledBudget == 2);
                    G.CompleteMissionObjective("b"); G.CompleteMissionObjective("c");
                    Add(key + "_controls_cancel_untriggered", G.mission.CancelledBudget == 6 && G.PendingDeployments == 0);
                    Add(key + "_active_enemies_preserved", live == 3 && G.ExpeditionOptionalEnemies == live && G.ExpeditionMainComplete);
                }
                else if (level == 11)
                {
                    yield return new WaitForSeconds(4.5f);
                    var boss = Member("boss").actor;
                    Add(key + "_boss_deployed", boss && boss.boss);
                    if (boss)
                    {
                        yield return new WaitForSeconds(4);
                        Add(key + "_first_phase_two_escort_cap", G.mission.ActiveEscorts == 2);
                        boss.Damage(BossEncounter.ShieldMaximum);
                        G.mission.Tick(0);
                        Add(key + "_rifle_budget_waits_for_capacity", G.mission.GroupTriggered("phase-two") && Member("phase-two").state == EncounterBudgetState.Untriggered && G.mission.ActiveEscorts <= 2);
                        KillEscorts();
                        yield return new WaitForSeconds(4);
                        boss.Damage(BossEncounter.HealthMaximum * .55f); G.mission.Tick(0);
                        Add(key + "_half_health_stage_latched", G.mission.GroupTriggered("phase-three") && G.mission.ActiveEscorts <= 2);
                        boss.Damage(100000); G.mission.Tick(0);
                        Add(key + "_boss_death_cancels_reserved_escorts", Member("phase-three").state == EncounterBudgetState.Cancelled && G.PendingDeployments == 0);
                        Add(key + "_live_escorts_not_deleted", G.ExpeditionOptionalEnemies > 0);
                    }
                    G.CompleteMissionObjective("b"); G.CompleteMissionObjective("c"); G.CompleteMissionObjective("a");
                }
                else
                {
                    foreach (var objective in definition.objectives) G.CompleteMissionObjective(objective.id);
                    if (level == 3) Add(key + "_intercept_still_warns_after_main", G.ExpeditionMainComplete && Member("intercept").state == EncounterBudgetState.Warning);
                    if (level == 6) Add(key + "_island_order_and_latch", G.MissionObjectiveComplete("a") && G.MissionObjectiveComplete("b") && G.MissionObjectiveComplete("c") && !G.CompleteMissionObjective("a"));
                    if (level == 10) Add(key + "_fields_not_mandatory", !G.MissionObjectiveComplete("field-left") && !G.MissionObjectiveComplete("field-right") && G.ExpeditionMainComplete);
                }
                Balance(key + "_budget_conserved_before_exit");
                Add(key + "_mission_ready", G.ExpeditionMainComplete, G.ExpeditionObjective);
                if (level > 0) Add(key + "_zero_q_allowed", G.totalSwaps == 0);
                bool ended = G.FinishExpedition(true);
                Add(key + "_settlement", ended && G.expeditionReceipt != null && G.expeditionReceipt.success);
                if (ended)
                {
                    Add(key + "_exit_cancels_only_pending", G.PendingDeployments == 0 && G.mission.WarningBudget == 0 && G.mission.UntriggeredBudget == 0);
                    if (level == 7) Add(key + "_bonus_paid_on_extraction", G.expeditionReceipt.bonus && G.expeditionReceipt.reward == 70);
                }
                else G.FinishExpedition(false);
                G.OpenExpeditionBoard(); yield return null;
            }
            Finish();
        }

        void ConfigurationChecks()
        {
            Add("configuration_uses_resource_asset", MissionCatalog.Configuration && !ReferenceEquals(MissionCatalog.All, MissionCatalog.Defaults));
            var defaults = MissionCatalog.CreateDefaultDefinitions();
            int[] expected = { 2, 3, 3, 4, 6, 6, 5, 6, 8, 9, 7, 7 };
            bool baseline = true;
            for (int i = 0; i < 12; i++) baseline &= defaults[i].TotalBudget == expected[i];
            Add("default_catalog_matches_v06_design_budget", baseline);
            defaults[3].groups[0].roles = new[] { new EncounterRole(EnemyKind.Assault) };
            defaults[3].groups[1].warningSeconds = 5;
            defaults[3].groups[1].trigger = EncounterTrigger.Elapsed;
            defaults[3].groups[1].triggerSeconds = 12;
            defaults[3].requiredObjectives = 0;
            defaults[3].extraction = "QA changed exit label";
            Add("configuration_allows_rule_edits", MissionCatalog.ValidationErrors(defaults).Count == 0);
            Add("configuration_copy_does_not_mutate_defaults", MissionCatalog.Defaults[3].TotalBudget == 4
                && MissionCatalog.Defaults[3].groups[1].warningSeconds == 3.2f && MissionCatalog.Defaults[3].requiredObjectives == 1);
            var malformed = MissionCatalog.CreateDefaultDefinitions();
            malformed[0].level = 4;
            malformed[4].groups[0].warningSeconds = float.NaN;
            malformed[11].maximumEscorts = 3;
            Add("configuration_reports_errors_across_chapters", MissionCatalog.ValidationErrors(malformed).Count >= 3);
            malformed = MissionCatalog.CreateDefaultDefinitions();
            malformed[3].groups[1].objective = "missing";
            Add("configuration_rejects_unknown_objective", MissionCatalog.ValidationErrors(malformed).Count > 0);
            malformed = MissionCatalog.CreateDefaultDefinitions();
            malformed[1].groups[0].afterDefeated = "second";
            Add("configuration_rejects_dependency_cycle", MissionCatalog.ValidationErrors(malformed).Count > 0);
            malformed = MissionCatalog.CreateDefaultDefinitions();
            malformed[3].groups[0].id = "renamed";
            Add("configuration_rejects_unbound_stable_id", MissionCatalog.ValidationErrors(malformed).Count > 0);
        }

        IEnumerator FirstGrenadeProbe(string key)
        {
            yield return new WaitForSeconds(4.5f);
            var member = Member("first"); var actor = member != null ? member.actor : null;
            var cue = actor ? actor.GetComponent<EncounterGrenadeCue>() : null;
            Add(key + "_first_grenade_role_configured", actor && cue && member.role.firstGrenade,
                "member=" + (member == null ? "missing" : member.Key) + ";actor=" + (actor ? actor.transform.position.ToString() : "missing") + ";cue=" + (cue ? cue.State.ToString() : "missing"));
            if (!actor || !cue) { Add(key + "_real_first_grenade", false, "Missing real actor or cue component"); yield break; }
            bool placeFound = false; int groundRejected = 0, volumeRejected = 0, sightRejected = 0;
            for (int side = 0; side < 8; side++)
            {
                Vector3 point = actor.transform.position + new Vector3(Mathf.Cos(side * Mathf.PI / 4), 0, Mathf.Sin(side * Mathf.PI / 4)) * 9;
                if (!EnemyMotion.GroundAt(point, out var ground)) { groundRejected++; continue; }
                if (!EnemyMotion.ClearVolume(ground, .3f, 1.8f)) { volumeRejected++; continue; }
                if (Physics.Linecast(actor.AimPoint, ground + Vector3.up * 1.6f, Layers.WorldMask)) { sightRejected++; continue; }
                P.SetFeet(ground); P.verticalSpeed = 0; Aim(actor.AimPoint); Physics.SyncTransforms(); placeFound = true; break;
            }
            Vector3 actorStart = actor.transform.position;
            Add(key + "_first_grenade_visible_fixture", placeFound,
                "player=" + P.transform.position + ";actor=" + actorStart + ";groundRejected=" + groundRejected + ";volumeRejected=" + volumeRejected + ";sightRejected=" + sightRejected);
            if (!placeFound) { Add(key + "_real_first_grenade", false, "No valid native visible standing point; grenade mechanism was not exercised"); yield break; }
            float start = Time.time, until = start + 3; int blockedFrames = 0, rangeFrames = 0, silenceFrames = 0; float observedVisibleSeconds = 0;
            G.qaSuppressAI = false;
            while (Time.time < until && actor && actor.Alive && !cue.LastThrown)
            {
                float distance = Vector3.Distance(actor.transform.position, P.transform.position);
                bool blocked = Physics.Linecast(actor.AimPoint, P.Eye.position, Layers.WorldMask);
                if (blocked) blockedFrames++;
                if (distance < 5 || distance > 20) rangeFrames++;
                if (actor.Silenced) silenceFrames++;
                if (!blocked && distance >= 5 && distance <= 20 && !actor.Silenced && actor.stunLeft <= 0) observedVisibleSeconds += Time.deltaTime;
                yield return null;
            }
            G.qaSuppressAI = true;
            var grenade = cue.LastThrown;
            bool physical = grenade && grenade.GetComponent<Rigidbody>() && grenade.GetComponent<Collider>() && grenade.remaining > 0;
            Add(key + "_real_first_grenade", actor && placeFound && actor.grenadesThrown > 0 && cue.State == FirstGrenadeCueState.Thrown && physical,
                "grenades=" + actor.grenadesThrown + ";nativeGrenade=" + (grenade ? grenade.GetInstanceID().ToString() : "missing") + ";fuse=" + (grenade ? grenade.remaining.ToString("F3") : "n/a")
                + ";state=" + cue.State + ";preparation=" + cue.PreparationSeconds.ToString("F3") + ";elapsedGameSeconds=" + (Time.time - start).ToString("F3")
                + ";observedVisibleSeconds=" + observedVisibleSeconds.ToString("F3") + ";blockedFrames=" + blockedFrames + ";rangeFrames=" + rangeFrames + ";silenceFrames=" + silenceFrames
                + ";actorStart=" + actorStart + ";actorEnd=" + actor.transform.position + ";player=" + P.transform.position);
        }

        void DataFixture()
        {
            try
            {
                var store = new ExpeditionStore(Path.Combine(RunStorage.Root, "v06-mission-fixture"));
                for (int level = 0; level < 12; level++)
                {
                    string run = store.Begin(level);
                    store.Settle(run, true, false, 30 + level);
                }
                store.Equip(0, 4); store.SetAttachment(4, 3); store.SelectSpecialization(2); store.Upgrade(2); store.UpgradeKnife();
                var reloaded = new ExpeditionStore(Path.GetDirectoryName(store.SavePath));
                Add("growth_v4_compatible", reloaded.Profile.version == 4 && reloaded.Profile.highestUnlocked == 11
                    && reloaded.Profile.primary == 4 && reloaded.Profile.attachments[4] == 3 && reloaded.Profile.ranks[2] == 1 && reloaded.Profile.knifeRank == 1
                    && reloaded.Profile.receipts.Count == 12);
                string replay = reloaded.Begin(0);
                reloaded.Settle(replay, true, false, 15); int credits = reloaded.Profile.credits;
                reloaded.Settle(replay, true, true, 10);
                Add("settlement_idempotent_after_content_change", reloaded.Profile.credits == credits);
                G.expeditionStore = reloaded;
            }
            catch (Exception error) { Add("data_fixture", false, error.ToString()); }
        }
        void Finish()
        {
            if (finished) return;
            finished = true; Time.timeScale = 1;
            report.passed = report.checks.Count > 0 && report.checks.TrueForAll(check => check.passed);
            File.WriteAllText(Path.Combine(output ?? RunStorage.Root, "mission-v06-report.json"), JsonUtility.ToJson(report, true));
            Debug.Log("MISSION_V06_QA_RESULT=" + report.passed + ";checks=" + report.checks.Count);
            Application.Quit(report.passed ? 0 : 1);
        }
    }
}

