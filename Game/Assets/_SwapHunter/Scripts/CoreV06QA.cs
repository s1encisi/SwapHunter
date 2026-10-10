using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

namespace SwapHunter
{
    // Explicit native fixtures: this proves rule execution, not first-time player experience or FPS.
    public sealed class CoreV06QA : MonoBehaviour
    {
        [Serializable] public sealed class Check { public string name, detail; public bool passed; }
        [Serializable] public sealed class Report
        {
            public string kind = "v0.6 native core rules, objective damage and anchor fixtures; not human playtesting";
            public string version;
            public bool passed;
            public List<Check> checks = new List<Check>();
        }
        readonly Report report = new Report();
        string output;
        float began;
        bool done;
        Transform fixtures;
        DemoGame G => DemoGame.I;
        PlayerMotor P => G.player;
        readonly Vector3 origin = new Vector3(300, .03f, 0);

        void Awake()
        {
            began = Time.realtimeSinceStartup;
            var args = Environment.GetCommandLineArgs(); int index = Array.IndexOf(args, "-qaOutput");
            output = index >= 0 && index + 1 < args.Length ? args[index + 1] : RunStorage.Root;
            Directory.CreateDirectory(output); report.version = Application.version;
            Application.logMessageReceived += Error;
        }
        void Error(string message, string stack, LogType type)
        { if (type == LogType.Exception || type == LogType.Error || type == LogType.Assert) Add("runtime_error", false, message + "\n" + stack); }
        void Update() { if (!done && Time.realtimeSinceStartup - began > 150) { Add("watchdog", false, "150 real seconds"); Finish(); } }
        void Add(string name, bool passed, string detail)
        { report.checks.Add(new Check { name = name, passed = passed, detail = detail }); Debug.Log("V06_CORE " + name + " " + passed + " " + detail); }
        GameObject Box(string name, Vector3 center, Vector3 size, int layer = Layers.World)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name; go.layer = layer;
            go.transform.SetParent(fixtures, true); go.transform.position = center; go.transform.localScale = size;
            return go;
        }
        PhaseAnchor Anchor(string name, Vector3 feet)
        {
            var go = new GameObject(name); go.layer = Layers.Actor; go.transform.SetParent(fixtures, true); go.transform.position = feet;
            var collider = go.AddComponent<CapsuleCollider>(); collider.radius = .35f; collider.height = 1.4f; collider.center = Vector3.up * .7f;
            var anchor = go.AddComponent<PhaseAnchor>(); G.anchors.Add(anchor); G.anchorOrigins.Add(feet); return anchor;
        }
        DamageableObjective Objective(string id, Vector3 center, Vector3 size, float health, Action<DamageableObjective> callback = null)
        { var prop = Box(id, center, size); var objective = prop.AddComponent<DamageableObjective>(); objective.Configure(id, health, callback); Physics.SyncTransforms(); return objective; }
        void Place(Vector3 feet) { P.CancelActions(); P.SetFeet(feet); P.verticalSpeed = 0; P.cooldown = 0; }
        void Aim(Vector3 point)
        { Vector3 delta = point - P.Eye.position; P.SetLook(Mathf.Atan2(delta.x, delta.z) * Mathf.Rad2Deg, -Mathf.Atan2(delta.y, new Vector2(delta.x, delta.z).magnitude) * Mathf.Rad2Deg); }
        bool Near(Vector3 a, Vector3 b) => Vector3.Distance(a, b) < .12f;
        void ResetPair(PhaseAnchor anchor, Vector3 source, Vector3 destination)
        { Place(source); anchor.MoveTo(destination); Physics.SyncTransforms(); Aim(anchor.AimPoint); }

        IEnumerator Start()
        {
            while (!Application.isBatchMode && !UnityEngine.Rendering.SplashScreen.isFinished) yield return null;
            G.StartRun(true); G.qaSuppressAI = true; G.ClearDeployments();
            foreach (var enemy in G.enemies.ToArray()) if (enemy) enemy.gameObject.SetActive(false);
            fixtures = new GameObject("v0.6 core validation fixtures").transform;
            Box("floor", new Vector3(300, -.5f, 15), new Vector3(60, 1, 70)); Physics.SyncTransforms();
            var surface = fixtures.gameObject.AddComponent<NavMeshSurface>(); surface.collectObjects = CollectObjects.Children;
            surface.layerMask = Layers.WorldMask; surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders; surface.BuildNavMesh();
            Place(origin); yield return null;
            var anchor = Anchor("core validation anchor", origin + Vector3.forward * 8);
            var fieldObject = new GameObject("core interference field"); fieldObject.transform.SetParent(fixtures, true);
            var field = fieldObject.AddComponent<PhaseInterferenceField>(); field.Configure("validation-field", 2);
            field.SetActive(false);
            Add("baseline_rules", Mathf.Approximately(G.config.swapRange, 22) && Mathf.Approximately(G.config.swapCooldown, 3.5f) && Mathf.Approximately(G.config.swapWindup, .08f) && Mathf.Approximately(G.config.swapStun, .65f), "range/cooldown/windup/stun=" + G.config.swapRange + "/" + G.config.swapCooldown + "/" + G.config.swapWindup + "/" + G.config.swapStun);
            Add("invalid_target_reason", P.ValidateSwap(null).Code == SwapFailureReason.InvalidTarget, P.ValidateSwap(null).ToString());
            ResetPair(anchor, origin, origin + Vector3.forward * 8);
            float oldRange = G.config.swapRange; G.config.swapRange = 5.25f;
            var rangeResult = P.ValidateSwap(anchor);
            Add("range_reason_reads_configuration", rangeResult.Code == SwapFailureReason.OutOfRange && rangeResult.ToDisplayText(G.config.swapRange).Contains(G.config.swapRange.ToString("0.#")), rangeResult + ";display=" + rangeResult.ToDisplayText(G.config.swapRange)); G.config.swapRange = oldRange;
            fieldObject.transform.position = P.transform.position; field.SetActive(true);
            Add("source_inside_local_field", P.ValidateSwap(anchor).Code == SwapFailureReason.Interference && !P.RequestSwap(anchor) && P.cooldown == 0, P.ValidateSwap(anchor).ToString());
            fieldObject.transform.position = anchor.transform.position;
            Add("target_inside_local_field", P.ValidateSwap(anchor).Code == SwapFailureReason.Interference && !P.RequestSwap(anchor), P.ValidateSwap(anchor).ToString());
            var otherFieldGo = new GameObject("independent validation field"); otherFieldGo.transform.SetParent(fixtures, true); otherFieldGo.transform.position = P.transform.position;
            var otherField = otherFieldGo.AddComponent<PhaseInterferenceField>(); otherField.Configure("independent-field", 1.5f);
            field.SetActive(false);
            Add("fields_switch_off_independently", P.ValidateSwap(anchor).Code == SwapFailureReason.Interference && P.ValidateSwap(anchor).FieldId == "independent-field", P.ValidateSwap(anchor).ToString());
            otherField.SetActive(false); field.SetActive(true);
            fieldObject.transform.position = origin + Vector3.forward * 4;
            Add("ray_crosses_field_endpoints_outside", P.ValidateSwap(anchor).IsValid, P.ValidateSwap(anchor).ToString());
            Vector3 source = P.transform.position, target = anchor.transform.position; float hp = P.health, yaw = P.yaw; int ammo = P.ammo[P.weapon], swaps = P.swapCount;
            bool requested = P.RequestSwap(anchor); yield return new WaitForSeconds(.18f);
            Add("middle_field_allows_actual_exchange", requested && P.swapCount == swaps + 1 && Near(P.transform.position, target) && Near(anchor.transform.position, source), "player=" + P.transform.position + ";anchor=" + anchor.transform.position);
            Add("swap_keeps_health_weapon_heading", P.health == hp && P.ammo[P.weapon] == ammo && Mathf.Abs(Mathf.DeltaAngle(P.yaw, yaw)) < .01f, "hp=" + P.health + ";ammo=" + P.ammo[P.weapon]);
            field.SetActive(false); ResetPair(anchor, origin, origin + Vector3.forward * 8);
            for (int endpoint = 0; endpoint < 2; endpoint++)
            {
                source = P.transform.position; target = anchor.transform.position; swaps = P.swapCount;
                requested = P.RequestSwap(anchor); fieldObject.transform.position = endpoint == 0 ? source : target; field.SetActive(true);
                yield return new WaitForSeconds(.18f);
                Add("field_appears_during_windup_" + endpoint, requested && P.swapCount == swaps && Near(P.transform.position, source) && Near(anchor.transform.position, target) && P.cooldown == 0 && !P.preparing, "source/target unchanged;reason=" + P.ValidateSwap(anchor));
                field.SetActive(false); ResetPair(anchor, origin, origin + Vector3.forward * 8);
            }
            requested = P.RequestSwap(anchor); source = P.transform.position; swaps = P.swapCount; G.SetState(RunState.Paused);
            yield return new WaitForSecondsRealtime(.15f); G.SetState(RunState.Playing); yield return new WaitForSeconds(.12f);
            Add("pause_cancels_pending_swap", requested && P.swapCount == swaps && Near(P.transform.position, source) && !P.preparing && P.cooldown == 0, "No delayed commit after resume");
            ResetPair(anchor, origin, origin + Vector3.forward * 8); requested = P.RequestSwap(anchor); P.CancelActions(); yield return new WaitForSeconds(.15f);
            Add("explicit_action_cancel_no_cooldown", requested && Near(P.transform.position, origin) && !P.preparing && P.cooldown == 0, "CancelActions stops coroutine");
            ResetPair(anchor, origin, origin + Vector3.forward * 8); requested = P.RequestSwap(anchor); anchor.gameObject.SetActive(false); yield return new WaitForSeconds(.15f);
            Add("target_disabled_during_windup", requested && Near(P.transform.position, origin) && P.cooldown == 0, "Target lifetime revalidated"); anchor.gameObject.SetActive(true);
            anchor.gameObject.SetActive(false); Place(origin);
            var rollbackEnemy = EnemyActor.Spawn(EnemyKind.Training, origin + Vector3.forward * 8, G.stage);
            Add("rollback_fixture_spawned", rollbackEnemy, "Uses native enemy MoveTo failure, not a replacement swap implementation");
            if (rollbackEnemy)
            {
                Aim(rollbackEnemy.AimPoint); source = P.transform.position; target = rollbackEnemy.transform.position; swaps = P.swapCount;
                var motion = rollbackEnemy.motion; requested = P.RequestSwap(rollbackEnemy); rollbackEnemy.motion = null;
                yield return new WaitForSeconds(.16f); rollbackEnemy.motion = motion;
                Add("relocation_failure_rolls_back_both", requested && P.swapCount == swaps && Near(P.transform.position, source) && Near(rollbackEnemy.transform.position, target) && P.cooldown == 0 && P.controller.enabled, "Player controller and both endpoints restored");
                rollbackEnemy.gameObject.SetActive(false);
            }

            // Exercise all current weapon entry points, including the real ballistic launcher.
            Place(origin); var shootingProp = Objective("weapon-target", origin + new Vector3(0, 1.45f, 6), new Vector3(3, 3, .7f), 100000);
            Aim(shootingProp.AimPoint);
            Add("objective_is_not_q_target", P.FindTarget() == null && !shootingProp.GetComponent<SwapTarget>(), "Objective collider blocks aim ray but never offers Q");
            for (int weapon = 0; weapon < WeaponCatalog.Count; weapon++)
            {
                Place(origin); P.SetLoadout(weapon, weapon == 0 ? 1 : 0); Aim(shootingProp.AimPoint); yield return new WaitForSeconds(.23f);
                float before = shootingProp.Health; int beforeAmmo = P.ammo[weapon]; bool fired = P.Fire();
                yield return new WaitForSeconds(weapon == 9 ? 1.1f : weapon == 10 ? .8f : weapon == 2 ? .5f : .12f);
                Add("objective_actual_weapon_" + weapon, fired && P.ammo[weapon] < beforeAmmo && shootingProp.Health < before, "damage=" + (before - shootingProp.Health));
            }
            Place(origin); P.SetLoadout(0, 1); Aim(shootingProp.AimPoint);
            var cover = Box("weapon cover", origin + new Vector3(0, 1.5f, 3), new Vector3(5, 4, .4f)); Physics.SyncTransforms(); yield return new WaitForSeconds(.25f);
            hp = shootingProp.Health; requested = P.Fire(); yield return new WaitForSeconds(.15f);
            Add("objective_hitscan_world_cover", requested && shootingProp.Health == hp, "World wall receives shot first");
            cover.SetActive(false); shootingProp.gameObject.SetActive(false);

            var blastProp = Objective("blast-target", origin + new Vector3(0, .75f, 6), Vector3.one, 1000);
            Vector3 blastPoint = origin + new Vector3(0, .6f, 4);
            hp = blastProp.Health; var ordnance = PlayerOrdnance.Launch(P, blastPoint, Vector3.forward, 95); ordnance.Explode();
            Add("objective_player_explosion", blastProp.Health < hp, "Uses PlayerOrdnance.Explode radial LOS path");
            hp = blastProp.Health; var grenade = GrenadeActor.Spawn(blastPoint, blastPoint, 30); grenade.Explode(); grenade.Explode();
            Add("objective_enemy_grenade", blastProp.Health < hp, "Uses GrenadeActor.Explode radial LOS path");
            float after = blastProp.Health; grenade.Explode(); Add("enemy_grenade_explodes_once", blastProp.Health == after, "No duplicate explosion damage");
            cover = Box("blast cover", origin + new Vector3(0, 1.5f, 5), new Vector3(5, 4, .4f)); Physics.SyncTransforms(); hp = blastProp.Health;
            ordnance = PlayerOrdnance.Launch(P, blastPoint, Vector3.forward, 95); ordnance.Explode(); grenade = GrenadeActor.Spawn(blastPoint, blastPoint, 30); grenade.Explode();
            Add("objective_both_explosions_world_cover", blastProp.Health == hp, "Both explosion routes respect world cover"); cover.SetActive(false); blastProp.gameObject.SetActive(false);

            Place(origin); P.combatTools.ResetCombat(); var meleeProp = Objective("melee-target", origin + new Vector3(0, 1.1f, 2), new Vector3(.8f, 1.5f, .5f), 100);
            Aim(meleeProp.AimPoint); hp = meleeProp.Health; requested = P.combatTools.Melee();
            Add("objective_melee_exact_24", requested && Mathf.Approximately(hp - meleeProp.Health, 24), "damage=" + (hp - meleeProp.Health));
            cover = Box("melee cover", origin + new Vector3(0, 1.3f, 1), new Vector3(3, 3, .3f)); Physics.SyncTransforms(); P.combatTools.ResetCombat(); hp = meleeProp.Health;
            requested = P.combatTools.Melee(); Add("objective_melee_world_cover", requested && meleeProp.Health == hp, "Melee cannot hit prop through wall"); cover.SetActive(false); meleeProp.gameObject.SetActive(false);
            int deaths = 0, kills = G.totalKills;
            var once = Objective("single-callback", origin + new Vector3(4, 1, 4), Vector3.one, 24, objective => { deaths++; objective.ApplyDamage(999, origin); });
            once.ApplyDamage(24, origin); once.ApplyDamage(24, origin);
            Add("objective_death_callback_once", deaths == 1 && once.Destroyed && once.Health == 0 && G.totalKills == kills, "Reentrant and repeated damage do not repeat task completion or enemy rewards"); once.gameObject.SetActive(false);

            // Anchor transport records completion once; maintenance resets only the anchor positions.
            Place(origin); anchor.gameObject.SetActive(true); anchor.MoveTo(origin + Vector3.right * 6); int connections = 0;
            var socketGo = new GameObject("validation socket"); socketGo.transform.SetParent(fixtures, true); socketGo.transform.position = origin + new Vector3(6, 0, 5);
            var socket = socketGo.AddComponent<PhaseAnchorSocket>(); socket.Configure("socket-a", .65f, () => connections++);
            G.SetState(RunState.Paused); anchor.MoveTo(socketGo.transform.position); yield return new WaitForSecondsRealtime(.12f);
            Add("socket_pauses_with_game", !socket.Connected && connections == 0, "No hidden objective progress while paused"); G.SetState(RunState.Playing); yield return null;
            Add("socket_detects_real_anchor", socket.Connected && connections == 1, "Anchor feet, not E interaction");
            anchor.MoveTo(origin + Vector3.right * 6); yield return null;
            Add("socket_latches_after_anchor_moves", socket.Connected && connections == 1, "Bridge/task remains completed");
            var second = Anchor("second maintenance anchor", origin + new Vector3(10, 0, 5));
            var reset = fixtures.gameObject.AddComponent<AnchorMaintenanceReset>(); Vector3 firstHome = origin + new Vector3(6, 0, 5), secondHome = origin + new Vector3(10, 0, 9);
            reset.Configure(new[] { anchor, second }, new[] { firstHome, secondHome });
            Vector3 firstBefore = anchor.transform.position, secondBefore = second.transform.position;
            Place(firstHome); Physics.SyncTransforms(); requested = reset.TryReset();
            Add("reset_rejects_player_in_home_atomically", !requested && Near(anchor.transform.position, firstBefore) && Near(second.transform.position, secondBefore) && Near(P.transform.position, firstHome), reset.LastFailure);
            Place(origin); var resetBlocker = Box("second anchor home blocker", secondHome + Vector3.up * .7f, Vector3.one); Physics.SyncTransforms(); requested = reset.TryReset();
            Add("reset_later_endpoint_failure_moves_nothing", !requested && Near(anchor.transform.position, firstBefore) && Near(second.transform.position, secondBefore), reset.LastFailure); resetBlocker.SetActive(false);
            source = P.transform.position; requested = reset.TryReset(); yield return null;
            Add("reset_only_moves_anchors", requested && Near(anchor.transform.position, firstHome) && Near(second.transform.position, secondHome) && Near(P.transform.position, source) && socket.Connected && connections == 1, "No player teleport or repeated socket reward");
            field.SetActive(true); fieldObject.SetActive(false);
            Add("disabled_field_not_registered", !PhaseInterferenceField.Contains(fieldObject.transform.position), "No stale field after scene disable");
            Finish();
        }
        void Finish()
        {
            if (done) return; done = true; Time.timeScale = 1;
            report.passed = report.checks.Count > 0 && report.checks.TrueForAll(check => check.passed);
            File.WriteAllText(Path.Combine(output, "core-v06-report.json"), JsonUtility.ToJson(report, true));
            Application.Quit(report.passed ? 0 : 1);
        }
        void OnDestroy() { Application.logMessageReceived -= Error; }
    }
}
