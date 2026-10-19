using UnityEngine;
namespace SwapHunter
{
 // Decorative rescue feedback lives under the attempt's world and adds no collision or objectives.
 public sealed class StoryScenery:MonoBehaviour
 {
  DemoGame game;int level;Transform boat;Renderer[] routeLights=new Renderer[3];bool[] latched=new bool[3];float progress;
  public void Build(DemoGame owner,int chapter)
  {
   game=owner;level=chapter;
   var parent=new GameObject("N1 Rescue Environment").transform;parent.SetParent(transform,false);
   boat=new GameObject("Rescue workboat").transform;boat.SetParent(parent,false);boat.localPosition=new Vector3(-43,-.8f,35);
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
   for(int i=0;i<3;i++)
   {
    var p=new Vector3(-21,2,22+i*10);var tower=Shapes.Box("Rescue route beacon "+i,parent,p,new Vector3(.3f,4,.3f),3);
    routeLights[i]=Shapes.Box("Route status",parent,p+Vector3.up*2.2f,new Vector3(.7f,.7f,.7f),4).GetComponent<Renderer>();
   }
   D2WorldText.Create(parent,chapter>=8?"归航通道，请勿封闭":"应急救援支路 →",game.expeditionMap.spawn+new Vector3(3,2.2f,4),.45f);
   if(chapter>=7)for(int i=0;i<3;i++)SetBeacon(i);
   var sun=transform.Find("Chapter sun");if(sun){var light=sun.GetComponent<Light>();if(light){float night=Mathf.Sin(chapter/11f*Mathf.PI);light.color=Color.Lerp(new Color(1,.87f,.68f),new Color(.55f,.7f,1),night);light.intensity=Mathf.Lerp(1.4f,.85f,night);}}
  }
  void SetBeacon(int i){if(latched[i])return;latched[i]=true;if(routeLights[i])routeLights[i].sharedMaterial=Shapes.Mat(10);}
  void Update()
  {
   if(!game||!game.IsPlaying||game.mission==null)return;
   if(level==4){if(game.MissionObjectiveComplete("a"))SetBeacon(0);if(game.MissionObjectiveComplete("b"))SetBeacon(1);}
   if(level==6){if(game.MissionObjectiveComplete("a"))SetBeacon(0);if(game.MissionObjectiveComplete("b"))SetBeacon(1);if(game.MissionObjectiveComplete("c"))SetBeacon(2);}
   if(game.ExpeditionMainComplete)SetBeacon(2);
   bool sail=level>=8||(level==4&&(game.MissionObjectiveComplete("a")||game.MissionObjectiveComplete("b")));
   if(sail&&boat){progress=Mathf.Min(1,progress+Time.deltaTime/80);boat.localPosition=new Vector3(-43,-.8f,35+progress*30);}
  }
 }
}

