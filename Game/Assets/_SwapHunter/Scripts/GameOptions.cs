using System;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace SwapHunter
{
    [Serializable]
    public sealed class ActionBinding
    {
        public string primary, secondary;
        public ActionBinding(string primary, string secondary = "") { this.primary = primary; this.secondary = secondary; }
    }
    [Serializable]
    public sealed class GameOptions
    {
        public const int CurrentVersion = 5;
        public int version = CurrentVersion;
        public float sensitivity = 1, verticalSensitivity = 1, aimSensitivity = .75f;
        public bool invertY, toggleAim, toggleSprint, toggleCrouch;
        public bool muteWhenUnfocused = true, dynamicResolution;
        public int displayIndex, resolutionWidth = 1280, resolutionHeight = 720, refreshRateHz;
        public float renderScale = .85f, dynamicResolutionMin = .60f, uiScale = 1;
        public float musicVolume = .45f, ambientVolume = .50f, uiVolume = .70f;
        public int mouseDpi = 800;
        public float fov = 60, viewModelFov = 68, volume = .7f, effectsVolume = .85f, feedbackVolume = .75f, shake = .35f, weaponMotion = .65f;
        public int difficulty = 1, quality = 1, resolution = 0, windowMode = 0, frameLimit = 1;
        public bool vSync = false, dynamicCrosshair = true, crosshairDot = true, hitMarkers = true;
        public float crosshairSize = 5, crosshairGap = 4, crosshairThickness = 1.5f, crosshairOpacity = .95f;
        public int crosshairColor = 0;
        public float moveSpeedScale = 1;
        public ActionBinding[] actions = DefaultBindings();
        public static readonly string[] ActionNames = { "前进", "后退", "左移", "右移", "跳跃 / 翻越", "冲刺", "相位换位", "装填", "交互", "慢走", "主武器", "副武器", "开火", "瞄准", "上一把武器", "下一把武器", "暂停", "蹲伏", "战术背包", "使用消耗品", "A盾 / 定向屏障", "战术刀 / 近战", "投掷飞刀", "召回飞刀", "职业技能" };
        public static ActionBinding[] DefaultBindings() => new[]
        {
            new ActionBinding("K:W"),new ActionBinding("K:S"),new ActionBinding("K:A"),new ActionBinding("K:D"),
            new ActionBinding("K:Space"),new ActionBinding("K:LeftShift"),new ActionBinding("K:Q"),new ActionBinding("K:R"),
            new ActionBinding("K:E"),new ActionBinding("K:LeftAlt"),new ActionBinding("K:Digit1"),new ActionBinding("K:Digit2"),
            new ActionBinding("M:0"),new ActionBinding("M:1"),new ActionBinding("W:Up"),new ActionBinding("W:Down"),new ActionBinding("K:Escape"),
            new ActionBinding("K:LeftCtrl"),new ActionBinding("K:Tab"),new ActionBinding("K:F"),new ActionBinding("K:C"),new ActionBinding("K:V"),new ActionBinding("K:G"),new ActionBinding("K:X"),new ActionBinding("K:Z")
        };
        public GameOptions Clone() => JsonUtility.FromJson<GameOptions>(JsonUtility.ToJson(this));
        public void Validate()
        {
            if (version < 4)
            {
                int oldResolution = Mathf.Clamp(resolution, 0, 2);
                resolutionWidth = new[] { 1280, 1600, 1920 }[oldResolution];
                resolutionHeight = new[] { 720, 900, 1080 }[oldResolution];
                renderScale = new[] { .7f, .85f, 1f }[Mathf.Clamp(quality, 0, 2)];
                musicVolume = .45f; ambientVolume = effectsVolume; uiVolume = feedbackVolume;
                dynamicResolutionMin = .60f; muteWhenUnfocused = true;
            }
            var defaultsForNumbers = new GameOptions();
            foreach (var field in typeof(GameOptions).GetFields())
                if (field.FieldType == typeof(float)) { float value = (float)field.GetValue(this); if (float.IsNaN(value) || float.IsInfinity(value)) field.SetValue(this, field.GetValue(defaultsForNumbers)); }
            sensitivity = Mathf.Clamp(sensitivity, .05f, 8); verticalSensitivity = Mathf.Clamp(verticalSensitivity, .5f, 2);
            aimSensitivity = Mathf.Clamp(aimSensitivity, .2f, 1.5f); mouseDpi = Mathf.Clamp(mouseDpi, 100, 32000);
            fov = Mathf.Clamp(fov, 50, 85); viewModelFov = Mathf.Clamp(viewModelFov, 55, 90);
            volume = Mathf.Clamp01(volume); effectsVolume = Mathf.Clamp01(effectsVolume); feedbackVolume = Mathf.Clamp01(feedbackVolume);
            musicVolume = Mathf.Clamp01(musicVolume); ambientVolume = Mathf.Clamp01(ambientVolume); uiVolume = Mathf.Clamp01(uiVolume);
            if(uiScale<=0)uiScale=1;uiScale=Mathf.Clamp(uiScale,.8f,1);
            renderScale = Mathf.Clamp(renderScale, .5f, 1.2f); dynamicResolutionMin = Mathf.Clamp(dynamicResolutionMin, .5f, renderScale);
            displayIndex = Mathf.Max(0, displayIndex); resolutionWidth = Mathf.Clamp(resolutionWidth, 640, 7680); resolutionHeight = Mathf.Clamp(resolutionHeight, 480, 4320); refreshRateHz = Mathf.Clamp(refreshRateHz, 0, 500);
            shake = Mathf.Clamp01(shake); weaponMotion = Mathf.Clamp01(weaponMotion); moveSpeedScale = Mathf.Clamp(moveSpeedScale, .75f, 1.25f);
            difficulty = Mathf.Clamp(difficulty, 0, 2); quality = Mathf.Clamp(quality, 0, 2); resolution = Mathf.Clamp(resolution, 0, 2);
            windowMode = Mathf.Clamp(windowMode, 0, 1); frameLimit = Mathf.Clamp(frameLimit, 0, 4); crosshairColor = Mathf.Clamp(crosshairColor, 0, 4);
            crosshairSize = Mathf.Clamp(crosshairSize, 2, 12); crosshairGap = Mathf.Clamp(crosshairGap, 1, 12); crosshairThickness = Mathf.Clamp(crosshairThickness, 1, 4); crosshairOpacity = Mathf.Clamp(crosshairOpacity, .3f, 1);
            var defaults = DefaultBindings();
            if (actions == null) actions = defaults;
            else if (actions.Length != defaults.Length)
            {
                int preserved = Mathf.Min(actions.Length, defaults.Length);
                var migrated = new ActionBinding[defaults.Length];
                Array.Copy(actions, migrated, preserved);
                for (int i = preserved; i < migrated.Length; i++)
                {
                    string binding = defaults[i].primary;
                    // Preserve previous custom keys; a new action cannot take an occupied binding.
                    for (int j = 0; j < i; j++)
                        if (migrated[j] != null && (migrated[j].primary == binding || migrated[j].secondary == binding)) { binding = ""; break; }
                    migrated[i] = new ActionBinding(binding);
                }
                actions = migrated;
            }
            for (int i = 0; i < actions.Length; i++)
            {
                if (actions[i] == null) actions[i] = defaults[i];
                if (!string.IsNullOrEmpty(actions[i].primary) && !BindingInput.Valid(actions[i].primary)) actions[i].primary = defaults[i].primary;
                if (!string.IsNullOrEmpty(actions[i].secondary) && !BindingInput.Valid(actions[i].secondary)) actions[i].secondary = "";
            }
            version = CurrentVersion;
        }
        public int Conflict(string code, int action, int slot)
        {
            for (int i = 0; i < actions.Length; i++)
            {
                if (!(i == action && slot == 0) && actions[i].primary == code) return i * 2;
                if (!(i == action && slot == 1) && actions[i].secondary == code) return i * 2 + 1;
            }
            return -1;
        }
        public void Bind(int action, int slot, string code, bool swapConflict)
        {
            if (!string.IsNullOrEmpty(code) && !BindingInput.Valid(code)) throw new ArgumentException("Invalid input binding");
            int conflict = string.IsNullOrEmpty(code) ? -1 : Conflict(code, action, slot);
            string old = slot == 0 ? actions[action].primary : actions[action].secondary;
            if (conflict >= 0 && !swapConflict) throw new InvalidOperationException("Binding conflict requires a choice");
            if (conflict >= 0)
            {
                if (conflict % 2 == 0) actions[conflict / 2].primary = old;
                else actions[conflict / 2].secondary = old;
            }
            if (slot == 0) actions[action].primary = code; else actions[action].secondary = code;
        }
    }
    public static class BindingInput
    {
        public static bool Valid(string code)
        {
            if (string.IsNullOrEmpty(code)) return false;
            if (code.StartsWith("K:")) return Enum.TryParse(code.Substring(2), out Key key) && key != Key.None && Enum.IsDefined(typeof(Key), key);
            return code == "M:0" || code == "M:1" || code == "M:2" || code == "M:3" || code == "M:4" || code == "W:Up" || code == "W:Down";
        }
        public static bool Read(string code, Keyboard keyboard, Mouse mouse, bool pressed)
        {
            if (string.IsNullOrEmpty(code)) return false;
            ButtonControl control = null;
            if (code.StartsWith("K:") && keyboard != null && Enum.TryParse(code.Substring(2), out Key key)) control = keyboard[key];
            if (mouse != null)
            {
                switch (code)
                {
                    case "M:0": control = mouse.leftButton; break; case "M:1": control = mouse.rightButton; break;
                    case "M:2": control = mouse.middleButton; break; case "M:3": control = mouse.backButton; break; case "M:4": control = mouse.forwardButton; break;
                    case "W:Up": return mouse.scroll.ReadValue().y > .1f; case "W:Down": return mouse.scroll.ReadValue().y < -.1f;
                }
            }
            return control != null && (pressed ? control.wasPressedThisFrame : control.isPressed);
        }
        public static string Label(string code)
        {
            if (string.IsNullOrEmpty(code)) return "未绑定";
            switch (code) { case "M:0": return "鼠标左键"; case "M:1": return "鼠标右键"; case "M:2": return "鼠标中键"; case "M:3": return "鼠标侧键 4"; case "M:4": return "鼠标侧键 5"; case "W:Up": return "滚轮上"; case "W:Down": return "滚轮下"; }
            return code.Substring(2).Replace("LeftShift", "Shift").Replace("LeftAlt", "Alt").Replace("LeftCtrl", "Ctrl").Replace("Digit", "");
        }
    }
    public static class SettingsStore
    {
        public static string FilePath => Path.Combine(RunStorage.Root, "settings-v5.json");
        public static string LegacyFilePath => Path.Combine(RunStorage.Root, "settings-v4.json");
        public static string OldestFilePath => Path.Combine(RunStorage.Root, "settings-v2.json");
        public static string LastWarning { get; private set; } = "";

        public static GameOptions Load() => LoadDirectory(RunStorage.Root, !RunStorage.IsIsolated);

        // Explicit directory API for isolated storage checks and imported profiles.
        // It never reads PlayerPrefs, changes RunStorage.Root, or automatically writes a recovered file.
        public static GameOptions LoadFromDirectory(string directory) => LoadDirectory(directory, false);

        static GameOptions LoadDirectory(string directory, bool readLegacyPlayerPrefs)
        {
            LastWarning = "";
            string root = Path.GetFullPath(directory);
            string current = Path.Combine(root, "settings-v5.json");
            string[] candidates = {
                current, current + ".bak",
                Path.Combine(root, "settings-v4.json"), Path.Combine(root, "settings-v4.json.bak"),
                Path.Combine(root, "settings-v2.json"), Path.Combine(root, "settings-v2.json.bak")
            };
            var failures = new System.Collections.Generic.List<string>();
            for (int index = 0; index < candidates.Length; index++)
            {
                string path = candidates[index];
                if (!File.Exists(path)) continue;
                try
                {
                    GameOptions loaded = LoadFromPath(path);
                    if (index == 1) LastWarning = "设置主文件不可用，已从有效备份恢复；原文件保留，恢复结果尚未写回。";
                    else if (index >= 2) LastWarning = failures.Count > 0
                        ? "设置主文件无法读取，已恢复旧版配置；原文件已保留。"
                        : "已读取旧版设置；下次保存生成新版文件，旧版原件保留。";
                    if (failures.Count > 0) LastWarning += " " + string.Join("；", failures);
                    return loaded;
                }
                catch (Exception error) when (error is InvalidDataException || error is IOException || error is UnauthorizedAccessException || error is ArgumentException)
                {
                    failures.Add(Path.GetFileName(path) + "：" + error.Message);
                    if (error is InvalidDataException)
                    {
                        try { ArchiveDamagedFile(path); }
                        catch (Exception archiveError) when (archiveError is IOException || archiveError is UnauthorizedAccessException)
                        {
                            // The original is still untouched. Report this instead of pretending an archive exists.
                            failures.Add("原文件保留在原路径，但无法额外归档：" + archiveError.Message);
                        }
                    }
                }
            }
            if (failures.Count > 0)
                LastWarning = "设置文件与可用备份无法读取，已使用兼容默认值；原件保留，未写入恢复结果。 " + string.Join("；", failures);
            var settings = new GameOptions();
            if (readLegacyPlayerPrefs)
            {
                settings.sensitivity = PlayerPrefs.GetFloat("SH.sensitivity", 1); settings.fov = PlayerPrefs.GetFloat("SH.fov", 60);
                settings.volume = PlayerPrefs.GetFloat("SH.volume", .7f); settings.shake = PlayerPrefs.GetFloat("SH.shake", .35f);
                settings.invertY = PlayerPrefs.GetInt("SH.invert", 0) != 0; settings.difficulty = PlayerPrefs.GetInt("SH.difficulty", 1); settings.quality = PlayerPrefs.GetInt("SH.quality", 1);
                for (int i = 0; i < 9; i++)
                {
                    string key = PlayerPrefs.GetString("SH.key" + i, "");
                    if (BindingInput.Valid("K:" + key)) settings.actions[i].primary = "K:" + key;
                }
            }
            settings.Validate();
            return settings;
        }

        public static GameOptions LoadFromPath(string path)
        {
            string text = File.ReadAllText(path);
            string trimmed = text.Trim();
            if (!trimmed.StartsWith("{", StringComparison.Ordinal) || !trimmed.EndsWith("}", StringComparison.Ordinal)
                || !System.Text.RegularExpressions.Regex.IsMatch(text, "\"version\"\\s*:"))
                throw new InvalidDataException("设置不是包含版本信息的完整 JSON 对象。");
            GameOptions settings;
            try { settings = JsonUtility.FromJson<GameOptions>(text); }
            catch (ArgumentException error) { throw new InvalidDataException("设置 JSON 无法解析。", error); }
            if (settings == null || settings.version < 1 || settings.version > GameOptions.CurrentVersion
                || !System.Text.RegularExpressions.Regex.IsMatch(text, "\"actions\"\\s*:")
                || settings.actions == null || settings.actions.Length == 0)
                throw new InvalidDataException("设置版本或按键记录不完整，未将其当作有效备份。");
            settings.Validate();
            return settings;
        }

        static string ArchiveDamagedFile(string path)
        {
            // Keep the historical archive naming contract, separate from the last-valid .bak.
            string archived = path + ".backup-" + DateTime.UtcNow.ToString("yyyyMMddHHmmssfff") + "-" + Guid.NewGuid().ToString("N");
            File.Copy(path, archived, false);
            return archived;
        }

        public static void SaveToPath(GameOptions settings, string path)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            LastWarning = "";
            try
            {
                string fullPath = Path.GetFullPath(path);
                string directory = Path.GetDirectoryName(fullPath);
                Directory.CreateDirectory(directory);
                GameOptions normalized = settings.Clone();
                normalized.Validate();
                string temporary = fullPath + ".tmp-" + Guid.NewGuid().ToString("N");
                byte[] bytes = new System.Text.UTF8Encoding(false).GetBytes(JsonUtility.ToJson(normalized, true));
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
                {
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush(true);
                }
                if (File.Exists(fullPath))
                {
                    bool validPrimary = true;
                    try { LoadFromPath(fullPath); }
                    catch (InvalidDataException) { validPrimary = false; }
                    string backup = fullPath + ".bak";
                    if (!validPrimary)
                    {
                        ArchiveDamagedFile(fullPath); // Failure aborts the save before replacement.
                        backup = null; // Never replace a good .bak with a damaged primary.
                    }
                    else if (File.Exists(backup))
                    {
                        try { LoadFromPath(backup); }
                        catch (InvalidDataException) { ArchiveDamagedFile(backup); }
                    }
                    File.Replace(temporary, fullPath, backup);
                }
                else File.Move(temporary, fullPath);
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException || error is ArgumentException || error is NotSupportedException)
            {
                LastWarning = "设置未保存：原设置文件保持不变；若已生成临时文件则保留供排查。 " + error.Message;
                throw;
            }
        }

        public static void Save(GameOptions settings) => SaveToPath(settings, FilePath);
    }
}
