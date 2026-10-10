using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace SwapHunter
{
    // Measurement harness only. It does not pool projectiles or set a desired improvement result.
    public sealed class PerformanceV06QA : MonoBehaviour
    {
        [Serializable] public sealed class Settings
        {
            public int requestedWidth, requestedHeight, actualWidth, actualHeight, cameraPixelWidth, cameraPixelHeight;
            public int targetFrameRate, vSyncCount, qualityIndex, msaaSamples;
            public float renderScale, timeScale, maximumDeltaTime, captureDeltaTime;
            public bool dynamicResolution, focused, osForeground, osForegroundAvailable;
            public string fullScreenMode;
        }
        [Serializable] public sealed class Distribution
        {
            public int samples;
            public double meanMs = -1, p50Ms = -1, p95Ms = -1, p99Ms = -1, worstMs = -1, averageFps = -1;
        }
        [Serializable] public sealed class AllocationMetric
        {
            public string name, scope, unit, unavailableReason;
            public bool available;
            public int samples;
            public long totalBytes = -1, peakFrameBytes = -1;
            public double meanFrameBytes = -1;
        }
        [Serializable] public sealed class Diagnostic
        {
            public bool requested, completed, profilerRecorderAvailable, threadBytesAvailable;
            public int boltsCreated, allocationEvents = -1;
            public long emptyLoopThreadBytes = -1, spawnThreadBytes = -1, recorderValue = -1;
            public double spawnThreadBytesPerBolt = -1;
            public string recorderUnit, limitation = "Post-sample synchronous Bolt.Spawn scope including its real presentation initialization; not all per-frame GC, native allocations or a call-stack profile. Empty-loop control is reported, not silently subtracted.";
        }
        [Serializable] public sealed class Report
        {
            public string kind = "Foreground chapter-12 sustained OmniFire baseline/after measurement; stationary view, forced boss phase and restored player health are explicit stress fixtures, not normal gameplay.";
            public string fixture = "v06-chapter12-omni-fixed-v1", stage, displayMode;
            public string version, unityVersion, buildGuid, assemblyMvid, assemblySha256, assemblyPath, sourceManifestSha256;
            public string graphicsDevice, graphicsApi, processor, utcStarted, utcFinished, outcome = "not_started";
            public int processorCount, systemMemoryMb, graphicsMemoryMb, seed, frames, liveBoltsAtEnd, peakLiveBolts, omniCasts, foregroundLossCount;
            public bool developmentBuild, editor, batchMode, measurementValid, passed;
            public bool cpuGpuBreakdownAvailable = false;
            public string cpuGpuLimitation = "Frame intervals use a real wall clock at consecutive Unity frame resumes. They include rendering, CPU work, waits and the configured cap; separate CPU/GPU execution times are not collected.";
            public string instrumentation = "Preallocated sample structs, no frame-loop CSV/string logging; root effects are inspected for active real Bolt components each frame. Working set and Unity allocated memory are polled every 0.5 seconds. Observer overhead is included and must be held constant between stages.";
            public string percentileMethod = "nearest rank: ceil(p * sampleCount) - 1";
            public float warmupSeconds = 8, requestedSampleSeconds = 30;
            public double elapsedSampleSeconds, focusFraction;
            public bool workingSetAvailable;
            public long initialWorkingSetBytes = -1, peakWorkingSetBytes = -1, finalWorkingSetBytes = -1, peakUnityAllocatedBytes = -1;
            public Settings settings = new Settings();
            public Distribution frameTime = new Distribution();
            public AllocationMetric frameGc = new AllocationMetric { name = "GC Allocated In Frame", scope = "Unity frame counter; not restricted to this component or explicitly to the main thread", unit = "Bytes" };
            public AllocationMetric mainThreadGc = new AllocationMetric { name = "GC.GetAllocatedBytesForCurrentThread delta", scope = "Managed allocations on the Unity main thread between sampler resumes, including gameplay and measurement overhead", unit = "Bytes" };
            public Diagnostic allocationDiagnostic = new Diagnostic();
            public List<string> notes = new List<string>();
            public List<string> errors = new List<string>();
        }
        struct Sample
        {
            public int frame, liveBolts, skill, skillStep;
            public double elapsed, wallMs;
            public float deltaTime;
            public bool focused, foreground;
            public long frameGcBytes, threadGcBytes, workingSetBytes, unityAllocatedBytes;
        }
        const int Capacity = 65536;
        readonly Report report = new Report();
        Sample[] samples;
        int count, processId;
        string output;
        bool done, threadAllocationAvailable, recorderOpened;
        double began, sampleStart, previousFrame, nextMemoryPoll;
        long previousThreadAllocated, lastWorkingSet = -1, lastUnityAllocated = -1;
        int focusCount, startCasts;
        ProfilerRecorder frameGcRecorder;
        EnemyActor fixtureBoss;
        UniversalRenderPipelineAsset pipeline;
        float diagnosticSink;
        DemoGame G => DemoGame.I;
        PlayerMotor P => G.player;

        [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr window, out uint ownerProcess);

        void Awake()
        {
            began = Time.realtimeSinceStartupAsDouble;
            report.utcStarted = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
            var args = Environment.GetCommandLineArgs(); output = Argument(args, "-qaOutput") ?? RunStorage.Root;
            Directory.CreateDirectory(output);
            report.stage = Argument(args, "-performanceStage") ?? "";
            report.displayMode = Argument(args, "-performanceDisplayMode") ?? "Foreground";
            report.seed = Integer(args, "-performanceSeed", 60031);
            report.settings.requestedWidth = Integer(args, "-performanceWidth", 1920);
            report.settings.requestedHeight = Integer(args, "-performanceHeight", 1080);
            report.settings.targetFrameRate = Integer(args, "-performanceCap", 120);
            report.settings.renderScale = Number(args, "-performanceRenderScale", .85f);
            report.allocationDiagnostic.requested = Array.IndexOf(args, "-performanceAllocationDiagnostic") >= 0;
            report.version = Application.version; report.unityVersion = Application.unityVersion; report.buildGuid = Application.buildGUID;
            report.developmentBuild = Debug.isDebugBuild; report.editor = Application.isEditor; report.batchMode = Application.isBatchMode;
            report.graphicsDevice = SystemInfo.graphicsDeviceName; report.graphicsApi = SystemInfo.graphicsDeviceType.ToString();
            report.processor = SystemInfo.processorType; report.processorCount = SystemInfo.processorCount;
            report.systemMemoryMb = SystemInfo.systemMemorySize; report.graphicsMemoryMb = SystemInfo.graphicsMemorySize;
            using (var process = System.Diagnostics.Process.GetCurrentProcess()) processId = process.Id;
            Identity(); Application.logMessageReceived += Error;
        }
        static string Argument(string[] args, string key)
        { int i = Array.IndexOf(args, key); return i >= 0 && i + 1 < args.Length ? args[i + 1] : null; }
        int Integer(string[] args, string key, int fallback)
        {
            string text = Argument(args, key); if (text == null) return fallback;
            if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value)) return value;
            report.errors.Add("Invalid integer argument: " + key); return fallback;
        }
        float Number(string[] args, string key, float fallback)
        {
            string text = Argument(args, key); if (text == null) return fallback;
            if (float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float value) && !float.IsNaN(value) && !float.IsInfinity(value)) return value;
            report.errors.Add("Invalid numeric argument: " + key); return fallback;
        }
        void Identity()
        {
            try
            {
                var assembly = typeof(PerformanceV06QA).Assembly; report.assemblyMvid = assembly.ManifestModule.ModuleVersionId.ToString();
                report.assemblyPath = assembly.Location;
                if (!string.IsNullOrEmpty(report.assemblyPath) && File.Exists(report.assemblyPath)) report.assemblySha256 = Hash(report.assemblyPath);
                else report.notes.Add("Managed assembly file unavailable; build GUID/version remain recorded.");
                string manifest = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "source-manifest.json"));
                if (File.Exists(manifest)) report.sourceManifestSha256 = Hash(manifest);
            }
            catch (Exception error) { report.notes.Add("Additional assembly identity unavailable: " + error.Message); }
        }
        static string Hash(string path)
        { using (var stream = File.OpenRead(path)) using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant(); }
        void Error(string message, string stack, LogType type)
        { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) report.errors.Add(message + "\n" + stack); }
        void Update()
        {
            if (!done && Time.realtimeSinceStartupAsDouble - began > 150)
            { report.errors.Add("150-second real-time watchdog; phase=" + report.outcome); Finish("watchdog"); }
        }
        bool Foreground(out bool available)
        {
            available = Application.platform == RuntimePlatform.WindowsPlayer || Application.platform == RuntimePlatform.WindowsEditor;
            if (!available) return Application.isFocused;
            GetWindowThreadProcessId(GetForegroundWindow(), out uint foregroundProcess);
            return foregroundProcess == (uint)processId;
        }
        bool Preconditions()
        {
            if (report.stage != "Baseline" && report.stage != "After") report.errors.Add("-performanceStage must explicitly be Baseline or After.");
            if (report.displayMode != "Foreground") report.errors.Add("Only DisplayMode Foreground can produce performance evidence.");
            if (Application.isBatchMode) report.errors.Add("Batch/background invocation rejected: not foreground performance evidence.");
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) report.errors.Add("No real GPU device: not rendering/performance evidence.");
            if (!RunStorage.IsValidation) report.errors.Add("Validation storage is not isolated.");
            if (Time.captureDeltaTime != 0 || Time.captureFramerate != 0) report.errors.Add("Fixed capture timing is enabled; real frame-rate measurement rejected.");
            if (report.settings.requestedWidth < 640 || report.settings.requestedHeight < 360 || report.settings.requestedWidth > 7680 || report.settings.requestedHeight > 4320) report.errors.Add("Requested resolution outside supported fixture range.");
            if (report.settings.targetFrameRate != -1 && (report.settings.targetFrameRate < 30 || report.settings.targetFrameRate > 360)) report.errors.Add("Frame cap must be -1 or between 30 and 360.");
            if (report.settings.renderScale < .5f || report.settings.renderScale > 1.5f) report.errors.Add("Render scale must be within 0.5..1.5.");
            return report.errors.Count == 0;
        }
        IEnumerator Start()
        {
            if (!Preconditions()) { Finish("rejected_not_performance_evidence"); yield break; }
            report.outcome = "waiting_for_splash";
            double splashStart = Time.realtimeSinceStartupAsDouble;
            while (!UnityEngine.Rendering.SplashScreen.isFinished && Time.realtimeSinceStartupAsDouble - splashStart < 20) yield return null;
            if (!UnityEngine.Rendering.SplashScreen.isFinished) { report.errors.Add("Foreground splash did not finish within 20 real seconds."); Finish("startup_timeout"); yield break; }
            if (!Application.isFocused || !Foreground(out _)) { report.errors.Add("Game is not the focused foreground process at startup; no samples collected."); Finish("rejected_not_performance_evidence"); yield break; }
            report.outcome = "setting_up";
            UnityEngine.Random.InitState(report.seed);
            var store = new ExpeditionStore(Path.Combine(RunStorage.Root, "v06-performance-fixture"));
            for (int level = store.Profile.highestUnlocked; level < 11; level++) { string run = store.Begin(level); store.Settle(run, true, false, 30); }
            G.expeditionStore = store; G.qaSuppressAI = true;
            if (!G.BeginExpedition(11)) { report.errors.Add("Chapter 12 setup failed: " + G.expeditionNotice); Finish("fixture_failed"); yield break; }
            // Keep the new authored chapter geometry, isolate native boss/projectile work from mission reinforcements.
            G.mission?.Stop("performance fixture"); G.ClearDeployments();
            foreach (var enemy in G.enemies) if (enemy) enemy.gameObject.SetActive(false);
            fixtureBoss = EnemyActor.Spawn(EnemyKind.Elite, new Vector3(0, .03f, 38), G.stage);
            if (!fixtureBoss) { report.errors.Add("Authored chapter-12 boss site was not a legal spawn."); Finish("fixture_failed"); yield break; }
            var encounter = fixtureBoss.boss ? fixtureBoss.boss : fixtureBoss.EnableBoss();
            fixtureBoss.Damage(BossEncounter.ShieldMaximum + BossEncounter.HealthMaximum * .5f + 1);
            if (encounter.Phase != BossPhase.Combined) { report.errors.Add("Native boss failed to enter combined phase."); Finish("fixture_failed"); yield break; }
            P.SetFeet(new Vector3(0, .04f, 28)); P.verticalSpeed = 0; P.SetLook(0, 0);
            G.options.dynamicResolution = false; G.options.quality = 1; G.options.renderScale = report.settings.renderScale;
            G.ApplySettings(false); QualitySettings.vSyncCount = 0; Application.targetFrameRate = report.settings.targetFrameRate;
            pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (!pipeline) { report.errors.Add("Expected URP asset is unavailable."); Finish("fixture_failed"); yield break; }
            pipeline.renderScale = report.settings.renderScale;
            Screen.SetResolution(report.settings.requestedWidth, report.settings.requestedHeight, FullScreenMode.Windowed);
            samples = new Sample[Capacity]; OpenCounters();
            G.qaSuppressAI = false; UnityEngine.Random.InitState(report.seed);
            KeepPressure(); report.outcome = "warming_up";
            double warmup = Time.realtimeSinceStartupAsDouble;
            while (Time.realtimeSinceStartupAsDouble - warmup < report.warmupSeconds)
            {
                if (!RuntimeConditions(false)) { Finish("invalid_foreground_or_timing"); yield break; }
                KeepPressure(); yield return null;
            }
            CaptureSettings();
            if (Screen.width != report.settings.requestedWidth || Screen.height != report.settings.requestedHeight)
            { report.errors.Add("Actual render resolution differs from the requested comparison resolution."); Finish("settings_not_applied"); yield break; }
            report.outcome = "sampling";
            sampleStart = previousFrame = Time.realtimeSinceStartupAsDouble; nextMemoryPoll = sampleStart;
            startCasts = encounter.SpatialCasts;
            if (threadAllocationAvailable) previousThreadAllocated = GC.GetAllocatedBytesForCurrentThread();
            while (Time.realtimeSinceStartupAsDouble - sampleStart < report.requestedSampleSeconds)
            {
                KeepPressure(); yield return null;
                double now = Time.realtimeSinceStartupAsDouble;
                if (!RuntimeConditions(true)) { Finish("invalid_foreground_or_timing"); yield break; }
                if (count == Capacity) { report.errors.Add("Preallocated sample buffer exhausted; no truncated result accepted."); Finish("sample_overflow"); yield break; }
                long frameBytes = frameGcRecorder.Valid && frameGcRecorder.Count > 0 ? frameGcRecorder.LastValue : -1;
                long threadBytes = -1;
                if (threadAllocationAvailable)
                { long allocated = GC.GetAllocatedBytesForCurrentThread(); threadBytes = allocated - previousThreadAllocated; previousThreadAllocated = allocated; }
                if (now >= nextMemoryPoll)
                {
                    nextMemoryPoll = now + .5; lastWorkingSet = RuntimeMemory.WorkingSet(); lastUnityAllocated = Profiler.GetTotalAllocatedMemoryLong();
                    if (lastWorkingSet > 0)
                    {
                        report.workingSetAvailable = true;
                        if (report.initialWorkingSetBytes < 0) report.initialWorkingSetBytes = lastWorkingSet;
                        report.peakWorkingSetBytes = Math.Max(report.peakWorkingSetBytes, lastWorkingSet);
                    }
                    report.peakUnityAllocatedBytes = Math.Max(report.peakUnityAllocatedBytes, lastUnityAllocated);
                }
                int bolts = CountLiveBolts(); report.peakLiveBolts = Math.Max(report.peakLiveBolts, bolts); focusCount++;
                samples[count] = new Sample { frame = Time.frameCount, elapsed = now - sampleStart, wallMs = (now - previousFrame) * 1000,
                    deltaTime = Time.deltaTime, liveBolts = bolts, skill = (int)encounter.Skill, skillStep = (int)encounter.SkillStep,
                    focused = Application.isFocused, foreground = Foreground(out _), frameGcBytes = frameBytes, threadGcBytes = threadBytes,
                    workingSetBytes = lastWorkingSet, unityAllocatedBytes = lastUnityAllocated };
                previousFrame = now; count++;
            }
            report.elapsedSampleSeconds = previousFrame - sampleStart; report.omniCasts = encounter.SpatialCasts - startCasts;
            report.liveBoltsAtEnd = CountLiveBolts(); report.finalWorkingSetBytes = RuntimeMemory.WorkingSet(); CloseCounters();
            if (report.omniCasts < 6 || report.peakLiveBolts == 0) report.errors.Add("Expected sustained native OmniFire pressure was not observed.");
            if (report.errors.Count == 0 && report.allocationDiagnostic.requested) RunAllocationDiagnostic();
            report.measurementValid = report.errors.Count == 0 && count > 0 && report.elapsedSampleSeconds >= report.requestedSampleSeconds;
            Finish(report.measurementValid ? "measurement_complete" : "invalid_measurement");
        }
        bool RuntimeConditions(bool requireStableSettings)
        {
            if (!Application.isFocused || !Foreground(out _)) { report.foregroundLossCount++; report.errors.Add("Focus/foreground lost. Partial samples are not accepted as performance evidence."); return false; }
            if (!G.IsPlaying || !Mathf.Approximately(Time.timeScale, 1) || Time.captureDeltaTime != 0 || Time.captureFramerate != 0)
            { report.errors.Add("Gameplay clock or state changed; no fixed-dt or paused FPS evidence is accepted."); return false; }
            if (requireStableSettings && (Screen.width != report.settings.actualWidth || Screen.height != report.settings.actualHeight || Application.targetFrameRate != report.settings.targetFrameRate || QualitySettings.vSyncCount != report.settings.vSyncCount || !Mathf.Approximately(pipeline.renderScale, report.settings.renderScale) || G.options.dynamicResolution))
            { report.errors.Add("Resolution/render scale/cap/vSync changed during sampling; comparison invalid."); return false; }
            return true;
        }
        void CaptureSettings()
        {
            var settings = report.settings; settings.actualWidth = Screen.width; settings.actualHeight = Screen.height;
            settings.cameraPixelWidth = P.cameraView.pixelWidth; settings.cameraPixelHeight = P.cameraView.pixelHeight;
            settings.renderScale = pipeline.renderScale; settings.vSyncCount = QualitySettings.vSyncCount;
            settings.targetFrameRate = Application.targetFrameRate; settings.qualityIndex = G.quality; settings.msaaSamples = pipeline.msaaSampleCount;
            settings.dynamicResolution = G.options.dynamicResolution; settings.fullScreenMode = Screen.fullScreenMode.ToString();
            settings.timeScale = Time.timeScale; settings.captureDeltaTime = Time.captureDeltaTime; settings.maximumDeltaTime = Time.maximumDeltaTime;
            settings.focused = Application.isFocused; settings.osForeground = Foreground(out bool available); settings.osForegroundAvailable = available;
        }
        void KeepPressure()
        {
            if (!fixtureBoss || !fixtureBoss.Alive) return;
            P.health = G.config.playerHealth;
            var encounter = fixtureBoss.boss;
            if (encounter.SkillStep != BossSkillStep.Execute && encounter.SkillStep != BossSkillStep.Telegraph) encounter.TryBeginSkill(BossSkill.OmniFire);
        }
        int CountLiveBolts()
        {
            int live = 0;
            for (int i = 0; i < G.effects.childCount; i++)
            { var child = G.effects.GetChild(i); var bolt = child.GetComponent<Bolt>(); if (bolt && bolt.isActiveAndEnabled) live++; }
            return live;
        }
        void OpenCounters()
        {
            try
            {
                long before = GC.GetAllocatedBytesForCurrentThread(); var positive = new byte[4096]; positive[0] = 1;
                long observed = GC.GetAllocatedBytesForCurrentThread() - before; GC.KeepAlive(positive);
                threadAllocationAvailable = observed >= 4096; report.mainThreadGc.available = threadAllocationAvailable;
                if (threadAllocationAvailable) previousThreadAllocated = GC.GetAllocatedBytesForCurrentThread();
                else report.mainThreadGc.unavailableReason = "Known 4096-byte positive control was not observed (delta=" + observed + "); this metric and its diagnostic bytes remain unavailable, not zero.";
                report.notes.Add("Main-thread monotonic allocation counter positive control observed " + observed + " bytes.");
            }
            catch (Exception error) { threadAllocationAvailable = false; report.mainThreadGc.available = false; report.mainThreadGc.unavailableReason = error.Message; }
            try
            {
                var handles = new List<ProfilerRecorderHandle>(); ProfilerRecorderHandle.GetAvailable(handles);
                foreach (var handle in handles)
                {
                    var description = ProfilerRecorderHandle.GetDescription(handle);
                    if (description.Name != "GC Allocated In Frame") continue;
                    report.frameGc.unit = description.UnitType.ToString();
                    if (description.UnitType != ProfilerMarkerDataUnit.Bytes) { report.frameGc.unavailableReason = "Counter unit is not Bytes."; return; }
                    frameGcRecorder = new ProfilerRecorder(handle, 1, ProfilerRecorderOptions.Default | ProfilerRecorderOptions.StartImmediately);
                    recorderOpened = true; report.frameGc.available = frameGcRecorder.Valid; break;
                }
                if (!report.frameGc.available) report.frameGc.unavailableReason = "GC Allocated In Frame counter unavailable in this build/runtime.";
            }
            catch (Exception error) { report.frameGc.available = false; report.frameGc.unavailableReason = error.Message; }
        }
        void CloseCounters() { if (recorderOpened) { frameGcRecorder.Dispose(); recorderOpened = false; } }
        void RunAllocationDiagnostic()
        {
            report.outcome = "allocation_diagnostic"; fixtureBoss.suppressAI = true;
            const int batch = 64; var bolts = new Bolt[batch]; var directions = new Vector3[batch];
            for (int i = 0; i < batch; i++) { float a = i * Mathf.PI * 2 / batch; directions[i] = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)); }
            var diagnostic = report.allocationDiagnostic; diagnostic.threadBytesAvailable = threadAllocationAvailable;
            ProfilerRecorder allocationRecorder = default; bool opened = false;
            try
            {
                var handles = new List<ProfilerRecorderHandle>(); ProfilerRecorderHandle.GetAvailable(handles);
                foreach (var handle in handles)
                {
                    var description = ProfilerRecorderHandle.GetDescription(handle);
                    if (description.Name != "GC.Alloc") continue;
                    diagnostic.recorderUnit = description.UnitType.ToString();
                    allocationRecorder = new ProfilerRecorder(handle, 1, ProfilerRecorderOptions.WrapAroundWhenCapacityReached | ProfilerRecorderOptions.SumAllSamplesInFrame | ProfilerRecorderOptions.CollectOnlyOnCurrentThread);
                    opened = true; diagnostic.profilerRecorderAvailable = allocationRecorder.Valid; break;
                }
                long before = threadAllocationAvailable ? GC.GetAllocatedBytesForCurrentThread() : 0;
                for (int i = 0; i < batch; i++) diagnosticSink += directions[i].sqrMagnitude;
                if (threadAllocationAvailable) diagnostic.emptyLoopThreadBytes = GC.GetAllocatedBytesForCurrentThread() - before;
                if (diagnostic.profilerRecorderAvailable) { allocationRecorder.Reset(); allocationRecorder.Start(); }
                before = threadAllocationAvailable ? GC.GetAllocatedBytesForCurrentThread() : 0;
                for (int i = 0; i < batch; i++) bolts[i] = Bolt.Spawn(fixtureBoss, fixtureBoss.AimPoint + Vector3.up * .25f, directions[i], 23, 5);
                if (threadAllocationAvailable) diagnostic.spawnThreadBytes = GC.GetAllocatedBytesForCurrentThread() - before;
                if (diagnostic.profilerRecorderAvailable)
                {
                    allocationRecorder.Stop();
                    if (allocationRecorder.Count > 0)
                    { var sample = allocationRecorder.GetSample(0); diagnostic.recorderValue = sample.Value; diagnostic.allocationEvents = (int)sample.Count; }
                    else report.notes.Add("GC.Alloc recorder was valid but yielded no diagnostic sample; no zero-allocation claim made.");
                }
                foreach (var bolt in bolts) if (bolt) diagnostic.boltsCreated++;
                if (threadAllocationAvailable) diagnostic.spawnThreadBytesPerBolt = diagnostic.spawnThreadBytes / (double)batch;
                diagnostic.completed = diagnostic.boltsCreated == batch;
                if (!diagnostic.profilerRecorderAvailable) report.notes.Add("GC.Alloc main-thread ProfilerRecorder unavailable; inspect thread-byte diagnostic if available, or use Unity Profiler allocation call stacks.");
            }
            catch (Exception error) { report.notes.Add("Allocation diagnostic unavailable: " + error.Message); }
            finally
            {
                if (opened) allocationRecorder.Dispose();
                foreach (var bolt in bolts) if (bolt) bolt.Retire();
            }
        }
        void Summarize()
        {
            report.frames = count; report.focusFraction = count == 0 ? 0 : focusCount / (double)count;
            if (count == 0) return;
            report.elapsedSampleSeconds = samples[count - 1].elapsed;
            var times = new double[count]; double sum = 0;
            for (int i = 0; i < count; i++) { times[i] = samples[i].wallMs; sum += times[i]; Accumulate(report.frameGc, samples[i].frameGcBytes); Accumulate(report.mainThreadGc, samples[i].threadGcBytes); }
            Array.Sort(times); report.frameTime.samples = count; report.frameTime.meanMs = sum / count;
            report.frameTime.averageFps = count / report.elapsedSampleSeconds; report.frameTime.p50Ms = Percentile(times, .5); report.frameTime.p95Ms = Percentile(times, .95); report.frameTime.p99Ms = Percentile(times, .99); report.frameTime.worstMs = times[count - 1];
            FinalizeMetric(report.frameGc); FinalizeMetric(report.mainThreadGc);
        }
        static double Percentile(double[] values, double percentile) => values[Math.Max(0, Math.Min(values.Length - 1, (int)Math.Ceiling(values.Length * percentile) - 1))];
        static void Accumulate(AllocationMetric metric, long bytes)
        { if (bytes < 0) return; if (metric.samples == 0) metric.totalBytes = 0; metric.totalBytes += bytes; metric.peakFrameBytes = Math.Max(metric.peakFrameBytes, bytes); metric.samples++; }
        static void FinalizeMetric(AllocationMetric metric)
        {
            if (metric.samples > 0) { metric.available = true; metric.meanFrameBytes = metric.totalBytes / (double)metric.samples; }
            else { metric.available = false; if (string.IsNullOrEmpty(metric.unavailableReason)) metric.unavailableReason = "No supported allocation samples were collected."; }
        }
        void WriteSamples()
        {
            using (var writer = new StreamWriter(Path.Combine(output, "performance-v06-frames.csv")))
            {
                writer.WriteLine("frame,elapsed_seconds,wall_frame_ms,engine_delta_seconds,unity_focused,os_foreground,live_bolts,boss_skill,boss_step,gc_frame_bytes,main_thread_allocated_bytes,working_set_bytes,unity_allocated_bytes");
                for (int i = 0; i < count; i++)
                {
                    var s = samples[i]; writer.WriteLine(string.Join(",", s.frame.ToString(CultureInfo.InvariantCulture), s.elapsed.ToString("F6", CultureInfo.InvariantCulture), s.wallMs.ToString("F6", CultureInfo.InvariantCulture), s.deltaTime.ToString("F6", CultureInfo.InvariantCulture), s.focused ? "1" : "0", s.foreground ? "1" : "0", s.liveBolts.ToString(CultureInfo.InvariantCulture), ((BossSkill)s.skill).ToString(), ((BossSkillStep)s.skillStep).ToString(), s.frameGcBytes.ToString(CultureInfo.InvariantCulture), s.threadGcBytes.ToString(CultureInfo.InvariantCulture), s.workingSetBytes.ToString(CultureInfo.InvariantCulture), s.unityAllocatedBytes.ToString(CultureInfo.InvariantCulture)));
                }
            }
        }
        void Finish(string outcome)
        {
            if (done) return; done = true; CloseCounters(); Summarize();
            report.outcome = outcome; report.utcFinished = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
            report.passed = report.measurementValid && report.errors.Count == 0;
            if (!report.measurementValid) report.notes.Add("This run is not valid performance evidence. Partial distributions, if present, are diagnostic only.");
            WriteSamples(); File.WriteAllText(Path.Combine(output, "performance-v06-report.json"), JsonUtility.ToJson(report, true));
            Debug.Log("PERFORMANCE_V06_QA_RESULT=" + report.passed + ";stage=" + report.stage + ";outcome=" + outcome + ";frames=" + count);
            Application.Quit(report.passed ? 0 : 1);
        }
        void OnDestroy() { Application.logMessageReceived -= Error; CloseCounters(); }
    }
}
