using System.Collections.Generic;
using UnityEngine;
namespace SwapHunter
{
 public sealed partial class PlayerMotor
 {
  float advanceClock=-1,flightLeft;Vector3 advanceDirection,flightVelocity;
  SwapTarget bufferedSwap;float bufferedUntil;
  public bool TacticalAdvanceActive=>advanceClock>=0&&advanceClock<.42f;
  public bool JumpTransportActive=>flightLeft>0;
  public bool BeginTacticalAdvance()
  {
   if(!G.IsPlaying||!Grounded||health<=0||SwapActionsLocked||TacticalAdvanceActive||IsVaulting)return false;
   Vector2 input=new Vector2((G.Held(3)?1:0)-(G.Held(2)?1:0),(G.Held(0)?1:0)-(G.Held(1)?1:0));
   var direction=input.sqrMagnitude>.01f?transform.right*input.x+transform.forward*input.y:Vector3.ProjectOnPlane(Eye.forward,Vector3.up);
   if(direction.sqrMagnitude<.01f)return false;advanceDirection=direction.normalized;advanceClock=0;horizontal=Vector3.zero;
   CancelFireSequence();D2HoldInteraction.CancelForAction();G.sound.Play("vault");G.Record("tactical_advance_start","8m;cooldown_never_refreshed");return true;
  }
  internal void TickV2Travel(float dt)
  {
   if(advanceClock>=0){advanceClock+=dt;if(advanceClock>=.42f)advanceClock=-1;}
   if(flightLeft>0){flightLeft-=dt;if(flightLeft<=0){flightLeft=0;flightVelocity=Vector3.zero;horizontal=Vector3.zero;}}
   if(bufferedSwap&&Time.time>bufferedUntil)bufferedSwap=null;
   if(bufferedSwap&&!TacticalAdvanceActive){var target=bufferedSwap;bufferedSwap=null;RequestSwap(target);}
  }
  internal Vector3 V2DesiredVelocity(Vector3 normal)
  {
   if(TacticalAdvanceActive)return advanceClock>=.06f&&advanceClock<.30f?advanceDirection*(8/.24f):Vector3.zero;
   return flightLeft>0?flightVelocity:normal;
  }
  internal bool BufferV2Swap(SwapTarget candidate)
  {
   if(!TacticalAdvanceActive)return false;
   if(advanceClock>=.32f&&candidate){bufferedSwap=candidate;bufferedUntil=Time.time+.10f;}
   G.Toast("推进位移中，换位将在动作结束时重新检查",1);return true;
  }
  public void StopV2Travel(){if(JumpTransportActive||TacticalAdvanceActive)horizontal=Vector3.zero;advanceClock=-1;flightLeft=0;flightVelocity=Vector3.zero;bufferedSwap=null;}
  public bool CanLaunchTo(Vector3 from,Vector3 destination,out string reason)
   =>PlanJumpV2(from,destination,out _,out _,out reason);
  bool PlanJumpV2(Vector3 from,Vector3 destination,out Vector3 velocity,out float duration,out string reason)
  {
   velocity=Vector3.zero;duration=0;reason="";Vector3 flat=Vector3.ProjectOnPlane(destination-from,Vector3.up);
   if(Mathf.Abs(flat.magnitude-20)>.05f){reason="投送水平距离应为20米";return false;}
   const float gravity=22;float apex=Mathf.Max(from.y,destination.y)+4;
   float vy=Mathf.Sqrt(2*gravity*(apex-from.y));duration=vy/gravity+Mathf.Sqrt(2*(apex-destination.y)/gravity);
   velocity=flat/duration+Vector3.up*vy;Vector3 previous=from;
   for(int i=1;i<=32;i++)
   {
    float t=duration*i/32;Vector3 next=from+velocity*t+Vector3.down*.5f*gravity*t*t,delta=next-previous;
    if(Physics.CapsuleCast(previous+Vector3.up*.37f,previous+Vector3.up*1.48f,.29f,delta.normalized,out var hit,delta.magnitude,Layers.WorldMask,QueryTriggerInteraction.Ignore))
    {if(i<32||hit.normal.y<.65f){reason="投送轨迹被实体结构阻挡："+hit.collider.name;return false;}}
    previous=next;
   }
   if(!StandingRoom(destination)){reason="大跳落点净空不足";return false;}
   return true;
  }
  public bool LaunchTo(Vector3 destination)
  {
   if(!G.IsPlaying||!Grounded||health<=0||SwapActionsLocked||TacticalAdvanceActive||IsVaulting)return false;
   if(!PlanJumpV2(transform.position,destination,out var velocity,out float duration,out string reason)){G.Toast(reason,2);return false;}
   verticalSpeed=velocity.y;flightVelocity=Vector3.ProjectOnPlane(velocity,Vector3.up);flightLeft=duration;horizontal=flightVelocity;
   G.sound.Play("vault");G.Record("jump_transport","horizontal=20;seconds="+duration+";landing="+destination);return true;
  }
 }
 public sealed class V2JumpPad:MonoBehaviour
 {
  static readonly List<V2JumpPad> active=new List<V2JumpPad>();
  public Vector3 Landing{get;private set;}
  public Vector3 LaunchFeet=>transform.position;
  LineRenderer preview;
  public static V2JumpPad Create(Transform parent,Vector3 launchFeet,Vector3 landingFeet)
  {
   if(Mathf.Abs(Vector3.ProjectOnPlane(landingFeet-launchFeet,Vector3.up).magnitude-20)>.05f)throw new System.ArgumentException("Jump pad must travel 20 horizontal metres");
   var go=new GameObject("20米投送板 / 站稳后按空格");go.transform.SetParent(parent,false);go.transform.localPosition=launchFeet;
   var pad=go.AddComponent<V2JumpPad>();pad.Landing=parent.TransformPoint(landingFeet);
   Shapes.Box("发射压板",go.transform,Vector3.up*.06f,new Vector3(2,.12f,2),5);
   var arrow=Shapes.Box("方向箭头",go.transform,Vector3.up*.14f,new Vector3(.3f,.05f,1.5f),10);
   arrow.transform.rotation=Quaternion.LookRotation(Vector3.ProjectOnPlane(pad.Landing-pad.LaunchFeet,Vector3.up));
   D2WorldText.Create(go.transform,"↑ 空格投送 20米 / 可重复",pad.LaunchFeet+Vector3.up*1.1f,.25f);
   Shapes.Ring(parent,pad.Landing+Vector3.up*.04f,1.4f,new Color(.3f,.9f,1),.06f);
   pad.preview=Shapes.Line(go.transform,pad.LaunchFeet,pad.Landing,new Color(.3f,.85f,1),.025f);pad.preview.positionCount=25;
   float apex=Mathf.Max(pad.LaunchFeet.y,pad.Landing.y)+4,vy=Mathf.Sqrt(44*(apex-pad.LaunchFeet.y)),duration=vy/22+Mathf.Sqrt(2*(apex-pad.Landing.y)/22);
   Vector3 velocity=Vector3.ProjectOnPlane(pad.Landing-pad.LaunchFeet,Vector3.up)/duration+Vector3.up*vy;
   for(int i=0;i<25;i++){float t=duration*i/24;pad.preview.SetPosition(i,pad.LaunchFeet+velocity*t-Vector3.up*11*t*t+Vector3.up*.15f);}pad.preview.enabled=false;return pad;
  }
  void OnEnable(){active.Add(this);}void OnDisable(){active.Remove(this);}
  void Update(){if(preview)preview.enabled=DemoGame.I&&DemoGame.I.IsPlaying&&DemoGame.I.player&&(DemoGame.I.player.transform.position-LaunchFeet).sqrMagnitude<64;}
  public static bool TryLaunch(PlayerMotor player)
  {
   foreach(var pad in active)
   {
    if(!pad)continue;var d=player.transform.position-pad.LaunchFeet;
    if(Mathf.Abs(d.y)>.3f||Mathf.Abs(d.x)>.9f||Mathf.Abs(d.z)>.9f)continue;
    Vector3 target=player.transform.position+Vector3.ProjectOnPlane(pad.Landing-pad.LaunchFeet,Vector3.up);target.y=pad.Landing.y;
    player.LaunchTo(target);return true;
   }
   return false;
  }
 }
}

