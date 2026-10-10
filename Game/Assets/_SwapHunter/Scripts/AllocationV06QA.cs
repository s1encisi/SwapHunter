using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using UnityEngine;
using UnityEngine.Rendering;

namespace SwapHunter
{
    public sealed class AllocationV06QA : MonoBehaviour
    {
        [Serializable] public sealed class Batch
        {
            public int index, requested, successfulSpawnCalls, distinctRootIdentities, newlyObservedRootIdentities, activeRootsAfterSpawn, observedObjectsAfterSpawn;
            public int liveBoltsBefore, liveBoltsAfterSpawn, liveBoltsAfterCleanup, destroyedRoots, retainedInactiveRoots, activeRootsAfterCleanup, destroyedObjects, retainedInactiveObjects, activeObjectsAfterCleanup;
            public int movedRetiredRoots, movedRetiredObjects;
            public float playerHealthBeforeRetiredObservation, playerHealthAfterRetiredObservation, sourceHealthBeforeRetiredObservation, sourceHealthAfterRetiredObservation;
            public long controlManagedBytes = -1, spawnManagedBytes = -1, controlAllocationEvents = -1, spawnAllocationEvents = -1;
            public bool controlHadProfilerSample, spawnHadProfilerSample;
            public long productionCreated = -1, productionRetired = -1;
            public double controlCallMilliseconds = -1, spawnCallMilliseconds = -1, cleanupWaitSeconds;
            public bool attempted, synchronousWindowMeasured, cleanupComplete, retiredObjectsStable, noDamageAfterCleanup;
        }
        [Serializable] public sealed class Report
        {
            public string kind = "Background-capable managed-allocation diagnostic of synchronous real Bolt.Spawn calls. No frame-rate, rendering-performance or player-experience conclusion.";
            public string fixture = "v06-bolt-spawn-allocation-v2-gcalloc-sync", stage, version, unityVersion, buildGuid, assemblyMvid, assemblySha256, graphicsApi, utcStarted, utcFinished;
            public string sourceState, cleanupPolicy = "Call the same production Bolt.Retire entry in every stage. Cross a native frame, verify inactive/destroyed objects, then observe two additional native frames for residual movement/activation/damage before the next batch. Destruction and inactive retention are reported separately.";
            public string interpretation = "Managed bytes include Bolt.Spawn and its actual EnemyProjectileVisual initialization. Empty-loop control is reported separately, never silently subtracted. Newly observed root identities are observations, not an instrumented native allocator count. Cleaned means inactive/destroyed GameObjects, not proof that managed wrappers were collected or memory returned to the OS. Synchronous elapsed time includes OS scheduling and is not frame time or GPU time.";
            public bool batchMode, focusedAtStart, developmentBuild, counterAvailable, passed, measurementValid;
            public long counterProbeObservedBytes = -1, totalSpawnManagedBytes = -1, totalControlManagedBytes = -1;
            public string counterFailure, outcome = "not_started";
            public SynchronousGcAllocationRecorder.Verification counterVerification;
            public bool objectLifecycleMeasurementValid, byteMeasurementComplete, eventMeasurementComplete;
            public int spawnByteSamples, controlByteSamples, spawnEventSamples, controlEventSamples;
            public long totalSpawnAllocationEvents = -1, totalControlAllocationEvents = -1, totalProductionCreated = -1, totalProductionRetired = -1;
            public double meanAllocationEventsPerSpawn = -1;
            public int seed = 60031, batchSize = 64, warmupBatches = 2, measuredBatches = 8, completedWarmups, completedBatches;
            public int totalSuccessfulSpawnCalls, totalNewlyObservedRoots, totalDestroyedRoots, totalRetainedInactiveRoots;
            public bool retiredDamageProbePassed;
            public string retiredDamageProbeDetail;
            public double meanManagedBytesPerSpawn = -1, meanSynchronousSpawnCallMilliseconds = -1;
            public Batch[] batches;
            public Batch[] warmups;
            public List<string> notes = new List<string>();
            public List<string> errors = new List<string>();
        }
        readonly Report report = new Report();
        readonly List<Bolt> liveBolts = new List<Bolt>(4096);
        BoltAllocationMeasurement measurement;
        SynchronousGcAllocationRecorder counter;
        EnemyActor source;
        string output;
        bool done, batchFailed;
        double started;
        DemoGame G => DemoGame.I;
        void Awake()
        {
            started = Time.realtimeSinceStartupAsDouble; report.utcStarted = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
            var args = Environment.GetCommandLineArgs(); int index = Array.IndexOf(args, "-qaOutput");
            output = index >= 0 && index + 1 < args.Length ? args[index + 1] : RunStorage.Root; Directory.CreateDirectory(output);
            index = Array.IndexOf(args, "-performanceStage"); report.stage = index >= 0 && index + 1 < args.Length ? args[index + 1] : "";
            report.version = Application.version; report.unityVersion = Application.unityVersion; report.buildGuid = Application.buildGUID;
            report.graphicsApi = SystemInfo.graphicsDeviceType.ToString(); report.batchMode = Application.isBatchMode;
            report.focusedAtStart = Application.isFocused; report.developmentBuild = Debug.isDebugBuild;
            report.batches = new Batch[report.measuredBatches];
            for (int i = 0; i < report.batches.Length; i++) report.batches[i] = new Batch { index = i, requested = report.batchSize };
            report.warmups = new Batch[report.warmupBatches];
            for (int i = 0; i < report.warmups.Length; i++) report.warmups[i] = new Batch { index = i, requested = report.batchSize };
            try
            {
                var assembly = typeof(AllocationV06QA).Assembly; report.assemblyMvid = assembly.ManifestModule.ModuleVersionId.ToString();
                if (!string.IsNullOrEmpty(assembly.Location) && File.Exists(assembly.Location))
                    using (var stream = File.OpenRead(assembly.Location)) using (var sha = SHA256.Create()) report.assemblySha256 = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
            }
            catch (Exception error) { report.notes.Add("Additional managed-build identity unavailable: " + error.Message); }
            Application.logMessageReceived += Error;
        }
        void Error(string message, string stack, LogType type)
        { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) report.errors.Add(message + "\n" + stack); }
        void Update()
        { if (!done && Time.realtimeSinceStartupAsDouble - started > 120) { report.errors.Add("120-second real-time allocation watchdog; phase=" + report.outcome); Finish("watchdog"); } }
        IEnumerator Start()
        {
            if (report.stage != "Baseline" && report.stage != "After") report.errors.Add("-performanceStage must be Baseline or After.");
            if (!RunStorage.IsValidation) report.errors.Add("Allocation QA requires isolated validation storage.");
            if (report.errors.Count > 0) { Finish("invalid_arguments_or_storage"); yield break; }
            report.outcome = "counter_probe";
            counter = new SynchronousGcAllocationRecorder(); report.counterVerification = counter.Evidence;
            report.counterAvailable = counter.Evidence.bytesVerified; report.counterFailure = counter.Evidence.failure;
            report.counterProbeObservedBytes = counter.Evidence.bytesVerified ? counter.Evidence.positiveProbeRawValue : -1;
            if (!report.counterAvailable) report.notes.Add("Synchronous byte counter not verified; byte fields remain missing while real object-lifecycle observations continue.");
            double splash = Time.realtimeSinceStartupAsDouble;
            while (!Application.isBatchMode && !UnityEngine.Rendering.SplashScreen.isFinished && Time.realtimeSinceStartupAsDouble - splash < 20) yield return null;
            if (!Application.isBatchMode && !UnityEngine.Rendering.SplashScreen.isFinished) { report.errors.Add("Foreground splash timeout; no allocation samples collected."); Finish("startup_timeout"); yield break; }
            report.outcome = "setting_up_real_source"; UnityEngine.Random.InitState(report.seed);
            var store = new ExpeditionStore(Path.Combine(RunStorage.Root, "v06-allocation-fixture"));
            for (int level = store.Profile.highestUnlocked; level < 11; level++) { string run = store.Begin(level); store.Settle(run, true, false, 30); }
            G.expeditionStore = store; G.qaSuppressAI = true;
            if (!G.BeginExpedition(11)) { report.errors.Add("Chapter-12 fixture failed: " + G.expeditionNotice); Finish("fixture_failed"); yield break; }
            G.mission?.Stop("allocation fixture"); G.ClearDeployments();
            foreach (var actor in G.enemies) if (actor) actor.gameObject.SetActive(false);
            source = EnemyActor.Spawn(EnemyKind.Elite, new Vector3(0, .03f, 38), G.stage);
            if (!source) { report.errors.Add("Real Elite source could not spawn."); Finish("fixture_failed"); yield break; }
            var boss = source.boss ? source.boss : source.EnableBoss();
            source.Damage(BossEncounter.ShieldMaximum + BossEncounter.HealthMaximum * .5f + 1);
            if (!boss.TryBeginSkill(BossSkill.OmniFire)) { report.errors.Add("Native OmniFire could not begin."); Finish("fixture_failed"); yield break; }
            G.qaSuppressAI = false; double deadline = Time.realtimeSinceStartupAsDouble + 5;
            while (boss.SkillStep != BossSkillStep.Execute && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            G.qaSuppressAI = true;
            if (boss.Skill != BossSkill.OmniFire || boss.SkillStep != BossSkillStep.Execute) { report.errors.Add("Real boss never entered the OmniFire Execute state."); Finish("fixture_failed"); yield break; }
            report.sourceState = source.kind + "/" + boss.Phase + "/" + boss.Skill + "/" + boss.SkillStep;
            BoltAllocationMeasurement.CountActiveBolts(G.effects, liveBolts);
            foreach (var bolt in liveBolts) if (bolt) bolt.Retire();
            yield return null;
            if (BoltAllocationMeasurement.CountActiveBolts(G.effects, liveBolts) != 0) { report.errors.Add("Setup projectiles were not actually cleaned before measurement."); Finish("fixture_cleanup_failed"); yield break; }
            yield return RetiredDamageProbe();
            if (!report.retiredDamageProbePassed) { Finish("retired_damage_probe_failed"); yield break; }
            // Remote coordinates prevent hits. Rendering cost is intentionally outside this synchronous allocation test.
            measurement = new BoltAllocationMeasurement(source, new Vector3(0, 1000, 0), report.batchSize, counter);
            report.notes.Add("Projectiles use the real OmniFire presentation branch and are spawned at (0,1000,0) to avoid hits; no rendered-frame or foreground performance claim is made.");
            UnityEngine.Random.InitState(report.seed);
            report.outcome = "warming_up";
            for (int batch = 0; batch < report.warmupBatches; batch++)
            {
                yield return MeasureBatch(report.warmups[batch]);
                if (batchFailed) { Finish("warmup_failed"); yield break; }
                report.completedWarmups++;
            }
            report.outcome = "measuring";
            for (int batch = 0; batch < report.measuredBatches; batch++)
            {
                yield return MeasureBatch(report.batches[batch]);
                if (batchFailed) { Finish("measurement_or_cleanup_failed"); yield break; }
                report.completedBatches++;
            }
            report.objectLifecycleMeasurementValid = report.errors.Count == 0 && report.completedWarmups == report.warmupBatches && report.completedBatches == report.measuredBatches;
            report.measurementValid = report.objectLifecycleMeasurementValid;
            Finish(report.measurementValid ? "allocation_measurement_complete" : "invalid_allocation_measurement");
        }
        IEnumerator MeasureBatch(Batch result)
        {
            batchFailed = false; result.attempted = true;
            result.liveBoltsBefore = BoltAllocationMeasurement.CountActiveBolts(G.effects, liveBolts);
            if (result.liveBoltsBefore != 0) { report.errors.Add("Unclean previous batch: active Bolts remain before index " + result.index); batchFailed = true; yield break; }
            try
            {
                var control = measurement.MeasureEmptyLoop(); var sample = measurement.MeasureSpawnBatch();
                result.synchronousWindowMeasured = true;
                result.controlManagedBytes = control.managedBytes; result.controlAllocationEvents = control.allocationEvents; result.controlHadProfilerSample = control.hadProfilerSample; result.controlCallMilliseconds = control.callMilliseconds;
                result.spawnManagedBytes = sample.managedBytes; result.spawnAllocationEvents = sample.allocationEvents; result.spawnHadProfilerSample = sample.hadProfilerSample; result.spawnCallMilliseconds = sample.callMilliseconds;
                result.productionCreated = sample.productionCreated;
                result.successfulSpawnCalls = sample.successfulSpawnCalls; result.distinctRootIdentities = sample.distinctRootIdentities; result.newlyObservedRootIdentities = sample.newlyObservedRootIdentities;
                result.activeRootsAfterSpawn = sample.activeRoots; result.observedObjectsAfterSpawn = sample.observedObjects;
                result.liveBoltsAfterSpawn = BoltAllocationMeasurement.CountActiveBolts(G.effects, liveBolts);
                if (sample.successfulSpawnCalls != report.batchSize || sample.distinctRootIdentities != report.batchSize || sample.activeRoots != report.batchSize || result.liveBoltsAfterSpawn != report.batchSize)
                    throw new InvalidOperationException("Actual returned/distinct/active projectile counts do not match the fixed batch size.");
            }
            catch (Exception error) { report.errors.Add("Batch " + result.index + ": " + error); batchFailed = true; }
            try { measurement.RequestCleanup(); }
            catch (Exception error) { report.errors.Add("Batch cleanup request " + result.index + ": " + error); batchFailed = true; }
            double cleanupStart = Time.realtimeSinceStartupAsDouble;
            yield return null; // Destroy is deferred. Always cross a native frame before declaring cleanup complete.
            var cleanup = measurement.ObserveCleanup();
            while (!cleanup.Complete && Time.realtimeSinceStartupAsDouble - cleanupStart < 3)
            { yield return null; cleanup = measurement.ObserveCleanup(); }
            result.cleanupWaitSeconds = Time.realtimeSinceStartupAsDouble - cleanupStart;
            if (cleanup.Complete)
            {
                measurement.CaptureRetiredPositions();
                result.playerHealthBeforeRetiredObservation = G.player.health; result.sourceHealthBeforeRetiredObservation = source.health;
                yield return null; yield return null;
                var motion = measurement.ObserveRetiredMotion(); cleanup = measurement.ObserveCleanup();
                result.movedRetiredRoots = motion.movedRoots; result.movedRetiredObjects = motion.movedObjects;
                result.retiredObjectsStable = cleanup.Complete && motion.movedRoots == 0 && motion.movedObjects == 0;
                result.playerHealthAfterRetiredObservation = G.player.health; result.sourceHealthAfterRetiredObservation = source.health;
                result.noDamageAfterCleanup = result.playerHealthAfterRetiredObservation == result.playerHealthBeforeRetiredObservation && result.sourceHealthAfterRetiredObservation == result.sourceHealthBeforeRetiredObservation;
            }
            result.productionRetired = cleanup.productionRetired;
            result.destroyedRoots = cleanup.destroyedRoots; result.retainedInactiveRoots = cleanup.retainedInactiveRoots; result.activeRootsAfterCleanup = cleanup.activeRoots;
            result.destroyedObjects = cleanup.destroyedObjects; result.retainedInactiveObjects = cleanup.retainedInactiveObjects; result.activeObjectsAfterCleanup = cleanup.activeObjects;
            result.liveBoltsAfterCleanup = BoltAllocationMeasurement.CountActiveBolts(G.effects, liveBolts);
            result.cleanupComplete = cleanup.Complete && result.liveBoltsAfterCleanup == 0 && result.retiredObjectsStable && result.noDamageAfterCleanup && result.productionRetired == result.successfulSpawnCalls;
            if (!result.cleanupComplete) { report.errors.Add("Batch " + result.index + " did not actually clear owned active objects."); batchFailed = true; }
        }
        IEnumerator RetiredDamageProbe()
        {
            // A live stray bolt would cross the player's collider within ~0.13 seconds.
            // Retire it through production code first, then observe native updates without direct damage injection.
            float health = G.player.health;
            Vector3 position = G.player.Eye.position + G.player.Eye.forward * 3;
            var bolt = Bolt.Spawn(source, position, -G.player.Eye.forward, 23, 5);
            var root = bolt.gameObject; bolt.Retire(); yield return null;
            Vector3 retiredPosition = root ? root.transform.position : position;
            yield return new WaitForSeconds(.3f);
            bool inactive = !root || !root.activeInHierarchy;
            bool stable = !root || (root.transform.position - retiredPosition).sqrMagnitude <= .000001f;
            report.retiredDamageProbePassed = inactive && stable && G.player.health == health && BoltAllocationMeasurement.CountActiveBolts(G.effects, liveBolts) == 0;
            report.retiredDamageProbeDetail = "start=" + position + ";destroyed=" + (!root) + ";inactive=" + inactive + ";stable=" + stable + ";health=" + health + " -> " + G.player.health;
            if (!report.retiredDamageProbePassed) report.errors.Add("Retired projectile retained activity, movement or damage during the native 0.3-second probe.");
        }
        void Summarize()
        {
            if (report.completedBatches == 0) return;
            long spawnBytes = 0, controlBytes = 0, spawnEvents = 0, controlEvents = 0; double synchronousMilliseconds = 0;
            report.totalProductionCreated = report.totalProductionRetired = 0;
            for (int i = 0; i < report.completedBatches; i++)
            {
                var b = report.batches[i];
                if (b.spawnManagedBytes >= 0) { spawnBytes += b.spawnManagedBytes; report.spawnByteSamples++; }
                if (b.controlManagedBytes >= 0) { controlBytes += b.controlManagedBytes; report.controlByteSamples++; }
                if (b.spawnAllocationEvents >= 0) { spawnEvents += b.spawnAllocationEvents; report.spawnEventSamples++; }
                if (b.controlAllocationEvents >= 0) { controlEvents += b.controlAllocationEvents; report.controlEventSamples++; }
                report.totalSuccessfulSpawnCalls += b.successfulSpawnCalls; report.totalNewlyObservedRoots += b.newlyObservedRootIdentities;
                report.totalProductionCreated += b.productionCreated; report.totalProductionRetired += b.productionRetired;
                report.totalDestroyedRoots += b.destroyedRoots; report.totalRetainedInactiveRoots += b.retainedInactiveRoots; synchronousMilliseconds += b.spawnCallMilliseconds;
            }
            report.byteMeasurementComplete = report.objectLifecycleMeasurementValid && report.counterAvailable && report.spawnByteSamples == report.measuredBatches;
            report.eventMeasurementComplete = report.objectLifecycleMeasurementValid && counter.Evidence.eventsVerified && report.spawnEventSamples == report.measuredBatches;
            if (report.byteMeasurementComplete) { report.totalSpawnManagedBytes = spawnBytes; report.meanManagedBytesPerSpawn = spawnBytes / (double)report.totalSuccessfulSpawnCalls; }
            if (report.controlByteSamples == report.measuredBatches) report.totalControlManagedBytes = controlBytes;
            if (report.eventMeasurementComplete) { report.totalSpawnAllocationEvents = spawnEvents; report.meanAllocationEventsPerSpawn = spawnEvents / (double)report.totalSuccessfulSpawnCalls; }
            if (report.controlEventSamples == report.measuredBatches) report.totalControlAllocationEvents = controlEvents;
            report.meanSynchronousSpawnCallMilliseconds = synchronousMilliseconds / report.completedBatches;
        }
        void Finish(string outcome)
        {
            if (done) return; done = true; report.outcome = outcome; report.utcFinished = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
            report.passed = report.measurementValid && report.errors.Count == 0; Summarize();
            if (report.objectLifecycleMeasurementValid) report.outcome = report.byteMeasurementComplete ? "synchronous_bytes_and_object_lifecycle_complete" : report.eventMeasurementComplete ? "allocation_events_and_object_lifecycle_bytes_missing" : "object_lifecycle_only_allocation_missing";
            counter?.Dispose();
            if (!report.measurementValid) report.notes.Add("This run did not produce a complete valid allocation measurement; partial values are diagnostics only.");
            File.WriteAllText(Path.Combine(output, "allocation-v06-report.json"), JsonUtility.ToJson(report, true));
            Debug.Log("ALLOCATION_V06_QA_RESULT=" + report.passed + ";stage=" + report.stage + ";batches=" + report.completedBatches);
            Application.Quit(report.passed ? 0 : 1);
        }
        void OnDestroy() { Application.logMessageReceived -= Error; counter?.Dispose(); }
    }
}
