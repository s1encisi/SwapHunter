using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.AI.Navigation;
namespace SwapHunter
{
 public sealed class MapV2District : MonoBehaviour {public string title;public Vector3 center;public float width,depth;public int industry;}
 public sealed class MapV2OwnedMesh : MonoBehaviour {public Mesh mesh;void OnDestroy(){if(mesh)Destroy(mesh);}}
 public static partial class ExpeditionWorld
 {
  static readonly Dictionary<string,Material> architectureMaterials=new Dictionary<string,Material>();
  static Material AM(string id,Color color,bool glass=false)
  {
   if(architectureMaterials.TryGetValue(id,out var material)&&material)return material;
   material=new Material(Shader.Find("Universal Render Pipeline/Lit"));material.name="自有地图材质 / "+id;material.SetColor("_BaseColor",color);material.SetFloat("_Smoothness",glass?.8f:.25f);
   if(glass){material.SetFloat("_Surface",1);material.SetFloat("_SrcBlend",(int)BlendMode.SrcAlpha);material.SetFloat("_DstBlend",(int)BlendMode.OneMinusSrcAlpha);material.SetFloat("_ZWrite",0);material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");material.renderQueue=3000;}
   architectureMaterials[id]=material;return material;
  }
  static GameObject AB(string name,Vector3 p,Vector3 size,Material material,bool collision=true)
  {
   var go=Shapes.Box(name,root,p,size,0,collision,collision?Layers.World:0);go.GetComponent<Renderer>().sharedMaterial=material;return go;
  }
  static GameObject AC(string name,Vector3 p,float radius,float height,Material material,bool collision=true)
  {
   var go=Shapes.Make(name,PrimitiveType.Cylinder,root,p,V(radius*2,height/2,radius*2),0,collision,collision?Layers.World:0);go.GetComponent<Renderer>().sharedMaterial=material;return go;
  }
  static void Pipe(Vector3 a,Vector3 b,float radius,Material material,bool collision=true)
  {var pipe=AC("有连接端的工艺管线",(a+b)/2,radius,Vector3.Distance(a,b),material,collision);pipe.transform.rotation=Quaternion.FromToRotation(Vector3.up,b-a);}
  static void Lamp(Vector3 feet,Color color,float height=5)
  {
   var steel=AM("灯具钢",new Color(.19f,.23f,.27f));AB("作业照明灯柱",feet+Vector3.up*height/2,V(.2f,height,.2f),steel);AB("灯具罩",feet+Vector3.up*height,V(1.1f,.18f,.6f),AM("灯罩",new Color(.85f,.8f,.63f)),false);
   var light=new GameObject("真实作业照明").AddComponent<Light>();light.transform.SetParent(root,false);light.transform.position=feet+Vector3.up*(height-.2f);light.type=LightType.Point;light.color=color;light.intensity=2.3f;light.range=18;light.shadows=LightShadows.None;
  }
  static void WallWithDoor(string name,Vector3 center,float width,float height,float yaw,Material mat,float door=5)
  {
   var previous=root;var frame=new GameObject(name).transform;frame.SetParent(root,false);frame.localPosition=center;frame.localRotation=Quaternion.Euler(0,yaw,0);root=frame;
   for(int side=-1;side<=1;side+=2)AB("承重墙段",V(side*(width+door)/4,height/2,0),V((width-door)/2,height,.35f),mat);
   if(height>3)AB("实体过梁",V(0,(height+3)/2,0),V(door,height-3,.35f),mat);root=previous;
  }
  static void GlassWall(string name,Vector3 center,float width,float height,float yaw,Material wall)
  {
   var previous=root;var frame=new GameObject(name).transform;frame.SetParent(root,false);frame.localPosition=center;frame.localRotation=Quaternion.Euler(0,yaw,0);root=frame;float door=4;var glass=AM("防弹观察玻璃",new Color(.35f,.75f,.82f,.23f),true);
   var openings=width>80?new[]{-width*.37f,0,width*.37f}:new[]{0f};float edge=-width/2;
   foreach(float opening in openings){float stop=opening-door/2;if(stop>edge){float w=stop-edge,cx=(edge+stop)/2;AB("窗下实墙",V(cx,.5f,0),V(w,1,.35f),wall);AB("窗上承重梁",V(cx,(height+3.6f)/2,0),V(w,height-3.6f,.35f),wall);AB("实体防弹观察玻璃",V(cx,2.3f,0),V(w,2.6f,.14f),glass);}AB("观察室通道门楣",V(opening,(height+3)/2,0),V(door,height-3,.35f),wall);edge=opening+door/2;}
   if(edge<width/2){float w=width/2-edge,cx=(edge+width/2)/2;AB("窗下实墙",V(cx,.5f,0),V(w,1,.35f),wall);AB("窗上承重梁",V(cx,(height+3.6f)/2,0),V(w,height-3.6f,.35f),wall);AB("实体防弹观察玻璃",V(cx,2.3f,0),V(w,2.6f,.14f),glass);}
   root=previous;
  }
  static void Shell(string name,Vector3 center,float width,float depth,float height,Material wall,bool roof=true,bool glass=false)
  {
   WallWithDoor(name+" / 前入口",center+V(0,0,-depth/2),width,height,0,wall,width>60?12:5);
   WallWithDoor(name+" / 后入口",center+V(0,0,depth/2),width,height,0,wall,width>60?12:5);
   if(glass){GlassWall(name+" / 左观察室",center+V(-width/2,0,0),depth,height,90,wall);GlassWall(name+" / 右观察室",center+V(width/2,0,0),depth,height,90,wall);}
   else {WallWithDoor(name+" / 左维护门",center+V(-width/2,0,0),depth,height,90,wall,width>60?12:4);WallWithDoor(name+" / 右维护门",center+V(width/2,0,0),depth,height,90,wall,width>60?12:4);}
   if(roof)AB(name+" / 实体屋顶",center+Vector3.up*(height+.2f),V(width+.5f,.4f,depth+.5f),wall);

  }
  static void StreetBlock(MapV2District d,bool laboratory=false)
  {
   var wall=AM(laboratory?"研究所白墙":"旧港砖墙",laboratory?new Color(.67f,.73f,.77f):new Color(.51f,.28f,.17f));var roof=AM("深灰屋面",new Color(.18f,.22f,.25f));var glass=AM("建筑暗窗",new Color(.10f,.20f,.24f));float x=d.center.x,z=d.center.z,y=d.center.y;
   if(!laboratory){float back=z-d.depth*.25f+13.25f;Deck("街区可达屋顶巡检桥",x,back-3,60,6,y+8.4f);Stairs(x+26,back+20.45f,8.4f,4,y,180);}
   AB("中央道路沥青",V(x,y+.018f,z),V(18,.025f,d.depth-4),AM("沥青",new Color(.10f,.12f,.14f)),false);
   for(int side=-1;side<=1;side+=2)
   {
    AB("连续人行道缘石",V(x+side*10,y+.12f,z),V(.35f,.24f,d.depth-4),AM("路缘",new Color(.63f,.62f,.57f)));
    for(int row=0;row<2;row++)
    {
     float bz=z+(row==0?-d.depth*.25f:d.depth*.29f);var c=V(x+side*22,y,bz);Shell(laboratory?"观察/实验用房":"港口临街建筑",c,16,row==0?26:18,8,wall,true,laboratory);
     for(int floor=0;floor<2;floor++)for(int k=-1;k<=1;k++)AB("可辨认的窗组",c+V(-side*8.2f,2.2f+floor*3.2f,k*5),V(.15f,1.8f,2.5f),glass,false);
     if(!laboratory){AB("店面遮雨棚",c+V(-side*10,3,-7),V(4,.16f,8),AM("砖街遮雨棚",new Color(.67f,.48f,.22f)));AB("店前检验台",c+V(-side*10,.65f,5),V(3,1.3f,3),roof);}
    }
    Lamp(V(x+side*10,y,z-d.depth*.15f),laboratory?new Color(.72f,.86f,1):new Color(1,.72f,.4f));
   }
  }
  static void RailCar(Vector3 p,float length=28)
  {
   var metal=AM("货运车厢漆",new Color(.18f,.31f,.34f));var dark=AM("货车底架",new Color(.08f,.10f,.11f));AB("停靠货车厢实体",p+V(0,3.4f,0),V(8,4.8f,length),metal);
   AB("货车顶弧轮廓",p+V(0,6,0),V(7.4f,.6f,length-.4f),metal);
   for(int side=-1;side<=1;side+=2)
   {
    AB("嵌入路面的钢轨",p+V(side*2.5f,.08f,0),V(.18f,.12f,length+14),dark);
    foreach(float dz in new[]{-length*.33f,length*.33f}){var wheel=AC("转向架真实车轮",p+V(side*3.6f,.9f,dz),.9f,.45f,dark);wheel.transform.rotation=Quaternion.Euler(0,0,90);}
    for(int k=-2;k<=2;k++)AB("货车侧板立筋",p+V(side*4.05f,3.4f,k*length/6),V(.18f,4.7f,.15f),dark,false);
   }
  }
  static void RailQuarter(MapV2District d)
  {
   float x=d.center.x,z=d.center.z,y=d.center.y;for(int side=-1;side<=1;side+=2){RailCar(V(x+side*17,y,z-d.depth*.18f));Lamp(V(x+side*11,y,z-d.depth*.35f),new Color(1,.66f,.3f),6);}
   AB("站台横向雨棚",V(x,y+7,z-d.depth*.31f),V(28,.35f,9),AM("站棚深蓝钢",new Color(.12f,.21f,.28f)));for(int side=-1;side<=1;side+=2)AB("雨棚支柱",V(x+side*12,y+3.5f,z-d.depth*.31f),V(.45f,7,.45f),AM("立柱",new Color(.27f,.31f,.34f)));
   for(int k=-5;k<=5;k++)AB("站台湿路反光带",V(x+k*1.4f,y+.02f,z),V(.55f,.015f,d.depth-8),AM("湿路微光",new Color(.15f,.24f,.30f)),false);
  }
  static void PipeQuarter(MapV2District d)
  {
   float x=d.center.x,z=d.center.z,y=d.center.y;float pipeHeight=visualLevel==5?11:visualLevel==2&&d.title.Contains("冷却井环底")?4:7;var pipe=AM("保温橙灰管",new Color(.57f,.43f,.24f));var tank=AM("冷却压力罐",new Color(.37f,.48f,.50f));
   for(int side=-1;side<=1;side+=2)
   {
    float tx=x+side*17;AC("有底座的压力储罐",V(tx,y+5,z-d.depth*.18f),4,10,tank);AB("压力罐承台",V(tx,y+.45f,z-d.depth*.18f),V(9,.9f,9),AM("混凝土承台",new Color(.32f,.33f,.31f)));
    if(pipeHeight>10)Pipe(V(tx,y+9.6f,z-d.depth*.18f),V(tx,y+pipeHeight,z-d.depth*.18f),.65f,pipe);Pipe(V(tx,y+pipeHeight,z-d.depth*.18f),V(tx,y+pipeHeight,z+d.depth*.28f),.65f,pipe);Pipe(V(tx,y+pipeHeight,z+d.depth*.28f),V(x+side*5,y+pipeHeight,z+d.depth*.28f),.65f,pipe);
    for(int row=0;row<3;row++){float pz=z-d.depth*.2f+row*d.depth*.22f;AB("管廊真实支架",V(tx,y+pipeHeight/2,pz),V(.45f,pipeHeight,.45f),tank);}
   }
   Pipe(V(x-17,y+pipeHeight,z+d.depth*.28f),V(x+17,y+pipeHeight,z+d.depth*.28f),.65f,pipe);Lamp(V(x+9,y,z-d.depth*.3f),new Color(1,.5f,.2f));
  }
  static void Rack(Vector3 p,float length,float height=7)
  {
   var frame=AM("冷链蓝色货架",new Color(.16f,.35f,.48f));var stock=AM("封装冷链箱",new Color(.69f,.74f,.71f));
   for(int side=-1;side<=1;side+=2)for(int end=-1;end<=1;end+=2)AB("货架立柱",p+V(side*2,height/2,end*length/2),V(.23f,height,.23f),frame);
   for(int level=0;level<3;level++){float h=.3f+level*height/3;AB("货架承托层",p+V(0,h,0),V(4.4f,.18f,length),frame);for(int i=0;i<3;i++)AB("托盘包装货物",p+V(0,h+.85f,-length/3+i*length/3),V(3.7f,1.5f,length/3-.8f),stock);}
  }
  static void WarehouseQuarter(MapV2District d)
  {
   var wall=AM("冷链夹芯墙",new Color(.47f,.60f,.65f));Shell(d.title+"实体仓房",d.center,d.width-2,d.depth-2,18,wall,true);
   float x=d.center.x,z=d.center.z,y=d.center.y;bool high=d.title.Contains("C仓");
   float cross=z-d.depth/2+39.5f;Stairs(d.depth<70?x:x+d.width/2-8,z-d.depth/2+6.05f,12,4,y);Deck("冷链上层横向检修廊",x,cross,d.width-12,8,y+12);
   foreach(int side in new[]{-1,1}){Deck("冷链货架上层巡检线",x+side*25,z,14,d.depth-16,y+12);for(int k=-1;k<=1;k+=2)AB("检修廊承重柱",V(x+side*31,y+6,z+k*d.depth*.25f),V(.45f,12,.45f),wall);}

   for(int side=-1;side<=1;side+=2){Rack(V(x+side*(high?29:17),y,z-d.depth*.13f),30);Rack(V(x+side*33,y,z+d.depth*.24f),20);Lamp(V(x+side*9,y,z-d.depth*.3f),new Color(.57f,.80f,1),8);}
   for(int i=-1;i<=1;i++)AB("顶棚灯带",d.center+V(i*24,13.5f,0),V(.5f,.14f,d.depth-10),AM("冷光灯带",new Color(.75f,.9f,1)),false);
  }
  static void RockMass(Vector3 center,float width,float depth,float bottom)
  {
   // Original low-poly coastal geology. Top stays under the authored deck; no inflated remote area is counted.
   float top=center.y-.64f;var ring=new[]{new Vector2(-.5f,-.36f),new Vector2(-.35f,-.5f),new Vector2(.35f,-.5f),new Vector2(.5f,-.35f),new Vector2(.5f,.35f),new Vector2(.35f,.5f),new Vector2(-.35f,.5f),new Vector2(-.5f,.35f)};
   var points=new List<Vector3>();for(int layer=0;layer<3;layer++)for(int i=0;i<8;i++){float scale=layer==0?1:layer==1?.96f:.82f;points.Add(new Vector3(center.x+ring[i].x*width*scale,layer==0?top:layer==1?Mathf.Lerp(top,bottom,.45f):bottom,center.z+ring[i].y*depth*scale));}
   var vertices=new List<Vector3>();var tris=new List<int>();void Tri(int a,int b,int c){int start=vertices.Count;vertices.Add(points[a]);vertices.Add(points[b]);vertices.Add(points[c]);tris.Add(start);tris.Add(start+1);tris.Add(start+2);}
   for(int layer=0;layer<2;layer++)for(int i=0;i<8;i++){int j=(i+1)%8;Tri(layer*8+i,layer*8+j,(layer+1)*8+i);Tri(layer*8+j,(layer+1)*8+j,(layer+1)*8+i);}for(int i=1;i<7;i++)Tri(0,i+1,i);
   var mesh=new Mesh{name="自有海蚀岩体"};mesh.SetVertices(vertices);mesh.SetTriangles(tris,0);mesh.RecalculateNormals();mesh.RecalculateBounds();var go=new GameObject("实体海蚀岩岸与悬崖");go.transform.SetParent(root,false);go.layer=Layers.World;go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=AM("海蚀岩层",new Color(.27f,.31f,.29f));go.AddComponent<MeshCollider>().sharedMesh=mesh;go.AddComponent<MapV2OwnedMesh>().mesh=mesh;var modifier=go.AddComponent<NavMeshModifier>();modifier.overrideArea=true;modifier.area=1;
  }
  static void Sea(float waterY)
  {
   AB("港口真实海面",V(0,waterY,80),V(900,.1f,900),AM("海面",new Color(.075f,.24f,.31f)),false);for(int i=0;i<24;i++)AB("海面远处浪带",V(-240+(i%6)*90,waterY+.08f,-120+(i/6)*135),V(32,.02f,.35f),AM("海面浅纹",new Color(.22f,.42f,.47f)),false);
  }
  static void CoastalQuarter(MapV2District d)
  {
   if(visualLevel==6&&d.title.StartsWith("A岛")){RockMass(d.center+V(-18,0,0),24,d.depth,-42);RockMass(d.center+V(18,0,0),24,d.depth,-42);}else RockMass(d.center,d.width,d.depth,-42);var wall=AM("海岸站房",new Color(.55f,.61f,.59f));
   for(int side=-1;side<=1;side+=2){Shell("沿岸通信维修间",d.center+V(side*18,0,-d.depth*.22f),12,20,6,wall,true);Lamp(d.center+V(side*10,0,-d.depth*.25f),new Color(.7f,.85f,1));}
   if(visualLevel==8){AC("通信塔实体天线杆",d.center+V(d.width*.34f,11,0),.65f,22,AM("塔杆白漆",new Color(.78f,.78f,.72f)));for(int i=0;i<3;i++)AB("天线反射阵列",d.center+V(d.width*.34f,10+i*4,0),V(7,.35f,3),AM("天线红白标识",i%2==0?new Color(.72f,.23f,.15f):new Color(.8f,.81f,.76f)));}
  }
  static void OreMound(Vector3 center,float radius,float height)
  {
   var mesh=new Mesh{name="自有矿料堆"};var v=new Vector3[10];v[0]=center+Vector3.up*height;for(int i=0;i<8;i++)v[i+1]=center+V(Mathf.Cos(i*Mathf.PI/4)*radius,0,Mathf.Sin(i*Mathf.PI/4)*radius);v[9]=center;var t=new List<int>();for(int i=0;i<8;i++){t.AddRange(new[]{0,(i+1)%8+1,i+1,9,i+1,(i+1)%8+1});}mesh.vertices=v;mesh.triangles=t.ToArray();mesh.RecalculateNormals();var go=new GameObject("矿料实际堆体");go.transform.SetParent(root,false);go.layer=Layers.World;go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=AM("矿料",new Color(.39f,.29f,.17f));go.AddComponent<MeshCollider>().sharedMesh=mesh;go.AddComponent<MapV2OwnedMesh>().mesh=mesh;
  }
  static void MineQuarter(MapV2District d)
  {
   for(int side=-1;side<=1;side+=2){OreMound(d.center+V(side*19,0,-d.depth*.18f),8,9);var steel=AM("矿机黄钢",new Color(.64f,.47f,.18f));for(int row=0;row<3;row++)AB("皮带机承重架",d.center+V(side*18,3,row*14),V(.55f,6,.55f),steel);AB("有支撑的矿料传送带",d.center+V(side*18,6,14),V(6,.65f,44),AM("传送带黑胶",new Color(.12f,.12f,.11f)));Lamp(d.center+V(side*10,0,-d.depth*.28f),new Color(1,.78f,.4f));}
  }
  static void InspectionQuarter(MapV2District d)
  {
   var white=AM("检疫白色围护",new Color(.76f,.83f,.86f));var blue=AM("检疫蓝标",new Color(.10f,.39f,.62f));for(int side=-1;side<=1;side+=2){Shell("人员设备检验舱",d.center+V(side*18,0,-d.depth*.22f),12,20,5,white,true);AB("检疫作业遮棚",d.center+V(side*18,5.3f,-d.depth*.22f),V(16,.3f,25),blue);Lamp(d.center+V(side*10,0,-d.depth*.33f),new Color(.85f,.94f,1));}
   for(int side=-1;side<=1;side+=2)AB("检验导向护栏",d.center+V(side*8,.6f,-d.depth*.20f),V(.2f,1.2f,22),blue);
  }
  static void TowerQuarter(MapV2District d)
  {
   var steel=AM("雷达场深蓝钢",new Color(.22f,.31f,.37f));for(int side=-1;side<=1;side+=2){Shell("火控设备维护库",d.center+V(side*(d.title.Contains("双塔中央")?37:23),0,-d.depth*.27f),18,24,7,steel,true);Lamp(d.center+V(side*11,0,-d.depth*.35f),new Color(1,.84f,.52f));}
   if(d.title.Contains("南方")||d.title.Contains("北方")){for(int side=-1;side<=1;side+=2)AB("装车起重机立架",d.center+V(side*11,8,0),V(1.2f,16,1.2f),AM("起重机安全橙",new Color(.74f,.45f,.15f)));AB("装车起重机横梁",d.center+V(0,16,0),V(24,1.2f,2),AM("起重机安全橙",new Color(.74f,.45f,.15f)));}
  }
  static void V2AuthorArchitecture()
  {
   foreach(var d in root.GetComponentsInChildren<MapV2District>())
   {
    switch(visualLevel){case 0:InspectionQuarter(d);break;case 1:StreetBlock(d);break;case 2:PipeQuarter(d);if(!d.title.Contains("冷却井环底"))Shell(d.title+"换热翼廊",d.center,d.width-2,d.depth-2,24,AM("冷却井深灰墙",new Color(.25f,.36f,.4f)),true);break;case 3:RailQuarter(d);break;case 4:TowerQuarter(d);break;case 5:PipeQuarter(d);break;case 6:CoastalQuarter(d);break;case 7:WarehouseQuarter(d);break;case 8:CoastalQuarter(d);break;case 9:MineQuarter(d);break;case 10:StreetBlock(d,true);Shell(d.title+"实验楼包络",d.center,d.width-2,d.depth-2,10,AM("实验楼外墙",new Color(.65f,.73f,.76f)),true,true);break;case 11:Shell(d.title+"反应设施包络",d.center,d.width-2,d.depth-2,d.title.Contains("中央")?30:10,AM("反应炉围护",new Color(.26f,.34f,.39f)),true);PipeQuarter(d);break;}
   }
   if(visualLevel==6||visualLevel==8)Sea(visualLevel==6?-6:-1.5f);
   if(visualLevel==11)
   {
    var core=AM("反应炉承压壁",new Color(.28f,.36f,.41f));AC("中央反应炉承压本体",V(0,17,43),8,34,core);for(int i=0;i<5;i++)AC("反应炉环形加强肋",V(0,4+i*6,43),8.7f,.8f,AM("反应炉环带",new Color(.62f,.4f,.17f)));
    for(int side=-1;side<=1;side+=2)Pipe(V(side*8,18,43),V(side*35,18,43),.8f,core);
   }
   V2BossArchitecture();V2ThemeLighting();
  }
  static void V2BossArchitecture()
  {
   var p=layout.MiniBossPosts[0];string[] names={"监理检验机组","执法闸口岗亭","监察冷却管组","壁垒车厢","校射火控设备","炉卫隔热罐","牵引卷扬柱","分拣机房","猎隼风罩","装运料斗","审查电气柱","相位门锁组件"};
   if(visualLevel==0)WallWithDoor("监理终检控制墙",p+V(0,0,12),36,5,0,AM("检疫白色围护",new Color(.76f,.83f,.86f)),10);
   if(visualLevel==4){for(int side=-1;side<=1;side+=2)Shell("火控校射阵列机房",p+V(side*30,0,4),12,20,7,AM("雷达场深蓝钢",new Color(.22f,.31f,.37f)),true);}
   if(visualLevel==8){for(int side=-1;side<=1;side+=2){AC("回收坪冷却风罩",p+V(side*28,3,12),3,6,AM("冷却风罩",new Color(.33f,.43f,.46f)));Pipe(p+V(side*28,6,12),p+V(side*28,6,-10),.7f,AM("塔杆白漆",new Color(.78f,.78f,.72f)));}}
   for(int side=-1;side<=1;side+=2)
   {
    var at=p+V(side*9,0,-10);if(visualLevel==3){RailCar(at+V(side*4,0,0),16);continue;}
    if(visualLevel==2||visualLevel==5||visualLevel==6)AC(names[visualLevel],at+Vector3.up*2.5f,1.8f,5,AM("Boss设施钢",new Color(.38f,.45f,.48f)));
    else {AB(names[visualLevel],at+Vector3.up*1.35f,V(3.4f,2.7f,5),AM("Boss作业设备",new Color(.3f,.39f,.44f)));AB("设备顶接线与可识别朝向",at+V(0,2.9f,0),V(3.8f,.25f,5.4f),AM("安全黄边",new Color(.77f,.57f,.18f)));}
    Feature(AB("Boss设备基础",at+V(0,.1f,0),V(4,.2f,5.6f),AM("设备基础",new Color(.29f,.32f,.32f))),"boss-cover-"+side,names[visualLevel],"形成内侧与外侧接近路线；可靠断开预告枪线","实体掩体；两侧均可绕行","本章专属Boss与固定停靠点");
   }
  }
  static void V2ThemeLighting()
  {
   bool night=visualLevel==3||visualLevel==6;var sun=root.Find("Chapter sun");if(sun){var light=sun.GetComponent<Light>();light.color=visualLevel==5||visualLevel==9?new Color(1,.77f,.45f):new Color(.82f,.91f,1);light.intensity=night?.20f:visualLevel==7||visualLevel==10||visualLevel==11?.65f:1.25f;}
   RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=night?new Color(.12f,.17f,.23f):visualLevel==5?new Color(.34f,.25f,.19f):new Color(.39f,.43f,.47f);RenderSettings.fogColor=night?new Color(.055f,.095f,.14f):visualLevel==9?new Color(.51f,.42f,.28f):new Color(.37f,.47f,.54f);RenderSettings.fogStartDistance=100;RenderSettings.fogEndDistance=400;
   if(RenderSettings.skybox){RenderSettings.skybox.SetColor("_Zenith",night?new Color(.015f,.035f,.075f):new Color(.08f,.22f,.34f));RenderSettings.skybox.SetColor("_Horizon",RenderSettings.fogColor);RenderSettings.skybox.SetColor("_Ground",RenderSettings.fogColor*.65f);RenderSettings.skybox.SetVector("_SunDir",night?new Vector4(-.5f,-.8f,-.7f,0):new Vector4(-.45f,.45f,-.65f,0));}
  }
 }
}
