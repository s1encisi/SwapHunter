using UnityEngine;
namespace SwapHunter
{
 public static partial class ExpeditionWorld
 {
  static void V2Calibration()
  {
   // Five independent test stations occupy three safe inspection courts, then a small combat loop.
   District("短距与侧向校准庭院",0,35,60,75,0,0);District("远距检验庭院",-70,35,60,75,0,0);District("锚点与高度检验庭院",70,35,60,75,0,0);
   District("侧台实战与设备交接",0,120,80,72,0,0);District("监理前厅与出口复核",0,200,80,72,0,3);
   Link("检验回路",V(-70,0,65),V(70,0,65),8);Link("校准后人员通道",V(0,0,72),V(0,0,92),10);Link("监理前厅连接",V(0,0,152),V(0,0,168),10);
   map.spawn=V(0,.04f,1);Door("$calibrated",V(0,0,80),10,"完成首次相位校准后打开检验挡板");
   Vector3[] training={V(-18,0,20),V(-63,0,42),V(22,0,55),V(65,3.5f,34),V(75,0,65)};
   V2Tower("低高差观察台",65,35,3.5f,18,18);V2Steps(65,17,3.5f);
   for(int i=0;i<training.Length;i++){Spawn("calibration",i,training[i],180,true);Anchor(training[i]+V(4,0,-6));Label("校准站 "+(i+1),training[i]+V(0,3,0),.45f);}
   V2Group("combat",V(4,0,119));V2Group("hall",V(4,0,176));Region("combat",V(0,3,118),V(78,12,70));Region("hall",V(0,3,175),V(78,12,30));
   Equipment("Props_Capsule",V(0,0,192),V(34,3.2f,5),"监理复核隔离舱","断开前厅火线；两侧检验回路可绕行");V2Boss(V(0,0,216),V(-12,0,214),V(18,0,210),V(0,4,209),V(70,12,58));
   V2AnchorChain(V(-20,0,92),V(20,0,128),V(-18,0,168),V(18,0,203));
   V2Supply(V(-28,0,83),"实战前保障柜",true);V2Supply(V(-28,0,163),"监理前厅保障柜",true);
   Door("$main",V(0,0,232),8,"监理确认后开放");Exit(V(0,0,236),"检疫站出口");V2Knife(0,V(-21,0,14));
   Deck("检疫复核双层作业廊",0,196,76,32,6);V2Steps(-34,226.45f,6,0,180);map.walkingCheckpoints.Add(V(-30,6,200));
   map.walkingCheckpoints.AddRange(new[]{V(-65,0,40),V(65,3.5f,34),V(0,0,120),V(0,0,205)});
  }
  static void V2ShieldLine()
  {
   // Two old-port street grids turn at the cargo inspection gate, with a rear enforcement courtyard.
   District("旧港前检查街区",-42,42,82,84,0,2);District("岗亭与员工后街",48,42,82,84,0,2);
   District("第二检查街区",-42,133,82,84,0,2);District("店后装卸与维修回路",48,133,82,84,0,2);
   District("执法官后检查场",0,221,82,84,0,3);
   Link("两街区检查路",V(-42,0,78),V(-42,0,97),10);Link("员工通道",V(48,0,82),V(48,0,100),8);Link("后场左入口",V(-42,0,171),V(-20,0,188),10);Link("后场侧巷",V(48,0,171),V(22,0,188),8);
   Link("前街横向人员路",V(-42,0,66),V(48,0,66),8);Link("后街横向检查路",V(-42,0,155),V(48,0,155),8);map.spawn=V(-42,.04f,3);V2Group("first",V(-40,0,46));V2Group("second",V(-32,0,131));V2Group("rear",V(13,0,228));
   Region("first",V(-35,4,44),V(100,10,80));Region("second",V(0,4,133),V(174,12,80));
   V2Tower("屋顶巡检通道",-58,126,4,22,20);V2Steps(-58,107,4);V2Steps(-50,145.45f,4,0,180);V2Tower("远处警戒楼",72,153,8,18,20);V2Steps(72,124,8);
   V2Gate(V(-10,0,223),8,V(-16,0,218));V2Boss(V(0,0,232),V(-21,0,216),V(20,0,218));
   V2AnchorChain(V(-20,0,29),V(14,0,63),V(-25,0,103),V(10,0,142),V(-21,0,185),V(20,0,214));
   V2Supply(V(-76,0,92),"两闸之间弹药柜",true);V2Supply(V(14,0,96),"员工值守柜");V2Supply(V(-33,0,188),"执法后场准备柜",true);
   Exit(V(0,0,258),"后检查场出口");map.walkingCheckpoints.AddRange(new[]{V(40,0,75),V(-58,4,126),V(48,0,160),V(0,0,217)});
  }
  static void V2CoolingWell()
  {
   // A contained vertical core is fed by three sizeable coolant service wings, not an outdoor city street.
   District("冷却支路西泵站",-72,8,80,90,0,1);District("冷却支路东泵站",72,8,80,90,0,1);District("入井净化作业场",0,-82,80,80,0,1);
   District("冷却井环底",0,24,54,72,0,1);District("上层控制与换热检修场",0,115,110,80,18,1);
   Link("西泵管路",V(-35,0,0),V(-20,0,0),10);Link("东泵管路",V(20,0,0),V(35,0,0),10);Link("净化进井线",V(0,0,-40),V(0,0,-8),10);
   for(int side=-1;side<=1;side+=2){Solid("冷却井 side wall",V(side*29,13,28),V(.6f,26,44));Solid("冷却井 entrance wall",V(side*29,13,-18),V(.6f,26,20));if(side<0)Solid("冷却井 rear wall",V(-17,13,61),V(20,26,.6f));else{Solid("冷却井 rear wall",V(10.5f,13,61),V(7,26,.6f));Solid("冷却井 rear wall",V(27,13,61),V(4,26,.6f));}}
   Equipment("Props_Vessel_Tall",V(0,0,22),V(12,23,12),"中央冷却芯","阻断井内对向射线；周边检修环可绕行");
   // Split rings preserve the open shaft and permanent visible stair routes.
   for(int i=1;i<=3;i++){float y=i*6;Deck("冷却井西检修环",-20,22,14,62,y);Deck("冷却井东检修环",20,22,14,62,y);Deck("冷却井北连桥",0,46,40,8,y);Deck("冷却井南连桥",0,-2,40,8,y);float stairX=i%2==1?-20:20;float footZ=i%2==1?-23.45f:67.45f;Deck("冷却环楼梯折返落地",stairX,i%2==1?-17:61,10,24,(i-1)*6);V2Steps(stairX,footZ,6,(i-1)*6,i%2==1?0:180);}
   Link("上层检修出口桥",V(0,18,48),V(0,18,73),10);
   map.spawn=V(0,.04f,-115);V2Group("lower",V(0,0,-12));V2Group("middle",V(-20,6,20),180,2,4);V2Group("upper",V(0,18,94));
   Region("middle",V(0,8,22),V(52,8,66));Region("upper",V(0,20,104),V(108,10,100));
   Objective("a",V(-6,18,100),"上层通行件");V2Boss(V(0,18,137),V(-20,18,131),V(20,18,131));
   V2Valve(V(-22,6,38),V(-20,6,26),V(20,6,26));Deck("开井投送起步舌台",-10,38,6,10,6);Deck("开井投送落地舌台",10,38,6,10,12);V2JumpPad.Create(root,V(-10,6,38),V(10,12,38));
   V2Supply(V(-43,0,-29),"井底净化柜",true);V2Supply(V(-20,12,42),"12米检修层弹药柜");V2Supply(V(-40,18,77),"上层监察前保障柜",true);
   V2AnchorChain(V(-20,0,15),V(20,6,20),V(-20,12,25),V(20,18,35),V(0,18,90));Exit(V(0,18,152),"冷却井上层出口");V2Knife(1,V(-20,12,44));
   map.walkingCheckpoints.AddRange(new[]{V(-20,6,20),V(20,12,20),V(-20,18,20),V(0,18,115)});
  }
  static void V2RailStation()
  {
   // Two lateral rail yards, carriage service lanes and a credentials shortcut.
   District("雨夜前站场",-55,15,100,94,0,2);District("凭据调度站场",55,15,100,94,0,3);
   District("车厢外环维修线",-55,122,100,94,0,2);District("撤离站台",55,122,100,94,0,2);
   for(int i=0;i<4;i++){Equipment("Props_ContainerFull",V(-90+i*38,0,38),V(10,4,24),"停靠货运车厢 "+(i+1),"分割两站场的长射线；左右车厢检修道常通");Shapes.Box("铁轨",root,V(-90+i*38,.025f,38),V(13,.04f,75),3);}
   V2Tower("站台连桥",0,44,5,60,10);V2Steps(-24,24,5);V2Tower("桥上调度室",55,30,9,26,20);V2Steps(55,0,9);
   Link("站场横向人员道",V(-55,0,-10),V(55,0,-10),10);Link("外环维修连通",V(-55,0,58),V(-55,0,80),10);Link("凭据中央捷径",V(0,0,57),V(0,0,80),12);Link("撤离站场横路",V(-55,0,95),V(55,0,95),10);map.spawn=V(-55,.04f,-30);V2Group("initial",V(-55,0,4));V2Group("credential",V(50,0,0));V2Group("intercept",V(67,0,126));Region("credential",V(55,5,15),V(100,22,100));
   Objective("a",V(52,9,33),"取得核心凭据");Door("a",V(0,0,70),10,"凭据接通 / 中央短路");V2Cart(V(-12,0,90),V(12,0,90),V(52,9,26));
   V2Boss(V(55,0,149),V(35,0,137),V(75,0,136));V2Supply(V(-96,0,-23),"前场弹药柜",true);V2Supply(V(45,9,27),"调度室医疗弹药柜",true);V2Supply(V(13,0,84),"撤离前保障柜");
   V2AnchorChain(V(-60,0,30),V(-24,5,44),V(16,5,44),V(52,9,34),V(38,0,71),V(14,0,109),V(52,0,135));Bonus(V(-88,0,151),"外环可选存储 / 无额外追兵");Exit(V(55,0,168),"对侧撤离站台");
   map.walkingCheckpoints.AddRange(new[]{V(-55,0,110),V(55,9,30),V(55,0,120)});
  }
  static void V2DualTowers()
  {
   // The exact central tower pair stays in Q range; outlying freight and maintenance districts give it context.
   District("双塔中央火控场",0,0,100,104,0,3);District("西雷达机房与装卸区",-100,0,94,100,0,2);District("东阵列供电与转运区",100,0,94,100,0,1);
   District("南方来料调度",0,-104,94,94,0,2);District("北方撤离装车位",0,104,94,94,0,2);
   Link("南调度进场",V(0,0,-61),V(0,0,-48),12);Link("北调度出场",V(0,0,48),V(0,0,61),12);Link("西机房通道",V(-58,0,-20),V(-46,0,-20),8);Link("东机房通道",V(46,0,-20),V(58,0,-20),8);map.spawn=V(0,.04f,-143);V2Tower("旧式低雷达塔",-20,10,8,20,22);V2Steps(-20,-19,8);V2Tower("新型高阵列塔",20,10,16,20,22);V2Steps(20,-39,16);
   // Distinct silhouettes: radar dish against an asymmetric antenna mast.
   FinalEnvironmentModels.Create("Props_Vessel_Tall",root,V(-25,13,16),V(5,10,3));FinalEnvironmentModels.Create("Column_Slim",root,V(26,23,16),V(1,16,1));
   Objective("a",V(-24,8,16),"左塔火控 / 开左遮蔽路");Objective("b",V(24,16,16),"右塔火控 / 开右遮蔽路");
   // First two left indices remain the D2 R-S relay pair; remaining snipers get separate windows.
   Spawn("left",0,V(-16,8,8),150,true);Spawn("left",1,V(-26,8,8),180,true);Spawn("left",2,V(-18,8,16),180,true);
   for(int i=3;i<8;i++)Spawn("left",i,V(-40+(i-3)%3*4,0,-5+(i-3)/3*5),170);
   for(int i=0;i<3;i++)Spawn("right",i,V(15+i*4,16,8+(i%2)*7),215,true);for(int i=3;i<8;i++)Spawn("right",i,V(35+(i-3)%3*4,0,-5+(i-3)/3*5),210);
   V2Group("guard",V(0,0,39));V2Group("reinforce",V(0,0,88));
   Region("left",V(-48,7,0),V(90,24,100));Region("right",V(48,11,0),V(90,38,100));Region("guard",V(0,4,32),V(90,12,46));
   Door("a",V(-37,0,48),8,"左节点遮蔽入口");Door("b",V(37,0,48),8,"右节点遮蔽入口");
   for(int side=-1;side<=1;side+=2){Solid("遮蔽路线 outer wall",V(side*42,1.6f,61),V(.5f,3.2f,28));Solid("遮蔽路线 inner wall",V(side*32,1.6f,61),V(.5f,3.2f,28));}
   V2Cart(V(-12,0,34),V(12,0,34),V(0,0,23));V2JumpPad.Create(root,V(-10,0,3),V(10,8,3));Deck("跳板中层落脚",10,0,8,8,8);V2Steps(10,-23.45f,8);
   var anchor=Anchor(V(0,.02f,-8));if(D2MaintenanceFacility.FeatureEnabled)D2MaintenanceFacility.Create(root,anchor,V(0,.02f,-5.5f),V(-3,.02f,-12),V(0,0,80));
   V2Boss(V(0,0,113),V(-18,0,105),V(18,0,105),V(0,5,103),V(86,16,92));
   V2Supply(V(-37,0,-38),"左塔机房弹药柜",true);V2Supply(V(36,0,-38),"右塔机房弹药柜",true);V2Supply(V(-88,0,35),"雷达维护柜");V2Supply(V(-38,0,65),"调度场前保障柜");
   V2AnchorChain(V(0,0,-105),V(-20,0,-69),V(-18,8,12),V(18,16,12),V(-38,0,68),V(0,0,99));Exit(V(0,0,148),"北方货运出口");V2Knife(2,V(-37,0,-32));
   map.walkingCheckpoints.AddRange(new[]{V(-20,8,10),V(20,16,10),V(0,0,105)});
  }
  static void V2Refinery()
  {
   District("来料卸压区",-76,0,86,96,0,1);District("A装置反应作业区",16,0,86,96,0,1);District("B装置炉前区",16,100,86,96,0,1);District("侧仓检修支路",-76,100,86,96,0,2);District("货舱前总泄压场",16,200,86,96,0,1);
   Link("卸压至A管线",V(-38,0,0),V(-22,0,0),10);Link("A至B管线",V(16,0,45),V(16,0,55),10);Link("侧仓进料道",V(-76,0,45),V(-76,0,55),8);Link("侧仓至B检修道",V(-38,0,100),V(-22,0,100),8);Link("炉前进料道",V(16,0,145),V(16,0,155),10);map.spawn=V(-76,.04f,-42);Equipment("Props_Vessel_Tall",V(16,0,56),V(18,20,18),"中央炼化炉","阻断跨作业区长视线");
   Deck("地下维修沟",-16,90,12,230,-3);V2Steps(-16.95f,20,3,-3,90);V2Steps(-16.95f,150,3,-3,90);Deck("炉区跨沟支路",-16,45,14,8);Deck("炉前跨沟返程",-16,142,14,8);V2Tower("高位管廊检修桥",-6,100,6,60,10);V2Steps(-29,78,6);
   V2Group("first",V(-72,0,-2));V2Group("second",V(15,0,10));V2Group("third",V(13,0,119));V2Group("side",V(-72,0,109));Region("side",V(-76,4,100),V(90,12,96));
   DamageTarget("a",V(3,0,31),"A货舱封锁装置",288);DamageTarget("b",V(30,0,134),"B排气封锁装置",432);
   Door("a",V(16,0,46),12,"A装置货舱通路");Door("b",V(16,0,150),12,"B装置炉前通路");V2Valve(V(-10,0,31),V(-5,0,46),V(32,0,46));V2Breakable(V(3,0,104),10,"炉前防爆隔断");V2Breakable(V(-60,0,124),9,"侧仓非承重隔断");
   V2Boss(V(16,0,216),V(-3,0,205),V(35,0,205));V2JumpPad.Create(root,V(-26,0,90),V(-6,6,90));Deck("管廊投送落点",-6,90,8,10,6);
   V2Supply(V(-110,0,-39),"卸压安全间保障柜",true);V2Supply(V(-22,0,-38),"A装置维护柜");V2Supply(V(-22,0,61),"B装置维护柜");V2Supply(V(-22,0,161),"炉卫前保障柜",true);
   V2AnchorChain(V(-75,0,8),V(-38,0,20),V(0,0,30),V(-5,6,99),V(15,0,140),V(0,0,178));Bonus(V(-102,0,134),"侧仓记录");Exit(V(16,0,244),"货舱出口");map.walkingCheckpoints.AddRange(new[]{V(-16,-3,80),V(-6,6,100),V(16,0,205)});
  }
 }
}
