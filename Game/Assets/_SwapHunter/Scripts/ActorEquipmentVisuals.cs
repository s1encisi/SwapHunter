using System.Collections.Generic;
using UnityEngine;

namespace SwapHunter
{
    // New CC0 blade and newly authored shield. No colliders or legacy model reuse.
    public static class ActorEquipmentVisuals
    {
        public const string BladeResourcePath="VisualV2/Weapons/phase_blade";
        public static Transform CreateBlade(Transform parent,string name="New phase blade")
        {
            var asset=Resources.Load<GameObject>(BladeResourcePath);
            if(!asset)throw new System.InvalidOperationException("Required new CC0 phase blade is missing: "+BladeResourcePath);
            var root=Object.Instantiate(asset,parent,false);root.name=name;root.transform.localPosition=Vector3.zero;root.transform.localRotation=Quaternion.identity;
            foreach(var collider in root.GetComponentsInChildren<Collider>(true)){collider.enabled=false;Object.Destroy(collider);}
            foreach(var body in root.GetComponentsInChildren<Rigidbody>(true))Object.Destroy(body);
            foreach(var behavior in root.GetComponentsInChildren<Behaviour>(true)){behavior.enabled=false;Object.Destroy(behavior);}
            if(!ImportedModels.Find(root.transform,"Grip"))throw new System.InvalidOperationException("New phase blade requires its authored Grip marker");
            ImportedModels.SetLayer(root.transform,parent?parent.gameObject.layer:Layers.Actor);return root.transform;
        }
        public static Transform CreateShield(Transform parent)
        {
            var root=new GameObject("New beveled guard shield");root.transform.SetParent(parent,false);
            var owned=root.AddComponent<ActorEquipmentResources>();
            var frame=Material(owned,"Shield frame",new Color(.22f,.29f,.34f),.75f,.42f);
            var panel=Material(owned,"Shield armor",new Color(.06f,.10f,.14f),.6f,.32f);
            var accent=Material(owned,"Shield identifier",new Color(.12f,.67f,.82f),.35f,.5f);
            // Blade extrusion is XY in its generator; rotate once for an upright, +Z-facing shield.
            var plate=Extrude(root.transform,owned,"Octagonal shield shell",new[]{new Vector2(-.36f,-.71f),new Vector2(.36f,-.71f),new Vector2(.5f,-.55f),new Vector2(.5f,.54f),new Vector2(.34f,.71f),new Vector2(-.34f,.71f),new Vector2(-.5f,.54f),new Vector2(-.5f,-.55f)},.075f,frame);
            plate.localRotation=Quaternion.Euler(90,0,0);
            var inset=Extrude(root.transform,owned,"Recessed armor panel",new[]{new Vector2(-.29f,-.61f),new Vector2(.29f,-.61f),new Vector2(.40f,-.48f),new Vector2(.40f,.47f),new Vector2(.28f,.60f),new Vector2(-.28f,.60f),new Vector2(-.40f,.47f),new Vector2(-.40f,-.48f)},.018f,panel);
            inset.localRotation=Quaternion.Euler(90,0,0);inset.localPosition=Vector3.forward*.075f;
            for(int side=-1;side<=1;side+=2)PartBox(root.transform,owned,"Edge identifier",new Vector3(side*.43f,.02f,.081f),new Vector3(.027f,.8f,.012f),accent);
            for(int i=0;i<3;i++)PartBox(root.transform,owned,"Top identifier",new Vector3(-.10f+i*.10f,.49f,.097f),new Vector3(.064f,.026f,.012f),accent);
            PartBox(root.transform,owned,"Rear wrist grip",new Vector3(0,.18f,-.11f),new Vector3(.22f,.042f,.04f),frame);
            Marker(root.transform,"Grip",new Vector3(0,.18f,-.13f));
            ImportedModels.SetLayer(root.transform,parent?parent.gameObject.layer:Layers.Actor);return root.transform;
        }
        static Material Material(ActorEquipmentResources owned,string name,Color color,float metallic,float smoothness)
        {
            var shader=Shader.Find("Universal Render Pipeline/Lit");if(!shader)throw new System.InvalidOperationException("New equipment requires the project URP Lit shader");
            var material=new Material(shader){name=name};material.SetColor("_BaseColor",color);material.SetFloat("_Metallic",metallic);material.SetFloat("_Smoothness",smoothness);owned.Add(material);return material;
        }
        static void Marker(Transform parent,string name,Vector3 point){var go=new GameObject(name);go.transform.SetParent(parent,false);go.transform.localPosition=point;}
        static Transform PartBox(Transform parent,ActorEquipmentResources owned,string name,Vector3 center,Vector3 size,Material material)
        {
            var part=Extrude(parent,owned,name,new[]{new Vector2(-size.x/2,-size.z/2),new Vector2(size.x/2,-size.z/2),new Vector2(size.x/2,size.z/2),new Vector2(-size.x/2,size.z/2)},size.y/2,material);part.localPosition=center;return part;
        }
        static Transform Extrude(Transform parent,ActorEquipmentResources owned,string name,Vector2[] outline,float halfDepth,Material material)
        {
            var vertices=new List<Vector3>();var triangles=new List<int>();
            void Face(Vector3[] points)
            {int first=vertices.Count;vertices.AddRange(points);for(int i=1;i<points.Length-1;i++){triangles.Add(first);triangles.Add(first+i);triangles.Add(first+i+1);}}
            var front=new Vector3[outline.Length];var back=new Vector3[outline.Length];
            for(int i=0;i<outline.Length;i++){front[i]=new Vector3(outline[i].x,halfDepth,outline[i].y);back[outline.Length-1-i]=new Vector3(outline[i].x,-halfDepth,outline[i].y);}
            System.Array.Reverse(front);System.Array.Reverse(back);Face(front);Face(back);
            for(int i=0;i<outline.Length;i++){int j=(i+1)%outline.Length;Face(new[]{new Vector3(outline[i].x,-halfDepth,outline[i].y),new Vector3(outline[i].x,halfDepth,outline[i].y),new Vector3(outline[j].x,halfDepth,outline[j].y),new Vector3(outline[j].x,-halfDepth,outline[j].y)});}
            var mesh=new Mesh{name=name};mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();owned.Add(mesh);
            var go=new GameObject(name);go.transform.SetParent(parent,false);go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=material;return go.transform;
        }
    }
    public sealed class ActorEquipmentResources:MonoBehaviour
    {
        readonly List<Object> owned=new List<Object>();
        public void Add(Object item){owned.Add(item);}
        void OnDestroy(){foreach(var item in owned)if(item)Destroy(item);owned.Clear();}
    }
}