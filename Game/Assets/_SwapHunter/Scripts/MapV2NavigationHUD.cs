using UnityEngine;
namespace SwapHunter {public sealed partial class DemoGame{
 static readonly string[] V2ShortTasks={
 "完成一次Q校准，处理指定守卫与本章小Boss","突破盾线，处理指定威胁后从货检口撤离","登上冷却井上层取通行件，击败本章小Boss",
 "取回凭据，突破本章小Boss后撤离","取得双塔授权，击败火控小Boss后撤离","摧毁两处封锁装置，完成本章小Boss战",
 "任意顺序接通三座继电座，再通过本章小Boss","任取两份数据匣，再突破本章小Boss","取顶层核心，沿永久通道下撤并击败小Boss",
 "关闭三站切断未落地增援，再通过小Boss","取得维护权限；固定干扰可绕行，击败小Boss","先通过门卫，再完成双授权、核心与最终Boss"};
 public string V2HudObjective{
 get{if(!expeditionActive||mission==null)return Objective;return mission.CanExtract?"主任务完成 · 前往出口撤离":V2ShortTasks[expeditionLevel]+(mission.Definition.requiredObjectives>0?" · 目标 "+mission.CompletedObjectives+"/"+mission.Definition.requiredObjectives:"")+" · 指定威胁 "+ExpeditionRequiredEnemies;}
 }
 void DrawV2ObjectiveGuide(){
 if(!IsPlaying||!expeditionActive||mission==null||RunStorage.IsDirectedTrial||!player||expeditionMap==null)return;
 Vector3 point=Vector3.zero;string label="";float best=float.PositiveInfinity;var layout=expeditionMap.root.GetComponent<MissionLayout>();
 if(mission.CanExtract){foreach(var n in expeditionMap.nodes)if(n&&n.kind==ExpeditionNodeKind.Extraction){point=n.Point;label="撤离 / "+n.label;break;}}
 else{
  bool miniPending=false;foreach(var m in mission.Members)if(!string.IsNullOrEmpty(m.group.miniBossId)&&!m.Defeated)miniPending=true;
  if(expeditionLevel==11&&miniPending){point=layout.MiniBossPosts[0]+Vector3.up;label="前厅 / 相位门卫";}
  else if(mission.Definition.minimumSwaps>totalSwaps&&expeditionMap.anchors.Count>0){point=expeditionMap.anchors[0].AimPoint;label="校准目标 / 实际完成一次Q";}
  else{
   foreach(var n in expeditionMap.nodes){
    if(!n||!layout.TryObjective(n,out var id)||mission.ObjectiveComplete(id))continue;
    if(expeditionLevel==7&&mission.CompletedObjectives>=2)continue;
    if(expeditionLevel==11&&id=="c"&&(!mission.ObjectiveComplete("a")||!mission.ObjectiveComplete("b")))continue;
    float distance=(n.Point-player.Eye.position).sqrMagnitude;if(distance<best){best=distance;point=n.Point;label="主目标 / "+n.label;}
   }
   if(label.Length==0)foreach(var m in mission.Members)if(m.role.required&&!m.Defeated){
    point=m.actor?m.actor.AimPoint:layout.Spawn(m.group.id,m.index).position+Vector3.up;label=string.IsNullOrEmpty(m.group.miniBossId)?"指定威胁 / "+m.group.label:"本章小Boss / "+m.group.label;break;
   }
  }
 }
 if(label.Length==0)return;
 Vector3 v=player.cameraView.WorldToViewportPoint(point);Vector2 screen=ViewportToUi(v);
 if(v.z<0){screen.x=player.Eye.InverseTransformPoint(point).x>0?viewWidth-180:180;label="转身 · "+label;screen.y=425;}
 screen.x=Mathf.Clamp(screen.x,175,viewWidth-175);screen.y=Mathf.Clamp(screen.y,410,526);if(Mathf.Abs(screen.x-viewWidth*.5f)<185&&Mathf.Abs(screen.y-450)<65)screen.y=520;
 if(Vector3.Distance(player.transform.position,point)<4)return;
 string text="◇ "+label+"  "+Vector3.Distance(player.transform.position,point).ToString("0")+"m";
 Fill(new Rect(screen.x-170,screen.y-16,340,42),new Color(.015f,.04f,.06f,.8f));Text(text,new Rect(screen.x-160,screen.y-10,320,34),15,Cyan,TextAnchor.MiddleCenter);
 }
}}

