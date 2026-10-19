using UnityEngine;
namespace SwapHunter
{
 public sealed class FocusTutorialGuide:MonoBehaviour
 {
  public TextMesh text;int stage=-1;
  void Update(){var g=DemoGame.I;if(!g||!g.IsPlaying||!text)return;int next=g.totalSwaps>0?2:g.totalShots>0?1:0;if(next==stage)return;stage=next;text.text=stage==0?"01  射击与持刀\n1 / 2 枪械 · R 装填\nE 取刀 · 3 持刀":stage==1?"02  交换两个位置\n瞄准锚点或训练机按 Q\n交换后回看原来的站位":"03  选择实战射角\n侧台守卫是指定目标\n从工房两侧接近监理";text.font.RequestCharactersInTexture(text.text,48);}
 }
 public sealed class FocusSignFit:MonoBehaviour
 {
  public Vector2 panelSize;string fittedText;
  void LateUpdate()
  {
   var text=GetComponent<TextMesh>();if(text.text==fittedText)return;var size=GetComponent<Renderer>().localBounds.size;
   if(size.x<=0||size.y<=0)return;text.characterSize*=Mathf.Min(1,Mathf.Min(panelSize.x*.88f/size.x,panelSize.y*.82f/size.y));fittedText=text.text;
  }
 }
 public sealed class FocusReactorFeedback:MonoBehaviour
 {
  public Renderer[] leftIndicators,rightIndicators,coreIndicators;bool left,right,core;
  static void Set(Renderer[] rs,Color c){var b=new MaterialPropertyBlock();b.SetColor("_BaseColor",c);b.SetColor("_Color",c);foreach(var r in rs)if(r)r.SetPropertyBlock(b);}
  void Update(){var g=DemoGame.I;if(!g||g.mission==null)return;if(!left&&g.MissionObjectiveComplete("a")){left=true;Set(leftIndicators,new Color(.3f,.8f,.65f));}if(!right&&g.MissionObjectiveComplete("b")){right=true;Set(rightIndicators,new Color(.3f,.8f,.65f));}if(!core&&g.ExpeditionMainComplete){core=true;Set(coreIndicators,new Color(.22f,.46f,.50f));}}
 }
 public static partial class ExpeditionWorld
 {
  static GameObject FBox(string name,Vector3 center,Vector3 size,Material material,bool solid=true)=>FocusEnvironment.Box(root,name,center,size,material,solid);
  static void FFloor(string name,float x,float z,float width,float depth,float y=0,bool steel=false)
  {
   var go=FBox(name,V(x,y-.18f,z),V(width,.36f,depth),steel?FocusEnvironment.Steel:FocusEnvironment.Concrete);var f=go.AddComponent<MapV2Footprint>();f.zone=name;f.center=new Vector2(x,z);f.size=new Vector2(width,depth);f.height=y;
  }
  static void FGround(string name,float width,float depth,float centerZ)
  {
   FBox("Continuous reinforced concrete foundation",V(0,-1.58f,centerZ),V(width,3,depth),FocusEnvironment.Concrete);
   var floor=FBox(name,V(0,-.04f,centerZ),V(width,.08f,depth),FocusEnvironment.Road);var f=floor.AddComponent<MapV2Footprint>();f.zone=name;f.center=new Vector2(0,centerZ);f.size=new Vector2(width,depth);f.height=0;
   for(int side=-1;side<=1;side+=2)FBox("Foundation drainage channel",V(side*(width/2-2),.012f,centerZ),V(.38f,.024f,depth-3),FocusEnvironment.Steel,false);
  }
  static void FWall(string name,Vector3 center,Vector3 size)=>FBox(name,center,size,FocusEnvironment.Concrete);
  static void FStairs(Vector3 upperEdge,float rise,float yaw=0,float width=3.6f)
  {
   int steps=Mathf.CeilToInt(rise/.18f);float step=rise/steps,run=.36f,span=(steps-1)*run;var rotation=Quaternion.Euler(0,yaw,0);var direction=rotation*Vector3.forward;var basePoint=upperEdge-Vector3.up*rise-direction*(span-.04f);
   var group=new GameObject("Visible maintenance stair").transform;group.SetParent(root,false);group.localPosition=basePoint;group.localRotation=rotation;
   for(int i=0;i<steps;i++){float h=(i+1)*step;FocusEnvironment.Box(group,"Concrete stair tread",V(0,h/2,i*run),V(width,h,run+.025f),FocusEnvironment.Concrete);FocusEnvironment.Box(group,"Stair nosing",V(0,h+.009f,i*run-.145f),V(width,.018f,.07f),FocusEnvironment.HazardSteel,false);}
  }
  static void FPlatform(string name,Vector3 feet,float width,float depth)
  {
   FFloor(name,feet.x,feet.z,width,depth,feet.y,true);
   foreach(int x in new[]{-1,1})foreach(int z in new[]{-1,1})FocusEnvironment.Cylinder(root,"Platform structural column",V(feet.x+x*(width/2-.45f),(feet.y-.36f)/2,feet.z+z*(depth/2-.45f)),.14f,feet.y-.36f,FocusEnvironment.Steel);
   FBox("Marked open exchange edge",feet+V(width/2-.12f,.015f,0),V(.12f,.03f,depth),FocusEnvironment.HazardSteel,false);
  }
  static void FRefuge(string name,Vector3 feet)
  {
   FPlatform(name,feet+Vector3.up*1.4f,5,5);FStairs(feet+V(0,1.4f,-2.5f),1.4f,0,3.8f);FStairs(feet+V(0,1.4f,2.5f),1.4f,180,3.8f);
   var mark=FBox("Wave refuge designation",feet+V(0,1.41f,0),V(2.2f,.02f,2.2f),FocusEnvironment.HazardSteel,false);Feature(mark,name,"设备检修与防地面泄压的抬高作业面","躲避地波；有前后两个实际坡梯入口","永久可达，不能以A盾代替","相邻枪线、近身退路和Boss地面预警");
  }
  static void FGate(string id,Vector3 feet,float width,string label)
  {
   var gate=FBox(label,feet+V(0,1.7f,0),V(width,3.4f,.45f),FocusEnvironment.PaintedSteel);layout.OpenOn(id,gate,false);FBox("Gate overhead motor",feet+V(0,3.8f,0),V(width+.8f,.45f,.8f),FocusEnvironment.Steel);var t=Label(label,feet+V(0,4.3f,0),.32f);t.GetComponent<ExpeditionWorldLabel>().visibleWith=gate;
  }
  static ExpeditionNode FTerminal(string id,Vector3 feet,string label)
  {
   var n=Objective(id,feet,label);FRepaint(n.gameObject);n.worldLabel.transform.position=feet+Vector3.up*2;n.worldLabel.characterSize=.045f;n.worldLabel.GetComponent<ExpeditionWorldLabel>().maximumDistance=13;return n;
  }
  static void FRepaint(GameObject go)
  {foreach(var renderer in go.GetComponentsInChildren<MeshRenderer>(true)){if(renderer.GetComponent<TextMesh>()||renderer.gameObject.name=="Node light")continue;renderer.sharedMaterial=FocusEnvironment.PaintedSteel;}}
  static void FSupply(Vector3 p,string label,bool medical=false)
  {V2Supply(p,label,medical);FRepaint(map.nodes[map.nodes.Count-1].gameObject);}
  static TextMesh FSign(string text,Vector3 position,float width=3,float yaw=180,float height=1.65f)
  {
   var parent=new GameObject("Physical information sign").transform;parent.SetParent(root,false);parent.localPosition=position;parent.localRotation=Quaternion.Euler(0,yaw,0);
   FocusEnvironment.Box(parent,"Sign metal backing",Vector3.zero,V(width,height,.09f),FocusEnvironment.PaintedSteel);var go=new GameObject("Printed instructions");go.transform.SetParent(parent,false);go.transform.localPosition=V(0,0,.055f);go.transform.localRotation=Quaternion.Euler(0,180,0);var t=go.AddComponent<TextMesh>();t.font=g.font;t.fontSize=48;t.characterSize=.065f;t.anchor=TextAnchor.MiddleCenter;t.alignment=TextAlignment.Center;t.color=new Color(.85f,.88f,.78f);t.text=text;t.font.RequestCharactersInTexture(text,48);var m=new Material(g.config.worldTextMaterial);m.mainTexture=t.font.material.mainTexture;t.GetComponent<MeshRenderer>().sharedMaterial=m;go.AddComponent<WorldFontAtlas>().Initialize(t.font,m);go.AddComponent<FocusSignFit>().panelSize=new Vector2(width,height);return t;
  }
  static void FBoundary(float halfWidth,float front,float rear)
  {
   FWall("Roadside reinforced perimeter",V(-halfWidth,1.6f,(front+rear)/2),V(.5f,3.2f,rear-front));FWall("Controlled-entry rear perimeter",V(0,1.6f,front),V(halfWidth*2,3.2f,.5f));FWall("Industrial station rear wall",V(0,1.6f,rear),V(halfWidth*2,3.2f,.5f));
   FWall("Coastal retaining parapet",V(halfWidth,.55f,(front+rear)/2),V(.5f,1.1f,rear-front));
   for(float z=front;z<=rear;z+=6){FocusEnvironment.Pipe(root,V(halfWidth,1.1f,z),V(halfWidth,2.2f,z),.065f,FocusEnvironment.Steel);}foreach(float y in new[]{1.45f,2.1f})FocusEnvironment.Pipe(root,V(halfWidth,y,front),V(halfWidth,y,rear),.06f,FocusEnvironment.Steel);
  }
  static void FBuilding(string title,Vector3 feet,Vector3 size)
  {
   FBox(title,feet+Vector3.up*size.y/2,size,FocusEnvironment.Concrete);FBox("Weathered standing-seam roof",feet+V(0,size.y+.12f,0),V(size.x+.5f,.24f,size.z+.5f),FocusEnvironment.Steel);
   for(int i=0;i<3;i++)FBox("Recessed industrial window",feet+V(-size.x*.28f+i*size.x*.28f,size.y*.61f,-size.z*.5f-.04f),V(size.x*.18f,1.2f,.08f),FocusEnvironment.PaintedSteel,false);
  }
  static void FStructuralRoof(Vector3 center,Vector3 size)
  {
   FBox("Canopy roof",center,size,FocusEnvironment.Steel);foreach(int x in new[]{-1,1})foreach(int z in new[]{-1,1})FBox("Canopy column",V(center.x+x*(size.x/2-.5f),center.y/2,center.z+z*(size.z/2-.5f)),V(.22f,center.y,.22f),FocusEnvironment.Steel);
  }
  static void FocusedCalibration()
  {
   FocusEnvironment.CoastalContext(root,0);FGround("滨海设备检疫站连续路面",48,92,32);FBoundary(24,-14,78);map.spawn=V(0,.04f,-7);
   // The first room presents one short firing lane, then a parallel exchange lane. The next spaces remain behind a real shutter.
   FBuilding("检疫站西侧设备库",V(-19,0,10),V(7,7,34));FBuilding("检疫站东侧计量间",V(19,0,8),V(7,7,30));FStructuralRoof(V(0,6,-5),V(28,.35f,14));
   FWall("校准室北墙左翼",V(-10.5f,2.3f,30),V(15,4.6f,.45f));FWall("校准室北墙右翼",V(10.5f,2.3f,30),V(15,4.6f,.45f));FGate("$calibrated",V(0,0,30),6,"完成一次真实Q后，检验闸门开放");
   FocusEnvironment.Machine(root,"靶场后方弹道收集柜",V(-7,0,11),V(3.4f,2.6f,1.2f),"拦截靶后弹道，清楚定义第一条12米射线");FocusEnvironment.Machine(root,"相位计量设备",V(2,0,18),V(2.8f,1.1f,3),"分隔站位价值，留出左右回望角度");
   Spawn("calibration",0,V(-7,0,8),180,true);Spawn("calibration",1,V(7,0,15),180,true);Spawn("calibration",2,V(-10,0,23),180,true);Spawn("calibration",3,V(12,2.4f,23),200,true);Spawn("calibration",4,V(0,0,26),180,true);
   FPlatform("高差校准检修台",V(12,2.4f,23),8,8);FStairs(V(12,2.4f,19),2.4f);
   Anchor(V(5,.02f,8));Anchor(V(-4,.02f,22));Anchor(V(10.5f,2.42f,21));
   FBox("训练刀桌面",V(-5,.85f,-3),V(2,.16f,1.1f),FocusEnvironment.Steel);foreach(int s in new[]{-1,1})FBox("桌腿",V(-5+s*.7f,.4f,-3),V(.12f,.8f,.65f),FocusEnvironment.Steel);V2Knife(0,V(-5,.95f,-3));
   var instructions=FSign("01  射击与持刀\n1 / 2 枪械 · R 装填\nE 取刀 · 3 持刀",V(-9,2.2f,-5),4,155);root.gameObject.AddComponent<FocusTutorialGuide>().text=instructions;
   FSign("相位设备校准线\n交换的是两个位置\nQ后回看原站位",V(11,2.5f,8),3.8f,180);FSign("检验闸门后：实战区\n先选射角，再处理指定守卫",V(0,4.8f,29.7f),5,180);
   FPlatform("侧台实战岗位",V(10,2.4f,35),8,8);FStairs(V(10,2.4f,39),2.4f,180);
   Spawn("combat",0,V(10,2.4f,35),200,true);Spawn("combat",1,V(-8,0,35),160);Region("combat",V(0,3,36),V(36,10,18));
   FocusEnvironment.Machine(root,"货检气动控制柜",V(0,0,36),V(3.2f,2.2f,2),"切断入口正中射线，两侧均有8—12米接近段");
   FWall("巡查室临场隔墙",V(-3.5f,2.6f,40),V(.4f,5.2f,12));FWall("监理前厅遮挡墙",V(-7,2.6f,45),V(22,5.2f,.4f));
   Spawn("hall",0,V(-11,0,40),160);Spawn("hall",1,V(-14,0,43),150);Spawn("hall",2,V(-8,0,43),175);Region("hall",V(-10,2.5f,40),V(14,7,12));
   FBox("右侧进入通道标线",V(12,.018f,44),V(.16f,.025f,9),FocusEnvironment.HazardSteel,false);Anchor(V(15,.02f,44));
   V2Boss(V(0,0,56),V(-7,0,54),V(7,0,57),V(0,3,57),V(34,8,22));
   FocusEnvironment.Machine(root,"监理扫描机组",V(-7,0,48),V(3.2f,2.2f,3),"进入监理区前可靠断线；右侧可继续横移");FocusEnvironment.Machine(root,"低位检验基座",V(7,0,63),V(3.6f,1.0f,3),"蹲伏遮挡，站起观察脉冲预告与Boss转身");
   FSupply(V(-13,0,-5),"教学保障柜",true);FSupply(V(-14,0,49),"监理前保障柜",true);FWall("检疫出口左门翼",V(-9.75f,1.7f,68),V(11.5f,3.4f,.4f));FWall("检疫出口右门翼",V(9.75f,1.7f,68),V(11.5f,3.4f,.4f));FGate("$main",V(0,0,68),8,"监理核验完成后开放撤离");Exit(V(0,0,73),"检疫站撤离口");
   FBuilding("监理记录与检查室",V(-19,0,59),V(7,8,27));FBuilding("检疫出口维护间",V(19,0,59),V(7,6,27));FocusEnvironment.Lamp(root,V(-14,0,3));FocusEnvironment.Lamp(root,V(16,0,47));
   map.walkingCheckpoints.AddRange(new[]{V(0,0,16),V(12,2.4f,23),V(10,2.4f,35),V(13,0,45),V(0,0,54)});
   var info=root.gameObject.AddComponent<FocusedArenaDefinition>();info.chapter=1;info.combatCore=new Bounds(V(0,2,54),V(34,8,26));info.preparation=map.spawn;info.observation=V(0,.04f,27);info.cameras=new[]{new FocusCameraPoint("01教学入口",map.spawn,V(0,1.5f,15)),new FocusCameraPoint("02刀具与射击",V(-4,.04f,-5),V(-5,.95f,-3)),new FocusCameraPoint("03相位与高差",V(0,.04f,13),V(12,3.4f,23)),new FocusCameraPoint("04监理接近",V(12,.04f,47),V(0,1.6f,56))};
  }
  static void FocusedFinalArena()
  {
   FocusEnvironment.CoastalContext(root,11);FGround("反应炉前沿工业站场",72,144,12);FBoundary(36,-60,84);map.spawn=V(0,.04f,-51);
   FBuilding("入口西侧调度楼",V(-26,0,-31),V(16,9,42));FBuilding("入口东侧泵房",V(26,0,-31),V(16,7,42));FStructuralRoof(V(0,6,-50),V(28,.4f,17));
   FSign("归航行动 / 核心控制区\n先通过相位门卫\n保留弹药，终战准备区在后方",V(-9,2.4f,-49),4.6f,155);
   V2Boss(V(0,0,-25),V(-8,0,-21),V(8,0,-25),V(0,3,-25),V(34,10,38));
   Vector3[] before={V(-14,0,-31),V(14,0,-31),V(-14,0,-21),V(14,0,-21),V(-14,0,-14),V(14,0,-14),V(-10,0,-37),V(10,0,-37),V(-15,0,-18),V(15,0,-18)};for(int i=0;i<before.Length;i++)Spawn("antechamber",i,before[i],180);
   FocusEnvironment.Machine(root,"隔离门液压机组",V(-7,0,-35),V(3.5f,2.25f,4),"门卫前两条接近线之间的全身遮挡");FocusEnvironment.Machine(root,"隔离阀检修基座",V(8,0,-14),V(4,1.1f,3),"低掩体有近侧退路，不能同时挡住两处锁点预告");
   FWall("隔离门西翼",V(-21,3,-6),V(34,6,.6f));FWall("隔离门东翼",V(21,3,-6),V(34,6,.6f));FGate("$mini",V(0,0,-6),8,"相位门卫解除后，准备区永久开放");
   FSupply(V(-13,0,-48),"前厅保障柜",true);FSupply(V(-12,0,-1),"终战前医疗弹药柜",true);
   FPlatform("终战观察平台",V(0,2,6),12,6);FStairs(V(0,2,3),2);FStairs(V(-6,2,6),2,90);FStairs(V(6,2,6),2,270);
   var glass=new Material(Shapes.Mat(0));glass.name="Focused bulletproof viewing glass";glass.SetColor("_BaseColor",new Color(.25f,.50f,.57f,.22f));glass.SetFloat("_Surface",1);glass.SetFloat("_SrcBlend",5);glass.SetFloat("_DstBlend",10);glass.SetFloat("_ZWrite",0);glass.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");glass.renderQueue=3000;FBox("实体防弹观察窗",V(0,3.25f,9.2f),V(10,2.1f,.14f),glass);FBox("观察窗下框",V(0,2.12f,9.2f),V(12,.24f,.4f),FocusEnvironment.Steel);FSign("观察窗\n两侧进入战区\n左机房 / 右管廊",V(-5.2f,2.8f,9.08f),1.4f,180,.85f);
   Region("final",V(0,4,40),V(68,16,78));Spawn("boss",0,V(0,0,39),180,true);
   // Left route: a covered machine room. Both ends stay open; authorization opens the shorter central service door.
   FWall("左机房外墙",V(-30,3.1f,40),V(.45f,6.2f,36));FBox("左机房实体屋顶",V(-24,6.35f,40),V(12.5f,.3f,36.5f),FocusEnvironment.Steel);
   foreach(var segment in new[]{new Vector2(22,22.5f),new Vector2(27.5f,33),new Vector2(39,46.5f),new Vector2(51.5f,58)})FWall("左机房下墙",V(-18,1.7f,(segment.x+segment.y)/2),V(.4f,3.4f,segment.y-segment.x));
   FWall("左机房上梁",V(-18,5.7f,40),V(.4f,1.0f,36));FWall("左窗前肩墙",V(-18,4.3f,27),V(.4f,1.8f,10));FWall("左窗后肩墙",V(-18,4.3f,50),V(.4f,1.8f,16));
   var leftShutter=FBox("左授权控制的检修中门",V(-18,1.7f,36),V(.4f,3.4f,6),FocusEnvironment.PaintedSteel);layout.OpenOn("a",leftShutter,false);
   FPlatform("左授权检修台",V(-24,3.2f,37),10,12);FStairs(V(-24,3.2f,31),3.2f);FStairs(V(-24,3.2f,43),3.2f,180);FTerminal("a",V(-24,3.2f,38),"左授权 / 开机房中门");Anchor(V(-19.9f,3.22f,34));
   // Right route is more exposed, but has a higher view and a shorter back-route after its authorization.
   FPlatform("右授权管廊",V(24,5,46),12,14);FStairs(V(24,5,39),5);FStairs(V(24,5,53),5,180);FTerminal("b",V(24,5,48),"右授权 / 开后方维护口");Anchor(V(19,5.02f,44));
   FWall("右维护口前隔断",V(18,1.25f,55.5f),V(.4f,2.5f,3));FWall("右维护口后隔断",V(18,1.25f,63.5f),V(.4f,2.5f,5));var rightShutter=FBox("右授权控制的后维护口",V(18,1.25f,59),V(.4f,2.5f,4),FocusEnvironment.PaintedSteel);layout.OpenOn("b",rightShutter,false);
   FRefuge("前沿检修避波台",V(-12,0,23));FRefuge("中场检修避波台",V(12,0,41));FRefuge("后沿检修避波台",V(-9,0,57));
   FocusEnvironment.Machine(root,"主变压器散热柜",V(0,0,29),V(4,2.3f,3),"把入口到Boss的长射线切成两段，左右均可通过");FocusEnvironment.Machine(root,"低位电缆检修箱",V(11,0,24),V(4,1.05f,3),"右管廊上行前的短暂停靠，暴露时仍需移动");FocusEnvironment.Machine(root,"左冷却控制机组",V(-12,0,43),V(3.4f,2.2f,4),"左中门出来后可靠断线，可退回机房或横穿核心");FocusEnvironment.Machine(root,"右后方补给配电柜",V(24,0,60),V(4,2.4f,4),"遮住Boss正面射线，但保留右入口的侧向压力");
   FSupply(V(-28,0,47),"左机房补给柜");FSupply(V(29,0,65),"右后方补给柜");
   Vector3[] phaseOne={V(-28.5f,0,52),V(-20,0,55),V(-24,3.2f,33),V(-26,0,55),V(-20,0,48),V(-29,0,44)};for(int i=0;i<phaseOne.Length;i++)Spawn("phase-one",i,phaseOne[i],90,i==2);
   Vector3[] phaseTwo={V(29,0,57),V(25,5,42),V(29,0,61),V(20,0,56),V(25,0,64),V(31,0,65),V(22,0,66)};for(int i=0;i<phaseTwo.Length;i++)Spawn("phase-two",i,phaseTwo[i],270,i==1);
   Vector3[] phaseThree={V(-29,0,64),V(-25,0,65),V(-29,0,59),V(-25,0,60),V(-30,0,69),V(-26,0,70),V(-22,0,68)};for(int i=0;i<phaseThree.Length;i++)Spawn("phase-three",i,phaseThree[i],110,i==2);
   var core=FTerminal("c",V(0,0,66),"地面核心 / 先双授权");core.useAction=()=>{if(!g.MissionObjectiveComplete("a")||!g.MissionObjectiveComplete("b")){g.Toast("先取得左右授权，顺序自由",2);return false;}return g.CompleteMissionObjective("c");};Bonus(V(29,0,71),"可选维护记录");Exit(V(-23,0,74),"临海维护出口 / 双授权、核心与两名Boss");
   Anchor(V(-7,.02f,17));Anchor(V(7,.02f,50));Anchor(V(-22,.02f,65));
   V2JumpPad.Create(root,V(8,0,15),V(28,0,15));V2JumpPad.Create(root,V(16,0,46),V(16,0,66));V2JumpPad.Create(root,V(-7,0,64),V(13,0,64));
   FocusEnvironment.Cylinder(root,"反应炉连续承压本体",V(0,12,80),7.8f,24,FocusEnvironment.Steel);foreach(float h in new[]{2f,7f,12f,17f,22f})FocusEnvironment.Cylinder(root,"反应炉加强环",V(0,h,80),8.1f,.45f,FocusEnvironment.PaintedSteel);
   FocusEnvironment.Pipe(root,V(-26,8.7f,34),V(-26,8.7f,77),.33f,FocusEnvironment.Steel);FocusEnvironment.Pipe(root,V(-26,8.7f,77),V(-7,8.7f,77),.33f,FocusEnvironment.Steel);FocusEnvironment.Pipe(root,V(28,9,44),V(28,9,78),.35f,FocusEnvironment.HazardSteel);FocusEnvironment.Pipe(root,V(28,9,78),V(7,9,78),.35f,FocusEnvironment.HazardSteel);
   foreach(var p in new[]{V(-26,0,34),V(-26,0,56),V(28,0,44),V(28,0,69)})FBox("真实管廊支架",p+Vector3.up*4.2f,V(.25f,8.4f,.25f),FocusEnvironment.Steel);
   var feedback=root.gameObject.AddComponent<FocusReactorFeedback>();feedback.leftIndicators=new[]{FBox("左授权供能指示",V(-17.7f,4.7f,36),V(.08f,.2f,1.8f),FocusEnvironment.HazardSteel,false).GetComponent<Renderer>()};feedback.rightIndicators=new[]{FBox("右授权供能指示",V(18.2f,2.7f,59),V(.08f,.2f,1.8f),FocusEnvironment.HazardSteel,false).GetComponent<Renderer>()};feedback.coreIndicators=new[]{FBox("反应炉状态面板",V(0,3.5f,71.9f),V(4,2,.08f),FocusEnvironment.HazardSteel,false).GetComponent<Renderer>()};
   FSign("左机房：遮蔽接近，绕行更长\n右管廊：射角更高，上行暴露\n黄色检修台可离开地面危险",V(8,1.3f,-5.65f),2.2f,0,1.0f);FocusEnvironment.Lamp(root,V(-15,0,-39));FocusEnvironment.Lamp(root,V(15,0,-10));FocusEnvironment.Lamp(root,V(-16,0,16));FocusEnvironment.Lamp(root,V(32,0,55));
   map.walkingCheckpoints.AddRange(new[]{V(0,0,-2),V(0,2,6),V(-24,3.2f,37),V(24,5,46),V(-12,1.4f,23),V(12,1.4f,41),V(-9,1.4f,57),V(0,0,64)});
   var info=root.gameObject.AddComponent<FocusedArenaDefinition>();info.chapter=12;info.combatCore=new Bounds(V(0,3,40),V(58,12,56));info.preparation=V(-9,.04f,-1);info.observation=V(0,2.04f,6);info.cameras=new[]{new FocusCameraPoint("12入口与前厅",map.spawn,V(0,1.5f,-25)),new FocusCameraPoint("12战前观察",info.observation,V(0,2.0f,39)),new FocusCameraPoint("12交战核心",V(0,.04f,18),V(0,1.6f,39)),new FocusCameraPoint("12左机房高位",V(-24,3.24f,34),V(0,1.5f,39)),new FocusCameraPoint("12右侧管廊",V(24,5.04f,44),V(0,1.6f,39)),new FocusCameraPoint("12后方与双退路",V(-3,.04f,63),V(0,1.5f,39))};
  }
 }
}
