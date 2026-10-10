using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.AI;

namespace SwapHunter
{
    // Same-chapter ownership diagnostic. No forced GC, UnloadUnusedAssets or FPS claim.
    public sealed class LifecycleV06QA : MonoBehaviour
    {
        [Serializable] public sealed class Check { public string name, detail; public bool passed; }
        [Serializable] public sealed class Counts
        {
            public int frame, gameObjects, activeSceneObjects, inactiveSceneObjects, materials, meshes, textures, navMeshData;
        }
        [Serializable] public sealed class Retained
        {
            public string kind, name, owner;
            public int instanceId;
            public bool active;
        }
        [Serializable] public sealed class Cycle
        {
            public int number, beginFrame, activityFrame, clearRequestFrame, afterCleanupFrame;
            public bool warmup, passed;
            public int gameObjectsTracked, gameObjectsDestroyed, retainedInactiveObjects, retainedActiveObjects;
            public int ownedMaterialsTracked, ownedMaterialsDestroyed, retainedOwnedMaterials;
            public int ownedNavigationTracked, ownedNavigationDestroyed, retainedOwnedNavigation;
            public int boltsSpawned, explicitRetireCalls, fieldCount, objectiveCount;
            public int retiredBoltsDestroyed, retiredBoltsInactive, retiredBoltsActive, poolRetainedBeforeClear;
            public bool poolDestroyedAfterClear;
            public Counts before, during, after;
            public List<Retained> retained = new List<Retained>();
        }
        [Serializable] public sealed class Report
        {
            public int schemaVersion = 1, chapter = 11, warmupCycles = 2, measuredCycles = 8, completedWarmups, completedMeasured;
            public string version, unityVersion, startedUtc, outcome = "not_started";
            public string scope = "AI-suppressed chapter 11 lifecycle fixture: two warmups plus eight measured Begin/Finish(false)/OpenBoard cycles. Fixed native Bolt, Line, field-toggle and objective-hit activity. Not combat completion, frame-rate, managed-GC or GPU-memory evidence.";
            public string ownership = "Track map/enemy/effect GameObjects materials explicitly owned by WorldFontAtlas/PhaseInterferencePresentation, and runtime NavMeshData owned by RuntimeNavigationOwner. Read sharedMaterial only; shared project assets are excluded from release expectations. Destroyed means Unity native object became unavailable, not that a managed wrapper was collected.";
            public string globalCounts = "Material/Mesh/Texture/NavMeshData totals are observations only; lazy caches and engine resources are not subject to an invented zero-growth pass threshold.";
            public bool passed, batchMode, forcedGc = false, unloadedUnusedAssets = false;
            public Counts initial, postWarmup, final;
            public bool classicDataRetainedOnDisable, classicNavigationRestored;
            public int classicTrianglesBefore, classicTrianglesAfter;
            public int finalMaterialDelta, finalMeshDelta, finalTextureDelta, finalNavMeshDataDelta;
            public List<Cycle> cycles = new List<Cycle>();
            public List<Check> checks = new List<Check>();
        }
        sealed class Tracked
        {
            public UnityEngine.Object value;
            public int id;
            public string name, owner;
            public bool material, navigation;
        }

        readonly Report report = new Report();
        string output;
        bool done, originalSuppression;
        float started;
        ExpeditionStore originalStore;
        DemoGame G => DemoGame.I;

        void Awake()
        {
            // Unity APIs are deliberately not used in field initializers.
            started = Time.realtimeSinceStartup;
            report.version = Application.version; report.unityVersion = Application.unityVersion;
            report.batchMode = Application.isBatchMode; report.startedUtc = DateTime.UtcNow.ToString("o");
            string[] args = Environment.GetCommandLineArgs(); int index = Array.IndexOf(args, "-qaOutput");
            output = Path.GetFullPath(index >= 0 && index + 1 < args.Length ? args[index + 1] : RunStorage.Root);
            Directory.CreateDirectory(output);
            Application.logMessageReceived += RuntimeError;
        }
        void RuntimeError(string message, string trace, LogType type)
        {
            if (!done && (type == LogType.Error || type == LogType.Exception || type == LogType.Assert))
                Add("runtime_error", false, message + "\n" + trace);
        }
        void Update()
        {
            if (!done && Time.realtimeSinceStartup - started > 240)
            { Add("watchdog", false, "240 real seconds"); Finish("timeout"); }
        }
        IEnumerator Start()
        {
            IEnumerator run = Run();
            while (!done)
            {
                object current = null; bool hasNext = false;
                try { hasNext = run.MoveNext(); if (hasNext) current = run.Current; }
                catch (Exception error) { Add("fixture_exception", false, error.ToString()); Finish("exception"); }
                if (done) yield break;
                if (!hasNext) { Finish("completed"); yield break; }
                yield return current;
            }
        }
        IEnumerator Run()
        {
            while (!G || !G.player || G.state == RunState.Loading) yield return null;
            bool isolated = RunStorage.IsValidation && G.qaMode;
            Add("validation_isolated", isolated, "Lifecycle flag must be registered before player startup");
            if (!isolated) yield break;
            // OnDisable is temporary for the classic world: verify that ownership does not free its data.
            var classic = G.world;
            var classicOwner = classic ? classic.GetComponent<RuntimeNavigationOwner>() : null;
            var classicData = classicOwner ? classicOwner.OwnedData : null;
            Add("classic_runtime_navigation_owned", classic && classic.gameObject.activeSelf && classicOwner && classicData,
                "Fresh classic surface owns its runtime data before entering an expedition");
            if (!classic || !classicOwner || !classicData) yield break;
            report.classicTrianglesBefore = NavMesh.CalculateTriangulation().vertices.Length;
            classic.gameObject.SetActive(false);
            yield return null;
            report.classicDataRetainedOnDisable = classicData && classicOwner.OwnedData == classicData;
            Add("classic_disable_preserves_owned_data", report.classicDataRetainedOnDisable, "Only OnDestroy may release navigation data");
            classic.gameObject.SetActive(true);
            yield return null;
            report.classicTrianglesAfter = NavMesh.CalculateTriangulation().vertices.Length;
            report.classicNavigationRestored = classicData && classicOwner.OwnedData == classicData
                && report.classicTrianglesBefore > 0 && report.classicTrianglesAfter == report.classicTrianglesBefore;
            Add("classic_navigation_reenabled", report.classicNavigationRestored, "triangles before=" + report.classicTrianglesBefore + "; after=" + report.classicTrianglesAfter);
            originalStore = G.expeditionStore; originalSuppression = G.qaSuppressAI;
            G.qaSuppressAI = true;
            var store = new ExpeditionStore(Path.Combine(RunStorage.Root, "lif"));
            for (int level = store.Profile.highestUnlocked; level < 10; level++)
            { string run = store.Begin(level); store.Settle(run, true, false, 1); }
            G.expeditionStore = store;
            report.initial = CountObjects();
            report.outcome = "cycling";
            for (int iteration = 0; iteration < report.warmupCycles + report.measuredCycles; iteration++)
            {
                var cycle = new Cycle { number = iteration + 1, warmup = iteration < report.warmupCycles, before = CountObjects() };
                report.cycles.Add(cycle);
                string tag = (cycle.warmup ? "warmup_" : "measured_") + cycle.number;
                int firstCheck = report.checks.Count;
                bool began = G.BeginExpedition(10); cycle.beginFrame = Time.frameCount;
                Add(tag + "_begin", began && G.expeditionMap != null, G.expeditionNotice);
                if (!began || G.expeditionMap == null) yield break;
                // Let imported instances and normal component initialization cross native frames.
                yield return null; yield return null;

                var source = EnemyActor.Spawn(EnemyKind.Assault, new Vector3(8, .03f, 11), G.stage);
                Add(tag + "_source_actor", source, "Fixed production EnemyActor.Spawn; AI remains suppressed");
                if (!source) yield break;
                source.suppressAI = true;
                var bolts = new Bolt[4];
                for (int i = 0; i < bolts.Length; i++)
                {
                    bolts[i] = Bolt.Spawn(source, new Vector3(0, 2 + i * .15f, 10), Vector3.right, 8, 1);
                    if (bolts[i]) cycle.boltsSpawned++;
                }
                Shapes.Line(G.effects, new Vector3(0, 2, 7), new Vector3(2, 2, 7), Color.cyan, .02f);
                var timed = Shapes.Line(G.effects, new Vector3(0, 2.2f, 7), new Vector3(2, 2.2f, 7), Color.white, .02f);
                timed.gameObject.AddComponent<TimedEffect>().life = .03f;
                var fields = G.expeditionMap.root.GetComponentsInChildren<PhaseInterferenceField>(true);
                var objectives = G.expeditionMap.root.GetComponentsInChildren<DamageableObjective>(true);
                cycle.fieldCount = fields.Length; cycle.objectiveCount = objectives.Length;
                Add(tag + "_chapter_fixture", fields.Length == 2 && objectives.Length >= 2 && cycle.boltsSpawned == 4,
                    "Two native fields, their damageable emitters, and four real Bolts");
                if (fields.Length > 0) fields[0].SetActive(false);
                if (objectives.Length > 0) objectives[0].ApplyDamage(1, G.player.Eye.position);

                List<Tracked> owned = TrackOwnedResources();
                cycle.activityFrame = Time.frameCount; cycle.during = CountObjects();
                BoltPool cyclePool = BoltPool.Current;
                yield return null;
                if (fields.Length > 0 && fields[0]) fields[0].SetActive(true);
                for (int i = 0; i < 2; i++) if (bolts[i]) { bolts[i].Retire(); cycle.explicitRetireCalls++; }
                Add(tag + "_production_retire_exercised", cycle.explicitRetireCalls == 2, "Other live effects remain for the normal world-clear path");
                yield return null;
                bool retiredLogicReset = true;
                for (int i = 0; i < 2; i++)
                {
                    if (!bolts[i]) { cycle.retiredBoltsDestroyed++; continue; }
                    if (bolts[i].gameObject.activeInHierarchy) cycle.retiredBoltsActive++; else cycle.retiredBoltsInactive++;
                    retiredLogicReset &= bolts[i].IsRetired && !bolts[i].owner && bolts[i].velocity == Vector3.zero && bolts[i].damage == 0 && bolts[i].life == 0;
                }
                cycle.poolRetainedBeforeClear = cyclePool ? cyclePool.RetainedCount : 0;
                Add(tag + "_in_mission_retirement", cycle.retiredBoltsActive == 0 && retiredLogicReset
                    && cycle.retiredBoltsDestroyed + cycle.retiredBoltsInactive == 2,
                    "Before leaving: destroyed=" + cycle.retiredBoltsDestroyed + "; valid inactive=" + cycle.retiredBoltsInactive + "; pool retained=" + cycle.poolRetainedBeforeClear);
                cycle.clearRequestFrame = Time.frameCount;
                bool settled = G.FinishExpedition(false);
                Add(tag + "_fixture_settlement", settled, "Failure settlement is intentional and isolated");
                if (!settled) yield break;
                G.OpenExpeditionBoard();
                // Scaled time is paused in menus. Never use WaitForSeconds here.
                yield return null; yield return null; yield return null;
                cycle.afterCleanupFrame = Time.frameCount;
                cycle.poolDestroyedAfterClear = !cyclePool;
                Classify(owned, cycle);
                cycle.after = CountObjects();
                Add(tag + "_native_cleanup_frames", cycle.afterCleanupFrame - cycle.clearRequestFrame >= 3, "At least three real Unity frames after normal cleanup");
                Add(tag + "_old_gameobjects_destroyed", cycle.gameObjectsTracked > 0 && cycle.retainedActiveObjects == 0 && cycle.retainedInactiveObjects == 0,
                    "Destroyed=" + cycle.gameObjectsDestroyed + "; retained inactive=" + cycle.retainedInactiveObjects + "; retained active=" + cycle.retainedActiveObjects);
                Add(tag + "_old_pool_destroyed", cycle.poolDestroyedAfterClear && !BoltPool.Current,
                    "The per-effects pool and idle/live Bolts must be destroyed after leaving the chapter; inactive retention is not exempt here");
                Add(tag + "_owned_materials_destroyed", cycle.ownedMaterialsTracked > 0 && cycle.retainedOwnedMaterials == 0,
                    "Owned=" + cycle.ownedMaterialsTracked + "; destroyed=" + cycle.ownedMaterialsDestroyed + "; retained=" + cycle.retainedOwnedMaterials);
                Add(tag + "_owned_navigation_destroyed", cycle.ownedNavigationTracked > 0 && cycle.retainedOwnedNavigation == 0,
                    "Owned runtime NavMeshData=" + cycle.ownedNavigationTracked + "; destroyed=" + cycle.ownedNavigationDestroyed + "; retained=" + cycle.retainedOwnedNavigation);
                Add(tag + "_world_detached", G.expeditionMap == null && G.mission == null && G.PendingDeployments == 0 && G.enemies.Count == 0,
                    "No old map/mission, live actor registry or pending warning budget");
                cycle.passed = true;
                for (int c = firstCheck; c < report.checks.Count; c++) cycle.passed &= report.checks[c].passed;
                if (cycle.warmup) report.completedWarmups++; else report.completedMeasured++;
                if (report.completedWarmups == report.warmupCycles && cycle.warmup) report.postWarmup = cycle.after;
                Save();
            }
            report.final = CountObjects();
            if (report.postWarmup != null)
            {
                report.finalMaterialDelta = report.final.materials - report.postWarmup.materials;
                report.finalMeshDelta = report.final.meshes - report.postWarmup.meshes;
                report.finalTextureDelta = report.final.textures - report.postWarmup.textures;
                report.finalNavMeshDataDelta = report.final.navMeshData - report.postWarmup.navMeshData;
            }
        }

        List<Tracked> TrackOwnedResources()
        {
            var result = new List<Tracked>(); var ids = new HashSet<int>();
            void ObjectTree(Transform root, string owner)
            {
                if (!root) return;
                foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                {
                    var value = transform.gameObject;
                    if (ids.Add(value.GetInstanceID())) result.Add(new Tracked { value = value, id = value.GetInstanceID(), name = value.name, owner = owner });
                }
            }
            ObjectTree(G.expeditionMap.root, "chapter map");
            foreach (var enemy in G.enemies) if (enemy) ObjectTree(enemy.transform, "chapter enemy");
            foreach (Transform child in G.effects) ObjectTree(child, "runtime effect"); // Persistent effects container is excluded.
            foreach (var owner in G.expeditionMap.root.GetComponentsInChildren<RuntimeNavigationOwner>(true))
            {
                var data = owner.OwnedData;
                if (data && ids.Add(data.GetInstanceID())) result.Add(new Tracked { value = data, id = data.GetInstanceID(),
                    name = data.name, owner = "RuntimeNavigationOwner.OnDestroy", navigation = true });
            }
            var shared = new HashSet<int>();
            foreach (var material in G.config.materials) if (material) shared.Add(material.GetInstanceID());
            if (G.config.worldTextMaterial) shared.Add(G.config.worldTextMaterial.GetInstanceID());
            if (G.font && G.font.material) shared.Add(G.font.material.GetInstanceID());
            void OwnedMaterial(Material material, string owner)
            {
                if (!material || shared.Contains(material.GetInstanceID()) || !ids.Add(material.GetInstanceID())) return;
                result.Add(new Tracked { value = material, id = material.GetInstanceID(), name = material.name, owner = owner, material = true });
            }
            foreach (var atlas in G.expeditionMap.root.GetComponentsInChildren<WorldFontAtlas>(true))
            {
                var renderer = atlas.GetComponent<Renderer>();
                if (renderer) OwnedMaterial(renderer.sharedMaterial, "WorldFontAtlas.OnDestroy");
            }
            foreach (var boundary in G.expeditionMap.root.GetComponentsInChildren<PhaseInterferencePresentation>(true))
                foreach (var line in boundary.GetComponentsInChildren<LineRenderer>(true))
                    if (line.sharedMaterial && line.sharedMaterial.name == "Local interference boundary")
                        OwnedMaterial(line.sharedMaterial, "PhaseInterferencePresentation.OnDestroy");
            return result;
        }
        static void Classify(List<Tracked> objects, Cycle cycle)
        {
            foreach (var item in objects)
            {
                if (item.navigation)
                {
                    cycle.ownedNavigationTracked++;
                    if (!item.value) { cycle.ownedNavigationDestroyed++; continue; }
                    cycle.retainedOwnedNavigation++;
                }
                else if (item.material)
                {
                    cycle.ownedMaterialsTracked++;
                    if (!item.value) { cycle.ownedMaterialsDestroyed++; continue; }
                    cycle.retainedOwnedMaterials++;
                }
                else
                {
                    cycle.gameObjectsTracked++;
                    if (!item.value) { cycle.gameObjectsDestroyed++; continue; }
                    if (((GameObject)item.value).activeInHierarchy) cycle.retainedActiveObjects++; else cycle.retainedInactiveObjects++;
                }
                cycle.retained.Add(new Retained { kind = item.navigation ? "owned NavMeshData" : item.material ? "owned Material" : "GameObject", name = item.name, instanceId = item.id,
                    owner = item.owner, active = !item.material && !item.navigation && ((GameObject)item.value).activeInHierarchy });
            }
        }
        static Counts CountObjects()
        {
            var all = Resources.FindObjectsOfTypeAll<GameObject>();
            var counts = new Counts { frame = Time.frameCount, gameObjects = all.Length,
                materials = Resources.FindObjectsOfTypeAll<Material>().Length, meshes = Resources.FindObjectsOfTypeAll<Mesh>().Length,
                textures = Resources.FindObjectsOfTypeAll<Texture>().Length, navMeshData = Resources.FindObjectsOfTypeAll<NavMeshData>().Length };
            foreach (var gameObject in all) if (gameObject && gameObject.scene.IsValid())
            { if (gameObject.activeInHierarchy) counts.activeSceneObjects++; else counts.inactiveSceneObjects++; }
            return counts;
        }
        void Add(string name, bool passed, string detail) { report.checks.Add(new Check { name = name, passed = passed, detail = detail }); }
        void Save() { File.WriteAllText(Path.Combine(output, "lifecycle-v06-report.json"), JsonUtility.ToJson(report, true)); }
        void Finish(string outcome)
        {
            if (done) return;
            done = true; report.outcome = outcome;
            report.passed = report.completedWarmups == report.warmupCycles && report.completedMeasured == report.measuredCycles
                && report.checks.Count > 0 && report.checks.TrueForAll(check => check.passed);
            Save();
            if (G) { G.qaSuppressAI = originalSuppression; if (originalStore != null) G.expeditionStore = originalStore; }
            Debug.Log("LIFECYCLE_V06_QA_RESULT=" + report.passed + ";warmups=" + report.completedWarmups + ";measured=" + report.completedMeasured);
            Application.Quit(report.passed ? 0 : 1);
        }
        void OnDestroy() { Application.logMessageReceived -= RuntimeError; }
    }
}
