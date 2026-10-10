using System;
using System.Collections.Generic;
using UnityEngine;
using Object=UnityEngine.Object;
namespace SwapHunter
{
 public static class FinalEnvironmentModels
 {
  static readonly Dictionary<string,GameObject> cache=new Dictionary<string,GameObject>();
  static readonly HashSet<string> previousImported=new HashSet<string>{"Column_Slim","FloorTile_Basic","Props_ContainerFull","Props_ComputerSmall","Wall_1"};
  public static string ResourcePath(string key)=>(previousImported.Contains(key)?"VisualD11/Environment/":"VisualV2/Environment/")+key;
  public static GameObject Create(string key,Transform parent,Vector3 center,Vector3 size,Quaternion? rotation=null)
  {
   if(!cache.TryGetValue(key,out var asset)||!asset){asset=Resources.Load<GameObject>(ResourcePath(key));if(!asset)throw new InvalidOperationException("缺少新场景模型："+key);cache[key]=asset;}
   var holder=new GameObject("New environment / "+key);holder.transform.SetParent(parent,false);holder.transform.localPosition=center;
   var fit=new GameObject("Mesh fit").transform;fit.SetParent(holder.transform,false);
   var model=Object.Instantiate(asset,fit,false);model.transform.localPosition=Vector3.zero;model.transform.localRotation=rotation??Quaternion.identity;model.transform.localScale=Vector3.one;
   foreach(var collider in model.GetComponentsInChildren<Collider>(true)){collider.enabled=false;Object.Destroy(collider);}
   foreach(var behaviour in model.GetComponentsInChildren<Behaviour>(true)){behaviour.enabled=false;Object.Destroy(behaviour);}
   foreach(var body in model.GetComponentsInChildren<Rigidbody>(true))Object.Destroy(body);
   var renderers=model.GetComponentsInChildren<Renderer>(true);Bounds bounds=BoundsIn(renderers,holder.transform);
   if(bounds.size.x<.00001f||bounds.size.y<.00001f||bounds.size.z<.00001f){Object.Destroy(holder);throw new InvalidOperationException("新模型包围盒无效："+key);}
   Vector3 scale=new Vector3(size.x/bounds.size.x,size.y/bounds.size.y,size.z/bounds.size.z);
   // Outer fit scale is applied after authored rotation; parent scale cancels in BoundsIn.
   fit.localScale=scale;fit.localPosition=-Vector3.Scale(bounds.center,scale);
   ImportedModels.SetLayer(holder.transform,0);Tint(renderers,key);return holder;
  }
  static Bounds BoundsIn(Renderer[] renderers,Transform parent)
  {
   Bounds result=default;bool first=true;
   foreach(var renderer in renderers)
   {
    var filter=renderer.GetComponent<MeshFilter>();Bounds local;
    if(renderer is SkinnedMeshRenderer skin)local=skin.localBounds;
    else if(filter&&filter.sharedMesh)local=filter.sharedMesh.bounds;
    else continue;
    Matrix4x4 matrix=parent.worldToLocalMatrix*renderer.localToWorldMatrix;
    for(int x=-1;x<=1;x+=2)for(int y=-1;y<=1;y+=2)for(int z=-1;z<=1;z+=2)
    {Vector3 point=matrix.MultiplyPoint3x4(local.center+Vector3.Scale(local.extents,new Vector3(x,y,z)));if(first){result=new Bounds(point,Vector3.zero);first=false;}else result.Encapsulate(point);}
   }
   if(first)throw new InvalidOperationException("场景模型没有可见网格");return result;
  }
  static void Tint(Renderer[] renderers,string key)
  {
   Color palette=key.Contains("Container")||key.Contains("Crate")?new Color(.72f,.49f,.23f):key.Contains("Chest")?new Color(.85f,.34f,.26f):key.Contains("Computer")?new Color(.46f,.62f,.72f):key.Contains("Floor")||key.Contains("Roof")?new Color(.31f,.41f,.49f):new Color(.47f,.58f,.67f);
   var block=new MaterialPropertyBlock();
   foreach(var renderer in renderers)
   {
    var materials=renderer.sharedMaterials;
    for(int i=0;i<materials.Length;i++)
    {
     var material=materials[i];if(!material)continue;string property=material.HasProperty("baseColorFactor")?"baseColorFactor":material.HasProperty("_BaseColor")?"_BaseColor":"_Color";if(!material.HasProperty(property))continue;Color authored=material.GetColor(property);
     block.Clear();renderer.GetPropertyBlock(block,i);Color tint=Color.Lerp(Color.white,palette,.45f);Color color=new Color(authored.r*tint.r,authored.g*tint.g,authored.b*tint.b,authored.a);
     block.SetColor(property,color);renderer.SetPropertyBlock(block,i);
    }
   }
  }
  public static void Replace(GameObject proxy,string key,Quaternion? rotation=null,bool tileWall=false)
  {
   var box=proxy.GetComponent<BoxCollider>();var mesh=proxy.GetComponent<MeshFilter>();
   Bounds bounds=box?new Bounds(box.center,box.size):mesh&&mesh.sharedMesh?mesh.sharedMesh.bounds:new Bounds(Vector3.zero,Vector3.one);
   var previous=proxy.GetComponentsInChildren<Renderer>(true);
   Vector3 world=Vector3.Scale(bounds.size,Abs(proxy.transform.lossyScale));int axis=world.x>=world.z?0:2;
   int count=tileWall?Mathf.Max(1,Mathf.CeilToInt(world[axis]/4)):1;
   for(int i=0;i<count;i++)
   {
    Vector3 size=bounds.size,center=bounds.center;size[axis]/=count;center[axis]+=-bounds.size[axis]/2+size[axis]*(i+.5f);
    Create(key,proxy.transform,center,size,rotation);
   }
   foreach(var renderer in previous)renderer.enabled=false;
  }
  static Vector3 Abs(Vector3 value)=>new Vector3(Mathf.Abs(value.x),Mathf.Abs(value.y),Mathf.Abs(value.z));
  public static void Structure(GameObject proxy)
  {
   string name=proxy.name.ToLowerInvariant();Vector3 size=Abs(proxy.transform.lossyScale);
   if(name.Contains("deck")||name.Contains("cover")||name.Contains("cargo container")||name=="walkable stair")return;
   if(name.Contains("wall")||name.Contains("perimeter")||name.Contains("wing")||name.Contains("partition"))
    Replace(proxy,"Wall_1",size.x<size.z?Quaternion.Euler(0,90,0):Quaternion.identity,true);
   else if(name.Contains("barrier")||name=="locked")Replace(proxy,"Door_Single",size.x<size.z?Quaternion.Euler(0,90,0):Quaternion.identity);
   else if(name.Contains("ramp")||name.Contains("tread")||name.Contains("roof")||name.Contains("riser")||name.Contains("lip"))Replace(proxy,"RoofTile_Empty");
   else
   {
    Quaternion rotation=size.x>size.y&&size.x>size.z?Quaternion.Euler(0,0,90):size.z>size.y?Quaternion.Euler(90,0,0):Quaternion.identity;
    Replace(proxy,"Column_Slim",rotation);
   }
  }
  public static void ReplaceNodeBody(Transform root,string key)
  {
   foreach(Transform child in root)if(child.name.StartsWith("New environment /",StringComparison.Ordinal)){child.gameObject.SetActive(false);Object.Destroy(child.gameObject);}
   Create(key,root,new Vector3(0,.7f,0),new Vector3(1.02f,1.4f,.8f));
  }
  public static GameObject Anchor(Transform parent)
  {
   var model=Create("Props_Capsule",parent,new Vector3(0,.675f,0),new Vector3(.62f,1.35f,.62f));
   Create("Props_Base",model.transform,new Vector3(0,-.59f,0),new Vector3(.60f,.16f,.60f));return model;
  }
  public static GameObject Terminal(Transform parent)=>Create("Props_Computer",parent,new Vector3(0,.7f,0),new Vector3(1.02f,1.4f,.8f));
  public static GameObject Supply(Transform parent,Vector3 center)=>Create("Props_Chest",parent,center,new Vector3(.65f,.48f,.55f));
 }
}
