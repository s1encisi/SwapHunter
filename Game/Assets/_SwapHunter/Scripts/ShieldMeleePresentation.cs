using UnityEngine;
namespace SwapHunter
{
 public sealed partial class EnemyActor
 {
  public bool ShieldBladeVisible=>!boss&&kind==EnemyKind.Shield&&VisualRig&&VisualRig.BladeVisible;
  public bool ShieldRifleVisible=>!boss&&kind==EnemyKind.Shield&&VisualRig&&VisualRig.RifleVisible;
  public float ShieldBladeGripError=>VisualRig?VisualRig.BladeGripError:float.PositiveInfinity;
  public float ShieldMeleePoseOffset=>VisualRig?VisualRig.MeleePoseOffset:0;
  public Vector3 ShieldBladePosition=>VisualRig?VisualRig.BladePosition:transform.position;
  void InitializeShieldMeleePresentation()
  {
   if(!VisualRig)throw new System.InvalidOperationException("Shield guard needs the new actor visual adapter");
   VisualRig.SetShieldMelee(TacticalState.Approach,0);
  }
  public void UpdateShieldMeleePresentation(TacticalState state,float seconds)
  {if(!boss&&VisualRig)VisualRig.SetShieldMelee(state,seconds);}
  void DisableShieldMeleePresentation(){if(VisualRig)VisualRig.DisableShieldMelee();}
 }
}