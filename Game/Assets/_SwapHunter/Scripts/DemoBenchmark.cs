using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace SwapHunter
{
    public sealed class DemoBenchmark : MonoBehaviour
    {
        [Serializable] sealed class Run
        {
            public int frames, focusedFrames, deaths, shots;
            public float seconds, p50Ms, p95Ms, averageFps;
        }
        [Serializable] sealed class Results
        {
            public string scenario = "Control tower, live enemy AI, scripted firing from five camera stations; 3 x 60 seconds after warmup";
            public string graphicsDevice, graphicsApi;
            public int width, height, memoryMb, quality;
            public string unityVersion;
            public int vSyncCount, targetFrameRate;
            public float renderScale;
            public List<Run> runs = new List<Run>();
        }
        DemoGame G => DemoGame.I;
        IEnumerator Start()
        {
            string[] args = Environment.GetCommandLineArgs(); int index = Array.IndexOf(args, "-qaOutput");
            string output = index >= 0 && index + 1 < args.Length ? args[index + 1] : RunStorage.Root; Directory.CreateDirectory(output);
            int checkpoint = RunStorage.Checkpoint, quality = G.quality; G.quality = 1; G.ApplySettings(); AudioListener.volume = 0;
            Results result = new Results { graphicsDevice = SystemInfo.graphicsDeviceName, graphicsApi = SystemInfo.graphicsDeviceType.ToString(), width = Screen.width, height = Screen.height, memoryMb = SystemInfo.systemMemorySize, quality = 1, renderScale = .85f };
            result.unityVersion = Application.unityVersion; result.vSyncCount = QualitySettings.vSyncCount; result.targetFrameRate = Application.targetFrameRate;
            Vector3[] stations = { new Vector3(0,.03f,122), new Vector3(-7,.03f,136), new Vector3(11,3.53f,142), new Vector3(-13,7.03f,149), new Vector3(0,.03f,151) };
            for (int run = 0; run < 3; run++)
            {
                UnityEngine.Random.InitState(9701); G.StartRun(); G.EnterStage(3, true); G.qaSuppressAI = false;
                float began = Time.realtimeSinceStartup; int lastStation = -1;
                var samples = new List<float>(15000); int focused = 0;
                int deaths = G.deaths, shots = G.totalShots;
                while (Time.realtimeSinceStartup - began < 65)
                {
                    float t = Time.realtimeSinceStartup - began;
                    if (G.state == RunState.Dead) { G.Retry(); lastStation = -1; }
                    if (G.combatComplete) { G.EnterStage(3, true); lastStation = -1; }
                    int station = Mathf.Clamp(Mathf.FloorToInt(Mathf.Max(0, t - 5) / 12), 0, 4);
                    if (station != lastStation) { G.player.SetFeet(stations[station]); lastStation = station; }
                    EnemyActor closest = null; float distance = float.MaxValue;
                    foreach (var enemy in G.enemies)
                    {
                        if (!enemy || !enemy.Alive || Physics.Linecast(G.player.Eye.position, enemy.AimPoint, Layers.WorldMask)) continue;
                        float d = Vector3.Distance(G.player.Eye.position, enemy.AimPoint); if (d < distance) { closest = enemy; distance = d; }
                    }
                    Vector3 aim = closest ? closest.AimPoint : new Vector3(0, 1.3f, 143);
                    Vector3 direction = (aim - G.player.Eye.position).normalized;
                    G.player.SetLook(Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg, -Mathf.Asin(direction.y) * Mathf.Rad2Deg);
                    if (closest) G.player.Fire();
                    if (G.player.reserve[0] <= 0) G.player.reserve[0] = G.config.rifleReserve;
                    yield return null;
                    if (t >= 5) { samples.Add(Time.unscaledDeltaTime * 1000); if (Application.isFocused) focused++; }
                }
                float sum = 0; foreach (float sample in samples) sum += sample; samples.Sort();
                Run data = new Run { frames = samples.Count, focusedFrames = focused, seconds = sum / 1000, p50Ms = samples[samples.Count / 2], p95Ms = samples[Mathf.FloorToInt((samples.Count - 1) * .95f)], averageFps = samples.Count / (sum / 1000), deaths = G.deaths - deaths, shots = G.totalShots - shots };
                result.runs.Add(data); File.WriteAllText(Path.Combine(output, "benchmark.json"), JsonUtility.ToJson(result, true)); Debug.Log("BENCHMARK_RUN=" + run + "; p95=" + data.p95Ms);
            }
            G.quality = quality; G.SaveSettings(); RunStorage.Checkpoint = checkpoint; 
            ScreenCapture.CaptureScreenshot(Path.Combine(output, "benchmark.png")); yield return new WaitForSecondsRealtime(.3f); Application.Quit();
        }
    }
}

