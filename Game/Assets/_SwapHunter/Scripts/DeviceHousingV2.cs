using System.Collections.Generic;using UnityEngine;
namespace SwapHunter
{
 public static class DeviceHousingV2
 {
  public static Renderer Create(Transform parent,EnemyActor carrier,out Vector3 size)
  {
   int kind=carrier?(int)carrier.kind:-1;
   size=kind==6?new Vector3(.13f,.26f,.13f):kind==10?new Vector3(.17f,.15f,.11f):kind==12?new Vector3(.14f,.18f,.12f):kind==5?new Vector3(.23f,.21f,.14f):kind==9?new Vector3(.20f,.28f,.14f):kind==13?new Vector3(.24f,.26f,.18f):new Vector3(.24f,.29f,.17f);
   var owned=parent.gameObject.AddComponent<ActorEquipmentResources>();
   Material Mat(string n,Color c,float metal){var m=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=n};m.SetColor("_BaseColor",c);m.SetFloat("_Metallic",metal);m.SetFloat("_Smoothness",.32f);owned.Add(m);return m;}
   var casing=Mat("Device gunmetal casing",new Color(.13f,.17f,.19f),.62f);var edge=Mat("Device machined rails",new Color(.32f,.37f,.39f),.78f);var dark=Mat("Recessed cooling channels",new Color(.025f,.035f,.04f),.1f);var lamp=Mat("Small amber status indicator",new Color(.85f,.34f,.055f),.15f);
   Transform Part(string name,Vector3 p,Vector3 dimensions,Material mat,bool bevel=true)
   {
    var go=new GameObject(name);go.layer=Layers.Actor;go.transform.SetParent(parent,false);go.transform.localPosition=p;
    var mesh=Shell(dimensions,bevel?Mathf.Min(dimensions.x,dimensions.y,dimensions.z)*.18f:0);owned.Add(mesh);go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=mat;return go.transform;
   }
   Part("Physical mounting rail",Vector3.back*size.z*.47f,new Vector3(size.x*.72f,size.y*1.05f,size.z*.20f),edge);
   var body=Part("Beveled protective housing",Vector3.zero,size,casing);
   Part("Inset service panel",Vector3.forward*size.z*.51f,new Vector3(size.x*.78f,size.y*.72f,.012f),edge);
   for(int row=0;row<3;row++)Part("Recessed ventilation slot",new Vector3(0,(row-1)*size.y*.13f,size.z*.55f),new Vector3(size.x*.55f,.014f,.006f),dark,false);
   Part("Status diode",new Vector3(size.x*.18f,size.y*.34f,size.z*.56f),new Vector3(size.x*.22f,.012f,.007f),lamp);
   foreach(int side in new[]{-1,1})foreach(int top in new[]{-1,1})
   {
    var bolt=GameObject.CreatePrimitive(PrimitiveType.Cylinder);bolt.name="Captive casing fastener";bolt.transform.SetParent(parent,false);bolt.transform.localPosition=new Vector3(side*size.x*.38f,top*size.y*.38f,size.z*.54f);bolt.transform.localRotation=Quaternion.Euler(90,0,0);bolt.transform.localScale=new Vector3(.015f,.005f,.015f);bolt.GetComponent<Collider>().enabled=false;Object.Destroy(bolt.GetComponent<Collider>());bolt.GetComponent<Renderer>().sharedMaterial=dark;bolt.layer=Layers.Actor;
   }
   return body.GetComponent<Renderer>();
  }
  static Mesh Shell(Vector3 s,float bevel)
  {
   float x=s.x*.5f,y=s.y*.5f,z=s.z*.5f,b=Mathf.Max(.0001f,bevel);var cross=new[]{new Vector2(-x+b,-y),new Vector2(x-b,-y),new Vector2(x,-y+b),new Vector2(x,y-b),new Vector2(x-b,y),new Vector2(-x+b,y),new Vector2(-x,y-b),new Vector2(-x,-y+b)};
   var v=new List<Vector3>();var t=new List<int>();void Face(Vector3[] p){int start=v.Count;v.AddRange(p);for(int i=1;i<p.Length-1;i++){t.Add(start);t.Add(start+i);t.Add(start+i+1);}}
   var front=new Vector3[8];var back=new Vector3[8];for(int i=0;i<8;i++){front[i]=new Vector3(cross[i].x,cross[i].y,z);back[7-i]=new Vector3(cross[i].x,cross[i].y,-z);}Face(front);Face(back);
   for(int i=0;i<8;i++){int n=(i+1)%8;Face(new[]{new Vector3(cross[i].x,cross[i].y,-z),new Vector3(cross[n].x,cross[n].y,-z),new Vector3(cross[n].x,cross[n].y,z),new Vector3(cross[i].x,cross[i].y,z)});}
   var mesh=new Mesh{name="Chamfered service casing"};mesh.SetVertices(v);mesh.SetTriangles(t,0);mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
  }
 }
}
