using System.Collections.Generic;
using UnityEngine;
namespace SwapHunter
{
 public sealed class DirectionalBarrier:MonoBehaviour
 {
  static readonly List<DirectionalBarrier> active=new List<DirectionalBarrier>();
  public float Remaining{get;private set;}
  public Vector3 Normal=>transform.forward;
  public Transform Owner{get;private set;}
  public int Blocks{get;private set;}
  public static DirectionalBarrier Spawn(Vector3 position,Vector3 forward,float duration,Transform owner)
  {
   var g=DemoGame.I;var root=Shapes.Box("A盾 / 固定方向",g.effects,position,new Vector3(2.7f,2.4f,.06f),9).transform;
   if(forward.sqrMagnitude<.001f)forward=Vector3.forward;root.rotation=Quaternion.LookRotation(forward.normalized,Vector3.up);
   var barrier=root.gameObject.AddComponent<DirectionalBarrier>();barrier.Remaining=duration;barrier.Owner=owner;
   // Feet and chevrons distinguish front/back without relying on colour.
   for(int side=-1;side<=1;side+=2){Shapes.Box("Shield footing",root,new Vector3(side*.44f,-.5f,0),new Vector3(.09f,.035f,5),3);Shapes.Box("Front-chevron",root,new Vector3(side*.24f,.1f,-.6f),new Vector3(.035f,.3f,.25f),5);}
   active.Add(barrier);return barrier;
  }
  void Update(){if(!DemoGame.I||!DemoGame.I.IsPlaying)return;Remaining-=Time.deltaTime;if(Remaining<=0)Destroy(gameObject);}
  void OnDestroy(){active.Remove(this);}
  bool Cross(Vector3 from,Vector3 to,out Vector3 point)
  {
   point=default;float a=Vector3.Dot(from-transform.position,Normal),b=Vector3.Dot(to-transform.position,Normal);
   if(a<=.0001f||b>=0||a-b<=.0001f)return false;point=Vector3.Lerp(from,to,a/(a-b));
   Vector3 local=transform.InverseTransformPoint(point);return Mathf.Abs(local.x)<=.5f&&Mathf.Abs(local.y)<=.5f;
  }
  static DirectionalBarrier Find(Vector3 from,Vector3 to,out Vector3 point)
  {
   point=default;DirectionalBarrier nearest=null;float best=float.PositiveInfinity;
   foreach(var barrier in active)if(barrier&&barrier.Remaining>0&&barrier.Cross(from,to,out var p)){float d=(p-from).sqrMagnitude;if(d<best){best=d;nearest=barrier;point=p;}}
   return nearest;
  }
  public static bool Intercept(Vector3 from,Vector3 to,out Vector3 point)
  {
   var found=Find(from,to,out point);if(!found)return false;found.Blocks++;
   DemoGame.I.sound.Play("shield",point);Shapes.Burst(point,new Color(.3f,.9f,1),.12f);DemoGame.I.Record("directional_shield_block","direction=front-to-back;owner="+(found.Owner?found.Owner.name:"departed"));return true;
  }
  public static float ExplosionMultiplier(Vector3 from,Vector3 to)=>Find(from,to,out _)? .35f:1;
  public static bool BlocksSegment(Vector3 from,Vector3 to)=>Find(from,to,out _);
 }
}

