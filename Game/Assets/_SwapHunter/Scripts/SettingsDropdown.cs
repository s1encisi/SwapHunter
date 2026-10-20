using System;
using UnityEngine;
namespace SwapHunter
{
    public sealed partial class DemoGame
    {
        string settingsDropdownId;
        string[] settingsDropdownChoices;
        Rect settingsDropdownAnchor, settingsDropdownBounds;
        Vector2 settingsDropdownScroll;
        int settingsDropdownIndex;
        internal bool SettingsDropdownOpen => !string.IsNullOrEmpty(settingsDropdownId);
        void CloseSettingsDropdown()
        {
            settingsDropdownId=null;settingsDropdownChoices=null;settingsDropdownScroll=Vector2.zero;
        }
        int SettingsDropdown(string label,int selected,string[] choices,Rect rect)
        {
            if(choices==null||choices.Length==0)return 0;
            selected=Mathf.Clamp(selected,0,choices.Length-1);
            if(Button(choices[selected]+"  ▾",rect,settingsDropdownId==label))
            {
                settingsDropdownId=label;settingsDropdownChoices=choices;settingsDropdownAnchor=rect;
                settingsDropdownIndex=selected;settingsDropdownScroll=Vector2.zero;PositionSettingsDropdown();
            }
            return selected;
        }
        void PositionSettingsDropdown()
        {
            float height=Mathf.Min(350,settingsDropdownChoices.Length*42+12);
            float below=settingsDropdownAnchor.yMax+6;
            float y=below+height<=800?below:Mathf.Max(150,settingsDropdownAnchor.y-height-6);
            settingsDropdownBounds=new Rect(settingsDropdownAnchor.x,y,settingsDropdownAnchor.width,height);
        }
        void CommitSettingsDropdown(int selected)
        {
            if(settingsDraft==null||!SettingsDropdownOpen)return;
            selected=Mathf.Clamp(selected,0,settingsDropdownChoices.Length-1);
            // Input changes the draft immediately. Repainting is presentation;
            // it must not be required to commit an accepted keyboard selection.
            switch(settingsDropdownId)
            {
                case "难度":settingsDraft.difficulty=selected;break;
                case "显示模式":settingsDraft.windowMode=selected;break;
                case "显示器":settingsDraft.displayIndex=selected;break;
                case "窗口分辨率":
                    var resolution=availableResolutions[selected];
                    settingsDraft.resolutionWidth=resolution.x;settingsDraft.resolutionHeight=resolution.y;break;
                case "画质 / 阴影与特效":
                    settingsDraft.quality=selected;settingsDraft.renderScale=new[]{.7f,.85f,1f}[selected];numberFields.Clear();break;
                case "帧率上限":settingsDraft.frameLimit=selected;break;
                case "颜色":settingsDraft.crosshairColor=selected;break;
                case "反转 Y 轴":settingsDraft.invertY=selected==1;break;
                case "瞄准方式":settingsDraft.toggleAim=selected==1;break;
                case "冲刺方式":settingsDraft.toggleSprint=selected==1;break;
                case "蹲伏方式":settingsDraft.toggleCrouch=selected==1;break;
                case "垂直同步":settingsDraft.vSync=selected==1;break;
                case "动态调整渲染比例":settingsDraft.dynamicResolution=selected==1;break;
                case "动态显示精度变化":settingsDraft.dynamicCrosshair=selected==1;break;
                case "中心点":settingsDraft.crosshairDot=selected==1;break;
                case "命中与击破提示":settingsDraft.hitMarkers=selected==1;break;
                case "切到其他窗口时静音":settingsDraft.muteWhenUnfocused=selected==1;break;
                default:throw new InvalidOperationException("Unbound settings dropdown: "+settingsDropdownId);
            }
            CloseSettingsDropdown();
        }
        void ConsumeSettingsDropdownOutsideClick()
        {
            if(!SettingsDropdownOpen)return;
            var e=Event.current;
            if(e.type==EventType.MouseDown&&!settingsDropdownBounds.Contains(e.mousePosition))
            {CloseSettingsDropdown();e.Use();GUIUtility.hotControl=0;}
        }
        bool UpdateSettingsDropdownInput()
        {
            if(!SettingsDropdownOpen)return false;
            var keys=KeyboardDevice;if(keys==null)return true;
            if(keys.escapeKey.wasPressedThisFrame){CloseSettingsDropdown();return true;}
            if(keys.downArrowKey.wasPressedThisFrame)settingsDropdownIndex=Mathf.Min(settingsDropdownChoices.Length-1,settingsDropdownIndex+1);
            if(keys.upArrowKey.wasPressedThisFrame)settingsDropdownIndex=Mathf.Max(0,settingsDropdownIndex-1);
            float row=settingsDropdownIndex*42,visible=settingsDropdownBounds.height-12;
            if(row<settingsDropdownScroll.y)settingsDropdownScroll.y=row;
            if(row+42>settingsDropdownScroll.y+visible)settingsDropdownScroll.y=row+42-visible;
            if(keys.enterKey.wasPressedThisFrame||keys.numpadEnterKey.wasPressedThisFrame)
            CommitSettingsDropdown(settingsDropdownIndex);
            return true;
        }
        void DrawSettingsDropdown()
        {
            if(!SettingsDropdownOpen)return;
            Rect bounds=settingsDropdownBounds;
            Fill(new Rect(bounds.x-3,bounds.y-3,bounds.width+6,bounds.height+6),new Color(.32f,.39f,.39f));
            Fill(bounds,new Color(.08f,.13f,.16f));
            bool enabled=GUI.enabled;GUI.enabled=true;
            bool scroll=settingsDropdownChoices.Length*42+12>bounds.height;
            Rect inner=new Rect(bounds.x+6,bounds.y+6,bounds.width-12,bounds.height-12);
            settingsDropdownScroll=GUI.BeginScrollView(inner,settingsDropdownScroll,new Rect(0,0,inner.width-(scroll?18:0),settingsDropdownChoices.Length*42),false,scroll);
            for(int i=0;i<settingsDropdownChoices.Length;i++)
            {
                if(Button((i==settingsDropdownIndex?"✓  ":"    ")+settingsDropdownChoices[i],new Rect(0,i*42,inner.width-(scroll?18:0),40),i==settingsDropdownIndex))
                {CommitSettingsDropdown(i);break;}
            }
            GUI.EndScrollView();GUI.enabled=enabled;
        }
    }
}
