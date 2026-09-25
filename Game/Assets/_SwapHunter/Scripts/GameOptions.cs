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
        public int version = 2;
        public float sensitivity = 1, verticalSensitivity = 1, aimSensitivity = .75f;
        public bool invertY, toggleAim, toggleSprint;
        public int mouseDpi = 800;
        public float fov = 60, viewModelFov = 68, volume = .7f, effectsVolume = .85f, feedbackVolume = .75f, shake = .35f, weaponMotion = .65f;
        public int difficulty = 1, quality = 1, resolution = 0, windowMode = 0, frameLimit = 1;
        public bool vSync = false, dynamicCrosshair = true, crosshairDot = true, hitMarkers = true;
        public float crosshairSize = 5, crosshairGap = 4, crosshairThickness = 1.5f, crosshairOpacity = .95f;
        public int crosshairColor = 0;
        public float moveSpeedScale = 1;
        public ActionBinding[] actions = DefaultBindings();
        public static readonly string[] ActionNames = { "前进", "后退", "左移", "右移", "跳跃", "冲刺", "相位换位", "装填", "交互", "慢走", "卡宾枪", "霰弹枪", "开火", "瞄准", "上一把武器", "下一把武器", "暂停" };
        public static ActionBinding[] DefaultBindings() => new[]
        {
            new ActionBinding("K:W"),new ActionBinding("K:S"),new ActionBinding("K:A"),new ActionBinding("K:D"),
            new ActionBinding("K:Space"),new ActionBinding("K:LeftShift"),new ActionBinding("K:Q"),new ActionBinding("K:R"),
            new ActionBinding("K:E"),new ActionBinding("K:LeftAlt"),new ActionBinding("K:Digit1"),new ActionBinding("K:Digit2"),
            new ActionBinding("M:0"),new ActionBinding("M:1"),new ActionBinding("W:Up"),new ActionBinding("W:Down"),new ActionBinding("K:Escape")
        };
        public GameOptions Clone() => JsonUtility.FromJson<GameOptions>(JsonUtility.ToJson(this));
        public void Validate()
        {
            var defaultsForNumbers = new GameOptions();
            foreach (var field in typeof(GameOptions).GetFields())
                if (field.FieldType == typeof(float)) { float value = (float)field.GetValue(this); if (float.IsNaN(value) || float.IsInfinity(value)) field.SetValue(this, field.GetValue(defaultsForNumbers)); }
            sensitivity = Mathf.Clamp(sensitivity, .05f, 8); verticalSensitivity = Mathf.Clamp(verticalSensitivity, .5f, 2);
            aimSensitivity = Mathf.Clamp(aimSensitivity, .2f, 1.5f); mouseDpi = Mathf.Clamp(mouseDpi, 100, 32000);
            fov = Mathf.Clamp(fov, 50, 85); viewModelFov = Mathf.Clamp(viewModelFov, 55, 90);
            volume = Mathf.Clamp01(volume); effectsVolume = Mathf.Clamp01(effectsVolume); feedbackVolume = Mathf.Clamp01(feedbackVolume);
            shake = Mathf.Clamp01(shake); weaponMotion = Mathf.Clamp01(weaponMotion); moveSpeedScale = Mathf.Clamp(moveSpeedScale, .75f, 1.25f);
            difficulty = Mathf.Clamp(difficulty, 0, 2); quality = Mathf.Clamp(quality, 0, 2); resolution = Mathf.Clamp(resolution, 0, 2);
            windowMode = Mathf.Clamp(windowMode, 0, 1); frameLimit = Mathf.Clamp(frameLimit, 0, 4); crosshairColor = Mathf.Clamp(crosshairColor, 0, 4);
            crosshairSize = Mathf.Clamp(crosshairSize, 2, 12); crosshairGap = Mathf.Clamp(crosshairGap, 1, 12); crosshairThickness = Mathf.Clamp(crosshairThickness, 1, 4); crosshairOpacity = Mathf.Clamp(crosshairOpacity, .3f, 1);
            var defaults = DefaultBindings();
            if (actions == null || actions.Length != defaults.Length) actions = defaults;
            for (int i = 0; i < actions.Length; i++)
            {
                if (actions[i] == null) actions[i] = defaults[i];
                if (!string.IsNullOrEmpty(actions[i].primary) && !BindingInput.Valid(actions[i].primary)) actions[i].primary = defaults[i].primary;
                if (!string.IsNullOrEmpty(actions[i].secondary) && !BindingInput.Valid(actions[i].secondary)) actions[i].secondary = "";
            }
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
        public static string FilePath => Path.Combine(RunStorage.Root, "settings-v2.json");
        public static GameOptions Load()
        {
            GameOptions settings;
            if (File.Exists(FilePath))
            {
                try { settings = JsonUtility.FromJson<GameOptions>(File.ReadAllText(FilePath)); if (settings == null) throw new IOException("Empty settings"); settings.Validate(); return settings; }
                catch { File.Copy(FilePath, FilePath + ".backup-" + DateTime.Now.ToString("yyyyMMddHHmmss"), true); }
            }
            settings = new GameOptions();
            settings.sensitivity = PlayerPrefs.GetFloat("SH.sensitivity", 1); settings.fov = PlayerPrefs.GetFloat("SH.fov", 60);
            settings.volume = PlayerPrefs.GetFloat("SH.volume", .7f); settings.shake = PlayerPrefs.GetFloat("SH.shake", .35f);
            settings.invertY = PlayerPrefs.GetInt("SH.invert", 0) != 0; settings.difficulty = PlayerPrefs.GetInt("SH.difficulty", 1); settings.quality = PlayerPrefs.GetInt("SH.quality", 1);
            for (int i = 0; i < 9; i++) { string key = PlayerPrefs.GetString("SH.key" + i, ""); if (BindingInput.Valid("K:" + key)) settings.actions[i].primary = "K:" + key; }
            settings.Validate(); return settings;
        }
        public static void Save(GameOptions settings)
        {
            settings.Validate(); Directory.CreateDirectory(RunStorage.Root);
            string temporary = FilePath + ".tmp"; File.WriteAllText(temporary, JsonUtility.ToJson(settings, true));
            File.Copy(temporary, FilePath, true); File.Delete(temporary);
        }
    }
}
