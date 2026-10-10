using UnityEngine;
namespace SwapHunter
{
 public sealed partial class EnemyActor
 {
  LineRenderer bossEnergyRing;
  public bool BossBladeVisible=>VisualRig&&VisualRig.BladeVisible;
  public bool BossRifleVisible=>VisualRig&&VisualRig.RifleVisible;
  public void InitializeBossPresentation()
  {
   if(!VisualRig)throw new System.InvalidOperationException("Commander needs the new actor visual adapter");
   VisualRig.ConvertToBoss();RefreshVisualReferences();
   bossEnergyRing=Shapes.Ring(transform,new Vector3(0,.08f,0),.75f,new Color(.12f,.8f,1),.035f);
   bossEnergyRing.useWorldSpace=false;bossEnergyRing.transform.localPosition=Vector3.zero;bossEnergyRing.transform.localRotation=Quaternion.identity;bossEnergyRing.transform.localScale=Vector3.one;
   BossReadabilityVisual.Attach(boss);UpdateBossPresentation(true,0,0,AimPoint+transform.forward);
  }
  public void UpdateBossPresentation(bool melee,float windup,float swing,Vector3 aim)
  {
   if(bossEnergyRing)bossEnergyRing.gameObject.SetActive(boss&&boss.Energy>0&&Alive);
   if(VisualRig)VisualRig.SetBossPresentation(melee,windup,swing,aim);
  }
 }
}