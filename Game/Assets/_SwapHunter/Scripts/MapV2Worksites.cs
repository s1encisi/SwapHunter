using UnityEngine;
namespace SwapHunter
{
 public static partial class ExpeditionWorld
 {
  static void V2StreetMaintenanceYard()
  {
   // Both east entrances match the real west doors of the existing emergency-street buildings.
   Deck("街区消防与车辆检修院",-166,133,42,46);
   var wall=AM("旧港砖墙",new Color(.51f,.28f,.17f));var steel=AM("检修院钢",new Color(.24f,.30f,.31f));
   AB("检修院西围墙",V(-187,1.4f,133),V(.35f,2.8f,46),wall);AB("检修院南围墙",V(-166,1.4f,110),V(42,2.8f,.35f),wall);AB("检修院北围墙",V(-166,1.4f,156),V(42,2.8f,.35f),wall);
   AB("检修院两门之间墙段",V(-145,1.4f,134.8f),V(.35f,2.8f,32.4f),wall);
   AB("检修院南门翼",V(-145,1.4f,112.3f),V(.35f,2.8f,4.6f),wall);AB("检修院北门翼",V(-145,1.4f,155.8f),V(.35f,2.8f,.4f),wall);
   Workshop("消防检修值守间",-174,143,0,16,12);
   var pump=AB("消防泵组与检修岛",V(-177,1.35f,123),V(6,2.7f,5),steel);Feature(pump,"street-fire-pump","为检查街区的消防立管供水","分隔院内两条返回路线并提供可靠遮挡","固定实体设备，不生成敌人或奖励","南入口连接前楼侧门，北入口连接后楼侧门");
   Pipe(V(-177,2.7f,123),V(-183,2.7f,123),.25f,steel);Pipe(V(-183,2.7f,123),V(-183,2.7f,147),.25f,steel);
   AB("车辆检修地沟盖板",V(-157,.02f,132),V(6,.035f,18),AM("检修地沟格栅",new Color(.16f,.2f,.21f)),false);Lamp(V(-158,0,119),new Color(1,.73f,.42f));
   Label("消防检修院 / 前楼与后楼双向通行",V(-160,3,132),.43f);map.walkingCheckpoints.Add(V(-159,0,133));
  }
  static void V2ColdMaintenanceRoom()
  {
   // A linked cold-machine workroom; two separate doors share the warehouse's authored 12m side opening.
   Deck("冷机联通检修支区",-48,211,26,28);var wall=AM("冷链夹芯墙",new Color(.47f,.60f,.65f));var steel=AM("冷机检修钢",new Color(.26f,.36f,.41f));
   AB("冷机间西墙",V(-61,2.5f,211),V(.35f,5,28),wall);AB("冷机间南墙",V(-48,2.5f,197),V(26,5,.35f),wall);AB("冷机间北墙",V(-48,2.5f,225),V(26,5,.35f),wall);AB("冷机间实体顶",V(-48,5.2f,211),V(26,.4f,28),wall);
   AB("冷机间南门翼",V(-35,2.5f,201.25f),V(.35f,5,8.5f),wall);AB("冷机间双门中柱",V(-35,2.5f,211),V(.35f,5,5),wall);AB("冷机间北门翼",V(-35,2.5f,220.75f),V(.35f,5,8.5f),wall);
   foreach(float z in new[]{207f,215f})AB("冷机维护门楣",V(-35,4,z),V(.35f,2,3),wall);
   var machine=AB("冷机压缩机检修岛",V(-47,1.45f,211),V(8,2.9f,3),steel);Feature(machine,"cold-maintenance","中央温控系统的压缩机与电气检修","中央设备分开南北两条近路，可借门框断线","固定机组；无额外敌人预算与奖励","两个维护门均接B仓后场");
   Pipe(V(-53,3.4f,203),V(-53,3.4f,219),.30f,steel);Pipe(V(-53,3.4f,211),V(-47,3.4f,211),.30f,steel);Lamp(V(-43,0,201),new Color(.65f,.85f,1),4.4f);
   Label("冷机检修 / 南北维护门联通",V(-48,3.8f,217),.36f);map.walkingCheckpoints.Add(V(-55,0,211));
  }
  static void V2ExpandWorksites()
  {
   switch(visualLevel)
   {
    case 0:District("检疫复核与观察支廊",72,154,64,56,0,0);Link("复核支路前门",V(36,0,137),V(45,0,137),8);Link("复核支路后门",V(36,0,175),V(45,0,175),8);break;
    case 1:District("居民应急检查支街",-114,133,62,70,0,2);Link("应急街前接入口",V(-78,0,108),V(-91,0,108),8);Link("应急街后接入口",V(-78,0,158),V(-91,0,158),8);V2StreetMaintenanceYard();break;
    case 2:District("上井换热与压力复核翼",90,115,70,70,18,1);Link("换热翼前接驳",V(48,18,99),V(62,18,99),8);Link("换热翼后接驳",V(48,18,132),V(62,18,132),8);break;
    case 3:District("前后站场车务检修支线",-145,87,80,80,0,2);Link("车务前站连接",V(-99,0,54),V(-113,0,54),10);Link("车务后站连接",V(-99,0,111),V(-113,0,111),10);break;
    case 4:District("雷达备件与南场转运线",-100,-72,90,44,0,3);Link("南调度至雷达维护",V(-55,0,-82),V(-43,0,-82),10);Link("雷达备件上行通路",V(-100,0,-56),V(-100,0,-44),10);break;
    case 5:District("炉后溶剂卸载与应急旁路",-76,200,90,75,0,1);Link("侧仓至炉后旁路",V(-76,0,144),V(-76,0,170),10);Link("炉后至货舱作业口",V(-35,0,205),V(-22,0,205),10);break;
    case 6:District("C岛船舶引导与转发检修台",99,80,50,40,16,3);Link("转发台前联络",V(68,16,68),V(80,16,68),8);Link("转发台后联络",V(68,16,92),V(80,16,92),8);break;
    case 7:District("B仓温控复核与冷机后场",0,211,70,62,0,1);Link("冷机前维护口",V(-16,0,173),V(-16,0,187),8);Link("温控侧维护口",V(18,0,173),V(18,0,187),8);V2ColdMaintenanceRoom();break;
    case 8:District("地面海事观测与回收支坪",-114,211,58,50,0,3);Link("观测坪前联络",V(-80,0,195),V(-93,0,195),8);Link("观测坪后联络",V(-80,0,226),V(-93,0,226),8);break;
    case 10:District("研究所应急观察与检修后翼",0,200,80,78,0,3);Link("权限后跨沟观察廊",V(0,0,122),V(0,0,171),12);Link("左外环接后翼",V(-36,0,160),V(-36,0,173),8);Link("右外环接后翼",V(36,0,160),V(36,0,173),8);break;
    case 11:District("反应炉应急换热与撤离旁翼",-115,122,90,70,0,1);Link("西授权至应急换热",V(-110,0,83),V(-110,0,95),10);Link("换热翼回终战外环",V(-75,0,113),V(-67,0,113),10);District("二级冷却与应急供能工段",-115,179,60,44,0,1);Link("两级换热检修门",V(-115,0,151),V(-115,0,163),10);break;
   }
  }
 }
}
