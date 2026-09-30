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
    // Native-player integration checks. Scripted actions do not measure fun or first-play duration.
    public sealed class CampaignQA : MonoBehaviour
    {
        [Serializable] public sealed class Check { public string name, detail; public bool passed; }
        [Serializable] public sealed class Report { public string version = Application.version, kind = "Scripted contract integration, not human playtesting"; public bool passed; public List<Check> checks = new List<Check>(); }
        Report report = new Report(); string output; float started; bool finished;
        DemoGame G => DemoGame.I;
        PlayerMotor P => G.player;
        void Awake() { started = Time.realtimeSinceStartup; Application.logMessageReceived += OnLog; }
        void Update() { if (!finished && Time.realtimeSinceStartup - started > 180) { Assert("watchdog", false, "180 second timeout"); Finish(); } }
        void OnLog(string message, string trace, LogType kind) { if (kind == LogType.Error || kind == LogType.Exception || kind == LogType.Assert) Assert("runtime", false, message); }
        void Assert(string name, bool ok, string detail) { report.checks.Add(new Check { name = name, passed = ok, detail = detail }); Debug.Log("V03 " + name + " " + ok + " " + detail); }
        IEnumerator Start()
        {
            var args = Environment.GetCommandLineArgs(); int index = Array.IndexOf(args, "-qaOutput");
            output = index >= 0 && index + 1 < args.Length ? args[index + 1] : RunStorage.Root; Directory.CreateDirectory(output);
            while (!UnityEngine.Rendering.SplashScreen.isFinished) yield return null;
            G.qaSuppressAI = true; yield return new WaitForSecondsRealtime(.5f);
            Assert("catalog_boot", G.catalog != null && G.campaignStore != null, G.campaignNotice);
            if (G.campaignStore == null) { Finish(); yield break; }
            DomainChecks();
            foreach (var check in CampaignDataQA.Run(G.catalog, output)) Assert(check.name, check.passed, check.detail);
            G.BeginContract(G.catalog.contracts[0].id); yield return WaitDeployments(); yield return new WaitForSeconds(.15f);
            using (var locked = new FileStream(G.campaignStore.SavePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                P.Hurt(10000, P.transform.position); G.SetState(RunState.Playing);
                Assert("death_save_failure_freezes_play", G.settlementPending && !G.IsPlaying && P.health == 0, "Cannot resume zero-health combat while persistence fails");
            }
            G.FinishContract(true);
            Assert("retry_preserves_failed_outcome", !G.settlementPending && G.lastSettlement != null && !G.lastSettlement.success, "Retry cannot change failed outcome to success");
            G.OpenCampaignBoard(); yield return Capture("v03-board-new-profile");
            foreach (var definition in G.catalog.contracts)
            {
                int before = G.campaignStore.Profile.credits;
                Assert("begin_" + definition.id, G.BeginContract(definition.id), G.campaignNotice);
                yield return new WaitForSeconds(.3f);
                Assert("arena_" + definition.id, G.stage == definition.stage && G.campaignActive && !G.campaignMain, G.stage.ToString());
                Assert("closed_boundaries_" + definition.id, G.gates.TrueForAll(gate => !gate.open), "Contract isolated from classic progression");
                Route("bonus_route_" + definition.id, P.transform.position, G.bonusStation.Approach);
                Route("extraction_route_" + definition.id, P.transform.position, G.extractionStation.Approach);
                yield return StationPhysics(G.extractionStation, "extract_" + definition.id);
                yield return StationPhysics(G.bonusStation, "bonus_" + definition.id);
                yield return Station(G.extractionStation);
                Assert("reject_early_extract_" + definition.id, G.campaignActive && G.lastSettlement == null, "Main objective required");
                yield return Station(G.bonusStation);
                Assert("bonus_reinforcements_" + definition.id, G.campaignBonus && G.AliveEnemies >= 5, "Optional data starts two additional enemies");
                int count = G.AliveEnemies;
                Assert("bonus_once_" + definition.id, !G.UseCampaignStation(G.bonusStation) && G.AliveEnemies == count, "No duplicate reinforcements or bonus");
                if (G.stage == 1 || G.stage == 3)
                {
                    float limit = Time.time + 25;
                    while (!G.combatComplete && Time.time < limit)
                    { foreach (var e in G.enemies.ToArray()) if (e && e.Alive) e.Damage(10000); yield return new WaitForSeconds(.15f); }
                }
                foreach (var terminal in G.terminals.ToArray()) if (terminal.stage == G.stage)
                {
                    P.SetFeet(terminal.transform.position + new Vector3(0, .03f, -2)); Aim(terminal.Point);
                    yield return new WaitForSeconds(.15f); G.Interact();
                }
                Assert("main_" + definition.id, G.campaignMain && G.campaignActive && G.state == RunState.Playing, "Main completion does not auto-settle");
                yield return Capture("v03-contract-" + definition.id);
                // Environment route check: remove combat actors via normal death handling, then
                // traverse with the real CharacterController/gravity, without teleporting the route.
                foreach (var e in G.enemies.ToArray()) if (e && e.Alive) e.Damage(10000);
                // Verify the previously failing unassisted shortest route. No authored
                // stair waypoints, teleport, collision changes or increased step height.
                yield return WalkRoute("walk_to_bonus_" + definition.id, G.bonusStation.Approach);
                yield return WalkRoute("walk_to_extraction_" + definition.id, G.extractionStation.Approach);
                Aim(G.extractionStation.Point); yield return null; G.Interact(); yield return new WaitForSecondsRealtime(.1f);
                Assert("settlement_" + definition.id, !G.campaignActive && G.lastSettlement != null && G.lastSettlement.success && G.campaignStore.Profile.credits == before + definition.reward + definition.bonusReward, "Expected " + (definition.reward + definition.bonusReward) + " earned");
                yield return Capture("v03-settlement-" + definition.id);
                G.OpenCampaignBoard();
            }
            string error;
            // Domain-level funding of this isolated test profile lets every unlock be exercised.
            // This shortcut is not reported as a played or timed mission.
            while (G.campaignStore.Profile.credits < 700)
            {
                string funding = G.campaignStore.Begin(G.catalog.contracts[0].id);
                G.campaignStore.MarkMainComplete(funding); G.campaignStore.Settle(funding, true);
            }
            foreach (var module in G.catalog.modules)
            {
                if (!G.campaignStore.Profile.unlocked.Contains(module.id)) G.campaignStore.Unlock(module.id, out error);
            }
            // Actual runtime benefits and costs, each using a loadout snapshot created by Begin.
            foreach (var module in G.catalog.modules)
            {
                G.campaignStore.Equip("", 0, out error); G.campaignStore.Equip("", 1, out error);
                bool equipped = G.campaignStore.Equip(module.id, 0, out error);
                Assert("equip_runtime_" + module.id, equipped, error ?? "");
                if (!equipped) continue;
                G.BeginContract(G.catalog.contracts[0].id); yield return WaitDeployments(); yield return new WaitForSeconds(.15f);
                Assert("failed_swap_no_module_window_" + module.id, !P.RequestSwap(null) && !G.GuardReady && !G.BreachReady, "Invalid swap does not activate a module");
                SwapTarget swapTarget;
                if (module.id == "anchor")
                {
                    swapTarget = G.anchors.Find(a => a.transform.position.z > 40 && a.transform.position.z < 70);
                    P.SetFeet(new Vector3(-10, .03f, 56)); P.verticalSpeed = 0; yield return new WaitForSeconds(.15f);
                }
                else swapTarget = G.enemies.Find(e => e.kind == EnemyKind.Sniper);
                Aim(swapTarget.AimPoint); int swaps = G.totalSwaps;
                Assert("module_swap_request_" + module.id, P.RequestSwap(swapTarget), P.ValidateSwap(swapTarget));
                yield return new WaitForSeconds(.15f);
                Assert("module_real_swap_" + module.id, G.totalSwaps == swaps + 1, "Full validated swap transaction activated the module");
                if (module.id == "guard") Assert("guard_effect_and_cost", G.ModuleDamageTaken < .7f && G.ModuleMovement < 1, "Temporary protection, permanent movement cost");
                if (module.id == "breach") { Assert("breach_range", G.ModuleShotDamage(4) > 1 && G.ModuleShotDamage(10) == 1, "Short-range only"); G.ConsumeModuleShot(); Assert("breach_consumed", G.ModuleShotDamage(4) == 1, "One shot window"); }
                if (module.id == "anchor") Assert("anchor_speed", G.ModuleMovement > 1.2f, "Anchor swap speed window");
                if (module.id == "scavenger") { P.reserve[0] = 0; P.Supply(0, 20, 0); Assert("scavenger_cost", P.reserve[0] == 30 && G.ModuleReload > 1, "Extra ammo, slower reload"); }
                if (module.id == "stabilizer")
                {
                    Assert("stabilizer_hip_neutral", G.ModuleSpread == 1 && G.ModuleAimMovement == 1, "ADS-only bonus and cost");
                    bool savedToggle = G.options.toggleAim; G.options.toggleAim = false;
                    var mouse = InputSystem.AddDevice<Mouse>(); G.qaMouse = mouse; G.qaInputEnabled = true;
                    InputSystem.QueueStateEvent(mouse, new MouseState { buttons = 2 }); yield return new WaitForSeconds(.3f);
                    Assert("stabilizer_ads_benefit_and_cost", P.IsAiming && G.ModuleSpread == .6f && G.ModuleAimMovement == .8f, "Real aim input activates both modifiers");
                    InputSystem.QueueStateEvent(mouse, new MouseState()); yield return null;
                    G.qaInputEnabled = false; G.qaMouse = null; InputSystem.RemoveDevice(mouse); G.options.toggleAim = savedToggle;
                }
                if (module.id == "overdrive") Assert("overdrive_cost", G.CampaignCooldown < G.config.swapCooldown && G.ModuleDamageTaken > 1, "Lower cooldown, higher damage taken");
                P.Hurt(10000, P.transform.position);
                Assert("death_ends_contract_" + module.id, !G.campaignActive && G.state == RunState.Dead && G.lastSettlement.totalReward == 0, "No pre-main reward on death");
                G.OpenCampaignBoard();
            }
            yield return Capture("v03-board-unlocks");
            G.StartRun(true); yield return new WaitForSeconds(.2f);
            Assert("practice_neutral", !G.campaignActive && !G.HasModule("overdrive") && G.ModuleMovement == 1, "Practice does not inherit campaign modifiers");
            G.SetState(RunState.Menu); Finish();
        }
        IEnumerator WaitDeployments()
        {
            float end=Time.time+10;while(G.PendingDeployments>0&&Time.time<end)yield return null;
            Assert("warned_deployments_complete",G.PendingDeployments==0,"pending="+G.PendingDeployments);
        }
        void DomainChecks()
        {
            try
            {
                string dir = Path.Combine(output, "domain-" + Guid.NewGuid().ToString("N")); var store = new CampaignStore(dir, G.catalog); var contract = G.catalog.contracts[0]; string error;
                Assert("reject_locked", !store.Equip("overdrive", 0, out error), error);
                string run = store.Begin(contract.id); store.MarkMainComplete(run); store.MarkBonusComplete(run);
                Assert("lock_during_run", !store.Equip("", 0, out error), error);
                var receipt = store.Settle(run, true); int credits = store.Profile.credits;
                Assert("reward_breakdown", receipt.baseReward == contract.reward && receipt.bonusReward == contract.bonusReward, JsonUtility.ToJson(receipt));
                store.Settle(run, true); Assert("idempotent", store.Profile.credits == credits, "Duplicate settlement has no effect");
                run = store.Begin(contract.id); store.MarkMainComplete(run); store.MarkBonusComplete(run);
                store = new CampaignStore(dir, G.catalog); receipt = store.RecoverInterrupted();
                Assert("interrupted_compensation", receipt != null && !receipt.success && receipt.totalReward == contract.failureReward && receipt.bonusReward == 0, receipt == null ? "null" : JsonUtility.ToJson(receipt));
                credits = store.Profile.credits; Assert("recover_once", store.RecoverInterrupted() == null && store.Profile.credits == credits, "Repeated recovery neutral");
                Assert("duplicate_equipment", !store.Equip(store.Profile.equipped[0], 1, out error), error);
                run = store.Begin(contract.id); bool rejected = false;
                try { store.Settle(run, true); } catch (InvalidOperationException) { rejected = true; }
                Assert("no_main_no_success", rejected && store.Profile.activeRun != null, "Cannot forge successful settlement"); store.Settle(run, false);
            }
            catch (Exception e) { Assert("domain_exception", false, e.ToString()); }
        }
        IEnumerator Station(CampaignStation station)
        { P.SetFeet(station.Approach + Vector3.up * .03f); Aim(station.Point); yield return new WaitForSeconds(.15f); G.Interact(); yield return new WaitForSecondsRealtime(.1f); }
        IEnumerator StationPhysics(CampaignStation station, string id)
        {
            P.SetFeet(station.Approach + Vector3.up * .03f); Aim(station.Point); yield return new WaitForSeconds(.2f);
            bool hit = Physics.Raycast(station.Point + Vector3.back * 2, Vector3.forward, out var contact, 4, Layers.WorldMask);
            Assert("station_blocks_shots_" + id, hit && contact.collider.GetComponent<CampaignStation>() == station, "Ray hits physical console instead of passing through its model");
            Assert("station_self_collider_interaction_" + id, G.FindCampaignStation() == station, "Own collision proxy does not occlude interaction");
            for (int i = 0; i < 30; i++) { P.controller.Move(Vector3.forward * .1f); yield return null; }
            float gap = station.transform.position.z - P.transform.position.z;
            Assert("station_blocks_player_" + id, gap > .55f && gap < 1, "Real controller stopped at console; gap=" + gap);
            P.SetFeet(station.Approach + Vector3.up * .03f); Aim(station.Point); yield return null;
            var obstruction = Shapes.Box("QA station occlusion", G.world, station.Point + Vector3.back, new Vector3(2, 3, .1f), 8, true, Layers.World);
            Physics.SyncTransforms(); Assert("station_wall_rejects_" + id, G.FindCampaignStation() != station, "Unrelated wall still blocks interaction");
            obstruction.SetActive(false); Destroy(obstruction); yield return null;
            var obstacle = station.GetComponent<NavMeshObstacle>(); obstacle.enabled = false; yield return null; yield return null;
            bool baseline = NavMesh.SamplePosition(station.transform.position + Vector3.up * .05f, out _, .1f, NavMesh.AllAreas);
            Assert("station_nav_baseline_" + id, baseline, "Position is navigable without the obstacle");
            obstacle.enabled = true; yield return null; yield return null;
            bool onStation = NavMesh.SamplePosition(station.transform.position + Vector3.up * .05f, out _, .1f, NavMesh.AllAreas);
            Assert("station_nav_avoids_body_" + id, !onStation, "Station footprint is carved from navigation");
            obstacle.enabled = false; yield return null; yield return null;
            Assert("station_nav_restores_" + id, NavMesh.SamplePosition(station.transform.position + Vector3.up * .05f, out _, .1f, NavMesh.AllAreas), "Removing obstacle restores navigation");
            obstacle.enabled = true; yield return null; yield return null;
        }
        IEnumerator WalkRoute(string id, Vector3 goal)
        {
            var path = new NavMeshPath();
            bool found = NavMesh.SamplePosition(P.transform.position, out var from, 1, NavMesh.AllAreas)
                && NavMesh.SamplePosition(goal, out var to, 1, NavMesh.AllAreas)
                && NavMesh.CalculatePath(from.position, to.position, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete;
            if (!found) { Assert(id, false, "No complete route to station approach"); yield break; }
            var keyboard = InputSystem.AddDevice<Keyboard>(); var oldKeyboard = G.qaKeyboard;
            bool oldInput = G.qaInputEnabled; var bindings = G.options.actions;
            G.options.actions = GameOptions.DefaultBindings(); G.SyncLegacyBindings(); G.qaKeyboard = keyboard; G.qaInputEnabled = true;
            P.CancelActions();
            float limit = Time.realtimeSinceStartup + 40; int corner = 1;
            try
            {
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
                while (corner < path.corners.Length && Time.realtimeSinceStartup < limit)
                {
                    Vector3 delta = path.corners[corner] - P.transform.position;
                    Vector3 horizontal = Vector3.ProjectOnPlane(delta, Vector3.up);
                    if (horizontal.magnitude < .18f && Mathf.Abs(delta.y) < .6f) { corner++; continue; }
                    if (horizontal.sqrMagnitude > .001f) P.SetLook(Mathf.Atan2(horizontal.x, horizontal.z) * Mathf.Rad2Deg, 0);
                    yield return null;
                }
                InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return new WaitForSeconds(.2f);
                Assert(id, Vector3.Distance(P.transform.position, goal) < .6f, "InputSystem W + normal acceleration/gravity/collision ended at " + P.transform.position + "; goal=" + goal + "; corner=" + corner);
            }
            finally
            {
                G.qaInputEnabled = oldInput; G.qaKeyboard = oldKeyboard; G.options.actions = bindings; G.SyncLegacyBindings(); InputSystem.RemoveDevice(keyboard);
            }
        }
        void Aim(Vector3 point) { Vector3 d = (point - P.Eye.position).normalized; P.SetLook(Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg, -Mathf.Asin(d.y) * Mathf.Rad2Deg); }
        void Route(string id, Vector3 from, Vector3 to)
        { var path = new NavMeshPath(); bool ok = NavMesh.SamplePosition(from, out var a, 1, NavMesh.AllAreas) && NavMesh.SamplePosition(to, out var b, 1, NavMesh.AllAreas) && NavMesh.CalculatePath(a.position, b.position, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete; Assert(id, ok, path.status.ToString()); }
        IEnumerator Capture(string name) { yield return new WaitForEndOfFrame(); ScreenCapture.CaptureScreenshot(Path.Combine(output, name + ".png")); yield return new WaitForSecondsRealtime(.25f); }
        void Finish() { if (finished) return; finished = true; report.passed = report.checks.TrueForAll(x => x.passed); File.WriteAllText(Path.Combine(output, "campaign-report.json"), JsonUtility.ToJson(report, true)); Application.Quit(report.passed ? 0 : 1); }
        void OnDestroy() { Application.logMessageReceived -= OnLog; }
    }
}


