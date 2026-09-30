using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
namespace SwapHunter
{
 public sealed partial class DemoGame
 {
  Coroutine displayChange;float adaptiveTimer,adaptiveFrameTotal;int adaptiveFrames;
  void ApplyDisplayOptions()
  {
   if(displayChange!=null)StopCoroutine(displayChange);
   displayChange=StartCoroutine(ApplyDisplayRoutine());
  }
  IEnumerator ApplyDisplayRoutine()
  {
   var layouts=new List<DisplayInfo>();Screen.GetDisplayLayout(layouts);
   if(layouts.Count>0)
   {
    int index=Mathf.Clamp(options.displayIndex,0,layouts.Count-1);var display=layouts[index];
    if(!Screen.mainWindowDisplayInfo.Equals(display))
    {var move=Screen.MoveMainWindowTo(display,new Vector2Int(40,40));yield return move;}
   }
   if(options.windowMode==1)
   {
    var d=Screen.mainWindowDisplayInfo;
    Screen.SetResolution(d.width,d.height,FullScreenMode.FullScreenWindow);
   }
   else Screen.SetResolution(options.resolutionWidth,options.resolutionHeight,FullScreenMode.Windowed);
   displayChange=null;
  }
  void UpdateAdaptiveResolution()
  {
   if(!IsPlaying||!options.dynamicResolution)return;
   adaptiveTimer+=Time.unscaledDeltaTime;adaptiveFrameTotal+=Time.unscaledDeltaTime;adaptiveFrames++;
   if(adaptiveTimer<1)return;
   if(UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset pipeline)
   {
    float average=adaptiveFrameTotal/Mathf.Max(1,adaptiveFrames);
    float target=1f/Mathf.Min(90,Application.targetFrameRate>0?Application.targetFrameRate:60);
    float delta=average>target*1.12f?-.05f:average<target*.88f?.025f:0;
    pipeline.renderScale=Mathf.Clamp(pipeline.renderScale+delta,options.dynamicResolutionMin,options.renderScale);
   }
   adaptiveTimer=adaptiveFrameTotal=0;adaptiveFrames=0;
  }
 }
}


