using System;
using System.Collections.Generic;
using UnityEngine;
namespace SwapHunter
{
    // Authored equipment silhouettes attached to the normal-proportion tactical base body.
    public sealed class EnemyV2VisualEquipment:MonoBehaviour
    {
        readonly Dictionary<string,Transform> points=new Dictionary<string,Transform>();
        readonly List<Transform> articulated=new List<Transform>();
        readonly List<Quaternion> rest=new List<Quaternion>();
        ActorEquipmentResources owned; Material metal,armor,signal,rubber;int type,chapter;
        public bool MechanicalBody=>type==0||type==10||type==13||chapter==0||chapter==2||chapter==7||chapter==8||chapter==9;
        public Transform Weakpoint(string name)=>points.TryGetValue(name??"equipment",out var value)?value:points.TryGetValue("equipment",out value)?value:transform;
        public void Build(int archetype,int bossChapter)
        {
            type=archetype;chapter=bossChapter;owned=gameObject.AddComponent<ActorEquipmentResources>();
            metal=Mat("Brushed equipment alloy",new Color(.28f,.33f,.36f),.75f,.35f);armor=Mat("Harbor security armor",new Color(.18f,.22f,.24f),.35f,.42f);signal=Mat("Visible equipment status",new Color(.93f,.36f,.12f),.25f,.32f);rubber=Mat("Equipment joint rubber",new Color(.035f,.045f,.05f),0,.78f);
            if(bossChapter>=0){BuildBoss(bossChapter);return;}
            switch(type)
            {
                case 0: MachineCore(.19f,1.16f);Box("Training identification plate",new Vector3(0,1.24f,.18f),new Vector3(.25f,.12f,.035f),signal);Legs(false);Arm("Calibration sensor",.28f,1.35f);break;
                case 1:Box("Security identity collar",new Vector3(0,1.38f,.16f),new Vector3(.21f,.07f,.04f),signal);break;
                case 2:Arm("Optical rangefinder",-.25f,1.68f);Weak("equipment",new Vector3(-.25f,1.68f,.08f));break;
                case 3:Box("Shield shoulder support",new Vector3(-.28f,1.44f,0),new Vector3(.18f,.14f,.27f),armor);Weak("equipment",new Vector3(.18f,1.3f,-.22f));break;
                case 4:Pack("Command radio",new Vector3(0,1.32f,-.21f),new Vector3(.31f,.25f,.13f));Antenna(new Vector3(-.18f,1.44f,-.20f),.42f);Weak("equipment",new Vector3(0,1.34f,-.31f));break;
                case 5:Pack("Waist barrier generator",new Vector3(0,1.02f,-.18f),new Vector3(.26f,.18f,.15f));Arm("Barrier deployment arm",-.28f,1.05f);Weak("equipment",new Vector3(0,1.02f,-.28f));break;
                case 6:for(int s=-1;s<=1;s+=2){Cylinder("Back thrust nozzle",new Vector3(s*.13f,1.29f,-.21f),new Vector3(s*.13f,1.12f,-.27f),.062f,metal);Box("Mechanical calf spring",new Vector3(s*.17f,.34f,-.02f),new Vector3(.04f,.33f,.07f),metal);}Weak("equipment",new Vector3(.17f,.43f,-.02f));break;
                case 7:Pack("Pressure reservoir",new Vector3(0,1.27f,-.24f),new Vector3(.28f,.31f,.19f));Arm("Impact actuator",.35f,1.20f,true);Weak("equipment",new Vector3(0,1.27f,-.35f));break;
                case 8:Cylinder("Shoulder mortar",new Vector3(-.27f,1.44f,-.14f),new Vector3(-.27f,1.55f,.19f),.082f,metal);for(int n=0;n<3;n++)Cylinder("Mortar cartridge",new Vector3(.22f,1.08f+n*.10f,-.12f),new Vector3(.22f,1.08f+n*.10f,-.27f),.035f,armor);Weak("equipment",new Vector3(.24f,1.2f,-.22f));break;
                case 9:Coil("Repair cable reel",new Vector3(0,1.28f,-.23f),.13f);Arm("Repair tool",.31f,1.1f);Box("Folded maintenance frame",new Vector3(-.19f,1.2f,-.24f),new Vector3(.05f,.43f,.08f),metal);Weak("equipment",new Vector3(0,1.28f,-.31f));break;
                case 10:MachineCore(.20f,1.12f);for(int s=-1;s<=1;s+=2){Box("Drone wing",new Vector3(s*.32f,1.17f,0),new Vector3(.42f,.05f,.14f),metal);Coil("Protected rotor",new Vector3(s*.46f,1.19f,0),.15f,true);}Lens(new Vector3(0,1.13f,.21f),.08f);Weak("equipment",new Vector3(0,1.13f,.24f));break;
                case 11:Pack("Phase beacon pack",new Vector3(0,1.31f,-.20f),new Vector3(.25f,.28f,.13f));for(int s=-1;s<=1;s++)Antenna(new Vector3(s*.14f,1.42f,-.19f),s==0?.44f:.34f);Weak("equipment",new Vector3(0,1.5f,-.20f));break;
                case 12:for(int s=-1;s<=1;s+=2){Arm("Forearm tether launcher",s*.30f,1.17f);Coil("Tether reel",new Vector3(s*.16f,1.25f,-.21f),.10f);}Weak("equipment",new Vector3(.30f,1.17f,.13f));break;
                case 13:MachineCore(.22f,1.05f);Legs(true);Cylinder("Suppression barrel sleeve",new Vector3(0,1.20f,.20f),new Vector3(0,1.20f,.63f),.085f,metal);Pack("Suppression ammo box",new Vector3(.30f,1.04f,0),new Vector3(.22f,.25f,.22f));Weak("equipment",new Vector3(.41f,1.04f,0));break;
            }
            if(!points.ContainsKey("equipment"))Weak("equipment",new Vector3(0,1.24f,-.20f));
        }
        void BuildBoss(int n)
        {
            switch(n)
            {
                case 0:MachineCore(.16f,1.20f);Legs(false);Arm("Single calibration scan arm",.39f,1.38f);Lens(new Vector3(.39f,1.38f,.24f),.085f);break;
                case 1:Box("Wide enforcement backplate",new Vector3(0,1.27f,-.17f),new Vector3(.48f,.53f,.13f),armor);Arm("Mechanical brake clamp",.40f,1.05f,true);Box("Enforcement shield crest",new Vector3(-.35f,1.45f,.20f),new Vector3(.25f,.22f,.12f),metal);break;
                case 2:MachineCore(.24f,1.22f);Legs(false);for(int s=-1;s<=1;s+=2)Arm("Track grabbing arm",s*.43f,1.26f,true);Lens(new Vector3(0,1.52f,.18f),.10f);break;
                case 3:for(int s=-1;s<=1;s+=2){Pack("Station twin barrier generator",new Vector3(s*.23f,1.37f,-.24f),new Vector3(.19f,.42f,.19f));Arm("Generator stabilizer",s*.37f,1.2f);}break;
                case 4:Antenna(new Vector3(-.24f,1.52f,-.18f),.57f);Lens(new Vector3(-.24f,1.87f,-.11f),.062f);Pack("Artillery waist fire control",new Vector3(.25f,1.02f,.04f),new Vector3(.18f,.22f,.19f));break;
                case 5:for(int s=-1;s<=1;s+=2)Cylinder("Insulated pressure tank",new Vector3(s*.17f,1.03f,-.27f),new Vector3(s*.17f,1.54f,-.27f),.145f,armor);Arm("Pressure discharge arm",.40f,1.2f,true);break;
                case 6:for(int s=-1;s<=1;s+=2){Coil("Exposed towing wheel",new Vector3(s*.38f,1.35f,.03f),.16f);Arm("Winch arm",s*.36f,1.2f);Box("Winch counterweight",new Vector3(s*.27f,.94f,-.20f),new Vector3(.18f,.25f,.20f),metal);}break;
                case 7:MachineCore(.30f,.89f);Wheels();Arm("Rotating sorting arm",.42f,1.12f,true);for(int k=0;k<3;k++)Box("Sorting tool socket",new Vector3(-.29f,1.0f+k*.12f,.13f),new Vector3(.14f,.085f,.14f),k==1?signal:metal);break;
                case 8:MachineCore(.19f,1.18f);for(int s=-1;s<=1;s+=2){var wing=Box("Foldable hunter wing",new Vector3(s*.42f,1.27f,0),new Vector3(.60f,.07f,.30f),metal);Move(wing);Cylinder("Hunter landing strut",new Vector3(s*.18f,1.02f,0),new Vector3(s*.30f,.20f,.12f),.045f,metal);}Lens(new Vector3(0,1.32f,.20f),.075f);break;
                case 9:MachineCore(.29f,1.01f);Wheels();for(int s=-1;s<=1;s+=2){Cylinder("Dispatch light mast",new Vector3(s*.23f,1.13f,0),new Vector3(s*.23f,1.72f,0),.025f,metal);Box("Traffic dispatch lamp",new Vector3(s*.23f,1.73f,.03f),new Vector3(.13f,.20f,.10f),signal);}Arm("Cannon support",.42f,1.2f,true);break;
                case 10:Coil("Ring phase backframe",new Vector3(0,1.42f,-.23f),.36f);for(int s=-1;s<=1;s+=2)Pack("Detached phase node",new Vector3(s*.32f,1.45f,-.25f),new Vector3(.16f,.24f,.16f));break;
                case 11:Box("Gate lock breastplate",new Vector3(0,1.25f,.19f),new Vector3(.35f,.35f,.12f),metal);Pack("Single shield projector",new Vector3(-.39f,1.33f,.03f),new Vector3(.25f,.40f,.25f));for(int s=-1;s<=1;s+=2)Box("Foot positioning fixture",new Vector3(s*.16f,.09f,.06f),new Vector3(.20f,.12f,.34f),metal);break;
            }
            Weak("equipment",n==0?new Vector3(.39f,1.38f,.22f):n==4?new Vector3(.25f,1.02f,.13f):new Vector3(0,1.28f,-.31f));
        }
        public void Pose(string key,float normalized,bool active)
        {
            float p=active?Mathf.Clamp01(normalized):0;for(int i=0;i<articulated.Count;i++)if(articulated[i])articulated[i].localRotation=rest[i]*Quaternion.Euler(-Mathf.Sin(p*Mathf.PI)*35,0,(i%2==0?1:-1)*Mathf.Sin(p*Mathf.PI)*12);
        }
        void MachineCore(float half,float y){Box("Machine armored core",new Vector3(0,y,0),new Vector3(half*2,.35f,.30f),armor);Cylinder("Exposed vertical coupling",new Vector3(0,y-.25f,0),new Vector3(0,y+.28f,0),.075f,metal);Lens(new Vector3(0,y+.24f,.05f),.09f);}
        void Legs(bool tripod){int count=tripod?3:2;for(int n=0;n<count;n++){float angle=tripod?n*120+30:n==0?-60:60;Vector3 foot=Quaternion.Euler(0,angle,0)*new Vector3(0,0,.38f);Vector3 knee=foot*.6f+Vector3.up*.50f;Cylinder("Articulated machine leg",new Vector3(0,.88f,0),knee,.045f,metal);Cylinder("Leg piston",knee,foot+Vector3.up*.07f,.04f,armor);Box("Machine foot",foot+Vector3.up*.055f,new Vector3(.18f,.11f,.25f),rubber);}}
        void Wheels(){for(int s=-1;s<=1;s+=2)for(int f=-1;f<=1;f+=2)Cylinder("Transport wheel",new Vector3(s*.34f,.27f,f*.25f),new Vector3(s*.46f,.27f,f*.25f),.20f,rubber);Box("Low transport chassis",new Vector3(0,.57f,0),new Vector3(.77f,.28f,.68f),armor);}
        void Lens(Vector3 p,float r){Cylinder("Visible sensor lens",p-Vector3.forward*.04f,p+Vector3.forward*.03f,r,metal);Cylinder("Lens glass",p+Vector3.forward*.03f,p+Vector3.forward*.037f,r*.72f,signal);}
        void Pack(string name,Vector3 p,Vector3 size){Box(name,p,size,armor);Box(name+" status",p-Vector3.forward*size.z*.52f,new Vector3(size.x*.6f,.035f,.01f),signal);}
        void Antenna(Vector3 p,float length){Cylinder("Antenna shaft",p,p+Vector3.up*length,.009f,metal);Box("Antenna receiver",p+Vector3.up*(length-.03f),new Vector3(.05f,.06f,.035f),rubber);}
        void Arm(string name,float x,float y,bool heavy=false){var pivot=new GameObject(name+" pivot").transform;pivot.SetParent(transform,false);pivot.localPosition=new Vector3(x,y,.03f);var fore=Box(name,new Vector3(x,y,.13f),new Vector3(heavy?.19f:.10f,heavy?.21f:.12f,.32f),metal);fore.SetParent(pivot,true);Move(pivot);}
        void Coil(string name,Vector3 p,float radius,bool horizontal=false){for(int i=0;i<12;i++){float a=i*Mathf.PI/6,b=(i+1)*Mathf.PI/6;Vector3 p1=new Vector3(Mathf.Cos(a),Mathf.Sin(a),0)*radius,p2=new Vector3(Mathf.Cos(b),Mathf.Sin(b),0)*radius;if(horizontal){p1=new Vector3(p1.x,0,p1.y);p2=new Vector3(p2.x,0,p2.y);}Cylinder(name,p+p1,p+p2,.018f,metal);}}
        void Move(Transform t){articulated.Add(t);rest.Add(t.localRotation);}
        void Weak(string n,Vector3 p){var t=new GameObject("Weakpoint / "+n).transform;t.SetParent(transform,false);t.localPosition=p;points[n]=t;}
        Material Mat(string n,Color c,float m,float smooth){var s=Shader.Find("Universal Render Pipeline/Lit");var result=new Material(s){name=n};result.SetColor("_BaseColor",c);result.SetFloat("_Metallic",m);result.SetFloat("_Smoothness",smooth);owned.Add(result);return result;}
        Transform Box(string name,Vector3 p,Vector3 size,Material material){var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(transform,false);go.transform.localPosition=p;go.transform.localScale=size;go.GetComponent<Collider>().enabled=false;Destroy(go.GetComponent<Collider>());go.GetComponent<Renderer>().sharedMaterial=material;go.layer=gameObject.layer;return go.transform;}
        Transform Cylinder(string name,Vector3 p,Vector3 q,float r,Material material){var go=GameObject.CreatePrimitive(PrimitiveType.Cylinder);go.name=name;go.transform.SetParent(transform,false);go.transform.localPosition=(p+q)*.5f;go.transform.localRotation=Quaternion.FromToRotation(Vector3.up,q-p);go.transform.localScale=new Vector3(r*2,(q-p).magnitude*.5f,r*2);go.GetComponent<Collider>().enabled=false;Destroy(go.GetComponent<Collider>());go.GetComponent<Renderer>().sharedMaterial=material;go.layer=gameObject.layer;return go.transform;}
    }
}
