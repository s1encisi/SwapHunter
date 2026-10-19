using UnityEngine;
namespace SwapHunter
{
 // Decorative rescue feedback lives under the attempt's world and adds no collision or objectives.
 public sealed class StoryScenery:MonoBehaviour
 {
  DemoGame game;int level;Transform boat;Renderer[] routeLights=new Renderer[3];bool[] latched=new bool[3];float progress;Vector3 boatOrigin;
  public void Build(DemoGame owner,int chapter)
  {
   game=owner;level=chapter;
   var parent=new GameObject("N1 Rescue Environment").transform;parent.SetParent(transform,false);
   boat=new GameObject("Rescue workboat").transform;boat.SetParent(parent,false);float minX=-43,minZ=35,maxZ=35;foreach(var f in transform.GetComponentsInChildren<MapV2Footprint>()){minX=Mathf.Min(minX,f.center.x-f.size.x/2-22);minZ=Mathf.Min(minZ,f.center.y-f.size.y/2);maxZ=Mathf.Max(maxZ,f.center.y+f.size.y/2);}boatOrigin=new Vector3(minX,chapter==6?-5.3f:-.8f,(minZ+maxZ)*.5f);boat.localPosition=boatOrigin;
   Shapes.Box("Hull",boat,Vector3.zero,new Vector3(7,1.2f,17),5);
   Shapes.Box("Deck",boat,new Vector3(0,.8f,0),new Vector3(6,.4f,14),2);
   Shapes.Box("Wheelhouse",boat,new Vector3(0,2,3),new Vector3(4,2.5f,4),1);
   Shapes.Box("Bridge windows",boat,new Vector3(2.02f,2.4f,3),new Vector3(.08f,.9f,3),7);
   Shapes.Box("Mast",boat,new Vector3(0,5,3),new Vector3(.18f,5,.18f),3);
   for(int i=0;i<(chapter>=4?5:0);i++)
   {
    Shapes.Make("Distant rescued passenger",PrimitiveType.Capsule,boat,new Vector3((i%2==0?-1:1)*1.2f,1.7f,-i*1.4f),new Vector3(.42f,.75f,.42f),0);
    Shapes.Box("Deck guidance light",boat,new Vector3(3.08f,1,-i*2),new Vector3(.1f,.2f,.7f),10);
   }
   int beacons=chapter==4?2:chapter==6?3:0;
   for(int i=0;i<beacons;i++)
   {
    foreach(var n in game.expeditionMap.nodes)if(n&&game.expeditionMap.root.GetComponent<MissionLayout>().TryObjective(n,out var id)&&id==new[]{"a","b","c"}[i])
    {
     var p=n.transform.position+new Vector3(-1.4f,.75f,0);Shapes.Box("对应救援支路的设备状态杆 "+i,parent,p,new Vector3(.12f,1.5f,.12f),3);
     routeLights[i]=Shapes.Box("支路通电指示",parent,p+Vector3.up*.9f,new Vector3(.25f,.25f,.25f),4).GetComponent<Renderer>();break;
    }
   }
   // Chapter architecture owns lighting. N1 adds only causal rescue feedback.

  }
  void SetBeacon(int i){if(latched[i])return;latched[i]=true;if(routeLights[i])routeLights[i].sharedMaterial=Shapes.Mat(10);}
  void Update()
  {
   if(!game||!game.IsPlaying||game.mission==null)return;
   if(level==4){if(game.MissionObjectiveComplete("a"))SetBeacon(0);if(game.MissionObjectiveComplete("b"))SetBeacon(1);}
   if(level==6){if(game.MissionObjectiveComplete("a"))SetBeacon(0);if(game.MissionObjectiveComplete("b"))SetBeacon(1);if(game.MissionObjectiveComplete("c"))SetBeacon(2);}
   if(game.ExpeditionMainComplete)SetBeacon(2);
   bool sail=level>=8||(level==4&&(game.MissionObjectiveComplete("a")||game.MissionObjectiveComplete("b")));
   if(sail&&boat){progress=Mathf.Min(1,progress+Time.deltaTime/80);boat.localPosition=boatOrigin+Vector3.forward*progress*30;}
  }
 }
}

