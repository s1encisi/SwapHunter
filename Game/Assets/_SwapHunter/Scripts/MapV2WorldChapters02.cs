using UnityEngine;
namespace SwapHunter
{
 public static partial class ExpeditionWorld
 {
  static void V2Islands()
  {
   District("A岛 / 相位接收站",0,0,60,60,0,3);District("B岛 / 环形稳压站",-44,80,60,60,8,1);District("C岛 / 高位转发站",44,80,60,60,16,3);
   map.spawn=V(0,.04f,-22);Deck("永久低层维护桥",0,38,8,88,-3.3f);Deck("永久低层横向检修桥",0,35,112,8,-3.3f);
   V2Steps(0,-9,3.3f,-3.3f);V2Steps(-44,22.05f,11.3f,-3.3f);V2Steps(44,2.05f,19.3f,-3.3f);Deck("B岛永久楼梯基础",-44,34,8,38,-3.3f);Deck("C岛永久楼梯基础",44,27,8,54,-3.3f);
   Deck("A岛下层楼梯接驳",0,-10,12,16,-3.3f);Link("B岛低层返程",V(0,-3.3f,35),V(-44,-3.3f,35),8);Link("C岛低层返程",V(0,-3.3f,35),V(44,-3.3f,35),8);
   // The nearest posts are 32.98m across B/C; their location, not the full island centres, defines the Q chain.
   Anchor(V(0,.02f,12));Anchor(V(-16,8.02f,62));Anchor(V(16,16.02f,62));
   Socket("a",V(0,.02f,18),"A接收座 / 通风支路");Socket("b",V(-16,8.02f,66),"B稳压座 / 应急照明");Socket("c",V(16,16.02f,66),"C转发座 / 救援专线");
   V2Shortcut("a",()=>{Deck("A至B锁存检修桥",-18,37,6,16);V2Steps(-18,30.55f,8);});V2Shortcut("b",()=>{Deck("B至C锁存接驳",-9,80,18,8,8);V2Steps(-5.45f,80,8,8,90);});V2Shortcut("c",()=>{V2Steps(18,10.55f,16);});Maintenance(V(-12,0,-12));
   V2Group("island-a",V(9,0,8),180,3);V2Group("island-b",V(-44,8,78));V2Group("island-c",V(42,16,74));V2Group("reinforce",V(45,16,100));
   Region("island-a",V(0,3,9),V(58,8,40));Region("island-b",V(-44,11,80),V(60,8,60));Region("island-c",V(44,19,80),V(60,8,60));
   V2Boss(V(44,16,91),V(31,16,93),V(60,16,88),V(44,19,92),V(54,10,30));
   V2JumpPad.Create(root,V(-10,0,20),V(10,0,20));
   V2Supply(V(-20,0,-20),"A岛维护保障柜",true);V2Supply(V(-64,8,60),"B岛维护弹药柜");V2Supply(V(24,16,60),"C岛Boss前保障柜",true);V2Supply(V(60,16,75),"转发间备用柜");
   Bonus(V(65,16,100),"C岛夜班记录");Exit(V(44,16,106),"C岛救援专线出口");V2Knife(3,V(-64,8,63));
   map.walkingCheckpoints.AddRange(new[]{V(0,-3.3f,45),V(-44,8,53),V(44,16,53),V(44,16,90)});
  }
  static void V2Warehouses()
  {
   District("中央冷链分拣大厅",0,0,90,90,0,3);District("A仓 / 入库缓冲与堆垛",-100,80,100,100,0,2);District("B仓 / 恒温长线存储",0,130,100,100,0,1);District("C仓 / 高架自动分拣",100,80,100,100,0,3);
   Link("A仓人员通道",V(-30,0,30),V(-100,0,42),8);Link("B仓冷机维护通道",V(0,0,35),V(0,0,90),8);Link("C仓员工通道",V(30,0,30),V(100,0,42),8);Link("A至B维护连接",V(-100,0,120),V(0,0,155),8);Link("B至C维护连接",V(0,0,155),V(100,0,120),8);
   map.spawn=V(0,.04f,-35);
   for(int i=0;i<3;i++){Equipment("Props_ContainerFull",V(-116+i*16,0,75+(i%2)*17),V(5,3.5f,18),"A仓标线货架 "+(i+1),"形成短距盾线与双侧搬运通道");V2Breakable(V(-101+i*10,0,96),6,"A仓空架护板 "+i);}
   Workshop("B仓冷机横向切换间",0,126,0,20,12);V2Tower("C仓分拣操作层",100,81,4,34,28);V2Steps(90,58,4);V2Tower("C仓高位检修层",116,110,8,24,22);V2Steps(116,81,8);
   V2Group("warehouse-a",V(-100,0,65));V2Group("warehouse-b",V(0,0,105));V2Group("warehouse-c",V(100,0,61));Region("a",V(-100,5,80),V(100,14,100));Region("b",V(0,5,130),V(100,14,100));Region("c",V(100,6,80),V(100,22,100));
   Objective("a",V(-100,0,115),"A仓数据 / 停用推拒模块");Objective("b",V(-22,0,166),"B仓数据 / 停用测距模块");Objective("c",V(116,8,114),"C仓数据 / 停用换岗模块");
   V2Boss(V(0,0,10),V(-20,0,8),V(20,0,8),V(0,5,4),V(84,14,68));V2Cart(V(-16,0,20),V(16,0,20),V(-28,0,12));
   V2Supply(V(-33,0,-30),"中央前厅保障柜",true);V2Supply(V(32,0,-30),"中央备用弹药柜");V2Supply(V(-138,0,43),"A仓驻守柜");V2Supply(V(-38,0,92),"B仓驻守柜",true);V2Supply(V(62,0,43),"C仓驻守柜");
   V2AnchorChain(V(-25,0,23),V(-60,0,42),V(-95,0,64),V(0,0,68),V(0,0,104),V(25,0,23),V(60,0,42),V(99,4,82),V(114,8,106));Exit(V(0,0,-40),"中央汇合出口 / 任意两匣");
   map.walkingCheckpoints.AddRange(new[]{V(-100,0,110),V(-22,0,160),V(116,8,105),V(0,0,10)});
  }
  static void V2CliffTower()
  {
   // Five offset cliff terraces expose the sea and give each descending stage its own ground projection.
   District("32米塔顶 / 核心收发台",0,0,90,90,32,3);District("24米上层天线检修坪",80,0,110,100,24,3);District("16米中层通信舱",80,100,110,100,16,3);District("8米沿崖冷却层",-30,100,110,100,8,1);District("地面回收坪",-30,200,110,100,0,2);
   // Stair endpoints meet terrace edges; they never climb through a floor slab.
   V2Steps(64.45f,0,8,24,270);V2Steps(64.45f,28,8,24,270);V2Steps(80,69.45f,8,16,180);V2Steps(108,69.45f,8,16,180);V2Steps(5.55f,100,8,8,90);V2Steps(5.55f,128,8,8,90);V2Steps(-30,169.45f,8,0,180);V2Steps(-2,169.45f,8,0,180);
   Door("a",V(20,32,12),8,"取出核心后应急观察窗收起");map.spawn=V(-25,32.04f,-30);Objective("a",V(-18,32,14),"取出通信核心 / 开始下撤");Bonus(V(-32,32,28),"塔顶侧路记录");
   V2Group("top",V(9,32,4));V2Group("middle",V(81,24,6));V2Group("lower-middle",V(83,16,107));V2Group("bottom",V(-24,0,193));Region("middle",V(82,27,0),V(108,8,96));Region("lower-middle",V(25,17,100),V(214,22,96));Region("bottom",V(-30,4,200),V(106,12,96));
   V2Boss(V(-30,0,222),V(-53,6,213),V(-7,6,213));V2Tower("回收坪左维修台",-53,213,6,14,16);V2Steps(-53,191,6);V2Tower("回收坪右维修台",-7,213,6,14,16);V2Steps(-7,191,6);
   V2JumpPad.Create(root,V(35,32,18),V(55,24,18));V2Cart(V(-48,0,195),V(-12,0,195),V(-62,0,187));
   V2Supply(V(-34,32,-32),"塔顶出发保障柜");V2Supply(V(37,24,-37),"24米维修室医疗柜",true);V2Supply(V(-72,8,63),"8米冷却层弹药柜");V2Supply(V(-72,0,163),"地面Boss前保障柜",true);
   V2AnchorChain(V(30,32,5),V(65,24,6),V(80,24,42),V(80,16,77),V(32,16,109),V(-3,8,109),V(-30,8,140),V(-30,0,176));V2Knife(4,V(-70,8,65));Exit(V(-30,0,244),"地面货运回收出口");
   map.walkingCheckpoints.AddRange(new[]{V(80,24,0),V(80,16,100),V(-30,8,100),V(-30,0,200)});
  }
  static void V2Mine()
  {
   District("中央矿料装运场",0,0,100,110,0,2);District("左道岔控制站",-95,70,100,100,0,3);District("右卸料控制站",90,95,100,100,0,3);District("后皮带机控制站",-10,170,100,100,0,1);District("运输督导检修场",-10,266,100,92,0,2);
   Link("左控制站装运通道",V(-40,0,36),V(-95,0,29),10);Link("右控制站卸料路",V(40,0,39),V(90,0,54),10);Link("西侧回站环路",V(-95,0,112),V(-45,0,150),8);Link("东侧回站环路",V(90,0,137),V(25,0,177),8);Link("督导前检修入口",V(-10,0,215),V(-10,0,226),10);
   map.spawn=V(0,.04f,-43);Deck("矿场下沉维修沟",0,84,10,170,-3);V2Steps(-1.95f,15,3,-3,90);V2Steps(-1.95f,150,3,-3,90);Deck("矿场跨沟前桥",0,45,12,8);Deck("矿场跨沟后桥",0,132,12,8);Deck("皮带机双向楼梯基础",0,101,100,18);V2Tower("皮带机上维护桥",0,120,6,100,10);V2Steps(-42,98,6);
   Equipment("Props_Vessel_Tall",V(-12,0,67),V(18,10,20),"矿料卸载漏斗","隔断中央与右站射线；下方维护带可绕行");
   V2Group("initial",V(16,0,2));V2Group("left",V(-95,0,99));V2Group("right",V(90,0,124));V2Group("rear",V(-10,0,201));Region("left",V(-95,4,70),V(98,12,98));Region("right",V(90,4,95),V(98,12,98));Region("rear",V(-10,4,170),V(98,12,98));
   Objective("a",V(-95,0,64),"关闭左入口 / 取消未落地增援");Objective("b",V(90,0,89),"关闭右入口 / 取消未落地增援");Objective("c",V(-10,0,164),"关闭后入口 / 取消未落地增援");
   V2Boss(V(-10,0,280),V(-30,0,268),V(10,0,268));var trackNode=Node(V(-36,0,256),ExpeditionNodeKind.Jammer,"道岔开关 / 更改冲撞路线");trackNode.repeatable=true;var track=trackNode.gameObject.AddComponent<MapV2Track>();track.routes=new[]{V(-30,0,280),V(10,0,280)};trackNode.useAction=()=>{track.Switch();g.Toast("道岔已切换，轨道冲撞终点改变",2);return true;};
   for(int side=-1;side<=1;side+=2)Shapes.Box("督导冲撞轨道",root,V(-10+side*20,.025f,270),V(1,.04f,30),3);
   V2Supply(V(-38,0,-42),"中央装运保障柜",true);V2Supply(V(-133,0,33),"左站弹药柜");V2Supply(V(52,0,58),"右站弹药柜");V2Supply(V(-48,0,133),"后站弹药柜");V2Supply(V(-48,0,232),"督导前保障柜",true);
   V2AnchorChain(V(-25,0,24),V(-61,0,34),V(-95,0,63),V(-61,0,113),V(-28,0,140),V(17,0,159),V(50,0,130),V(88,0,93));Bonus(V(-125,0,101),"左站后仓记录");Exit(V(-10,0,304),"矿场运输出口");map.walkingCheckpoints.AddRange(new[]{V(-95,0,64),V(90,0,89),V(-10,0,164),V(-10,0,260)});
  }
  static void V2Laboratory()
  {
   District("西侧洁净外环",-75,0,100,110,0,3);District("东侧观察外环",75,0,100,110,0,3);District("左反相实验区",-75,110,100,110,0,3);District("右反相实验区",75,110,100,110,0,3);Deck("权限与审查中央连接",0,55,50,140);
   Link("外环南连接",V(-75,0,-40),V(75,0,-40),10);Link("外环北连接",V(-75,0,155),V(75,0,155),10);Link("西观察入口",V(-35,0,40),V(0,0,40),8);Link("东观察入口",V(35,0,70),V(0,0,70),8);
   map.spawn=V(-75,.04f,-43);Deck("下层维护线",0,65,10,160,-4);V2Steps(-4.45f,50,4,-4,90);V2Steps(-4.45f,103,4,-4,90);Deck("下层楼梯回转间",-8,50,8,10,-4);Deck("下层楼梯后回转间",-8,103,8,10,-4);Deck("审查厅跨沟整面地台",0,8,50,50);Deck("核心权限跨沟操作桥",0,119,14,10);Deck("观察室跨沟联络桥",0,90,12,8);V2Tower("实验观察层",0,92,6,42,18);V2Steps(-14,68,6);
   Jammer("field-left",V(-14,1,67),6,V(-24,0,59),V(-14,0,70));Jammer("field-right",V(14,1,98),6,V(24,0,90),V(14,0,101));
   V2Group("outer",V(-70,0,-5));V2Group("left",V(-75,0,103));V2Group("right",V(75,0,105));V2Group("core",V(0,0,26));Region("left",V(-75,5,110),V(100,14,100));Region("right",V(75,5,110),V(100,14,100));Region("core",V(0,4,30),V(48,12,52));
   Objective("a",V(0,0,119),"取得核心维护权限");V2Boss(V(0,0,8),V(-16,0,18),V(16,0,18),V(0,5,10),V(48,14,50));
   Equipment("Props_Vessel_Tall",V(-11,0,0),V(4,5,4),"审查厅设备柱A","断开校准射线，保持内外两条绕路");Equipment("Props_Vessel_Tall",V(11,0,0),V(4,5,4),"审查厅设备柱B","提供对称但不重叠的安全角");
   V2Supply(V(-113,0,-42),"外环出发保障柜",true);V2Supply(V(-113,0,72),"左实验值守柜");V2Supply(V(37,0,72),"右实验值守柜");V2Supply(V(0,0,142),"权限后保障柜",true);
   V2AnchorChain(V(-55,0,-15),V(-23,0,0),V(0,0,33),V(-14,0,68),V(14,0,98),V(0,0,118),V(50,0,130),V(-50,0,130));Bonus(V(17,0,99),"可选实验记录 / 局部干扰风险");Exit(V(0,0,-12),"安全外环归航出口");V2Knife(5,V(0,0,144));
   map.walkingCheckpoints.Add(V(0,-4,65));map.walkingCheckpoints.AddRange(new[]{V(-75,0,110),V(75,0,110),V(0,0,119),V(0,0,8)});
  }
  static void V2Reactor()
  {
   District("前置相位隔离厅",0,-130,80,90,0,3);District("两战间安全准备区",0,-60,80,50,0,3);District("中央反应炉作业包络",0,44,164,176,0,1);District("西侧授权维护翼",-110,44,80,90,0,3);District("东侧授权维护翼",110,44,80,90,0,3);
   Link("门卫后永久准备通路",V(0,0,-90),V(0,0,-80),12);Link("西授权维修入口",V(-75,0,44),V(-110,0,44),10);Link("东授权维修入口",V(75,0,44),V(110,0,44),10);
   map.spawn=V(0,.04f,-165);V2Group("antechamber",V(0,0,-130));V2Boss(V(0,0,-108),V(-17,0,-116),V(19,0,-120),V(0,4,-126),V(74,12,80));Door("$mini",V(0,0,-86),16,"相位门卫击败后永久开放");foreach(int side in new[]{-1,1})Solid("隔离厅门侧 wall",V(side*24,3,-86),V(32,6,.7f));Solid("隔离厅门楣 wall",V(0,4.5f,-86),V(16,3,.7f));
   // Final encounter is a different instance and different room, with original phase framework.
   Spawn("boss",0,V(0,0,57),180,true);Region("final",V(0,8,44),V(160,32,168));V2Group("phase-one",V(-45,0,12));V2Group("phase-two",V(45,0,65));V2Group("phase-three",V(0,0,114));
   V2Tower("左授权台",-35,42,6,24,24);V2Steps(-35,16,6);V2Tower("右授权台",35,68,6,24,24);V2Steps(35,42,6);Deck("12米反应炉观察廊",0,90,100,12,12);V2Steps(-44,54.55f,12);V2Steps(44,54.55f,12);
   // Three permanent 1.4m wave refuges, each with two visible ramp entrances.
   foreach(var p in new[]{V(-18,0,25),V(18,0,57),V(-14,0,91)}){Tower(p.x,p.z,1.4f,7,7);Ramp(p.x,p.z-6,1.4f,6,4,guideSides:true);Ramp(p.x,p.z+6,1.4f,6,4,yaw:180,guideSides:true);}
   Objective("a",V(-39,6,48),"左授权 / 解除左控制链");Objective("b",V(39,6,74),"右授权 / 解除右控制链");var core=Objective("c",V(0,0,117),"地面相位核心 / 先完成双授权");
   core.useAction=()=>{if(g.mission==null || !g.mission.ObjectiveComplete("a") || !g.mission.ObjectiveComplete("b")){g.Toast("先取得左右两处授权，顺序自由",3);return false;}return g.CompleteMissionObjective("c");};
   V2JumpPad.Create(root,V(-10,0,4),V(10,0,4));V2JumpPad.Create(root,V(-35,0,12),V(-35,6,32));V2JumpPad.Create(root,V(35,6,60),V(55,0,60));
   V2Supply(V(-28,0,-160),"门卫前保障柜",true);V2Supply(V(-28,0,-75),"两战间医疗弹药柜",true);V2Supply(V(-68,0,-25),"终战西维护柜",true);V2Supply(V(68,0,111),"终战东维护柜");
   V2AnchorChain(V(-20,0,-36),V(0,0,0),V(-35,6,42),V(0,0,58),V(35,6,68),V(0,12,90),V(0,0,110));Bonus(V(110,0,70),"东观察翼记录");Exit(V(0,0,127),"核心归航出口 / 双授权、核心与两名Boss");
   map.walkingCheckpoints.AddRange(new[]{V(0,0,-60),V(-35,6,42),V(35,6,68),V(0,12,90),V(-18,1.4f,25),V(18,1.4f,57),V(-14,1.4f,91)});
  }
 }
}
