using UnityEngine;
namespace SwapHunter
{
 public sealed partial class EnemyActor
 {
  Transform shieldBlade,shieldBladeHand,shieldMeleeArm,shieldMeleeForearm;
  Quaternion shieldArmRest,shieldForearmRest,shieldArmReady,shieldForearmReady;
  Renderer[] shieldRifleRenderers;
  static readonly Vector3 ShieldBladeGrip=new Vector3(0,-.0275f,.04f);
  public bool ShieldBladeVisible=>shieldBlade&&shieldBlade.gameObject.activeInHierarchy;
  public bool ShieldRifleVisible
  {
   get {if(shieldRifleRenderers!=null)foreach(var r in shieldRifleRenderers)if(r&&r.enabled&&r.gameObject.activeInHierarchy)return true;return false;}
  }
  public float ShieldBladeGripError=>shieldBlade&&shieldBladeHand?Vector3.Distance(shieldBlade.TransformPoint(new Vector3(0,0,-.105f)),shieldBladeHand.TransformPoint(ShieldBladeGrip)):float.PositiveInfinity;
  public float ShieldMeleePoseOffset=>shieldMeleeArm&&shieldMeleeForearm?Mathf.Max(Quaternion.Angle(shieldMeleeArm.localRotation,shieldArmReady),Quaternion.Angle(shieldMeleeForearm.localRotation,shieldForearmReady)):0;
  public Vector3 ShieldBladePosition=>shieldBlade?shieldBlade.position:transform.position;

  void InitializeShieldMeleePresentation()
  {
   shieldBladeHand=ImportedModels.Find(model,"hand_R");shieldMeleeArm=ImportedModels.Find(model,"arm_R");shieldMeleeForearm=ImportedModels.Find(model,"forearm_R");
   if(!shieldBladeHand||!shieldMeleeArm||!shieldMeleeForearm||!weaponPivot)throw new System.InvalidOperationException("Shield melee weapon rig is incomplete");
   var prefab=Resources.Load<GameObject>("BlenderActors/combat-knife-v05");if(!prefab)throw new System.InvalidOperationException("Shield melee phase blade asset is missing");
   shieldArmRest=shieldMeleeArm.localRotation;shieldForearmRest=shieldMeleeForearm.localRotation;
   // The imported rig is X-mirrored below the coordinate-correction wrapper: local Y/Z rotations have opposite signs to authoring space.
   shieldArmReady=shieldArmRest*Quaternion.Euler(0,0,-35);shieldForearmReady=shieldForearmRest*Quaternion.Euler(-15,0,0);
   shieldRifleRenderers=weaponPivot.GetComponentsInChildren<Renderer>();foreach(var r in shieldRifleRenderers)if(r)r.enabled=false;
   const float scale=1.45f;
   shieldBlade=ImportedModels.Create(prefab,shieldBladeHand,Vector3.zero,Vector3.one*scale).transform;shieldBlade.name="Shield guard melee phase blade";
   shieldBlade.localRotation=Quaternion.Euler(-25,0,12);shieldBlade.localPosition=ShieldBladeGrip-shieldBlade.localRotation*(new Vector3(0,0,-.105f)*scale);ImportedModels.SetLayer(shieldBlade,Layers.Actor);
   UpdateShieldMeleePresentation(TacticalState.Approach,0);
  }
  public void UpdateShieldMeleePresentation(TacticalState state,float seconds)
  {
   if(!shieldBlade||boss)return;
   Quaternion arm=shieldArmReady,forearm=shieldForearmReady;
   Quaternion windupArm=shieldArmRest*Quaternion.Euler(-25,30,-55),windupForearm=shieldForearmRest*Quaternion.Euler(-65,0,10);
   Quaternion strikeArm=shieldArmRest*Quaternion.Euler(15,20,-75),strikeForearm=shieldForearmRest*Quaternion.Euler(60,0,-5);
   if(state==TacticalState.MeleeWarning)
   {
    float progress=Mathf.Clamp01(1-seconds/.65f);
    if(progress<.7f){float t=Mathf.SmoothStep(0,1,progress/.7f);arm=Quaternion.Slerp(shieldArmReady,windupArm,t);forearm=Quaternion.Slerp(shieldForearmReady,windupForearm,t);}
    else {float t=Mathf.SmoothStep(0,1,(progress-.7f)/.3f);arm=Quaternion.Slerp(windupArm,strikeArm,t);forearm=Quaternion.Slerp(windupForearm,strikeForearm,t);}
   }
   else if(state==TacticalState.Recover)
   {
    float t=Mathf.SmoothStep(0,1,Mathf.Clamp01(1-seconds/1.1f));arm=Quaternion.Slerp(strikeArm,shieldArmReady,t);forearm=Quaternion.Slerp(strikeForearm,shieldForearmReady,t);
   }
   shieldMeleeArm.localRotation=arm;shieldMeleeForearm.localRotation=forearm;
  }
  void DisableShieldMeleePresentation()
  {
   if(!shieldBlade)return;shieldBlade.gameObject.SetActive(false);
   if(shieldMeleeArm)shieldMeleeArm.localRotation=shieldArmRest;
   if(shieldMeleeForearm)shieldMeleeForearm.localRotation=shieldForearmRest;
   foreach(var r in shieldRifleRenderers)if(r)r.enabled=true;
  }
 }
}
