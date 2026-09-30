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
        Vector2 bindingScroll;
        readonly List<Vector2Int> availableResolutions = new List<Vector2Int>();
        readonly List<DisplayInfo> availableDisplays = new List<DisplayInfo>();
        readonly Dictionary<string, string> numberFields = new Dictionary<string, string>();
        GUIStyle inputStyle, smallButton, clearButtonStyle;
        internal GameOptions PendingOptions => settingsDraft;
        internal void SelectSettingsTab(int tab) { settingsTab = Mathf.Clamp(tab, 0, 5); bindingCapture = -1; pendingConflict = -1; numberFields.Clear(); }
        internal void SetBindingScrollForReview(float y) { bindingScroll.y = Mathf.Max(0, y); }
        public void OpenSettings()
        {
            RefreshDisplayChoices(); bindingScroll = Vector2.zero; resetConfirmation = false;
            settingsOpen = true; settingsDraft = options.Clone(); numberFields.Clear(); settingsNotice = SettingsStore.LastWarning; bindingCapture = -1; pendingConflict = -1;
        }
        public void CancelSettings()
        {
            settingsOpen = false; settingsDraft = null; bindingCapture = -1; pendingConflict = -1; resetConfirmation = false; numberFields.Clear();
        }
        void RefreshDisplayChoices()
        {
            availableResolutions.Clear();
            foreach (var mode in Screen.resolutions)
            {
                var size = new Vector2Int(mode.width, mode.height);
                if (size.x >= 960 && size.y >= 540 && !availableResolutions.Contains(size)) availableResolutions.Add(size);
            }
            foreach (var size in new[] { new Vector2Int(1280,720), new Vector2Int(1600,900), new Vector2Int(1920,1080), new Vector2Int(options.resolutionWidth,options.resolutionHeight) })
                if (!availableResolutions.Contains(size)) availableResolutions.Add(size);
            availableResolutions.Sort((a,b) => a.x == b.x ? a.y.CompareTo(b.y) : a.x.CompareTo(b.x));
            availableDisplays.Clear(); Screen.GetDisplayLayout(availableDisplays);
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
            Fill(FullScreenGuiRect, new Color(.015f,.022f,.045f,1));
            bool outerEnabled = GUI.enabled;
            GUI.enabled = outerEnabled && pendingConflict < 0 && !resetConfirmation;
            float x = Mathf.Max(36, (viewWidth - 1240) / 2), width = Mathf.Min(1240, viewWidth - 72), cx = x + 222, cw = width - 222;
            Fill(new Rect(0,0,14,900), Cyan);
            Fill(new Rect(viewWidth-176,0,176,900), new Color(.025f,.055f,.13f));
            Text("SETTINGS / 操作终端", new Rect(x, 29, 760, 30), 16, Cyan);
            Text("设置", new Rect(x-3, 68, 300, 62), 44);
            Text("调整后点击应用；取消会保留原设置。", new Rect(x+222, 98, 750, 32), 17, Muted);
            string[] tabs = { "鼠标与操作", "键位绑定", "画面与性能", "准星", "声音", "动作与难度" };
            for (int i = 0; i < tabs.Length; i++)
                if (Button(tabs[i], new Rect(x, 173 + i * 65, 184, 51), settingsTab == i)) { settingsTab = i; bindingCapture = -1; pendingConflict = -1; bindingScroll = Vector2.zero; numberFields.Clear(); }
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
                    if (Button("蹲伏方式：" + (settingsDraft.toggleCrouch ? "切换" : "按住"), new Rect(cx + col + 36, 437, col, 45))) settingsDraft.toggleCrouch = !settingsDraft.toggleCrouch;
                    float cm = 360f / (.04f * settingsDraft.sensitivity * settingsDraft.mouseDpi) * 2.54f;
                    Text("约 " + cm.ToString("F1") + " cm / 360°", new Rect(cx + col + 36, 528, col, 42), 27, Cyan);
                    Text("按所填 DPI 估算；请与鼠标实际 DPI 一致。\n鼠标增量不额外加入平滑或加速。", new Rect(cx + col + 36, 580, col, 115), 17, Muted);
                    break;
                case 1:
                    Text("动作", new Rect(cx, 164, 175, 35), 18, Muted);
                    float bindW = (cw - 193) / 2;
                    Text("主绑定", new Rect(cx + 180, 164, bindW, 35), 18, Muted);
                    Text("副绑定", new Rect(cx + 180 + bindW, 164, bindW, 35), 18, Muted);
                    bindingScroll = GUI.BeginScrollView(new Rect(cx, 208, cw, 526), bindingScroll, new Rect(0,0,cw-22,GameOptions.ActionNames.Length * 34));
                    for (int i = 0; i < GameOptions.ActionNames.Length; i++)
                    {
                        float y = i * 34;
                        if (i % 2 == 0) Fill(new Rect(0,y,cw-25,32), new Color(.028f,.045f,.08f));
                        Text(GameOptions.ActionNames[i], new Rect(10,y,165,30), 17);
                        for (int slot = 0; slot < 2; slot++)
                        {
                            int capture = i * 2 + slot; string binding = slot == 0 ? settingsDraft.actions[i].primary : settingsDraft.actions[i].secondary;
                            string caption = bindingCapture == capture ? "等待输入…" : BindingInput.Label(binding);
                            float bx = 180 + slot * bindW;
                            if (SmallButton(caption,new Rect(bx,y+2,bindW-40,28))) { bindingCapture=capture; captureAfter=Time.unscaledTime+.2f; pendingConflict=-1; }
                            if (ClearBindingButton(new Rect(bx+bindW-37,y+2,28,28))) { if(slot==0)settingsDraft.actions[i].primary="";else settingsDraft.actions[i].secondary=""; }
                        }
                    }
                    GUI.EndScrollView();
                    Text("向下滚动查看蹲伏、背包与消耗品。Esc 取消捕获；F12 保留截图。",new Rect(cx,741,cw,31),16,Muted);
                    break;
                case 2:
                    Text("显示与性能",new Rect(cx,164,cw,40),25,Cyan);
                    settingsDraft.windowMode=Cycle("显示模式",settingsDraft.windowMode,new[]{"窗口","无边框全屏"},cx,217,col);
                    var monitorNames = new string[Mathf.Max(1,availableDisplays.Count)];
                    for(int i=0;i<monitorNames.Length;i++)monitorNames[i]=availableDisplays.Count==0?"当前显示器":(i+1)+" / "+availableDisplays[i].name;
                    settingsDraft.displayIndex=Cycle("显示器",Mathf.Clamp(settingsDraft.displayIndex,0,monitorNames.Length-1),monitorNames,cx,303,col);
                    if(availableResolutions.Count==0)RefreshDisplayChoices();
                    string[] resolutionNames=new string[availableResolutions.Count];int selectedResolution=0;
                    for(int i=0;i<availableResolutions.Count;i++){var size=availableResolutions[i];resolutionNames[i]=size.x+" × "+size.y;if(size.x==settingsDraft.resolutionWidth&&size.y==settingsDraft.resolutionHeight)selectedResolution=i;}
                    int nextResolution=Cycle("窗口分辨率",selectedResolution,resolutionNames,cx,389,col);
                    settingsDraft.resolutionWidth=availableResolutions[nextResolution].x;settingsDraft.resolutionHeight=availableResolutions[nextResolution].y;
                    int nextQuality=Cycle("画质 / 阴影与特效",settingsDraft.quality,new[]{"简约","均衡","精细"},cx,475,col);
                    if(nextQuality!=settingsDraft.quality){settingsDraft.quality=nextQuality;settingsDraft.renderScale=new[]{.7f,.85f,1f}[nextQuality];numberFields.Clear();}
                    settingsDraft.frameLimit=Cycle("帧率上限",settingsDraft.frameLimit,new[]{"60","120","144","240","不限"},cx,561,col);
                    settingsDraft.vSync=Toggle("垂直同步",settingsDraft.vSync,cx,657,col);
                    float rx=cx+col+36;
                    settingsDraft.fov=Numeric("垂直视野角度",settingsDraft.fov,50,85,rx,217,col);
                    settingsDraft.viewModelFov=Numeric("武器视野角度",settingsDraft.viewModelFov,55,90,rx,303,col);
                    settingsDraft.renderScale=Numeric("最高渲染比例",settingsDraft.renderScale,.5f,1.2f,rx,389,col);
                    settingsDraft.dynamicResolution=Toggle("动态调整渲染比例",settingsDraft.dynamicResolution,rx,479,col);
                    bool canEdit=GUI.enabled;GUI.enabled=canEdit&&settingsDraft.dynamicResolution;
                    settingsDraft.dynamicResolutionMin=Numeric("最低渲染比例",Mathf.Min(settingsDraft.dynamicResolutionMin,settingsDraft.renderScale),.5f,settingsDraft.renderScale,rx,547,col);
                    GUI.enabled=canEdit;
                    Text("无边框使用所选屏幕的桌面分辨率。\n动态比例优先维持流畅；界面保持清晰。\n垂直同步开启时受显示刷新率限制。",new Rect(rx,652,col,100),16,Muted);
                    break;
                case 3:
                    Text("准星预览", new Rect(cx, 164, cw, 40), 25, Cyan);
                    settingsDraft.crosshairSize = Numeric("线条长度", settingsDraft.crosshairSize, 2, 12, cx, 222, col);
                    settingsDraft.crosshairGap = Numeric("中心间隙", settingsDraft.crosshairGap, 1, 12, cx, 310, col);
                    settingsDraft.crosshairThickness = Numeric("线条粗细", settingsDraft.crosshairThickness, 1, 4, cx, 398, col);
                    settingsDraft.crosshairOpacity = Numeric("不透明度", settingsDraft.crosshairOpacity, .3f, 1, cx, 486, col);
                    settingsDraft.crosshairColor = Cycle("颜色", settingsDraft.crosshairColor, new[] { "电蓝", "白色", "亮绿", "琥珀", "粉色" }, cx, 574, col);
                    Fill(new Rect(cx + col + 36, 221, col, 205), new Color(.085f,.13f,.17f)); DrawCrosshair(cx + col + 36 + col / 2, 324, settingsDraft, 0);
                    settingsDraft.dynamicCrosshair = Toggle("动态显示精度变化", settingsDraft.dynamicCrosshair, cx + col + 36, 452, col);
                    settingsDraft.crosshairDot = Toggle("中心点", settingsDraft.crosshairDot, cx + col + 36, 518, col);
                    settingsDraft.hitMarkers = Toggle("命中与击破提示", settingsDraft.hitMarkers, cx + col + 36, 584, col);
                    break;
                case 4:
                    Text("声音分层",new Rect(cx,164,cw,40),25,Cyan);
                    settingsDraft.volume=Numeric("主音量",settingsDraft.volume,0,1,cx,222,col);
                    settingsDraft.effectsVolume=Numeric("武器与战斗音效",settingsDraft.effectsVolume,0,1,cx,312,col);
                    settingsDraft.feedbackVolume=Numeric("命中反馈",settingsDraft.feedbackVolume,0,1,cx,402,col);
                    settingsDraft.musicVolume=Numeric("音乐",settingsDraft.musicVolume,0,1,cx+col+36,222,col);
                    settingsDraft.ambientVolume=Numeric("环境氛围",settingsDraft.ambientVolume,0,1,cx+col+36,312,col);
                    settingsDraft.uiVolume=Numeric("界面提示",settingsDraft.uiVolume,0,1,cx+col+36,402,col);
                    settingsDraft.muteWhenUnfocused=Toggle("切到其他窗口时静音",settingsDraft.muteWhenUnfocused,cx,516,cw);
                    Text("主音量控制全部声音；其余滑杆分别调整对应声音。\n调整界面提示音不影响枪声与命中反馈。",new Rect(cx,594,cw,90),18,Muted);
                    break;
                case 5:
                    Text("动作与挑战",new Rect(cx,164,cw,40),25,Cyan);
                    settingsDraft.shake=Numeric("镜头动作反馈",settingsDraft.shake,0,1,cx,222,col);
                    settingsDraft.weaponMotion=Numeric("武器晃动强度",settingsDraft.weaponMotion,0,1,cx,324,col);
                    settingsDraft.moveSpeedScale=Numeric("移动速度倍率",settingsDraft.moveSpeedScale,.75f,1.25f,cx,426,col);
                    settingsDraft.uiScale=Numeric("界面缩放（0.8–1.0）",settingsDraft.uiScale,.8f,1,cx,646,col);
                    settingsDraft.difficulty=Cycle("难度",settingsDraft.difficulty,new[]{"轻松","标准","硬核"},cx+col+36,222,col);
                    Text("降低晃动不会移除射击后坐力。\n\n移动、腾空和连续射击仍影响精度；蹲伏、点射和停步帮助控制弹着。",new Rect(cx+col+36,344,col,185),19,Muted);
                    Text("基础行走  "+(config.walkSpeed*settingsDraft.moveSpeedScale).ToString("F1")+" m/s    /    冲刺  "+(config.sprintSpeed*settingsDraft.moveSpeedScale).ToString("F1")+" m/s",new Rect(cx,564,cw,55),24,Cyan);
                    break;
            }
            Text(settingsNotice, new Rect(cx, 781, cw, 27), 16, Cyan);
            if (Button("恢复本页默认", new Rect(x, 818, 210, 47))) resetConfirmation = true;
            if (Button("取消", new Rect(x + width - 370, 818, 165, 47))) CancelSettings();
            if (Button("应用并返回", new Rect(x + width - 192, 818, 192, 47), true)) { ApplyDraft(); CancelSettings(); }
            GUI.enabled = outerEnabled;
            if (pendingConflict >= 0)
            {
                Modal("绑定冲突", BindingInput.Label(pendingCode) + " 已用于“" + GameOptions.ActionNames[pendingConflict / 2] + "”。\n交换两个绑定？若当前槽为空，原动作将被清空。", () => { settingsDraft.Bind(bindingCapture / 2, bindingCapture % 2, pendingCode, true); pendingConflict = -1; bindingCapture = -1; }, () => { pendingConflict = -1; bindingCapture = -1; });
            }
            else if (resetConfirmation) Modal("恢复本页默认", "只重置当前页面，点击“应用并返回”后保存。", ResetSettingsTab, () => resetConfirmation = false);
        }
        bool SmallButton(string label, Rect rect)
        {
            return Button(label, rect);
        }
        bool ClearBindingButton(Rect rect)
        {
            return Button("×", rect);
        }
        bool Toggle(string label, bool value, float x, float y, float width) => Button(label + "   " + (value ? "开" : "关"), new Rect(x,y,width,45), value) ? !value : value;
        int Cycle(string label, int value, string[] values, float x, float y, float width)
        {
            Text(label, new Rect(x,y,width,30),18,Muted);
            value=Mathf.Clamp(value,0,values.Length-1);
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
            Fill(FullScreenGuiRect,new Color(0,0,0,.7f));float x=viewWidth/2-300;
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
                case 0:settingsDraft.sensitivity=d.sensitivity;settingsDraft.verticalSensitivity=d.verticalSensitivity;settingsDraft.aimSensitivity=d.aimSensitivity;settingsDraft.mouseDpi=d.mouseDpi;settingsDraft.invertY=false;settingsDraft.toggleAim=false;settingsDraft.toggleSprint=false;settingsDraft.toggleCrouch=false;break;
                case 1:settingsDraft.actions=GameOptions.DefaultBindings();break;
                case 2:settingsDraft.fov=d.fov;settingsDraft.viewModelFov=d.viewModelFov;settingsDraft.windowMode=d.windowMode;settingsDraft.resolution=d.resolution;settingsDraft.quality=d.quality;settingsDraft.frameLimit=d.frameLimit;settingsDraft.vSync=d.vSync;settingsDraft.displayIndex=d.displayIndex;settingsDraft.resolutionWidth=d.resolutionWidth;settingsDraft.resolutionHeight=d.resolutionHeight;settingsDraft.refreshRateHz=d.refreshRateHz;settingsDraft.renderScale=d.renderScale;settingsDraft.dynamicResolution=d.dynamicResolution;settingsDraft.dynamicResolutionMin=d.dynamicResolutionMin;break;
                case 3:settingsDraft.crosshairSize=d.crosshairSize;settingsDraft.crosshairGap=d.crosshairGap;settingsDraft.crosshairThickness=d.crosshairThickness;settingsDraft.crosshairOpacity=d.crosshairOpacity;settingsDraft.crosshairColor=d.crosshairColor;settingsDraft.crosshairDot=d.crosshairDot;settingsDraft.dynamicCrosshair=d.dynamicCrosshair;settingsDraft.hitMarkers=d.hitMarkers;break;
                case 4:settingsDraft.volume=d.volume;settingsDraft.effectsVolume=d.effectsVolume;settingsDraft.feedbackVolume=d.feedbackVolume;settingsDraft.musicVolume=d.musicVolume;settingsDraft.ambientVolume=d.ambientVolume;settingsDraft.uiVolume=d.uiVolume;settingsDraft.muteWhenUnfocused=d.muteWhenUnfocused;break;
                case 5:settingsDraft.uiScale=d.uiScale;settingsDraft.shake=d.shake;settingsDraft.weaponMotion=d.weaponMotion;settingsDraft.moveSpeedScale=d.moveSpeedScale;settingsDraft.difficulty=d.difficulty;break;
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




