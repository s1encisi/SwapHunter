using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
namespace SwapHunter
{
 [Serializable] public sealed class FocusCameraPoint { public string name;public Vector3 feet,lookAt;public FocusCameraPoint(string n,Vector3 p,Vector3 t){name=n;feet=p;lookAt=t;} }
 public sealed class FocusedArenaDefinition:MonoBehaviour
 {
  public int chapter;public Bounds combatCore;public Vector3 observation,preparation;public FocusCameraPoint[] cameras;
 }
 public sealed class FocusEnvironmentState:MonoBehaviour
 {
  Material oldSky,sky;FogMode oldFogMode;AmbientMode oldAmbientMode;Color oldSkyColor,oldEquator,oldGround,oldFog;bool oldFogEnabled;float oldFogStart,oldFogEnd,oldShadowDistance;bool applied,boatPlaced;
  public void Apply(int level)
  {
   oldSky=RenderSettings.skybox;oldAmbientMode=RenderSettings.ambientMode;oldSkyColor=RenderSettings.ambientSkyColor;oldEquator=RenderSettings.ambientEquatorColor;oldGround=RenderSettings.ambientGroundColor;oldFog=RenderSettings.fogColor;oldFogMode=RenderSettings.fogMode;oldFogEnabled=RenderSettings.fog;oldFogStart=RenderSettings.fogStartDistance;oldFogEnd=RenderSettings.fogEndDistance;oldShadowDistance=QualitySettings.shadowDistance;
   var shader=Resources.Load<Shader>("FocusEnvironment/FocusedSky");var texture=Resources.Load<Texture2D>("FocusEnvironment/coastal_sky");if(!shader||!texture)throw new InvalidOperationException("Focused CC0 sky is missing");
   sky=new Material(shader);sky.name="Focused coastal HDR sky";sky.SetTexture("_Sky",texture);sky.SetFloat("_Exposure",level==0?.72f:.82f);sky.SetFloat("_Rotation",115);RenderSettings.skybox=sky;
   RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=new Color(.43f,.50f,.56f);RenderSettings.ambientEquatorColor=new Color(.30f,.35f,.36f);RenderSettings.ambientGroundColor=new Color(.17f,.18f,.17f);RenderSettings.fog=true;RenderSettings.fogColor=new Color(.48f,.55f,.60f);RenderSettings.fogMode=FogMode.Linear;RenderSettings.fogStartDistance=160;RenderSettings.fogEndDistance=380;QualitySettings.shadowDistance=100;
   var sun=transform.Find("Chapter sun");if(sun){var light=sun.GetComponent<Light>();light.color=level==0?new Color(1,.89f,.73f):new Color(1,.95f,.85f);light.intensity=1.18f;light.shadows=LightShadows.Soft;light.shadowBias=.035f;light.shadowNormalBias=.15f;sun.rotation=Quaternion.Euler(33,-42,0);}
   foreach(Transform child in transform)if(child.name.StartsWith("New environment /")&&Mathf.Abs(child.localPosition.x)>300)child.gameObject.SetActive(false);
   applied=true;
  }
  void Start(){var info=GetComponent<FocusedArenaDefinition>();if(info)foreach(var text in GetComponentsInChildren<TextMesh>())if(text.gameObject.name=="Wayfinding"&&text.text.StartsWith(info.chapter.ToString("00")+" / "))text.gameObject.SetActive(false);DynamicGI.UpdateEnvironment();}
  void LateUpdate()
  {
   if(boatPlaced)return;var story=transform.Find("N1 Rescue Environment");if(!story)return;var boat=story.Find("Rescue workboat");if(!boat)return;
   // Move only this chapter's decorative story parent; StoryScenery retains its normal sailing state.
   story.position+=new Vector3(78,-6.3f,24)-boat.position;boatPlaced=true;
  }
  void OnDisable()
  {
   if(!applied)return;applied=false;
   if(RenderSettings.skybox==sky){RenderSettings.skybox=oldSky;RenderSettings.ambientMode=oldAmbientMode;RenderSettings.ambientSkyColor=oldSkyColor;RenderSettings.ambientEquatorColor=oldEquator;RenderSettings.ambientGroundColor=oldGround;RenderSettings.fog=oldFogEnabled;RenderSettings.fogMode=oldFogMode;RenderSettings.fogColor=oldFog;RenderSettings.fogStartDistance=oldFogStart;RenderSettings.fogEndDistance=oldFogEnd;QualitySettings.shadowDistance=oldShadowDistance;}
   if(sky)Destroy(sky);
  }
 }
 public static class FocusEnvironment
 {
  static readonly Dictionary<string,Material> materials=new Dictionary<string,Material>();
  public static Material Surface(string type,Color tint,float metres=2,float metal=0)
  {
   string key=type+"/"+tint.ToString()+"/"+metres+"/"+metal;if(materials.TryGetValue(key,out var value)&&value)return value;
   var shader=Resources.Load<Shader>("FocusEnvironment/FocusedSurface");if(!shader)throw new InvalidOperationException("Focused surface shader is missing");value=new Material(shader){name="Focused / "+type,enableInstancing=true};value.SetTexture("_BaseMap",Resources.Load<Texture2D>("FocusEnvironment/"+type+"_color"));value.SetTexture("_NormalMap",Resources.Load<Texture2D>("FocusEnvironment/"+type+"_normalgl"));value.SetTexture("_RoughnessMap",Resources.Load<Texture2D>("FocusEnvironment/"+type+"_roughness"));value.SetColor("_BaseColor",tint);value.SetFloat("_TileSize",metres);value.SetFloat("_NormalStrength",type=="metal"?.5f:.4f);value.SetFloat("_Metallic",metal);materials[key]=value;return value;
  }
  public static Material Concrete=>Surface("concrete",new Color(.66f,.66f,.60f),2.4f);
  public static Material Road=>Surface("asphalt",new Color(1,.98f,.92f),3.5f);
  public static Material Steel=>Surface("metal",new Color(.54f,.59f,.58f),1.1f,.65f);
  public static Material PaintedSteel=>Surface("metal",new Color(.29f,.40f,.43f),1.1f,.3f);
  public static Material HazardSteel=>Surface("metal",new Color(.88f,.58f,.16f),1.1f,.25f);
  public static GameObject Box(Transform parent,string name,Vector3 center,Vector3 size,Material material,bool solid=true)
  {var go=Shapes.Box(name,parent,center,size,0,solid,solid?Layers.World:0);go.GetComponent<Renderer>().sharedMaterial=material;return go;}
  public static GameObject Cylinder(Transform parent,string name,Vector3 center,float radius,float height,Material material,bool solid=true)
  {var go=Shapes.Make(name,PrimitiveType.Cylinder,parent,center,new Vector3(radius*2,height/2,radius*2),0,solid,solid?Layers.World:0);go.GetComponent<Renderer>().sharedMaterial=material;return go;}
  public static void Pipe(Transform parent,Vector3 from,Vector3 to,float radius,Material material,bool solid=true)
  {var pipe=Cylinder(parent,"Connected service pipe",(from+to)/2,radius,Vector3.Distance(from,to),material,solid);pipe.transform.rotation=Quaternion.FromToRotation(Vector3.up,to-from);}
  public static void Lamp(Transform parent,Vector3 feet,float height=4.5f)
  {
   Box(parent,"Lamp foundation",feet+Vector3.up*.15f,new Vector3(.6f,.3f,.6f),Concrete);Pipe(parent,feet+Vector3.up*.3f,feet+Vector3.up*height,.09f,Steel);
   Box(parent,"Weatherproof lamp housing",feet+Vector3.up*height,new Vector3(.65f,.18f,.45f),PaintedSteel,false);var go=new GameObject("Warm work light");go.transform.SetParent(parent,false);go.transform.localPosition=feet+Vector3.up*(height-.25f);var l=go.AddComponent<Light>();l.type=LightType.Point;l.range=12;l.intensity=1.9f;l.color=new Color(1,.85f,.62f);l.shadows=LightShadows.None;
  }
  public static void Machine(Transform root,string name,Vector3 feet,Vector3 size,string role)
  {
   var body=Box(root,name,feet+Vector3.up*size.y/2,size,PaintedSteel);body.AddComponent<CoverSite>();var feature=body.AddComponent<MapV2WorldFeature>();feature.Describe(name,"固定工业机组："+name,role,"实体挡弹；从侧面与背面维护通道绕行","承台、管线与相邻控制面板");
   Box(root,"Machine concrete plinth",feet+Vector3.up*.12f,new Vector3(size.x+.3f,.24f,size.z+.3f),Concrete);
   for(int i=0;i<6;i++)Box(root,"Cooling fins",feet+new Vector3(-size.x*.4f+i*size.x*.16f,size.y*.55f,-size.z*.5f-.04f),new Vector3(.075f,size.y*.7f,.12f),Steel,false);
   Box(root,"Top service cover",feet+Vector3.up*(size.y+.08f),new Vector3(size.x+.12f,.16f,size.z+.12f),Steel);
   Box(root,"Marked service edge",feet+new Vector3(0,.03f,-size.z*.5f-.45f),new Vector3(size.x+.6f,.03f,.15f),HazardSteel,false);
  }
  static void DistantFactory(Transform root,Vector3 p,Vector3 size)
  {
   Box(root,"Port plant building",p+Vector3.up*size.y/2,size,Concrete,false);Box(root,"Industrial pitched roof ridge",p+Vector3.up*(size.y+.4f),new Vector3(size.x+.8f,.8f,size.z+.8f),PaintedSteel,false);
   for(int i=0;i<4;i++)Box(root,"Factory window strip",p+new Vector3(-size.x*.3f+i*size.x*.2f,size.y*.57f,-size.z*.5f-.03f),new Vector3(size.x*.13f,2,.08f),PaintedSteel,false);
   Cylinder(root,"Connected exhaust stack",p+new Vector3(size.x*.28f,size.y+5,size.z*.15f),1.4f,16,Steel,false);
  }
  static void Terrain(Transform root)
  {
   const int nx=33,nz=33;var v=new Vector3[nx*nz];var uv=new Vector2[v.Length];var tris=new List<int>();
   for(int z=0;z<nz;z++)for(int x=0;x<nx;x++)
   {float wx=-350+x*700f/(nx-1),wz=-180+z*580f/(nz-1);float ridge=Mathf.Max(Mathf.SmoothStep(0,1,Mathf.InverseLerp(130,260,wz)),Mathf.SmoothStep(0,1,Mathf.InverseLerp(150,300,-wx)));float h=-1.6f+ridge*(28+24*Mathf.PerlinNoise((wx+1000)*.013f,(wz+1000)*.011f));if(wx>48)h=Mathf.Min(h,-1.6f-(wx-48)*.25f);v[z*nx+x]=new Vector3(wx,Mathf.Max(-16,h),wz);uv[z*nx+x]=new Vector2(wx/4,wz/4);if(x<nx-1&&z<nz-1){int a=z*nx+x;tris.AddRange(new[]{a,a+nx,a+1,a+1,a+nx,a+nx+1});}}
   var mesh=new Mesh{name="Continuous coastal industrial landform"};mesh.vertices=v;mesh.uv=uv;mesh.triangles=tris.ToArray();mesh.RecalculateNormals();var go=new GameObject("Continuous land and distant hills");go.transform.SetParent(root,false);go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=Surface("concrete",new Color(.32f,.34f,.26f),6);go.AddComponent<MapV2OwnedMesh>().mesh=mesh;
  }
  public static void CoastalContext(Transform root,int level)
  {
   root.gameObject.AddComponent<FocusEnvironmentState>().Apply(level);Terrain(root);
   Box(root,"Port approach road",new Vector3(-53,-1.55f,20),new Vector3(12,.15f,280),Road,false);
   var water=new Material(Shapes.Mat(0));water.name="Focused harbor water";water.SetColor("_BaseColor",new Color(.055f,.21f,.27f));water.SetFloat("_Metallic",.15f);water.SetFloat("_Smoothness",.86f);Box(root,"Harbor water",new Vector3(190,-7,65),new Vector3(330,.2f,430),water,false);
   for(int i=0;i<7;i++)Box(root,"Road worn lane marking",new Vector3(-53,-1.45f,-90+i*34),new Vector3(.14f,.015f,15),HazardSteel,false);
   DistantFactory(root,new Vector3(-85,-1.6f,20),new Vector3(36,14,60));DistantFactory(root,new Vector3(-95,-1.6f,108),new Vector3(55,24,38));DistantFactory(root,new Vector3(-18,-1.6f,140),new Vector3(52,20,40));DistantFactory(root,new Vector3(88,-1.6f,156),new Vector3(50,17,44));
   for(int i=0;i<3;i++){var p=new Vector3(72+i*28,-.6f,52+i*25);Box(root,"Harbor pier",p,new Vector3(44,1.2f,14),Concrete,false);for(int s=-1;s<=1;s+=2){Box(root,"Dock crane legs",p+new Vector3(s*8,12,0),new Vector3(1.2f,24,1.2f),HazardSteel,false);}Box(root,"Dock crane crossbeam",p+new Vector3(0,24,0),new Vector3(35,1.2f,2.4f),HazardSteel,false);Pipe(root,p+new Vector3(12,24,0),p+new Vector3(12,5,0),.05f,Steel,false);}
   for(int i=0;i<5;i++){var p=new Vector3(-125+i*17,-1.6f,185);Cylinder(root,"Port reserve storage tank",p+Vector3.up*10,5.5f,20,PaintedSteel,false);}
  }
 }
}
