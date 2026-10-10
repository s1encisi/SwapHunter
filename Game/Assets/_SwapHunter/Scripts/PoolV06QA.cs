using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace SwapHunter
{
    // Production correctness checks only; the frozen Allocation v2 fixture remains untouched.
    public sealed class PoolV06QA : MonoBehaviour
    {
        [Serializable] public sealed class Check { public string name, detail; public bool passed; }
        [Serializable] public sealed class Report
        {
            public string kind = "Native Bolt reuse, real collision/expiry, retention overflow and effects-lifecycle regression fixtures; no FPS or byte-allocation claim.";
            public string version, buildGuid;
            public bool passed;
            public int retentionLimit;
            public long createdAtStart, createdAtEnd, retiredAtStart, retiredAtEnd;
            public List<Check> checks = new List<Check>();
        }
        readonly Report report = new Report();
        readonly List<Bolt> observedBolts = new List<Bolt>(4096);
        string output;
        bool done;
        float began;
        DemoGame G => DemoGame.I;
        PlayerMotor P => G.player;
        void Awake()
        {
            began = Time.realtimeSinceStartup;
            var args = Environment.GetCommandLineArgs(); int i = Array.IndexOf(args, "-qaOutput");
            output = i >= 0 && i + 1 < args.Length ? args[i + 1] : RunStorage.Root; Directory.CreateDirectory(output);
            report.version = Application.version; report.buildGuid = Application.buildGUID; report.retentionLimit = BoltPool.MaximumRetained;
            report.createdAtStart = Bolt.TotalCreated; report.retiredAtStart = Bolt.TotalRetired;
            Application.logMessageReceived += Error;
        }
        void Error(string message, string stack, LogType type)
        { if (type == LogType.Exception || type == LogType.Error || type == LogType.Assert) Add("runtime_error", false, message + "\n" + stack); }
        void Update() { if (!done && Time.realtimeSinceStartup - began > 90) { Add("watchdog", false, "90 real seconds"); Finish(); } }
        void Add(string name, bool passed, string detail)
        { report.checks.Add(new Check { name = name, passed = passed, detail = detail }); Debug.Log("POOL_V06_QA " + name + " " + passed + " " + detail); }
        int ActiveBolts() => BoltAllocationMeasurement.CountActiveBolts(G.effects, observedBolts);
        void RetireActive()
        { ActiveBolts(); foreach (var bolt in observedBolts) if (bolt) bolt.Retire(); }
        IEnumerator Start()
        {
            Add("isolated_storage", RunStorage.IsValidation, RunStorage.Root);
            if (!RunStorage.IsValidation) { Finish(); yield break; }
            G.StartRun(); G.qaSuppressAI = true; G.ClearDeployments();
            foreach (var actor in G.enemies) if (actor) actor.gameObject.SetActive(false);
            P.SetFeet(new Vector3(0, .03f, 7)); P.verticalSpeed = 0; P.SetLook(0, 0);
            var assault = EnemyActor.Spawn(EnemyKind.Assault, new Vector3(0, .03f, 13), G.stage);
            var sniper = EnemyActor.Spawn(EnemyKind.Sniper, new Vector3(3, .03f, 13), G.stage);
            var commander = EnemyActor.Spawn(EnemyKind.Elite, new Vector3(-3, .03f, 13), G.stage);
            Add("native_sources_spawned", assault && sniper && commander, "Assault, sniper and commander fixtures");
            if (!assault || !sniper || !commander) { Finish(); yield break; }
            assault.suppressAI = sniper.suppressAI = true;
            var boss = commander.EnableBoss(); commander.Damage(BossEncounter.ShieldMaximum + BossEncounter.HealthMaximum * .5f + 1);
            bool started = boss.TryBeginSkill(BossSkill.OmniFire); G.qaSuppressAI = false;
            float deadline = Time.realtimeSinceStartup + 5;
            while (boss.SkillStep != BossSkillStep.Execute && Time.realtimeSinceStartup < deadline) yield return null;
            G.qaSuppressAI = true;
            Add("native_omni_visual_source", started && boss.Skill == BossSkill.OmniFire && boss.SkillStep == BossSkillStep.Execute, boss.Skill + "/" + boss.SkillStep);
            if (boss.SkillStep != BossSkillStep.Execute) { Finish(); yield break; }
            RetireActive(); yield return null;

            var first = Bolt.Spawn(assault, new Vector3(100, 4, 0), Vector3.forward, 23, 5);
            int rootId = first.GetInstanceID(); var core = first.transform.Find("Tracer core"); var tail = first.transform.Find("Short projectile tail").GetComponent<LineRenderer>();
            int coreId = core.gameObject.GetInstanceID(), tailId = tail.gameObject.GetInstanceID();
            yield return new WaitForSeconds(.06f);
            Add("first_shot_native_motion", first.gameObject.activeInHierarchy && first.transform.position.z > .1f && tail.enabled, "position=" + first.transform.position + ";tail=" + tail.enabled);
            first.life = .15f; first.damage = 77; long retired = Bolt.TotalRetired; first.Retire(); int retained = BoltPool.Current.RetainedCount;
            first.Retire();
            Add("retire_once_clears_logic", !first.gameObject.activeInHierarchy && first.IsRetired && !first.owner && first.velocity == Vector3.zero && first.damage == 0 && first.life == 0 && Bolt.TotalRetired == retired + 1 && BoltPool.Current.RetainedCount == retained, "retiredDelta=" + (Bolt.TotalRetired - retired) + ";retained=" + retained);

            long created = Bolt.TotalCreated;
            var second = Bolt.Spawn(sniper, new Vector3(110, 6, 2), Vector3.left, 40, 11);
            Add("reuse_preserves_root_and_geometry", second.GetInstanceID() == rootId && second.transform.Find("Tracer core").gameObject.GetInstanceID() == coreId && second.transform.Find("Short projectile tail").gameObject.GetInstanceID() == tailId && Bolt.TotalCreated == created, "Rent did not create another root, core or tail");
            CheckReset("sniper", second, sniper, new Vector3(110, 6, 2), Vector3.left * 40, 11, .020f, .014f, new Color(1, .93f, .72f));
            second.Retire();
            var omni = Bolt.Spawn(commander, new Vector3(120, 7, 3), Vector3.back, 23, 7);
            Add("same_instance_for_omni", omni.GetInstanceID() == rootId, "root=" + omni.GetInstanceID());
            CheckReset("omni", omni, commander, new Vector3(120, 7, 3), Vector3.back * 23, 7, .030f, .020f, new Color(1, .39f, .14f));
            omni.Retire();
            var ordinary = Bolt.Spawn(assault, new Vector3(130, 8, 4), Vector3.right, 18, 9);
            CheckReset("assault_after_omni", ordinary, assault, new Vector3(130, 8, 4), Vector3.right * 18, 9, .018f, .012f, new Color(1, .82f, .48f));
            ordinary.Retire();
            var unowned = Bolt.Spawn(null, new Vector3(140, 9, 5), Vector3.zero, 0, 4);
            Add("null_owner_and_zero_direction_reset", !unowned.owner && unowned.velocity == Vector3.zero && unowned.transform.rotation == Quaternion.identity && unowned.life == 3, "No prior enemy or heading leaked"); unowned.Retire();

            float health = P.health;
            var retiredThreat = Bolt.Spawn(assault, P.Eye.position + Vector3.right * 3, Vector3.left, 23, 9);
            retiredThreat.Retire(); Vector3 retiredPosition = retiredThreat.transform.position;
            yield return new WaitForSeconds(.3f);
            Add("retired_projectile_does_not_move_or_hurt", !retiredThreat.gameObject.activeInHierarchy && retiredThreat.transform.position == retiredPosition && P.health == health && ActiveBolts() == 0, "health=" + health + " -> " + P.health);

            var hit = Bolt.Spawn(assault, P.Eye.position + Vector3.right * 3, Vector3.left, 23, 9);
            float expected = 9 * G.DamageMultiplier * G.ModuleDamageTaken * G.ExpeditionDamageTaken; deadline = Time.realtimeSinceStartup + 1;
            while (P.health == health && Time.realtimeSinceStartup < deadline) yield return null;
            Add("reused_shot_applies_current_damage", Mathf.Abs(health - P.health - expected) < .05f && !hit.gameObject.activeInHierarchy && hit.IsRetired, "expected=" + expected + ";observed=" + (health - P.health));
            RetireActive();
            var expiry = Bolt.Spawn(sniper, new Vector3(1000, 500, 0), Vector3.forward, 23, 5); int expiryId = expiry.GetInstanceID();
            Add("native_lifetime_reinitialized", expiry.life == 3, "life=" + expiry.life); yield return new WaitForSeconds(3.2f);
            Add("native_expiry_returns_to_pool", expiry && !expiry.gameObject.activeInHierarchy && expiry.IsRetired, "expired id=" + expiryId);

            int count = BoltPool.MaximumRetained + 17; var burst = new Bolt[count]; var ids = new HashSet<int>();
            int idleBefore = BoltPool.Current.RetainedCount; created = Bolt.TotalCreated; retired = Bolt.TotalRetired;
            for (int i = 0; i < count; i++) { burst[i] = Bolt.Spawn(assault, new Vector3(1000, 500, i * .01f), Vector3.forward, 23, 5); if (burst[i]) ids.Add(burst[i].GetInstanceID()); }
            Add("retention_cap_never_drops_live_shots", ids.Count == count && ActiveBolts() == count, "requested=" + count + ";actualActive=" + ActiveBolts() + ";created=" + (Bolt.TotalCreated - created) + ";idleBefore=" + idleBefore);
            foreach (var bolt in burst) if (bolt) bolt.Retire(); yield return null;
            int destroyed = 0, inactive = 0; foreach (var bolt in burst) { if (!bolt) destroyed++; else if (!bolt.gameObject.activeInHierarchy) inactive++; }
            Add("idle_cap_discards_only_excess_retirements", BoltPool.Current.RetainedCount == BoltPool.MaximumRetained && inactive == BoltPool.MaximumRetained && destroyed == 17 && ActiveBolts() == 0 && Bolt.TotalRetired - retired == count,
                "inactive=" + inactive + ";destroyed=" + destroyed + ";retiredDelta=" + (Bolt.TotalRetired - retired));

            EnemyActor owner = assault;
            for (int restart = 0; restart < 3; restart++)
            {
                var active = Bolt.Spawn(owner, new Vector3(1000, 500, 0), Vector3.forward, 23, 5);
                var oldPool = BoltPool.Current; var oldObjects = G.effects.GetComponentsInChildren<Bolt>(true); var oldIds = new HashSet<int>();
                foreach (var bolt in oldObjects) if (bolt) oldIds.Add(bolt.GetInstanceID());
                G.EnterStage(0, true); G.ClearDeployments(); G.qaSuppressAI = true; owner = G.enemies.Find(e => e && e.kind == EnemyKind.Training);
                Add("restart_source_" + restart, owner, "Real EnterStage resets effects and creates the chapter training actor"); if (!owner) break;
                var fresh = Bolt.Spawn(owner, new Vector3(1000, 500, 10 + restart), Vector3.left, 31, 6); var newPool = BoltPool.Current;
                bool immediate = newPool != oldPool && !oldIds.Contains(fresh.GetInstanceID()) && fresh.owner == owner && fresh.life == 3 && fresh.transform.parent == G.effects;
                yield return null;
                int oldRemaining = 0; foreach (var bolt in oldObjects) if (bolt) oldRemaining++;
                Add("same_frame_restart_has_no_cross_scene_reuse_" + restart, immediate && oldRemaining == 0 && !oldPool && BoltPool.Current == newPool && fresh && fresh.gameObject.activeInHierarchy,
                    "oldRemaining=" + oldRemaining + ";oldScopeDisposed=" + (!oldPool) + ";newScopeAlive=" + (BoltPool.Current == newPool));
                fresh.Retire(); yield return null;
            }
            Finish();
        }
        void CheckReset(string key, Bolt bolt, EnemyActor owner, Vector3 origin, Vector3 velocity, float damage, float coreWidth, float tailWidth, Color tint)
        {
            var core = bolt.transform.Find("Tracer core"); var tail = bolt.transform.Find("Short projectile tail").GetComponent<LineRenderer>();
            var block = new MaterialPropertyBlock(); tail.GetPropertyBlock(block); Color actualTail = block.GetColor("_BaseColor");
            core.GetComponent<Renderer>().GetPropertyBlock(block); Color actualCore = block.GetColor("_BaseColor");
            bool logic = bolt.owner == owner && bolt.velocity == velocity && bolt.damage == damage && bolt.life == 3 && !bolt.IsRetired && bolt.enabled && bolt.gameObject.activeInHierarchy && bolt.transform.position == origin && bolt.transform.localScale == Vector3.one;
            bool visuals = Mathf.Abs(core.localScale.x - coreWidth) < .0001f && Mathf.Abs(core.localScale.z - coreWidth) < .0001f && Mathf.Abs(tail.startWidth - tailWidth) < .0001f
                && !tail.enabled && tail.GetPosition(0) == Vector3.zero && tail.GetPosition(1) == Vector3.zero && ColorNear(actualTail, tint) && ColorNear(actualCore, Color.Lerp(tint, Color.white, .55f))
                && bolt.GetComponents<EnemyProjectileVisual>().Length == 1 && core.GetComponent<Renderer>().enabled;
            Add(key + "_full_logic_reset", logic, "owner=" + (bolt.owner ? bolt.owner.kind.ToString() : "none") + ";damage=" + bolt.damage + ";life=" + bolt.life + ";velocity=" + bolt.velocity);
            Add(key + "_visual_origin_color_and_tail_reset", visuals, "coreScale=" + core.localScale + ";tailWidth=" + tail.startWidth + ";tailColor=" + actualTail);
        }
        static bool ColorNear(Color a, Color b) => Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b) + Mathf.Abs(a.a - b.a) < .001f;
        void Finish()
        {
            if (done) return; done = true;
            report.createdAtEnd = Bolt.TotalCreated; report.retiredAtEnd = Bolt.TotalRetired;
            report.passed = report.checks.Count > 0 && report.checks.TrueForAll(c => c.passed);
            File.WriteAllText(Path.Combine(output, "pool-v06-report.json"), JsonUtility.ToJson(report, true));
            Debug.Log("POOL_V06_QA_RESULT=" + report.passed + ";checks=" + report.checks.Count); Application.Quit(report.passed ? 0 : 1);
        }
        void OnDestroy() { Application.logMessageReceived -= Error; }
    }
}
