using UnityEngine;
namespace SwapHunter
{
 // Original 8-bar score. The loop is generated locally and has no third-party recordings.
 public static class PhaseScore
 {
  public static AudioClip Create()
  {
   const int rate=48000;const float bpm=112;float beat=60/bpm,duration=beat*32;
   var data=new float[Mathf.RoundToInt(rate*duration)];
   int[] roots={45,41,48,43};int[] notes={0,7,12,7,3,10,12,10};
   for(int i=0;i<data.Length;i++)
   {
    float t=i/(float)rate;int step=Mathf.FloorToInt(t/(beat*.5f));int bar=Mathf.FloorToInt(t/(beat*4));float local=t%(beat*.5f);
    int midi=roots[(bar/2)%4]+notes[step%8];float hz=440*Mathf.Pow(2,(midi-69)/12f);
    float env=Mathf.Sin(Mathf.PI*Mathf.Clamp01(local/(beat*.5f)))*Mathf.Exp(-local*7);
    float arp=(Mathf.Sin(2*Mathf.PI*hz*t)+.25f*Mathf.Sin(4*Mathf.PI*hz*t))*env*.06f;
    float bt=t%beat;float kick=Mathf.Sin(2*Mathf.PI*(47*bt+5*(1-Mathf.Exp(-35*bt))))*Mathf.Exp(-bt*27)*.075f;
    float bassHz=440*Mathf.Pow(2,(roots[(bar/2)%4]-12-69)/12f);float bass=Mathf.Sin(2*Mathf.PI*bassHz*t)*.027f;
    float edge=Mathf.Min(1,t/.02f)*Mathf.Clamp01((duration-t)/.02f);data[i]=(arp+kick+bass)*edge;
   }
   var clip=AudioClip.Create("Original phase score",data.Length,1,rate,false);clip.SetData(data,0);return clip;
  }
 }
}

