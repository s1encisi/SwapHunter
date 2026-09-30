using System.Collections.Generic;
using UnityEngine;
namespace SwapHunter
{
 public sealed partial class DemoGame
 {
  List<string> uiNavigation=new List<string>(),uiPrevious=new List<string>();
  string uiFocus="",uiActivate="",uiPressed="";int navigationFrame=-1;
  public string FocusedButton=>uiFocus;
  void BeginUiNavigation()
  {
   if(navigationFrame==Time.frameCount)return;navigationFrame=Time.frameCount;
   uiPrevious=uiNavigation;uiNavigation=new List<string>();
   if(IsPlaying||settingsOpen||qaMode&&!qaInputEnabled||KeyboardDevice==null)return;
   var keyboard=KeyboardDevice;
   bool forward=keyboard.tabKey.wasPressedThisFrame&&!keyboard.shiftKey.isPressed||keyboard.downArrowKey.wasPressedThisFrame;
   bool backward=keyboard.tabKey.wasPressedThisFrame&&keyboard.shiftKey.isPressed||keyboard.upArrowKey.wasPressedThisFrame;
   if((forward||backward)&&uiPrevious.Count>0)
   {
    int index=uiPrevious.IndexOf(uiFocus);if(index<0)index=backward?0:-1;index=(index+(backward?-1:1)+uiPrevious.Count)%uiPrevious.Count;
    uiFocus=uiPrevious[index];sound.Play("confirm");
   }
   if((keyboard.enterKey.wasPressedThisFrame||keyboard.spaceKey.wasPressedThisFrame)&&uiPrevious.Contains(uiFocus))uiActivate=uiFocus;
  }
  bool NavigationButton(string value,Rect r,bool primary)
  {
   bool enabled=GUI.enabled;string id=value+"@"+r.x+","+r.y;
   if(enabled&&!uiNavigation.Contains(id))uiNavigation.Add(id);
   bool hover=r.Contains(Event.current.mousePosition)&&enabled,focus=enabled&&uiFocus==id;
   if(Event.current.type==EventType.MouseDown&&Event.current.button==0&&hover){uiPressed=id;uiFocus=id;}
   bool down=enabled&&uiPressed==id&&(Event.current.type==EventType.MouseDown||MouseDevice!=null&&MouseDevice.leftButton.isPressed);
   Color background=!enabled?new Color(.035f,.045f,.068f):down?new Color(.05f,.22f,.47f):hover||focus?new Color(.13f,.35f,.63f):primary?new Color(.07f,.31f,.90f):new Color(.055f,.080f,.13f);
   GUI.enabled=true;Fill(r,background);
   Fill(new Rect(r.x,r.y,hover||focus?5:2,r.height),primary||hover||focus?Cyan:new Color(.24f,.31f,.42f));
   Fill(new Rect(r.x,r.yMax-1,r.width,focus?3:1),focus?Cyan:hover?Color.white:new Color(.18f,.24f,.34f));
   int size=r.width<70?17:r.width<160?16:r.width<260?18:20;bool wrap=textStyle.wordWrap;if(r.width<70)textStyle.wordWrap=false;
   Text(value,new Rect(r.x+(r.width<70?0:10)+(down?2:0),r.y+(down?2:0),r.width-(r.width<70?0:20),r.height),size,enabled?Color.white:new Color(.49f,.54f,.63f),r.width<70?TextAnchor.MiddleCenter:TextAnchor.MiddleLeft);textStyle.wordWrap=wrap;
   GUI.enabled=enabled;bool clicked=GUI.Button(r,GUIContent.none,GUIStyle.none);
   if(Event.current.rawType==EventType.MouseUp)uiPressed="";
   if(enabled&&uiActivate==id){uiActivate="";clicked=true;}
   if(clicked)sound.Play("confirm");return clicked;
  }
 }
}
