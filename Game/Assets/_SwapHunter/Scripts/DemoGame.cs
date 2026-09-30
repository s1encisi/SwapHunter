using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;

namespace SwapHunter
{
    public sealed partial class DemoGame : MonoBehaviour
    {
        public static DemoGame I { get; private set; }
        public DemoConfig config;
        public RunState state = RunState.Loading;
        public bool IsPlaying => state == RunState.Playing;
        public PlayerMotor player;
        public SoundBank sound;
        public Transform world, effects;
        public readonly List<EnemyActor> enemies = new List<EnemyActor>();
        public readonly List<SecurityGate> gates = new List<SecurityGate>();
        public readonly List<Terminal> terminals = new List<Terminal>();
        public readonly List<PhaseAnchor> anchors = new List<PhaseAnchor>();
        public readonly List<Vector3> anchorOrigins = new List<Vector3>();
        public readonly List<SupplyPickup> supplies = new List<SupplyPickup>();
        public int stage, wave, totalKills, totalSwaps, totalShots, totalHits, deaths;
        public float runSeconds;
        public GameOptions options = new GameOptions();
        public float sensitivity { get => options.sensitivity; set => options.sensitivity = value; }
        public float fov { get => options.fov; set => options.fov = value; }
        public float volume { get => options.volume; set => options.volume = value; }
        public float shake { get => options.shake; set => options.shake = value; }
        public bool invertY { get => options.invertY; set => options.invertY = value; }
        public bool tutorialMoved, tutorialShot, tutorialSwapped, stageComplete, combatComplete, hasCore, practice;
        public bool qaMode, qaSuppressAI, qaInputEnabled;
        public Keyboard qaKeyboard;
        public Mouse qaMouse;
        public Keyboard KeyboardDevice => qaMode ? qaKeyboard : Keyboard.current;
        public Mouse MouseDevice => qaMode ? qaMouse : Mouse.current;
        public int difficulty { get => options.difficulty; set => options.difficulty = value; }
        public int quality { get => options.quality; set => options.quality = value; }
        public float DamageMultiplier => difficulty == 0 ? .65f : difficulty == 2 ? 1.35f : 1;
        public readonly Key[] bindings = { Key.W, Key.S, Key.A, Key.D, Key.Space, Key.LeftShift, Key.Q, Key.R, Key.E };
        public readonly string[] bindingLabels = { "前进", "后退", "左移", "右移", "跳跃", "冲刺", "相位换位", "装填", "交互" };
        public readonly string[] stageNames = { "校准装置", "夺取制高点", "转移危险", "解除封锁", "撤离货运港" };
        public readonly string[] areaNames = { "PHASE LAB", "CARGO YARD", "TRANSFER HALL", "CONTROL TOWER", "EXTRACTION" };
        Camera menuCamera;
        float nextWaveAt = -1, toastUntil;
        public string toast = "";
        public bool settingsOpen;
        public int bindingCapture = -1;
        StreamWriter recorder;
        public Font font;
        public Terminal nearestTerminal;
        public string LogPath { get; private set; }

        IEnumerator Start()
        {
            I = this; Application.runInBackground = true;
            bool benchmark = Array.IndexOf(Environment.GetCommandLineArgs(), "-swapHunterBenchmark") >= 0;
            bool campaignQA = Array.IndexOf(Environment.GetCommandLineArgs(), "-swapHunterCampaignQA") >= 0;
            bool avReview = Array.IndexOf(Environment.GetCommandLineArgs(), "-swapHunterAVReview") >= 0;
            bool legacyQA = Array.IndexOf(Environment.GetCommandLineArgs(), "-swapHunterQA") >= 0;
            bool audioOnly = Array.IndexOf(Environment.GetCommandLineArgs(), "-swapHunterAudioReview") >= 0 && !avReview && !campaignQA && !benchmark && !legacyQA;
            qaMode = RunStorage.IsValidation && Array.IndexOf(Environment.GetCommandLineArgs(), "-swapHunterInteractiveReview") < 0;
            if (qaMode) InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            LoadSettings(); InitializeCampaign(); InitializeExpedition(); Shapes.Config = config; ImportedModels.Library = config.models;
            if (qaMode) Debug.Log("SWAPHUNTER_VALIDATION_ROOT=" + RunStorage.Root);
            if (!config.models) throw new InvalidOperationException("Three.js model library was not prepared");
            try { font = config.uiFont ? config.uiFont : Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei UI", "Microsoft YaHei", "SimHei", "Arial" }, 24); }
            catch { font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); }
            string folder = Path.Combine(RunStorage.Root, "Playtests"); Directory.CreateDirectory(folder);
            LogPath = Path.Combine(folder, "session-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".jsonl");
            recorder = new StreamWriter(LogPath, false) { AutoFlush = true };
            effects = new GameObject("Runtime effects and projectiles").transform;
            sound = gameObject.AddComponent<SoundBank>();
            if (audioOnly)
            {
                Debug.Log("SWAPHUNTER_AUDIO_ONLY_ROOT=" + RunStorage.Root);
                Application.Quit(sound.ReviewExportSucceeded ? 0 : 1); yield break;
            }
            Physics.IgnoreLayerCollision(Layers.Grenade, Layers.Actor, true); Physics.IgnoreLayerCollision(Layers.Grenade, Layers.Grenade, true);
            yield return null;
            WorldBuilder.Build(this);
            GameObject go = new GameObject("Player"); player = go.AddComponent<PlayerMotor>(); player.Initialize();player.tactics=go.AddComponent<PlayerTactics>();player.tactics.Initialize(player);player.combatTools=go.AddComponent<PlayerCombatTools>();player.combatTools.Initialize(player);
            var cameraData = player.cameraView.GetUniversalAdditionalCameraData(); cameraData.renderPostProcessing = false;
            menuCamera = new GameObject("Menu overview").AddComponent<Camera>(); menuCamera.nearClipPlane = .1f; menuCamera.farClipPlane = 400;
            menuCamera.cullingMask &= ~(1 << Layers.ViewModel);
            menuCamera.backgroundColor = new Color(.08f, .14f, .20f); menuCamera.clearFlags = CameraClearFlags.Skybox; menuCamera.fieldOfView = 57;
            menuCamera.transform.position = new Vector3(-12, 5.6f, 34); menuCamera.transform.LookAt(new Vector3(4, 3.5f, 57));
            ApplySettings(!qaMode); SetState(RunState.Menu);
            Record("boot", "SwapHunter "+Application.version+" / " + Application.unityVersion);
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-swapHunterGunplayQA") >= 0) gameObject.AddComponent<GunplayPresentationQA>();
            else if (Array.IndexOf(Environment.GetCommandLineArgs(), "-swapHunterReviewFixQA") >= 0) gameObject.AddComponent<ReviewFixQA>();
            else if (Array.IndexOf(Environment.GetCommandLineArgs(), "-swapHunterNaturalBossQA") >= 0) gameObject.AddComponent<NaturalBossQA>();
            else if (Array.IndexOf(Environment.GetCommandLineArgs(), "-swapHunterBossPerformanceQA") >= 0) gameObject.AddComponent<BossPerformanceQA>();
            else if (Array.IndexOf(Environment.GetCommandLineArgs(), "-swapHunterShowcaseCapture") >= 0) gameObject.AddComponent<ShowcaseCapture>();
            else if (Array.IndexOf(Environment.GetCommandLineArgs(), "-swapHunterBattlefieldQA") >= 0) gameObject.AddComponent<BattlefieldQA>();
            else if (Array.IndexOf(Environment.GetCommandLineArgs(), "-swapHunterTutorialQA") >= 0) gameObject.AddComponent<TutorialQA>();
            else if (Array.IndexOf(Environment.GetCommandLineArgs(), "-swapHunterShopUIQA") >= 0) gameObject.AddComponent<ShopUIQA>();
            else if (Array.IndexOf(Environment.GetCommandLineArgs(), "-swapHunterDeploymentQA") >= 0) gameObject.AddComponent<DeploymentQA>();
            else if (Array.IndexOf(Environment.GetCommandLineArgs(), "-swapHunterTacticalAIQA") >= 0) gameObject.AddComponent<TacticalAIQA>();
            else if (Array.IndexOf(Environment.GetCommandLineArgs(), "-swapHunterCombatToolsQA") >= 0) gameObject.AddComponent<CombatToolsQA>();
            else if (Array.IndexOf(Environment.GetCommandLineArgs(), "-swapHunterBossSkillsQA") >= 0) gameObject.AddComponent<BossSkillsQA>();
            else if (Array.IndexOf(Environment.GetCommandLineArgs(), "-swapHunterTacticsQA") >= 0) gameObject.AddComponent<TacticsQA>();
            else if (Array.IndexOf(Environment.GetCommandLineArgs(), "-swapHunterBossQA") >= 0) gameObject.AddComponent<BossQA>();
            else if (Array.IndexOf(Environment.GetCommandLineArgs(), "-swapHunterMotionQA") >= 0) gameObject.AddComponent<MotionQA>();
            else if (Array.IndexOf(Environment.GetCommandLineArgs(), "-swapHunterExpeditionBenchmark") >= 0) gameObject.AddComponent<ExpeditionBenchmark>();
            else if (Array.IndexOf(Environment.GetCommandLineArgs(), "-swapHunterRouteQA") >= 0) gameObject.AddComponent<ExpeditionRouteRunner>();
            else if (Array.IndexOf(Environment.GetCommandLineArgs(), "-swapHunterExpeditionQA") >= 0) gameObject.AddComponent<ExpeditionQA>();
            else if (avReview) gameObject.AddComponent<AVReview>();
            else if (campaignQA) gameObject.AddComponent<CampaignQA>();
            else if (benchmark) gameObject.AddComponent<DemoBenchmark>();
            else if (legacyQA) gameObject.AddComponent<DemoQA>();
        }
        public bool Held(int i) => ReadAction(i, false);
        public bool Pressed(int i) => ReadAction(i, true);
        bool ReadAction(int i, bool pressed)
        {
            if (qaMode && !qaInputEnabled || i < 0 || i >= options.actions.Length) return false;
            var binding = options.actions[i];
            return BindingInput.Read(binding.primary, KeyboardDevice, MouseDevice, pressed) || BindingInput.Read(binding.secondary, KeyboardDevice, MouseDevice, pressed);
        }
        void Update()
        {
            if (state == RunState.Loading) return;
            UpdateAdaptiveResolution();UpdateDeployments();
            if ((!qaMode || qaInputEnabled) && KeyboardDevice != null && KeyboardDevice.f12Key.wasPressedThisFrame)
            {
                string path = Path.Combine(RunStorage.Root, "Screenshot-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".png");
                ScreenCapture.CaptureScreenshot(path); Toast("截图已保存", 2);
            }
            if (settingsOpen) { UpdateSettingsInput(); return; }
            if (expeditionActive)
            {
                if (Pressed(16) || ((!qaMode || qaInputEnabled) && KeyboardDevice != null && KeyboardDevice.escapeKey.wasPressedThisFrame))
                {
                    if (expeditionInventory) { expeditionInventory = false; SetState(RunState.Playing); }
                    else if (state == RunState.Playing) SetState(RunState.Paused);
                    else if (state == RunState.Paused && !expeditionPending) SetState(RunState.Playing);
                }
                UpdateExpedition(); return;
            }
            if (Pressed(16) || ((!qaMode || qaInputEnabled) && KeyboardDevice != null && KeyboardDevice.escapeKey.wasPressedThisFrame))
            {
                if (state == RunState.Playing) SetState(RunState.Paused);
                else if (state == RunState.Paused) SetState(RunState.Playing);
                else if (state == RunState.Menu && campaignBoard) campaignBoard = false;
                else if (state == RunState.Menu && expeditionBoard) expeditionBoard = false;
            }
            if (!IsPlaying) return;
            runSeconds += Time.deltaTime;
            nearestTerminal = FindTerminal();
            if(tutorialCourse&&tutorialCourse.Active)return;
            if ((stage == 1 || stage == 3) && !combatComplete)
            {
                if (AliveEnemies == 0 && nextWaveAt < 0)
                {
                    int maxWave = stage == 1 ? 2 : 3;
                    if (wave >= maxWave && !practice)
                    {
                        combatComplete = true; Toast(stage == 3 ? "封锁部队已解除 · 前往获取相位核心" : "区域已清空 · 启动控制台开启通路", 4);
                    }
                    else { nextWaveAt = Time.time + 3.5f; Toast("增援正在进入 · 利用间隙重新装填", 3); }
                }
                if (nextWaveAt > 0 && Time.time >= nextWaveAt) { nextWaveAt = -1; SpawnWave(); }
            }
            if (stageComplete && stage < 4 && !practice && !campaignActive)
            {
                float[] boundaries = { 30, 74, 118, 164 };
                if (player.transform.position.z > boundaries[stage]) EnterStage(stage + 1, false);
            }
            if (state == RunState.Playing && stage == 0 && tutorialMoved && tutorialShot && tutorialSwapped && !stageComplete && toastUntil < Time.unscaledTime)
                Toast("校准完成 · 靠近前方控制台按 " + KeyName(8) + " 开门", 3);
        }
        public int AliveEnemies
        {
            get { int count = 0; foreach(var deployment in deployments)if(deployment.arena==stage)count++; foreach (var enemy in enemies) if (enemy && enemy.Alive && enemy.arena == stage) count++; return count; }
        }
        public void StartRun(bool training = false, bool resume = false)
        {
            if (campaignActive || expeditionActive) return;
            expeditionBoard = false; campaignBoard = false; lastSettlement = null; ClearCampaignStations();
            if(tutorialCourse)tutorialCourse.End();
            practice = training; totalKills = totalSwaps = totalHits = totalShots = deaths = 0; runSeconds = 0;
            tutorialMoved = tutorialShot = tutorialSwapped = false; hasCore = false;
            EnterStage(training ? 1 : resume ? Mathf.Clamp(RunStorage.Checkpoint, 0, 4) : 0, true);
            SetState(RunState.Playing); Toast(training ? "自由试招 · 使用两种武器与换位组合，Esc 可退出" : "任务：穿过货运港，取得相位核心并撤离", 4);
        }
        public void EnterStage(int number, bool respawn)
        {
            ClearDeployments();stage = number; stageComplete = combatComplete = false; wave = 0; nextWaveAt = -1; hasCore = number == 4;
            foreach (EnemyActor enemy in enemies) if (enemy) { enemy.gameObject.SetActive(false); Destroy(enemy.gameObject); }
            enemies.Clear();
            foreach (Transform child in effects) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
            for (int i = 0; i < anchors.Count; i++) anchors[i].MoveTo(anchorOrigins[i]);
            foreach (Terminal terminal in terminals) terminal.ResetTerminal();
            foreach (SupplyPickup supply in supplies) supply.ResetPickup();
            for (int i = 0; i < gates.Count; i++) gates[i].SetOpen(i < number - 1);
            Vector3[] spawns = { new Vector3(0, .04f, 1), new Vector3(0, .04f, 32), new Vector3(0, .04f, 76), new Vector3(0, .04f, 120), new Vector3(0, .04f, 165) };
            if (respawn) player.ResetAt(spawns[number]);
            else { player.health = config.playerHealth; player.reserve[0] = Mathf.Max(config.rifleReserve, player.reserve[0]); player.reserve[1] = Mathf.Max(config.shotgunReserve, player.reserve[1]); Toast("检查点已记录 · 生命与备弹补充", 3); }
            if (number == 0) EnemyActor.Spawn(EnemyKind.Training, new Vector3(0, 0, 13), 0);
            else if (number < 4) SpawnWave();
            if (!practice && !campaignActive) { RunStorage.Checkpoint = number;  }
            Record("checkpoint", number.ToString());
        }
        public void SpawnWave()
        {
            enemies.RemoveAll(enemy => !enemy);
            wave++;
            if (stage == 1)
            {
                QueueDeployment(EnemyKind.Sniper, new Vector3(10, 4, 50.5f), stage);
                QueueDeployment(EnemyKind.Assault, new Vector3(-9, 0, 51), stage);
                QueueDeployment(EnemyKind.Assault, new Vector3(4, 0, 60), stage);
                if (wave > 1) QueueDeployment(EnemyKind.Shield, new Vector3(-2, 0, 56), stage);
            }
            else if (stage == 2)
            {
                QueueDeployment(EnemyKind.Sniper, new Vector3(-11, 4, 90), stage);
                QueueDeployment(EnemyKind.Assault, new Vector3(0, 0, 94), stage);
                QueueDeployment(EnemyKind.Assault, new Vector3(12, 0, 103), stage);
                QueueDeployment(EnemyKind.Shield, new Vector3(2, 0, 85), stage);
                QueueDeployment(EnemyKind.Shield, new Vector3(-5, 0, 103), stage);
            }
            else if (stage == 3)
            {
                QueueDeployment(EnemyKind.Sniper, new Vector3(-12, 7, 146.5f), stage);
                QueueDeployment(EnemyKind.Assault, new Vector3(12, 3.5f, 138), stage);
                QueueDeployment(EnemyKind.Assault, new Vector3(-5, 0, 131), stage);
                QueueDeployment(wave >= 3 ? EnemyKind.Elite : EnemyKind.Shield, new Vector3(1, 0, 146), stage);
                if (wave == 2) QueueDeployment(EnemyKind.Assault, new Vector3(-13, 0, 141), stage);
            }
            Record("wave_spawn", stage + ":" + wave);
        }
        public void EnemyKilled(EnemyActor enemy)
        {
            if (enemy.kind != EnemyKind.Training) totalKills++;
        }
        public Terminal FindTerminal()
        {
            Terminal nearest = null; float distance = 2.8f;
            foreach (Terminal terminal in terminals)
            {
                if (terminal.stage != stage || terminal.activated) continue;
                Vector3 delta = terminal.Point - player.Eye.position;
                if (delta.magnitude >= distance || Vector3.Dot(delta.normalized, player.Eye.forward) < .15f) continue;
                if (Physics.Linecast(player.Eye.position, terminal.Point, out var hit, Layers.WorldMask) && hit.collider.GetComponentInParent<Terminal>() != terminal) continue;
                distance = delta.magnitude; nearest = terminal;
            }
            return nearest;
        }
        public bool Interact()
        {
            if(tutorialCourse&&tutorialCourse.Active)return tutorialCourse.Interact();
            if (expeditionActive) return UseExpeditionNode(FindExpeditionNode());
            if (campaignActive) { var station = FindCampaignStation(); if (station) return UseCampaignStation(station); }
            Terminal terminal = FindTerminal();
            if (!terminal) return false;
            if (stage == 0 && !(tutorialMoved && tutorialShot && tutorialSwapped)) { Toast("先完成移动、射击与换位校准", 2); return false; }
            if ((stage == 1 || stage == 3) && !combatComplete) { Toast("控制台仍被封锁 · 先处理当前部队", 2); return false; }
            if (practice) { Toast("试招场会持续刷新敌人，Esc 可返回菜单", 2); return false; }
            terminal.Activate(); sound.Play("confirm"); Record("terminal", terminal.label);
            if (stage == 4) { SetState(RunState.Victory); RunStorage.Checkpoint = 0;  Record("victory", runSeconds.ToString("F2")); return true; }
            if (stage == 2)
            {
                int active = 0; foreach (var relay in terminals) if (relay.stage == 2 && relay.activated) active++;
                if (active < 2) { Toast("继电器已接通 · 还有一处", 3); return true; }
            }
            if (stage == 3) hasCore = true;
            if (campaignActive) { if (!CompleteContractMain()) terminal.ResetTerminal(); return campaignMain; }
            stageComplete = true; gates[stage].SetOpen(true);
            Toast(stage == 3 ? "核心已取得 · 前往撤离点" : "通路已开启 · 向前推进", 4); return true;
        }
        public void Die() { deaths++; Record("death", stage.ToString()); if (expeditionActive) FinishExpedition(false); else if (campaignActive) FinishContract(false); else SetState(RunState.Dead); }
        public void Retry() { if(tutorialCourse&&tutorialCourse.Active){tutorialCourse.RetryCheckpoint();return;} if (campaignActive || lastSettlement != null) return; EnterStage(stage, true); SetState(RunState.Playing); Record("retry", stage.ToString()); }
        public void SetState(RunState next)
        {
            if ((settlementPending || expeditionPending) && next != RunState.Paused) return;
            if (next == RunState.Menu && tutorialCourse) tutorialCourse.End();
            state = next; CancelSettings();
            Time.timeScale = next == RunState.Playing || next == RunState.Menu ? 1 : 0;
            bool playing = next == RunState.Playing;
            Cursor.lockState = playing && !qaMode ? CursorLockMode.Locked : CursorLockMode.None; Cursor.visible = !playing || qaMode;
            if (player) { if (!playing) player.CancelActions(); player.SetViewActive(next != RunState.Menu); }
            if (menuCamera) menuCamera.enabled = next == RunState.Menu;
        }
        public void HandleFocusLoss() { if (IsPlaying) SetState(RunState.Paused); }
        void OnApplicationFocus(bool focus) { if (!qaMode) { AudioListener.pause = !focus && options.muteWhenUnfocused; if (!focus) HandleFocusLoss(); } }
        public void Toast(string message, float duration = 2) { toast = message; toastUntil = Time.unscaledTime + duration; }
        public bool ToastVisible => Time.unscaledTime < toastUntil;
        public string Objective => tutorialCourse&&tutorialCourse.Active?tutorialCourse.Instruction:expeditionActive ? ExpeditionObjective : campaignActive ? CampaignObjective : ObjectiveClassic;
        public string ObjectiveClassic
        {
            get
            {
                if (practice) return "尝试换位抢高点、绕盾与两枪配合 · Esc 返回";
                if (stageComplete) return "通路已开启，沿发光边线向前推进";
                if (stage == 0) return !tutorialMoved ? "使用移动按键前进 · 鼠标观察 · " + KeyName(5) + " 冲刺" : !tutorialShot ? KeyName(12) + " 射击前方训练机 · " + KeyName(7) + " 装填" : !tutorialSwapped ? "瞄准机器人或高台信标，按 " + KeyName(6) + " 交换位置" : "靠近前方控制台，按 " + KeyName(8) + " 开启出口";
                if (stage == 1) return combatComplete ? "靠近区域末端控制台，按 " + KeyName(8) + " 解除封锁" : "处理两批守卫 · 抢占高台，避免停留在交叉火力中";
                if (stage == 2) { int n = 0; foreach (var t in terminals) if (t.stage == 2 && t.activated) n++; return "启动两处继电器  " + n + " / 2  · 不必清除所有敌人"; }
                if (stage == 3) return combatComplete ? "前往末端控制台，按 " + KeyName(8) + " 取得相位核心" : "击退增援与封锁指挥官 · 组合运用换位、掩体和火力";
                return "前往飞船前的撤离终端，按 " + KeyName(8) + " 完成任务";
            }
        }
        public void SyncLegacyBindings()
        {
            for (int i = 0; i < bindings.Length; i++)
            {
                string code = options.actions[i].primary;
                bindings[i] = code != null && code.StartsWith("K:") && Enum.TryParse(code.Substring(2), out Key key) ? key : Key.None;
            }
        }
        public void LoadSettings() { options = SettingsStore.Load(); SyncLegacyBindings(); }
        public void SaveSettings() { SettingsStore.Save(options); ApplySettings(); }
        public void ApplySettings(bool display = false)
        {
            options.Validate(); AudioListener.volume = options.volume; AudioListener.pause = !qaMode && !Application.isFocused && options.muteWhenUnfocused;
            QualitySettings.vSyncCount = options.vSync ? 1 : 0;
            Application.targetFrameRate = new[] { 60, 120, 144, 240, -1 }[options.frameLimit];
            if (UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset asset)
            {
                asset.renderScale = options.renderScale;
                asset.shadowDistance = quality == 0 ? 25 : quality == 1 ? 55 : 80;
                asset.msaaSampleCount = quality == 0 ? 1 : quality == 1 ? 2 : 4;
            }
            if (player) player.ApplyViewSettings();
            if (display && !qaMode) ApplyDisplayOptions();
        }
        [Serializable] sealed class LogEntry { public string build = Application.version; public string evt, detail; public float time; public int stage; }
        public void Record(string evt, string detail)
        {
            ShowcaseCapture.Event(evt,detail);
            if (recorder == null) return;
            try { recorder.WriteLine(JsonUtility.ToJson(new LogEntry { evt = evt, detail = detail, time = runSeconds, stage = stage })); }
            catch (IOException e) { Debug.LogWarning("Playtest logging stopped: " + e.Message); try { recorder.Dispose(); } catch (IOException) { } recorder = null; }
        }
        void OnDestroy() { recorder?.Dispose(); if (I == this) I = null; }
        void OnApplicationQuit() { if (Array.IndexOf(Environment.GetCommandLineArgs(), "-swapHunterAVReview") < 0) SaveSettings(); }
    }
}











