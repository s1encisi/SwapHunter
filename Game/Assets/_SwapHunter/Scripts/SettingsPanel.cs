using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SwapHunter
{
    public sealed partial class DemoGame
    {
        GameOptions settingsDraft;
        int settingsTab, pendingConflict = -1;
        string pendingCode, settingsNotice = "";
        bool resetConfirmation;
        float captureAfter;
        readonly Dictionary<string, string> numberFields = new Dictionary<string, string>();
        GUIStyle inputStyle, smallButton, clearButtonStyle;
        internal GameOptions PendingOptions => settingsDraft;
        internal void SelectSettingsTab(int tab) { settingsTab = Mathf.Clamp(tab, 0, 4); bindingCapture = -1; pendingConflict = -1; numberFields.Clear(); }
        public void OpenSettings()
        {
            settingsOpen = true; settingsDraft = options.Clone(); numberFields.Clear(); settingsNotice = ""; bindingCapture = -1; pendingConflict = -1;
        }
        public void CancelSettings()
        {
            settingsOpen = false; settingsDraft = null; bindingCapture = -1; pendingConflict = -1; resetConfirmation = false; numberFields.Clear();
        }
        void UpdateSettingsInput()
        {
            if (settingsDraft == null) settingsDraft = options.Clone();
            var keyboard = KeyboardDevice; var mouse = MouseDevice;
            if (bindingCapture < 0)
            {
                if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) CancelSettings();
                return;
            }
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) { bindingCapture = -1; pendingConflict = -1; return; }
            if (pendingConflict >= 0 || Time.unscaledTime < captureAfter) return;
            string code = null;
            if (keyboard != null) foreach (var key in keyboard.allKeys) if (key.wasPressedThisFrame) { code = "K:" + key.keyCode; break; }
            if (mouse != null)
            {
                if (mouse.leftButton.wasPressedThisFrame) code = "M:0";
                else if (mouse.rightButton.wasPressedThisFrame) code = "M:1";
                else if (mouse.middleButton.wasPressedThisFrame) code = "M:2";
                else if (mouse.backButton.wasPressedThisFrame) code = "M:3";
                else if (mouse.forwardButton.wasPressedThisFrame) code = "M:4";
                else if (mouse.scroll.ReadValue().y > .1f) code = "W:Up";
                else if (mouse.scroll.ReadValue().y < -.1f) code = "W:Down";
            }
            if (code == null) return;
            if (code == "K:F12") { settingsNotice = "F12 保留用于截图，请选择其他按键。"; return; }
            int action = bindingCapture / 2, slot = bindingCapture % 2;
            pendingConflict = settingsDraft.Conflict(code, action, slot);
            if (pendingConflict >= 0) { pendingCode = code; return; }
            settingsDraft.Bind(action, slot, code, false); bindingCapture = -1; settingsNotice = "键位已暂存；点击应用后生效。";
        }
        public void ApplyDraft()
        {
            if (settingsDraft == null) return;
            options = settingsDraft.Clone(); options.Validate(); SyncLegacyBindings(); SaveSettings(); ApplySettings(true);
            settingsDraft = options.Clone(); numberFields.Clear(); settingsNotice = "设置已应用并保存。";
        }
        void DrawSettingsV2()
        {
            if (settingsDraft == null) settingsDraft = options.Clone();
            if (inputStyle == null)
            {
                inputStyle = new GUIStyle(GUI.skin.textField) { font = font, fontSize = 17, alignment = TextAnchor.MiddleRight, padding = new RectOffset(8, 8, 3, 3) };
                smallButton = new GUIStyle(buttonStyle) { fontSize = 16, padding = new RectOffset(12, 10, 2, 2) };
                clearButtonStyle = new GUIStyle(smallButton) { alignment = TextAnchor.MiddleCenter, padding = new RectOffset(0,0,0,0), fontSize = 17 };
            }
            Fill(new Rect(0, 0, viewWidth, 900), new Color(.025f,.042f,.055f,1));
            GUI.enabled = pendingConflict < 0 && !resetConfirmation;
            float x = Mathf.Max(36, (viewWidth - 1240) / 2), width = Mathf.Min(1240, viewWidth - 72), cx = x + 222, cw = width - 222;
            Text("设置", new Rect(x, 40, 300, 58), 38);
            Text("调整适合你的操作节奏", new Rect(x, 105, 750, 32), 17, Muted);
            string[] tabs = { "鼠标与操作", "键位绑定", "画面与性能", "准星", "声音与手感" };
            for (int i = 0; i < tabs.Length; i++)
                if (Button(tabs[i], new Rect(x, 173 + i * 65, 184, 51), settingsTab == i)) { settingsTab = i; bindingCapture = -1; pendingConflict = -1; numberFields.Clear(); }
            Fill(new Rect(cx - 20, 164, 1, 590), new Color(.22f,.31f,.35f));
            float col = (cw - 36) / 2;
            switch (settingsTab)
            {
                case 0:
                    Text("精确输入", new Rect(cx, 164, cw, 40), 25, Cyan);
                    settingsDraft.sensitivity = Numeric("基础灵敏度", settingsDraft.sensitivity, .05f, 8, cx, 221, col);
                    settingsDraft.verticalSensitivity = Numeric("垂直灵敏度倍率", settingsDraft.verticalSensitivity, .5f, 2, cx, 312, col);
                    settingsDraft.aimSensitivity = Numeric("瞄准灵敏度倍率", settingsDraft.aimSensitivity, .2f, 1.5f, cx, 403, col);
                    settingsDraft.mouseDpi = Mathf.RoundToInt(Numeric("鼠标 DPI（用于估算）", settingsDraft.mouseDpi, 100, 32000, cx, 494, col, true));
                    settingsDraft.invertY = Toggle("反转 Y 轴", settingsDraft.invertY, cx + col + 36, 221, col);
                    if (Button("瞄准方式：" + (settingsDraft.toggleAim ? "切换" : "按住"), new Rect(cx + col + 36, 293, col, 45))) settingsDraft.toggleAim = !settingsDraft.toggleAim;
                    if (Button("冲刺方式：" + (settingsDraft.toggleSprint ? "切换" : "按住"), new Rect(cx + col + 36, 365, col, 45))) settingsDraft.toggleSprint = !settingsDraft.toggleSprint;
                    float cm = 360f / (.04f * settingsDraft.sensitivity * settingsDraft.mouseDpi) * 2.54f;
                    Text("约 " + cm.ToString("F1") + " cm / 360°", new Rect(cx + col + 36, 469, col, 42), 27, Cyan);
                    Text("按所填 DPI 估算；请与鼠标实际 DPI 一致。\n鼠标增量不额外加入平滑或加速。", new Rect(cx + col + 36, 526, col, 115), 17, Muted);
                    break;
                case 1:
                    Text("动作", new Rect(cx, 164, 195, 35), 18, Muted); Text("主绑定", new Rect(cx + 195, 164, 230, 35), 18, Muted); Text("副绑定", new Rect(cx + 435, 164, 250, 35), 18, Muted);
                    for (int i = 0; i < GameOptions.ActionNames.Length; i++)
                    {
                        float y = 207 + i * 31;
                        Text(GameOptions.ActionNames[i], new Rect(cx, y, 180, 30), 17);
                        for (int slot = 0; slot < 2; slot++)
                        {
                            int capture = i * 2 + slot; string binding = slot == 0 ? settingsDraft.actions[i].primary : settingsDraft.actions[i].secondary;
                            string caption = bindingCapture == capture ? "按键 / 鼠标…" : BindingInput.Label(binding);
                            if (SmallButton(caption, new Rect(cx + 195 + slot * 240, y, 198, 27))) { bindingCapture = capture; captureAfter = Time.unscaledTime + .2f; pendingConflict = -1; }
                            if (ClearBindingButton(new Rect(cx + 396 + slot * 240, y, 30, 27))) { if (slot == 0) settingsDraft.actions[i].primary = ""; else settingsDraft.actions[i].secondary = ""; }
                        }
                    }
                    Text("支持鼠标侧键与滚轮。Esc 始终可返回，F12 保留用于截图。", new Rect(cx, 750, cw, 36), 15, Muted);
                    break;
                case 2:
                    Text("显示与性能", new Rect(cx, 164, cw, 40), 25, Cyan);
                    settingsDraft.windowMode = Cycle("显示模式", settingsDraft.windowMode, new[] { "窗口", "无边框全屏" }, cx, 222, col);
                    settingsDraft.resolution = Cycle("窗口分辨率", settingsDraft.resolution, new[] { "1280 × 720", "1600 × 900", "1920 × 1080" }, cx, 310, col);
                    settingsDraft.quality = Cycle("画质", settingsDraft.quality, new[] { "简约", "均衡", "精细" }, cx, 398, col);
                    settingsDraft.frameLimit = Cycle("帧率上限", settingsDraft.frameLimit, new[] { "60", "120", "144", "240", "不限" }, cx, 486, col);
                    settingsDraft.vSync = Toggle("垂直同步", settingsDraft.vSync, cx, 596, col);
                    settingsDraft.fov = Numeric("垂直视野角度", settingsDraft.fov, 50, 85, cx + col + 36, 222, col);
                    settingsDraft.viewModelFov = Numeric("武器视野角度", settingsDraft.viewModelFov, 55, 90, cx + col + 36, 322, col);
                    Text("垂直同步开启时，实际帧率上限受显示器刷新率控制。\n\n无边框全屏使用桌面分辨率；窗口分辨率只影响窗口模式。", new Rect(cx + col + 36, 445, col, 170), 17, Muted);
                    break;
                case 3:
                    Text("准星预览", new Rect(cx, 164, cw, 40), 25, Cyan);
                    settingsDraft.crosshairSize = Numeric("线条长度", settingsDraft.crosshairSize, 2, 12, cx, 222, col);
                    settingsDraft.crosshairGap = Numeric("中心间隙", settingsDraft.crosshairGap, 1, 12, cx, 310, col);
                    settingsDraft.crosshairThickness = Numeric("线条粗细", settingsDraft.crosshairThickness, 1, 4, cx, 398, col);
                    settingsDraft.crosshairOpacity = Numeric("不透明度", settingsDraft.crosshairOpacity, .3f, 1, cx, 486, col);
                    settingsDraft.crosshairColor = Cycle("颜色", settingsDraft.crosshairColor, new[] { "青绿", "白色", "亮绿", "琥珀", "粉色" }, cx, 574, col);
                    Fill(new Rect(cx + col + 36, 221, col, 205), new Color(.085f,.13f,.17f)); DrawCrosshair(cx + col + 36 + col / 2, 324, settingsDraft, 0);
                    settingsDraft.dynamicCrosshair = Toggle("动态显示精度变化", settingsDraft.dynamicCrosshair, cx + col + 36, 452, col);
                    settingsDraft.crosshairDot = Toggle("中心点", settingsDraft.crosshairDot, cx + col + 36, 518, col);
                    settingsDraft.hitMarkers = Toggle("命中与击破提示", settingsDraft.hitMarkers, cx + col + 36, 584, col);
                    break;
                case 4:
                    Text("声音与动作反馈", new Rect(cx, 164, cw, 40), 25, Cyan);
                    settingsDraft.volume = Numeric("主音量", settingsDraft.volume, 0, 1, cx, 222, col);
                    settingsDraft.effectsVolume = Numeric("枪械与环境音效", settingsDraft.effectsVolume, 0, 1, cx, 310, col);
                    settingsDraft.feedbackVolume = Numeric("命中与界面提示音", settingsDraft.feedbackVolume, 0, 1, cx, 398, col);
                    settingsDraft.shake = Numeric("镜头动作反馈", settingsDraft.shake, 0, 1, cx + col + 36, 222, col);
                    settingsDraft.weaponMotion = Numeric("武器晃动强度", settingsDraft.weaponMotion, 0, 1, cx + col + 36, 310, col);
                    settingsDraft.moveSpeedScale = Numeric("移动速度倍率", settingsDraft.moveSpeedScale, .75f, 1.25f, cx + col + 36, 398, col);
                    settingsDraft.difficulty = Cycle("难度", settingsDraft.difficulty, new[] { "轻松", "标准", "硬核" }, cx, 501, col);
                    Text("卡宾枪行走 " + (config.walkSpeed * settingsDraft.moveSpeedScale).ToString("F1") + " m/s\n冲刺 " + (config.sprintSpeed * settingsDraft.moveSpeedScale).ToString("F1") + " m/s", new Rect(cx + col + 36, 508, col, 84), 21, Cyan);
                    Text("降低镜头/武器晃动不会移除射击后坐力。\n移动、腾空和持续连射仍会影响精度。", new Rect(cx, 623, cw, 70), 17, Muted);
                    break;
            }
            Text(settingsNotice, new Rect(cx, 768, cw, 32), 16, Cyan);
            if (Button("恢复本页默认", new Rect(x, 818, 210, 47))) resetConfirmation = true;
            if (Button("取消", new Rect(x + width - 370, 818, 165, 47))) CancelSettings();
            if (Button("应用并返回", new Rect(x + width - 192, 818, 192, 47), true)) { ApplyDraft(); CancelSettings(); }
            GUI.enabled = true;
            if (pendingConflict >= 0)
            {
                Modal("绑定冲突", BindingInput.Label(pendingCode) + " 已用于“" + GameOptions.ActionNames[pendingConflict / 2] + "”。\n交换两个绑定？若当前槽为空，原动作将被清空。", () => { settingsDraft.Bind(bindingCapture / 2, bindingCapture % 2, pendingCode, true); pendingConflict = -1; bindingCapture = -1; }, () => { pendingConflict = -1; bindingCapture = -1; });
            }
            else if (resetConfirmation) Modal("恢复本页默认", "只重置当前页面，点击“应用并返回”后保存。", ResetSettingsTab, () => resetConfirmation = false);
        }
        bool SmallButton(string label, Rect rect)
        {
            Color old = GUI.backgroundColor; GUI.backgroundColor = new Color(.12f,.18f,.22f); bool result = GUI.Button(rect, label, smallButton); GUI.backgroundColor = old; return result;
        }
        bool ClearBindingButton(Rect rect)
        {
            Color old = GUI.backgroundColor; GUI.backgroundColor = new Color(.12f,.18f,.22f); bool clicked = GUI.Button(rect, "×", clearButtonStyle); GUI.backgroundColor = old; return clicked;
        }
        bool Toggle(string label, bool value, float x, float y, float width) => Button(label + "   " + (value ? "开" : "关"), new Rect(x,y,width,45), value) ? !value : value;
        int Cycle(string label, int value, string[] values, float x, float y, float width)
        {
            Text(label, new Rect(x,y,width,30),18,Muted);
            return Button(values[value] + "    ›",new Rect(x,y+34,width,42)) ? (value+1)%values.Length : value;
        }
        float Numeric(string label, float value, float min, float max, float x, float y, float width, bool integer = false)
        {
            string id="option:"+label;
            Text(label,new Rect(x,y,width-130,36),18);
            if(!numberFields.ContainsKey(id))numberFields[id]=value.ToString(integer?"F0":"F2",CultureInfo.InvariantCulture);
            GUI.SetNextControlName(id); string typed=GUI.TextField(new Rect(x+width-112,y,112,32),numberFields[id],10,inputStyle);
            if(typed!=numberFields[id])
            {
                numberFields[id]=typed;
                if(float.TryParse(typed,NumberStyles.Float,CultureInfo.InvariantCulture,out float number)&&!float.IsNaN(number)&&!float.IsInfinity(number))value=Mathf.Clamp(number,min,max);
            }
            float slider=GUI.HorizontalSlider(new Rect(x,y+48,width,20),value,min,max);
            if(Mathf.Abs(slider-value)>.0001f){value=integer?Mathf.Round(slider):slider;numberFields[id]=value.ToString(integer?"F0":"F2",CultureInfo.InvariantCulture);}
            return value;
        }
        void Modal(string title,string message,Action accept,Action cancel)
        {
            Fill(new Rect(0,0,viewWidth,900),new Color(0,0,0,.7f));float x=viewWidth/2-300;
            Fill(new Rect(x,270,600,310),Ink);Text(title,new Rect(x+28,294,545,52),30);
            Text(message,new Rect(x+28,363,545,105),18,Muted);
            if(Button("确认",new Rect(x+28,502,260,48),true))accept();
            if(Button("取消",new Rect(x+310,502,260,48)))cancel();
        }
        void ResetSettingsTab()
        {
            var d=new GameOptions();
            switch(settingsTab)
            {
                case 0:settingsDraft.sensitivity=d.sensitivity;settingsDraft.verticalSensitivity=d.verticalSensitivity;settingsDraft.aimSensitivity=d.aimSensitivity;settingsDraft.mouseDpi=d.mouseDpi;settingsDraft.invertY=false;settingsDraft.toggleAim=false;settingsDraft.toggleSprint=false;break;
                case 1:settingsDraft.actions=GameOptions.DefaultBindings();break;
                case 2:settingsDraft.fov=d.fov;settingsDraft.viewModelFov=d.viewModelFov;settingsDraft.windowMode=d.windowMode;settingsDraft.resolution=d.resolution;settingsDraft.quality=d.quality;settingsDraft.frameLimit=d.frameLimit;settingsDraft.vSync=d.vSync;break;
                case 3:settingsDraft.crosshairSize=d.crosshairSize;settingsDraft.crosshairGap=d.crosshairGap;settingsDraft.crosshairThickness=d.crosshairThickness;settingsDraft.crosshairOpacity=d.crosshairOpacity;settingsDraft.crosshairColor=d.crosshairColor;settingsDraft.crosshairDot=d.crosshairDot;settingsDraft.dynamicCrosshair=d.dynamicCrosshair;settingsDraft.hitMarkers=d.hitMarkers;break;
                case 4:settingsDraft.volume=d.volume;settingsDraft.effectsVolume=d.effectsVolume;settingsDraft.feedbackVolume=d.feedbackVolume;settingsDraft.shake=d.shake;settingsDraft.weaponMotion=d.weaponMotion;settingsDraft.moveSpeedScale=d.moveSpeedScale;settingsDraft.difficulty=d.difficulty;break;
            }
            numberFields.Clear();resetConfirmation=false;settingsNotice="默认值已暂存，应用后生效。";
        }
        void DrawCrosshair(float x,float y,GameOptions settings,float spread)
        {
            Color[] colors={Cyan,Color.white,new Color(.4f,1,.25f),new Color(1,.76f,.22f),new Color(1,.45f,.8f)};
            Color color=colors[settings.crosshairColor];color.a=settings.crosshairOpacity;
            float gap=settings.crosshairGap+(settings.dynamicCrosshair?Mathf.Min(22,spread*4):0),size=settings.crosshairSize,t=settings.crosshairThickness;
            Fill(new Rect(x-gap-size,y-t/2,size,t),color);Fill(new Rect(x+gap,y-t/2,size,t),color);
            Fill(new Rect(x-t/2,y-gap-size,t,size),color);Fill(new Rect(x-t/2,y+gap,t,size),color);
            if(settings.crosshairDot)Fill(new Rect(x-t/2,y-t/2,t,t),color);
        }
    }
}
