using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;
namespace SwapHunter
{
 public enum ExpeditionNodeKind { Objective,Extraction,Bonus,Jammer,Launch }
 public sealed class ExpeditionNode:MonoBehaviour
 {
  public ExpeditionNodeKind kind;public bool used;public string label;public Renderer lamp;
  public int objectiveIndex;public TextMesh worldLabel;public LineRenderer marker;bool extractionReady;
  public Vector3 Point=>transform.position+Vector3.up;
  public string DisplayLabel=>kind==ExpeditionNodeKind.Objective?"["+objectiveIndex.ToString("00")+"] "+label:label;
  void SetLabel(string value,Color color)
  {if(worldLabel){worldLabel.text=value;worldLabel.color=color;worldLabel.font.RequestCharactersInTexture(value,worldLabel.fontSize);}if(marker){marker.startColor=marker.endColor=color;var block=new MaterialPropertyBlock();block.SetColor("_BaseColor",color);block.SetColor("_Color",color);marker.SetPropertyBlock(block);}}
  public void Activate(){used=true;if(lamp)lamp.sharedMaterial=Shapes.Mat(10);SetLabel(DisplayLabel+" / 已完成",new Color(.52f,1,.64f));}
  public void SetExtractionReady(bool ready)
  {if(kind!=ExpeditionNodeKind.Extraction||extractionReady==ready)return;extractionReady=ready;if(lamp)lamp.sharedMaterial=Shapes.Mat(ready?10:4);SetLabel("入口撤离 / "+(ready?"可撤离":"主线未完成"),ready?new Color(.52f,1,.64f):new Color(.4f,.9f,1));}
 }
 public sealed class ExpeditionWorldLabel:MonoBehaviour
 {
  void LateUpdate()
  {
   var game=DemoGame.I;if(!game||!game.player||!game.player.cameraView)return;
   Transform view=game.player.cameraView.transform;transform.rotation=view.rotation;
   transform.localScale=Vector3.one*Mathf.Clamp(Vector3.Distance(transform.position,view.position)/10f,.35f,1f);
  }
 }
 public sealed class ExpeditionMap
 {
  public Transform root;
  public Vector3 spawn=new Vector3(0,.04f,3);
  public List<ExpeditionNode> nodes=new List<ExpeditionNode>();
  public List<Vector3> enemySpawns=new List<Vector3>();
  public List<PhaseAnchor> anchors=new List<PhaseAnchor>();
  public int objectiveCount;
  public GameObject returnBridge;public NavMeshSurface navigation;public bool returnBridgeReady;
 }
 public static class ExpeditionWorld
 {
  static DemoGame g;static Transform root;static ExpeditionMap map;
  static GameObject Solid(string n,Vector3 p,Vector3 s,int m=1)=>Shapes.Box(n,root,p,s,m,true,Layers.World);
  static void Floor(float x,float z,float width,float depth,float h=0)
  {
   var proxy=Solid("Deck / navigation surface",new Vector3(x,h-.35f,z),new Vector3(width,.7f,depth),0);
   proxy.GetComponent<Renderer>().enabled=false;
   for(float xx=-width/2;xx<width/2-.01f;xx+=4)for(float zz=-depth/2;zz<depth/2-.01f;zz+=4)
   {float w=Mathf.Min(4,width/2-xx),d=Mathf.Min(4,depth/2-zz);ImportedModels.Create(g.config.models.deck,root,new Vector3(x+xx+w/2,h,z+zz+d/2),new Vector3(w/4,1,d/4));}
  }
  static void Cover(float x,float z,float h=1.15f,float width=2.6f)
  {
   var cover=Solid("Cover collider",new Vector3(x,h/2,z),new Vector3(width,h,1.8f),2);cover.AddComponent<CoverSite>();
   ImportedModels.ReplaceBox(cover,g.config.models.cargo,new Vector3(4,2.4f,3));
   Shapes.Box("Cover edge",root,new Vector3(x,h+.025f,z),new Vector3(width,.05f,1.82f),4);
  }
  static void Tower(float x,float z,float height,float width=7,float depth=8)
  {
   Floor(x,z,width,depth,height);
   for(int sx=-1;sx<=1;sx+=2)for(int sz=-1;sz<=1;sz+=2)Solid("Tower pier",new Vector3(x+sx*(width/2-.5f),height/2-.1f,z+sz*(depth/2-.5f)),new Vector3(.6f,height,.6f),3);
  }
  static void Stairs(float x,float startZ,float height,float width=3)
  {
   int steps=Mathf.CeilToInt(height/.22f);float rise=height/steps,run=.55f;
   for(int i=0;i<steps;i++)
   {float h=(i+1)*rise;Solid("Stair tread",new Vector3(x,h/2,startZ+i*run),new Vector3(width,h,run+.01f),1);Shapes.Box("Stair nosing",root,new Vector3(x,h+.008f,startZ+i*run-run*.43f),new Vector3(width,.02f,.055f),4);}
   for(int side=-1;side<=1;side+=2)
   {
    float span=(steps-1)*run,angle=-Mathf.Atan2(height-rise,span)*Mathf.Rad2Deg;
    var rail=Solid("Stair side safety rail",new Vector3(x+side*(width/2+.06f),(height+rise)/2+.55f,startZ+span/2),new Vector3(.16f,1.25f,Mathf.Sqrt(span*span+(height-rise)*(height-rise))+.28f),3);
    rail.transform.localRotation=Quaternion.Euler(angle,0,0);rail.GetComponent<Renderer>().enabled=false;
    var top=Shapes.Box("Steel handrail",root,new Vector3(x+side*(width/2+.06f),(height+rise)/2+1.12f,startZ+span/2),new Vector3(.12f,.10f,Mathf.Sqrt(span*span+(height-rise)*(height-rise))+.3f),3);
    top.transform.localRotation=Quaternion.Euler(angle,0,0);
    for(int post=0;post<steps;post+=3)Shapes.Box("Handrail support",root,new Vector3(x+side*(width/2+.06f),(post+1)*rise+.55f,startZ+post*run),new Vector3(.08f,1.1f,.08f),3);
    var go=new GameObject("Stair side navigation exclusion");go.layer=Layers.World;go.transform.SetParent(root);go.transform.position=new Vector3(x+side*(width/2),height/2,startZ+span/2);
    var v=go.AddComponent<NavMeshModifierVolume>();v.area=1;v.size=new Vector3(.8f,height+1.4f,steps*run+.2f);
   }
  }
  static void Ramp(float x,float z,float height,float length,float width)
  {
   var p=Solid("Sloped access",new Vector3(x,height/2-.15f,z),new Vector3(width,.3f,Mathf.Sqrt(length*length+height*height)),1);
   p.transform.localRotation=Quaternion.Euler(-Mathf.Atan2(height,length)*Mathf.Rad2Deg,0,0);
  }
  static TextMesh Label(string value,Vector3 p,float size=.5f)
  {
   var go=new GameObject("Wayfinding / "+value);go.transform.SetParent(root);go.transform.localPosition=p;
   var t=go.AddComponent<TextMesh>();t.text=value;t.font=g.font;t.fontSize=64;t.characterSize=size/8;t.anchor=TextAnchor.MiddleCenter;t.color=new Color(.4f,.9f,1);
      t.font.RequestCharactersInTexture(value,64);
   if(g.config.worldTextMaterial){var material=new Material(g.config.worldTextMaterial);material.mainTexture=t.font.material.mainTexture;t.GetComponent<MeshRenderer>().sharedMaterial=material;go.AddComponent<WorldFontAtlas>().Initialize(t.font,material);}
   go.AddComponent<ExpeditionWorldLabel>();return t;
  }
  static PhaseAnchor Anchor(Vector3 p)
  {
   var go=new GameObject("Recoverable phase anchor");go.transform.SetParent(root);go.transform.position=p;go.layer=Layers.Actor;
   ImportedModels.Create(g.config.models.beacon,go.transform,Vector3.zero);
   var c=go.AddComponent<CapsuleCollider>();c.center=Vector3.up*.68f;c.radius=.32f;c.height=1.35f;
   var a=go.AddComponent<PhaseAnchor>();map.anchors.Add(a);g.anchors.Add(a);g.anchorOrigins.Add(p);
   Shapes.Ring(go.transform,p+Vector3.up*.035f,.85f,new Color(.1f,.8f,1),.04f);return a;
  }
  static ExpeditionNode Node(Vector3 p,ExpeditionNodeKind kind,string label)
  {
   var go=new GameObject(label);go.transform.SetParent(root);go.transform.position=p;go.layer=Layers.World;
   ImportedModels.Create(g.config.models.terminal,go.transform,Vector3.zero);
   var c=go.AddComponent<BoxCollider>();c.center=Vector3.up*.7f;c.size=new Vector3(1.02f,1.4f,.8f);
   var n=go.AddComponent<ExpeditionNode>();n.kind=kind;n.label=label;map.nodes.Add(n);if(kind==ExpeditionNodeKind.Objective)n.objectiveIndex=++map.objectiveCount;
   n.lamp=Shapes.Box("Node status",go.transform,new Vector3(0,1.45f,0),new Vector3(.85f,.08f,.1f),kind==ExpeditionNodeKind.Bonus?5:4).GetComponent<Renderer>();
   n.marker=Shapes.Ring(go.transform,p+Vector3.up*.04f,1,kind==ExpeditionNodeKind.Bonus?new Color(1,.5f,.1f):new Color(.1f,.85f,1),.045f);
   n.worldLabel=Label(kind==ExpeditionNodeKind.Extraction?"入口撤离 / 主线未完成":n.DisplayLabel,p+Vector3.up*2.7f,.45f);return n;
  }
  static void ReturnBridges(int level)
  {
   Transform worldRoot=root;map.returnBridge=new GameObject("Authorization return bridges");root=map.returnBridge.transform;root.SetParent(worldRoot);
   float x=level==6?-10:0;Floor(x,22,4,10.2f,.015f);
   for(int side=-1;side<=1;side+=2)Shapes.Box("Return bridge edge",root,new Vector3(x+side*1.93f,.055f,22),new Vector3(.09f,.05f,10),4);
   Label("回程桥 / 入口撤离",new Vector3(x,2.6f,22),.5f);
   if(level==6)
   {
    // Keep all relay islands disconnected until every authorization is active.
    Floor(-14,52,3,14.2f,.015f);Floor(0,59.5f,10,3,.015f);
    Label("回程 →",new Vector3(-14,2.6f,52),.4f);
   }
   root=worldRoot;map.returnBridge.SetActive(false);
  }
  static void LaunchPad(Vector3 p)
  {
   var go=new GameObject("One-use launch pad");go.transform.SetParent(root);go.transform.position=p;
   var node=go.AddComponent<ExpeditionNode>();node.kind=ExpeditionNodeKind.Launch;node.label="大跳装置 / 一次性";map.nodes.Add(node);
   node.lamp=Shapes.Box("Launch plate",go.transform,new Vector3(0,.035f,0),new Vector3(1.8f,.06f,1.8f),4).GetComponent<Renderer>();
   Shapes.Ring(go.transform,p+Vector3.up*.08f,1,new Color(.1f,.9f,1),.06f);
   Label("大跳 / LAUNCH",p+Vector3.up*1.4f,.38f);
  }
  static void Supply(Vector3 p)
  {
   var go=new GameObject("Field supply");go.transform.SetParent(root);go.transform.position=p;
   ImportedModels.Create(g.config.models.supply,go.transform,Vector3.zero);
   go.AddComponent<SupplyPickup>().Initialize();
  }
  public static ExpeditionMap Build(DemoGame game,int level)
  {
   g=game;map=new ExpeditionMap();root=new GameObject("Chapter "+(level+1)+" / "+ExpeditionCatalog.Levels[level].name).transform;map.root=root;
   try {
   var sun=new GameObject("Chapter sun").AddComponent<Light>();sun.transform.SetParent(root);sun.type=LightType.Directional;sun.transform.rotation=Quaternion.Euler(38,-38,0);sun.color=new Color(.82f,.91f,1);sun.intensity=1.6f;sun.shadows=LightShadows.Soft;
   for(int i=0;i<10;i++)
   {
    float side=i%2==0?-1:1,height=14+(i%4)*7,x=side*(31+(i%3)*9),z=9+i*9;
    Shapes.Box("Distant port tower",root,new Vector3(x,height*.5f-7,z),new Vector3(7,height,9),i%3==0?11:1);
    for(int band=0;band<3;band++)Shapes.Box("Skyline light ribbon",root,new Vector3(x,band*4+2,z-4.52f),new Vector3(5,.10f,.08f),4);
   }
   for(int side=-1;side<=1;side+=2)
   {
    Shapes.Box("Freight crane mast",root,new Vector3(side*25,8,47),new Vector3(.8f,26,.8f),3);
    Shapes.Box("Freight crane boom",root,new Vector3(side*21,21,47),new Vector3(16,.55f,.8f),3);
    Shapes.Box("Hanging crane cable",root,new Vector3(side*15,15,47),new Vector3(.045f,12,.045f),3);
   }
   Floor(0,8,30,18); // ends z17; the 10m gap is wider than the supported jump or vault.
   if(level==6){Floor(-10,36,12,18);Floor(10,51,12,18);Floor(-9,65,14,12);}
   else Floor(0,49,34,44); // begins z27
   for(int side=-1;side<=1;side+=2)
   {
    Solid("Cargo perimeter",new Vector3(side*17.5f,1.6f,49),new Vector3(.6f,3.2f,44),1);
    Shapes.Box("Phase route light",root,new Vector3(side*14,.06f,9),new Vector3(.08f,.06f,14),4);
    for(int k=0;k<4;k++){float z=31+k*12;Solid("Industrial frame",new Vector3(side*17,3.2f,z),new Vector3(.7f,6.4f,.7f),3);}
   }
   Solid("Rear limit",new Vector3(0,2,-1.3f),new Vector3(31,4,.6f),1);
   Solid("Far limit",new Vector3(0,2,71.4f),new Vector3(35,4,.6f),1);
   Label("PHASE / "+(level+1).ToString("00"),new Vector3(0,5,15),1.0f);
   Label("SWAP GAP",new Vector3(0,1.2f,16.7f),.55f);
   Node(new Vector3(-4,0,4),ExpeditionNodeKind.Extraction,"撤离 / EXTRACT");
   Anchor(new Vector3(level==6?-10:0,.02f,30));
   if(level==2||level==5||level==8||level==11) LaunchPad(new Vector3(3,.02f,14));
   if(level==11){LaunchPad(new Vector3(-7,.02f,43));LaunchPad(new Vector3(7,.02f,59));}
   if(level!=6){Anchor(level==7?new Vector3(12,.02f,62):new Vector3(-12,.02f,64));map.enemySpawns.Add(new Vector3(-9,0,38));map.enemySpawns.Add(new Vector3(9,0,44));map.enemySpawns.Add(new Vector3(0,0,62));}
   switch(level)
   {
    case 0:
     Cover(-5,36);Cover(5,43);Tower(8,50,2.2f);Stairs(8,40,2.2f);
     Node(new Vector3(0,0,53),ExpeditionNodeKind.Objective,"校准授权");
     Label(BindingInput.Label(g.options.actions[6].primary)+" / EXCHANGE",new Vector3(0,3,31),.65f);break;
    case 1:
     Cover(-4,36,2.4f,5);Cover(4,43,2.4f,5);Cover(-6,52);Cover(6,56);
     Node(new Vector3(0,0,63),ExpeditionNodeKind.Objective,"通行授权");
     Anchor(new Vector3(10,.02f,57));break;
    case 2:
     Tower(8,56,3.3f,9,15);Stairs(8,40,3.3f);Ramp(-9,43,2.2f,10,4);Tower(-9,54,2.2f,8,12);
     Solid("Crouch service roof",new Vector3(-9,1.45f,33),new Vector3(6,.3f,6),3);
     Solid("Vault maintenance lip",new Vector3(0,.55f,41),new Vector3(5,1.1f,1.3f),2);
     Label(BindingInput.Label(g.options.actions[17].primary)+" / LOW PASS",new Vector3(-9,2.4f,31),.4f);
     Anchor(new Vector3(8,3.32f,56));Node(new Vector3(8,3.3f,61),ExpeditionNodeKind.Objective,"上层继电");break;
    case 3:
     Cover(0,39,2.4f,5);Cover(-10,48);Tower(9,55,2.2f,8,12);Stairs(9,43,2.2f);
     Node(new Vector3(-11,0,58),ExpeditionNodeKind.Objective,"授权 A");Node(new Vector3(9,2.2f,58),ExpeditionNodeKind.Objective,"授权 B");Anchor(new Vector3(9,2.22f,54));break;
    case 4:
     Tower(-11,47,4.4f);Tower(11,60,3.3f);Stairs(-11,32,4.4f);Stairs(11,46,3.3f);
     for(int k=0;k<4;k++)Cover(k%2==0?-3:4,35+k*8,1.15f,4);
     Anchor(new Vector3(-11,4.42f,47));Anchor(new Vector3(11,3.32f,60));
     Node(new Vector3(-11,4.4f,49),ExpeditionNodeKind.Objective,"哨塔 A");Node(new Vector3(11,3.3f,62),ExpeditionNodeKind.Objective,"哨塔 B");
     map.enemySpawns.Add(new Vector3(-11,4.4f,45));map.enemySpawns.Add(new Vector3(11,3.3f,58));break;
    case 5:
     for(int x=-1;x<=1;x++)for(int z=0;z<3;z++)Cover(x*10,36+z*11,z%2==0?1.2f:2.3f,4);
     Node(new Vector3(-12,0,65),ExpeditionNodeKind.Objective,"危险区授权");Node(new Vector3(12,0,65),ExpeditionNodeKind.Objective,"货运授权");Anchor(new Vector3(12,.02f,54));break;
    case 6:
     Anchor(new Vector3(10,.02f,47));Anchor(new Vector3(-9,.02f,63));
     Node(new Vector3(-10,0,40),ExpeditionNodeKind.Objective,"继电 A");Node(new Vector3(10,0,54),ExpeditionNodeKind.Objective,"继电 B");Node(new Vector3(-9,0,67),ExpeditionNodeKind.Objective,"继电 C");
     map.enemySpawns.Add(new Vector3(-13,0,38));map.enemySpawns.Add(new Vector3(13,0,53));map.enemySpawns.Add(new Vector3(-12,0,65));break;
    case 7:
     for(int k=0;k<4;k++){float x=k%2==0?-7:7;Solid("Warehouse aisle",new Vector3(x,1.7f,35+k*8),new Vector3(17,3.4f,2),1);}
     Solid("Low service passage",new Vector3(10,1.43f,40),new Vector3(5,.3f,5),3);
     Tower(-10,63,2.2f,8,10);Stairs(-10,53,2.2f);Anchor(new Vector3(-10,2.22f,62));
     Node(new Vector3(-10,2.2f,65),ExpeditionNodeKind.Objective,"仓区授权");break;
    case 8:
     Tower(-10,45,2.2f);Tower(0,58,4.4f,8,10);Tower(11,65,6.6f,8,10);
     Stairs(-10,35,2.2f);Stairs(0,43,4.4f);Stairs(11,44,6.6f);
     Anchor(new Vector3(-10,2.22f,44));Anchor(new Vector3(0,4.42f,56));Anchor(new Vector3(11,6.62f,63));
     Node(new Vector3(-10,2.2f,47),ExpeditionNodeKind.Objective,"数据 A");Node(new Vector3(0,4.4f,60),ExpeditionNodeKind.Objective,"数据 B");Node(new Vector3(11,6.6f,67),ExpeditionNodeKind.Objective,"数据 C");
     map.enemySpawns.Add(new Vector3(11,6.6f,62));break;
    case 9:
     for(int k=0;k<3;k++){Cover(-7,36+k*12,1.3f,6);Cover(7,42+k*10,1.3f,6);Node(new Vector3(k==1?12:-12,0,37+k*13),ExpeditionNodeKind.Objective,"封锁段 "+(k+1));}
     Tower(0,65,2.2f,7,8);Stairs(0,55,2.2f);Anchor(new Vector3(0,2.22f,65));break;
    case 10:
     Node(new Vector3(7,0,10),ExpeditionNodeKind.Jammer,"关闭相位干扰");
     Shapes.Ring(root,new Vector3(0,.04f,8),12,new Color(1,.5f,.12f),.12f);
     Cover(-7,35,2.2f,6);Cover(7,46,2.2f,6);Tower(-9,60,3.3f,9,13);Stairs(-9,46,3.3f);
     Anchor(new Vector3(-9,3.32f,58));Node(new Vector3(-9,3.3f,64),ExpeditionNodeKind.Objective,"干扰主机");Node(new Vector3(12,0,64),ExpeditionNodeKind.Objective,"备用电源");break;
    case 11:
     Tower(-11,45,3.3f);Tower(11,57,4.4f);Stairs(-11,32,3.3f);Stairs(11,42,4.4f);
     Cover(0,38,2.3f,6);Cover(-4,57);Cover(4,62);
     Anchor(new Vector3(-11,3.32f,44));Anchor(new Vector3(11,4.42f,56));
     Node(new Vector3(-11,3.3f,47),ExpeditionNodeKind.Objective,"核心 A");Node(new Vector3(11,4.4f,59),ExpeditionNodeKind.Objective,"核心 B");Node(new Vector3(0,0,67),ExpeditionNodeKind.Objective,"相位核心");
     map.enemySpawns.Add(new Vector3(11,4.4f,54));break;
   }
   if(level>=3)Node(level==6?new Vector3(-12,0,69):new Vector3(14,0,68),ExpeditionNodeKind.Bonus,"可选数据 / +40");
   Supply(level==6?new Vector3(-7,0,35):new Vector3(-12,0,32));
   if(level!=9)Supply(level==6?new Vector3(7,0,55):new Vector3(12,0,60));
   ReturnBridges(level);
   var surface=root.gameObject.AddComponent<NavMeshSurface>();map.navigation=surface;surface.collectObjects=CollectObjects.Children;surface.layerMask=Layers.WorldMask;surface.useGeometry=NavMeshCollectGeometry.PhysicsColliders;surface.overrideVoxelSize=true;surface.voxelSize=.08f;surface.BuildNavMesh();
   Physics.SyncTransforms();return map;
   }
   catch
   {
    for(int i=g.anchors.Count-1;i>=0;i--)if(map.anchors.Contains(g.anchors[i])){g.anchors.RemoveAt(i);g.anchorOrigins.RemoveAt(i);}
    if(root){root.gameObject.SetActive(false);UnityEngine.Object.Destroy(root.gameObject);}throw;
   }
  }
 }
}








