using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using UnityEngine;

namespace SwapHunter
{
    // Opt-in native-player evidence, not player input or a human listening/playtest verdict.
    // Attach only from DemoGame's exclusive -swapHunterAVReview startup branch.
    public sealed class AVReview : MonoBehaviour
    {
        [Serializable] public sealed class Event
        {
            public string name, detail, screenshot;
            public float elapsed; public double dspTime; public int sampleFrame, activeVoices;
        }
        [Serializable] public sealed class MotionFrame
        {
            public string sequence, file;
            public float elapsed, reloadRemaining;
            public int sampleFrame;
        }
        [Serializable] public sealed class Report
        {
            public string kind = "Scripted native game / listener DSP capture. Not human playtesting or subjective audio approval.";
            public string capturePoint = "Passive OnAudioFilterRead on existing player AudioListener; no AudioSource or second listener added.";
            public string onAudioFilterReadDocs = "https://docs.unity3d.com/6000.0/Documentation/ScriptReference/MonoBehaviour.OnAudioFilterRead.html";
            public string listenerDocs = "https://docs.unity3d.com/6000.0/Documentation/Manual/class-AudioListener.html";
            public string unity, device, speakerMode, outputDirectory, floatWave, auditionWave;
            public int sampleRate, channels, sampleFrames, callbacks, maximumEventVoices, eventVoiceLimit, nonFiniteSamples, overUnitySamples, nearFullScaleSamples;
            public int capturedWidth, capturedHeight, bufferBytes;
            public float seconds, peak, rms, dcOffset, initialMasterVolume, stressMasterVolume;
            public bool captureAvailable, formatChanged, capacityReached, scriptCompleted, passed, settingsRestored, checkpointRestored;
            public List<Event> events = new List<Event>();
            public List<MotionFrame> motionFrames = new List<MotionFrame>();
            public string motionNote = "Consecutive native rendered frames with actual timestamps. Synchronous PNG readback has overhead; this is animation evidence, not an FPS benchmark. No interpolation or invented frames.";
            public List<string> errors = new List<string>();
            public string limitation = "DSP tap is not a microphone/device loopback recording; OS volume, speaker distortion and listener filter ordering are outside this evidence. PCM16 audition copy clamps out-of-range values; float WAV and report retain measured peaks.";
        }
        readonly Report report = new Report();
        AVListenerCapture tap; GameOptions savedOptions; UnityEngine.Random.State savedRandom;
        bool hadCheckpoint, savedSuppressAI, savedInput, restored, finishing, optionsSaved;
        int checkpoint, activeMotionCaptures; float born, timelineStart; string output;
        DemoGame G => DemoGame.I;
        PlayerMotor P => G.player;

        void Awake() { born = Time.realtimeSinceStartup; Application.logMessageReceived += OnLog; }
        void OnLog(string message, string stack, LogType type)
        {
            if ((type == LogType.Error || type == LogType.Exception || type == LogType.Assert) && report.errors.Count < 80)
                report.errors.Add(type + ": " + message);
        }
        void Update()
        {
            if (G && G.sound) report.maximumEventVoices = Mathf.Max(report.maximumEventVoices, G.sound.ActiveVoiceCount);
            if (!finishing && Time.realtimeSinceStartup - born > 60)
            {
                report.errors.Add("60-second script watchdog reached; partial evidence exported.");
                StopAllCoroutines(); StartCoroutine(Finish(false));
            }
        }
        IEnumerator Start()
        {
            string[] args = Environment.GetCommandLineArgs();
            if (Array.IndexOf(args, "-swapHunterAVReview") < 0) { Destroy(this); yield break; }
            int at = Array.IndexOf(args, "-qaOutput");
            string root = at >= 0 && at + 1 < args.Length ? args[at + 1] : RunStorage.Root;
            output = Path.Combine(Path.GetFullPath(root), "av-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N").Substring(0, 8));
            Directory.CreateDirectory(output); report.outputDirectory = output;
            if (!G.qaMode || G.campaignActive || GetComponent<DemoQA>() || GetComponent<CampaignQA>() || GetComponent<DemoBenchmark>())
            {
                report.errors.Add("AV review requires isolated qaMode and an exclusive startup branch.");
                yield return Finish(false); yield break;
            }
            savedOptions = G.options.Clone(); savedRandom = UnityEngine.Random.state;
            hadCheckpoint = RunStorage.HasCheckpoint; checkpoint = RunStorage.Checkpoint;
            savedSuppressAI = G.qaSuppressAI; savedInput = G.qaInputEnabled; optionsSaved = true;
            G.qaSuppressAI = true; G.qaInputEnabled = false;
            G.options = new GameOptions(); G.options.volume = .7f; G.options.effectsVolume = .85f; G.options.feedbackVolume = .75f;
            G.options.quality = 2; G.options.vSync = false; G.options.frameLimit = 1;
            G.SyncLegacyBindings(); G.ApplySettings(false); UnityEngine.Random.InitState(36127);
            report.initialMasterVolume = G.options.volume; report.unity = Application.unityVersion;
            report.device = SystemInfo.graphicsDeviceName; report.capturedWidth = Screen.width; report.capturedHeight = Screen.height;
            while (!UnityEngine.Rendering.SplashScreen.isFinished) yield return null;
            yield return new WaitForSecondsRealtime(.25f);
            var listeners = FindObjectsByType<AudioListener>(FindObjectsSortMode.None);
            int enabledCount = 0; foreach (var l in listeners) if (l.enabled && l.gameObject.activeInHierarchy) enabledCount++;
            var listener = P.Eye.GetComponent<AudioListener>();
            if (!listener || !listener.enabled || enabledCount != 1)
            { report.errors.Add("Expected exactly one enabled player AudioListener; found " + enabledCount); yield return Finish(false); yield break; }
            var config = AudioSettings.GetConfiguration(); report.sampleRate = config.sampleRate; report.speakerMode = config.speakerMode.ToString();
            report.channels = ChannelCount(config.speakerMode);
            if (report.sampleRate < 8000 || report.sampleRate > 192000 || report.channels < 1)
            { report.errors.Add("Unsupported audio device configuration."); yield return Finish(false); yield break; }
            tap = listener.gameObject.AddComponent<AVListenerCapture>();
            tap.Prepare(report.sampleRate, report.channels, 60); report.bufferBytes = tap.Capacity * sizeof(float);
            timelineStart = Time.realtimeSinceStartup; tap.Begin();
            report.eventVoiceLimit = G.sound.VoiceLimit;
            G.sound.ExportReview(Path.Combine(output, "isolated-clips"));
            G.StartRun(true); G.EnterStage(0, true); G.qaSuppressAI = true;
            yield return new WaitForSecondsRealtime(.4f);
            P.SetFeet(new Vector3(0, .03f, 3)); var target = G.enemies[0]; target.health = target.maximumHealth = 10000;
            Aim(target.AimPoint); Mark("carbine_single_start", "Real PlayerMotor.Fire, target health increased only in this isolated fixture.");
            for (int i = 0; i < 3; i++)
            { Aim(target.AimPoint); Require(P.Fire(), "carbine single shot rejected"); if (i == 0) yield return Frame("01-carbine-impact"); yield return new WaitForSecondsRealtime(.68f); }
            Mark("carbine_burst_start", "Eight real fire calls at legal weapon cadence.");
            for (int i = 0; i < 8; i++) { Aim(target.AimPoint); Require(P.Fire(), "carbine burst shot rejected"); yield return new WaitForSecondsRealtime(.145f); }
            yield return Frame("02-carbine-burst"); yield return new WaitForSecondsRealtime(.45f);
            P.ammo[0] = 4; Require(P.Reload(), "reload rejected"); Mark("reload_start", "Real reload animation and phased reload_out/in/bolt audio.");
            float duration = G.config.rifleReload;
            StartCoroutine(CaptureMotion("reload", duration + .15f));
            yield return new WaitForSecondsRealtime(duration * .20f); yield return Frame("03-reload-out");
            yield return new WaitForSecondsRealtime(duration * .30f); yield return Frame("04-reload-in-motion");
            yield return new WaitForSecondsRealtime(duration * .30f); yield return Frame("04b-reload-insert");
            yield return new WaitForSecondsRealtime(duration * .30f); yield return Frame("05-reload-complete");

            G.EnterStage(0, true); yield return new WaitForSecondsRealtime(.25f); P.SetFeet(new Vector3(0, .03f, 1));
            target = G.enemies[0]; target.health = target.maximumHealth = 500;
            var left = EnemyActor.Spawn(EnemyKind.Assault, new Vector3(-.8f, 0, 13), 0);
            var right = EnemyActor.Spawn(EnemyKind.Assault, new Vector3(.8f, 0, 13), 0);
            left.health = left.maximumHealth = right.health = right.maximumHealth = 500;
            P.SwitchWeapon(1); yield return new WaitForSecondsRealtime(.3f);
            Mark("shotgun_cluster_start", "Three native enemies; damage distribution measured, no claim that every pellet hits.");
            for (int i = 0; i < 3; i++)
            {
                Aim(new Vector3(i == 1 ? -.4f : .4f, 1.1f, 13)); Require(P.Fire(), "shotgun shot rejected");
                if (i == 0) yield return Frame("06-shotgun-cluster"); yield return new WaitForSecondsRealtime(.85f);
            }
            Mark("shotgun_cluster_result", "damage center=" + (500-target.health) + ", left=" + (500-left.health) + ", right=" + (500-right.health));
            target.health = 1; P.SetFeet(new Vector3(0, .03f, 10)); Aim(target.AimPoint); Require(P.Fire(), "kill shot rejected");
            StartCoroutine(CaptureMotion("death", .64f));
            yield return new WaitForSecondsRealtime(.18f); yield return Frame("07-death-motion");
            Mark("shotgun_kill", "Target alive after final shot=" + target.Alive); Require(!target.Alive, "demonstration kill missed");
            yield return new WaitForSecondsRealtime(.5f); yield return Frame("07b-death-rest");

            G.EnterStage(2, true); yield return new WaitForSecondsRealtime(.3f);
            target = G.enemies.Find(e => e.kind == EnemyKind.Shield); P.SetFeet(new Vector3(2, .03f, 81)); Aim(target.AimPoint);
            yield return new WaitForSecondsRealtime(.2f); float hp = target.health; Mark("shield_start", "Real frontal shield collision.");
            Require(P.Fire(), "shield shot rejected"); yield return Frame("08-shield-impact");
            Require(target.health == hp, "front shield shot unexpectedly damaged actor");
            yield return new WaitForSecondsRealtime(.4f); P.SetLook(90, 0); Mark("environment_start", "Real horizontal shot toward world wall.");
            Require(Physics.Raycast(P.Eye.position, P.Eye.forward, 65, Layers.WorldMask), "environment wall not in line of fire");
            Require(P.Fire(), "environment shot rejected"); yield return Frame("09-environment-impact"); yield return new WaitForSecondsRealtime(.5f);

            G.EnterStage(1, true); yield return new WaitForSecondsRealtime(.4f);
            target = G.enemies.Find(e => e.kind == EnemyKind.Sniper); Aim(target.AimPoint); P.cooldown = 0;
            Mark("swap_start", P.ValidateSwap(target)); Require(P.RequestSwap(target), "high-platform swap rejected");
            yield return new WaitForSecondsRealtime(.14f); yield return Frame("10-swap-arrival");
            Aim(target.AimPoint); yield return new WaitForSecondsRealtime(.35f); yield return Frame("11-high-platform-view");

            // This is deliberately synthetic voice stress, separately labelled from gameplay examples.
            G.options.volume = G.options.effectsVolume = G.options.feedbackVolume = 1; G.ApplySettings(false);
            report.stressMasterVolume = 1; Mark("synthetic_voice_stress_start", "SoundBank real sources at maximum user volume; explicitly not a gameplay scene.");
            string[] stress = { "rifle", "shotgun", "explosion", "phase", "death", "shield", "impact", "kill", "warning", "reload_bolt", "player_hit", "surface" };
            for (int n = 0; n < 64; n++)
            {
                string id = stress[n % stress.Length];
                G.sound.Play(id, P.Eye.position + P.Eye.forward * 1.5f + P.Eye.right * Mathf.Sin(n) * .7f);
                report.maximumEventVoices = Mathf.Max(report.maximumEventVoices, G.sound.ActiveVoiceCount);
                yield return new WaitForSecondsRealtime(.012f);
            }
            Mark("synthetic_voice_stress_end", "Observed event voice maximum=" + report.maximumEventVoices);
            Require(report.maximumEventVoices <= report.eventVoiceLimit, "event voice cap exceeded");
            Require(report.maximumEventVoices >= 12, "stress did not exercise substantial polyphony");
            yield return new WaitForSecondsRealtime(1.2f); yield return Finish(true);
        }
        static int ChannelCount(AudioSpeakerMode mode)
        {
            switch (mode) { case AudioSpeakerMode.Mono: return 1; case AudioSpeakerMode.Stereo: return 2; case AudioSpeakerMode.Quad: return 4;
                case AudioSpeakerMode.Surround: return 5; case AudioSpeakerMode.Mode5point1: return 6; case AudioSpeakerMode.Mode7point1: return 8; default: return 0; }
        }
        void Aim(Vector3 point) { Vector3 d = (point - P.Eye.position).normalized; P.SetLook(Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg, -Mathf.Asin(d.y) * Mathf.Rad2Deg); }
        void Require(bool ok, string message) { if (!ok) report.errors.Add(message); }
        void Mark(string name, string detail, string image = null)
        {
            report.events.Add(new Event { name = name, detail = detail, screenshot = image, elapsed = Time.realtimeSinceStartup - timelineStart,
                dspTime = AudioSettings.dspTime, sampleFrame = tap ? tap.Written / Math.Max(1, report.channels) : 0, activeVoices = G.sound.ActiveVoiceCount });
        }
        IEnumerator Frame(string name)
        {
            yield return new WaitForEndOfFrame();
            string path = Path.Combine(output, name + ".png"); ScreenCapture.CaptureScreenshot(path); Mark(name, "Native rendered screenshot requested at this audio sample position.", name + ".png");
            yield return null;
        }
        IEnumerator CaptureMotion(string name, float seconds)
        {
            activeMotionCaptures++;
            string folder = Path.Combine(output, name); Directory.CreateDirectory(folder);
            int count = 0; float end = Time.realtimeSinceStartup + seconds;
            try
            {
                while (Time.realtimeSinceStartup < end && count < 96)
                {
                    yield return new WaitForEndOfFrame();
                    string relative = name + "/frame-" + count.ToString("D3") + ".png";
                    var frame = new MotionFrame { sequence = name, file = relative, elapsed = Time.realtimeSinceStartup - timelineStart,
                        sampleFrame = tap.Written / Math.Max(1, report.channels), reloadRemaining = P.reloadLeft };
                    Texture2D texture = ScreenCapture.CaptureScreenshotAsTexture();
                    try { File.WriteAllBytes(Path.Combine(output, relative), texture.EncodeToPNG()); }
                    finally { Destroy(texture); }
                    report.motionFrames.Add(frame); count++;
                }
                Require(count >= (name == "reload" ? 8 : 5), "Insufficient consecutive frames for " + name + ": " + count);
            }
            finally { activeMotionCaptures--; }
        }
        IEnumerator Finish(bool completed)
        {
            if (finishing) yield break; finishing = true; report.scriptCompleted = completed;
            float captureDeadline = Time.realtimeSinceStartup + 2;
            while (activeMotionCaptures > 0 && Time.realtimeSinceStartup < captureDeadline) yield return null;
            Require(activeMotionCaptures == 0, "Motion capture did not finish before export");
            if (tap)
            {
                tap.Stop();
                // Stop publication is followed by draining an in-progress callback; never block the audio thread.
                float until = Time.realtimeSinceStartup + 2;
                while (tap.Writers != 0 && Time.realtimeSinceStartup < until) yield return null;
                if (tap.Writers != 0) report.errors.Add("Audio callback did not quiesce; buffer intentionally not exported.");
                else
                {
                    report.callbacks = tap.Callbacks; report.formatChanged = tap.FormatChanged; report.capacityReached = tap.CapacityReached;
                    report.sampleFrames = tap.Written / Math.Max(1, report.channels); report.seconds = report.sampleFrames / (float)Math.Max(1, report.sampleRate);
                    report.captureAvailable = report.callbacks > 0 && report.sampleFrames > 0;
                    if (!report.captureAvailable) report.errors.Add("No listener DSP data. Run native desktop player with an enabled audio device, without -noaudio.");
                    else ExportAudio(tap.Buffer, tap.Written);
                }
            }
            Restore();
            yield return new WaitForSecondsRealtime(.3f);
            foreach (var e in report.events) if (!string.IsNullOrEmpty(e.screenshot) && !File.Exists(Path.Combine(output, e.screenshot))) report.errors.Add("Missing screenshot: " + e.screenshot);
            report.passed = completed && report.captureAvailable && !report.capacityReached && !report.formatChanged && report.nonFiniteSamples == 0 && report.overUnitySamples == 0
                && report.peak > .0001f && report.errors.Count == 0 && report.settingsRestored && report.checkpointRestored;
            if (!string.IsNullOrEmpty(output)) File.WriteAllText(Path.Combine(output, "av-review.json"), JsonUtility.ToJson(report, true));
            Debug.Log("AV_REVIEW_OUTPUT " + output);
            Application.Quit(report.passed ? 0 : 1);
        }
        void ExportAudio(float[] samples, int count)
        {
            double sum = 0, power = 0;
            for (int index = 0; index < count; index++)
            {
                float v = samples[index]; if (float.IsNaN(v) || float.IsInfinity(v)) { report.nonFiniteSamples++; continue; }
                float abs = Math.Abs(v); report.peak = Math.Max(report.peak, abs); if (abs > 1) report.overUnitySamples++; if (abs >= .999f) report.nearFullScaleSamples++;
                sum += v; power += (double)v * v;
            }
            report.rms = (float)Math.Sqrt(power / Math.Max(1, count)); report.dcOffset = (float)(sum / Math.Max(1, count));
            report.floatWave = "listener-mix-float.wav"; report.auditionWave = "listener-mix-pcm16.wav";
            WriteWave(Path.Combine(output, report.floatWave), samples, count, true);
            WriteWave(Path.Combine(output, report.auditionWave), samples, count, false);
        }
        void WriteWave(string path, float[] samples, int count, bool floating)
        {
            int width = floating ? 4 : 2;
            using (var writer = new BinaryWriter(new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read)))
            {
                writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + count * width);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16); writer.Write((short)(floating ? 3 : 1)); writer.Write((short)report.channels);
                writer.Write(report.sampleRate); writer.Write(report.sampleRate * report.channels * width); writer.Write((short)(report.channels * width)); writer.Write((short)(width * 8));
                writer.Write(System.Text.Encoding.ASCII.GetBytes("data")); writer.Write(count * width);
                for (int i = 0; i < count; i++) { float v = samples[i]; if (floating) writer.Write(v); else writer.Write((short)(float.IsNaN(v) || float.IsInfinity(v) ? 0 : Mathf.RoundToInt(Mathf.Clamp(v, -1, 1) * 32767))); }
            }
        }
        void Restore()
        {
            if (restored || !optionsSaved || !G) return; restored = true;
            G.qaSuppressAI = savedSuppressAI; G.qaInputEnabled = savedInput; UnityEngine.Random.state = savedRandom;
            G.options = savedOptions; G.SyncLegacyBindings(); G.ApplySettings(false); report.settingsRestored = true;
            if (hadCheckpoint) RunStorage.Checkpoint = checkpoint; else RunStorage.ClearCheckpoint();
             report.checkpointRestored = RunStorage.HasCheckpoint == hadCheckpoint && (!hadCheckpoint || RunStorage.Checkpoint == checkpoint);
        }
        void OnApplicationQuit() { if (tap) tap.Stop(); Restore(); }
        void OnDestroy() { if (tap) tap.Stop(); Restore(); Application.logMessageReceived -= OnLog; }
    }

    // SPSC publication: one Unity audio thread writes, main thread reads only published data.
    // Callback never allocates, locks, logs, invokes Unity APIs, touches disk, or modifies output samples.
    // Unity docs: Audio Effects attached to a listener apply to all audible scene sounds; only one listener is allowed.
    public sealed class AVListenerCapture : MonoBehaviour
    {
        float[] buffer; int channels, writing, written, callbacks, armed, formatChanged, capacityReached;
        public float[] Buffer => buffer;
        public int Capacity => buffer == null ? 0 : buffer.Length;
        public int Written => Volatile.Read(ref written);
        public int Writers => Volatile.Read(ref writing);
        public int Callbacks => Volatile.Read(ref callbacks);
        public bool FormatChanged => Volatile.Read(ref formatChanged) != 0;
        public bool CapacityReached => Volatile.Read(ref capacityReached) != 0;
        public void Prepare(int rate, int expectedChannels, int seconds)
        {
            channels = expectedChannels;
            long requested = (long)rate * channels * Math.Min(60, Math.Max(1, seconds));
            int capacity = (int)Math.Min(requested, 96L * 1024 * 1024 / sizeof(float));
            capacity -= capacity % channels; buffer = new float[capacity];
        }
        public void Begin() { Volatile.Write(ref armed, 1); }
        public void Stop() { Volatile.Write(ref armed, 0); }
        void OnAudioFilterRead(float[] data, int channelCount)
        {
            if (Volatile.Read(ref armed) == 0) return;
            Interlocked.Increment(ref writing);
            try
            {
                if (Volatile.Read(ref armed) == 0) return;
                if (channelCount != channels) { Volatile.Write(ref formatChanged, 1); Volatile.Write(ref armed, 0); return; }
                int start = written, available = buffer.Length - start;
                int count = Math.Min(data.Length, available); count -= count % channels;
                if (count > 0) Array.Copy(data, 0, buffer, start, count);
                Volatile.Write(ref written, start + count); Interlocked.Increment(ref callbacks);
                if (count < data.Length || start + count >= buffer.Length) { Volatile.Write(ref capacityReached, 1); Volatile.Write(ref armed, 0); }
            }
            finally { Interlocked.Decrement(ref writing); }
        }
    }
}


