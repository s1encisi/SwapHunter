using UnityEngine;
namespace SwapHunter {public sealed partial class PlayerMotor{
 internal bool V2WeaponRay(Vector3 cameraDirection,out RaycastHit hit,out Vector3 endpoint,out Ray actualRay,out bool shielded)
 {
  float range=EffectiveWeaponStats.Read(weapon,G.config).Range;
  Vector3 intended=Eye.position+cameraDirection*range;
  if(EnemyActor.CastWeaponRay(Eye.position,cameraDirection,range,transform,out var aimed))intended=aimed.point;
  Vector3 origin=TracerOrigin();actualRay=new Ray(origin,(intended-origin).normalized);shielded=false;
  bool found;
  if(Physics.Linecast(Eye.position,origin,out hit,Layers.WorldMask,QueryTriggerInteraction.Ignore))found=true;
  else found=EnemyActor.CastWeaponRay(origin,actualRay.direction,Mathf.Max(.05f,Vector3.Distance(origin,intended))+.03f,transform,out hit);
  endpoint=found?hit.point:intended;
  if(DirectionalBarrier.Intercept(origin,endpoint,out var shieldPoint)){endpoint=shieldPoint;shielded=true;return false;}
  return found;
 }
}}

