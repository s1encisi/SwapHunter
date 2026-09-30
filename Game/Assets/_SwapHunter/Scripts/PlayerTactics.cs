using UnityEngine;
namespace SwapHunter {
public sealed class PlayerTactics:MonoBehaviour {
 public const float ShieldDuration=4,ShieldCooldown=10;
 public float ShieldLeft {get;private set;} public float ShieldCooldownLeft {get;private set;}
 public int ShieldBlocks {get;private set;}
 public Vector3 ShieldPosition=>barrier?barrier.position:Vector3.zero;
 public bool ShieldActive=>ShieldLeft>0&&barrier;
 PlayerMotor owner;Transform barrier;Vector3 normal;float flash;readonly MaterialPropertyBlock tint=new MaterialPropertyBlock();
 DemoGame G=>DemoGame.I;
 public void Initialize(PlayerMotor player){owner=player;}
 public void ResetCombat(){EndShield(false);ShieldCooldownLeft=0;ShieldBlocks=0;}
 void Update(){
  if(!owner||!G.IsPlaying)return;
  ShieldCooldownLeft=Mathf.Max(0,ShieldCooldownLeft-Time.deltaTime);
  if(ShieldLeft>0){ShieldLeft-=Time.deltaTime;if(ShieldLeft<=0)EndShield(true);}
  if(barrier){flash=Mathf.Max(0,flash-Time.deltaTime);tint.SetColor("_BaseColor",new Color(.18f,.88f,1,flash>0?.28f:.12f));barrier.GetComponent<Renderer>().SetPropertyBlock(tint);}
  if(G.Pressed(20))ActivateShield();
 }
 public bool ActivateShield(){
  if(!G.IsPlaying||owner.health<=0||ShieldCooldownLeft>0||ShieldActive)return false;
  normal=owner.Eye.forward.normalized;Vector3 p=owner.Eye.position+normal*1.35f;Quaternion rot=Quaternion.LookRotation(normal,owner.Eye.up);
  if(Physics.CheckBox(p,new Vector3(1.35f,1.2f,.06f),rot,Layers.WorldMask,QueryTriggerInteraction.Ignore)){G.Toast("屏障展开空间被阻挡",1.5f);return false;}
  barrier=Shapes.Box("A-shield directional barrier",G.effects,p,new Vector3(2.7f,2.4f,.07f),9).transform;barrier.rotation=rot;
  Vector3[] corners={new Vector3(-.5f,-.5f,0),new Vector3(.5f,-.5f,0),new Vector3(.5f,.5f,0),new Vector3(-.5f,.5f,0)};
  for(int i=0;i<4;i++)Shapes.Line(barrier,barrier.TransformPoint(corners[i]),barrier.TransformPoint(corners[(i+1)%4]),new Color(.2f,.95f,1),.022f);
  ShieldLeft=ShieldDuration;ShieldCooldownLeft=ShieldCooldown;G.sound.Play("phase");G.Record("a_shield_open",p+" facing "+normal);G.Toast("A盾已展开 · 固定朝向，侧背仍有风险",2);return true;
 }
 public bool Intercept(Vector3 from,Vector3 to,out Vector3 point){
  point=Vector3.zero;if(!ShieldActive)return false;
  float side=Vector3.Dot(from-barrier.position,normal),end=Vector3.Dot(to-barrier.position,normal);
  if(side<0||end>0||side-end<.00001f)return false;
  float t=side/(side-end);point=Vector3.Lerp(from,to,t);Vector3 local=Quaternion.Inverse(barrier.rotation)*(point-barrier.position);
  if(Mathf.Abs(local.x)>1.35f||Mathf.Abs(local.y)>1.2f)return false;
  ShieldBlocks++;flash=.07f;G.sound.Play("shield",point);Shapes.Burst(point,new Color(.25f,.9f,1),.2f);G.Record("a_shield_block",point.ToString());return true;
 }
 public bool BlockDirect(Vector3 source,Vector3 target){return Intercept(source,target,out var p);}
 void EndShield(bool feedback){bool active=barrier;ShieldLeft=0;if(barrier)Destroy(barrier.gameObject);barrier=null;if(active&&feedback){G.sound.Play("confirm");G.Record("a_shield_end","duration");}}
 void OnDisable(){EndShield(false);}
}
public sealed partial class DemoGame {
 void DrawTacticsHud(){DrawDeploymentHud();if(tutorialCourse&&tutorialCourse.Active&&tutorialCourse.Step<5)return;if(!player||!player.tactics)return;var t=player.tactics;Fill(new Rect(32,654,520,75),new Color(.015f,.03f,.045f,.76f));string s=t.ShieldActive?"持续 "+t.ShieldLeft.ToString("F1")+"秒":t.ShieldCooldownLeft>0?"冷却 "+t.ShieldCooldownLeft.ToString("F1")+"秒":"就绪";Text(KeyName(20)+"  A盾 · "+s,new Rect(49,658,488,28),18,t.ShieldActive?Cyan:Muted);
  if(player.combatTools&&(!tutorialCourse||!tutorialCourse.Active||tutorialCourse.Step>=6)){var tools=player.combatTools;string toolText=KeyName(21)+" 近战";if(!tutorialCourse||!tutorialCourse.Active||tutorialCourse.Step>=7)toolText+="   "+KeyName(22)+" 飞刀 · "+tools.KnifeStatus+"   "+(tools.KnifeRank>=3?KeyName(23)+" 召回":"研究 "+tools.KnifeRank+"/3");Text(toolText,new Rect(49,693,488,28),17,Muted);}}
}
}
