using UnityEngine;
namespace SwapHunter
{
 public sealed partial class DemoGame
 {
  void DrawD2Context()
  {
   if(!IsPlaying||!player)return;
   if(D2HoldInteraction.HasCandidate)
   {
    float x=viewWidth*.5f-295;Text(D2HoldInteraction.Hint,new Rect(x,737,590,32),19,Cyan,TextAnchor.MiddleCenter);
    Fill(new Rect(x,771,590,4),new Color(.1f,.2f,.25f));
    Fill(new Rect(x,771,590*Mathf.Clamp01(D2HoldInteraction.Progress),4),Cyan);
   }
   if(player.classSkill&&player.classSkill.ScanActive)
   {
    foreach(var relay in FireControlRelay.ActiveSnapshot())
    {
     if(!relay)continue;string summary=relay.ScanSummary(player.Eye.position,ClassSkillTuning.ScanRange);
     if(string.IsNullOrEmpty(summary))continue;
     Fill(new Rect(32,260,540,65),new Color(.01f,.04f,.07f,.92f));Text(summary,new Rect(48,270,508,50),18,Cyan);
    }
   }
  }
 }
}

