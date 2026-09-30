using UnityEngine;
namespace SwapHunter
{
 public sealed partial class EnemyActor
 {
  Transform bossBlade,bossForearm;Quaternion bossForearmRest;Renderer[] bossGunRenderers;LineRenderer bossEnergyRing;
  public bool BossBladeVisible=>bossBlade&&bossBlade.gameObject.activeInHierarchy;
  public bool BossRifleVisible=>bossGunRenderers!=null&&bossGunRenderers.Length>0&&bossGunRenderers[0].enabled;
  public void InitializeBossPresentation()
  {
   var hand=ImportedModels.Find(model,"hand_R");bossForearm=ImportedModels.Find(model,"forearm_R");
   if(!hand||!bossForearm||!weaponPivot)throw new System.InvalidOperationException("Commander weapon rig is incomplete");
   bossForearmRest=bossForearm.localRotation;bossGunRenderers=weaponPivot.GetComponentsInChildren<Renderer>();
   var blade=Resources.Load<GameObject>("BlenderActors/combat-knife-v05");if(!blade)throw new System.InvalidOperationException("Commander phase blade asset is missing");
   bossBlade=ImportedModels.Create(blade,hand,Vector3.zero,Vector3.one*1.7f).transform;bossBlade.name="Commander melee phase blade";
   bossBlade.localRotation=Quaternion.Euler(-25,0,12);bossBlade.localPosition=new Vector3(0,-.0275f,.04f)-bossBlade.localRotation*(new Vector3(0,0,-.105f)*1.7f);ImportedModels.SetLayer(bossBlade,Layers.Actor);
   bossEnergyRing=Shapes.Ring(transform,new Vector3(0,.08f,0),.75f,new Color(.12f,.8f,1),.035f);bossEnergyRing.useWorldSpace=false;bossEnergyRing.transform.localPosition=Vector3.zero;bossEnergyRing.transform.localRotation=Quaternion.identity;bossEnergyRing.transform.localScale=Vector3.one;
   UpdateBossPresentation(true,0,0,AimPoint+transform.forward);
  }
  public void UpdateBossPresentation(bool melee,float windup,float swing,Vector3 aim)
  {
   if(!bossBlade)return;bossBlade.gameObject.SetActive(melee);
   foreach(var renderer in bossGunRenderers)if(renderer)renderer.enabled=!melee;
   if(bossEnergyRing)bossEnergyRing.gameObject.SetActive(boss&&boss.Energy>0&&Alive);
   bossForearm.localRotation=melee?bossForearmRest*Quaternion.Euler(-50*windup+75*swing,0,-10*windup):bossForearmRest;
   if(!melee){Vector3 local=transform.InverseTransformDirection(aim-AimPoint);weaponPivot.localRotation=Quaternion.Euler(-Mathf.Atan2(local.y,new Vector2(local.x,local.z).magnitude)*Mathf.Rad2Deg,0,0);}
  }
 }
}
