using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
namespace SwapHunter
{
 // Native inspection renders. No scripted victory, save grant or visual-quality assertion.
 public static class EnemyV2Showcase
 {
  [Serializable]sealed class Manifest{public string purpose="Actual spawned enemies, frozen AI, neutral inspection lighting. Images require human review; geometry checks do not establish visual quality.";public List<View> views=new List<View>();}
  [Serializable]sealed class View{public string file,group,angle;public Vector3 camera;public List<Subject> subjects=new List<Subject>();}
  [Serializable]sealed class Subject{public string label,resource;public int archetype,chapterBoss=-1,animationClips,renderers;public float hp;public Vector3 feet;}
  static DemoGame G=>DemoGame.I;
  public static IEnumerator Run(Action<string,bool,string> add,string output)
  {
   if(!RunStorage.IsIsolated){add("enemy_showcase_isolated",false,"Refusing formal save");yield break;}
   Directory.CreateDirectory(output);var manifest=new Manifest();var origin=new Vector3(2600,0,0);var actors=new List<EnemyActor>();var labels=new List<GameObject>();GameObject root=null,cameraObject=null;Camera camera=null;
   bool wasAI=G.qaSuppressAI,oldFog=RenderSettings.fog,oldPlayerEnabled=G.player.enabled;var preview=G.player.GetComponent<SwapEndpointPreview>();bool oldPreviewEnabled=preview&&preview.enabled;Color oldAmbient=RenderSettings.ambientLight;var oldAmbientMode=RenderSettings.ambientMode;float oldScale=Time.timeScale;
   try
   {
    if(G.StoryModal)G.CloseStoryModal();if(G.expeditionActive){G.AbandonExpedition();G.OpenExpeditionBoard();}G.StartRun(false);G.qaSuppressAI=true;G.ClearDeployments();
    foreach(var enemy in G.enemies)if(enemy)enemy.gameObject.SetActive(false);
    root=new GameObject("EnemyV2 isolated visual stage");Shapes.Box("Inspection floor",root.transform,origin+Vector3.down*.5f,new Vector3(30,1,36),1,true,Layers.World);
    Physics.SyncTransforms();
    var nav=root.AddComponent<NavMeshSurface>();nav.collectObjects=CollectObjects.Children;nav.layerMask=Layers.WorldMask;nav.useGeometry=UnityEngine.AI.NavMeshCollectGeometry.PhysicsColliders;RuntimeNavigationOwner.BuildFresh(nav);
    // The physics floor and the fresh surface must both be registered before
    // FindSpawn performs its ground ray, NavMesh sample and capsule clearance.
    Physics.SyncTransforms();yield return null;
    var keyObject=new GameObject("Inspection key light");keyObject.transform.SetParent(root.transform);var key=keyObject.AddComponent<Light>();key.type=LightType.Directional;key.intensity=1.5f;key.color=new Color(1,.94f,.86f);key.shadows=LightShadows.Soft;keyObject.transform.rotation=Quaternion.Euler(38,-35,0);
    var fillObject=new GameObject("Inspection fill light");fillObject.transform.SetParent(root.transform);var fill=fillObject.AddComponent<Light>();fill.type=LightType.Point;fill.range=32;fill.intensity=60;fill.color=new Color(.68f,.80f,1);fillObject.transform.position=origin+new Vector3(7,5,8);
    RenderSettings.fog=false;RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.47f,.49f,.53f);
    cameraObject=new GameObject("EnemyV2 inspection camera");cameraObject.transform.SetParent(root.transform);camera=cameraObject.AddComponent<Camera>();camera.nearClipPlane=.05f;camera.farClipPlane=50;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.07f,.09f,.12f);camera.fieldOfView=24;camera.cullingMask=~(1<<Layers.ViewModel);camera.enabled=false;
    var urp=camera.GetUniversalAdditionalCameraData();urp.renderPostProcessing=false;urp.renderShadows=true;
    Time.timeScale=1;G.player.SetViewActive(false);G.player.target=null;G.player.enabled=false;if(preview)preview.enabled=false;
    for(int group=0;group<7;group++)
    {
     foreach(var actor in actors){if(!actor)continue;G.enemies.Remove(actor);actor.gameObject.SetActive(false);UnityEngine.Object.Destroy(actor.gameObject);}actors.Clear();foreach(var label in labels)if(label)UnityEngine.Object.Destroy(label);labels.Clear();yield return null;
     bool ordinary=group<3,finalBoss=group==6;int count=ordinary?(group==2?4:5):finalBoss?1:4;string title=ordinary?"普通原型 "+(group*5)+"–"+(group*5+count-1):finalBoss?"保留的原最终Boss":"独立章节小Boss B"+((group-3)*4+1).ToString("00")+"–B"+((group-3)*4+4).ToString("00");
     for(int i=0;i<count;i++)
     {
      int archetype=ordinary?group*5+i:4;var feet=origin+new Vector3((i-(count-1)*.5f)*2.3f,.035f,0);var actor=EnemyActor.Spawn((EnemyKind)archetype,feet,0);
      if(!actor){add("enemy_showcase_spawn_"+group+"_"+i,false,"kind="+archetype+";feet="+feet);continue;}
      actor.suppressAI=true;actor.agent.isStopped=true;actor.agent.ResetPath();
      if(finalBoss)actor.EnableBoss();else if(!ordinary)actor.EnableMiniBoss((group-3)*4+i);
      actor.transform.rotation=Quaternion.identity;actors.Add(actor);
      // Inspection labels replace per-component gameplay prompts only in this disposable fixture.
      foreach(var text in actor.GetComponentsInChildren<TextMesh>(true))text.gameObject.SetActive(false);
      string label=finalBoss?"原最终Boss\nHP "+actor.maximumHealth+" / 能量 "+BossEncounter.ShieldMaximum:actor.miniBoss?actor.miniBoss.Id+" "+actor.miniBoss.DisplayName+"\nHP "+actor.maximumHealth:((int)actor.kind).ToString("00")+" "+EnemyV2Definition.Name(actor.kind)+"\nHP "+actor.maximumHealth;
      var name=D2WorldText.Create(root.transform,label,feet+Vector3.up*2.65f,.22f);labels.Add(name.gameObject);
     }
     for(int angle=0;angle<2;angle++)
     {
      float yaw=angle==0?0:-55;foreach(var actor in actors){actor.transform.rotation=Quaternion.Euler(0,yaw,0);actor.VisualRig.SetLocomotion(0,0,false);actor.VisualRig.SetAim(actor.AimPoint+actor.transform.forward*20);}
      float distance=count==1?7.6f:14;camera.transform.position=origin+new Vector3(0,1.65f,distance);camera.transform.LookAt(origin+Vector3.up*1.2f);camera.fieldOfView=count==1?28:24;
      G.player.SetFeet(camera.transform.position-Vector3.up*1.62f);G.player.SetLook(180,0);Physics.SyncTransforms();yield return null;yield return null;
      // EnemyActor's suppressed update still aligns its visual aim; set the inspection pose last.
      foreach(var actor in actors)actor.VisualRig.SetAim(actor.AimPoint+actor.transform.forward*20);
      yield return new WaitForEndOfFrame();
      string file="enemy-group-"+(group+1).ToString("00")+"-"+(angle==0?"front":"three-quarter")+".png";Capture(camera,Path.Combine(output,file));var view=new View{file=file,group=title,angle=angle==0?"front / camera at standing eye height":"55-degree side / same standing camera",camera=camera.transform.position};
      foreach(var actor in actors)view.subjects.Add(new Subject{label=actor.miniBoss?actor.miniBoss.DisplayName:actor.boss?"原最终Boss":EnemyV2Definition.Name(actor.kind),archetype=(int)actor.kind,chapterBoss=actor.miniBoss?actor.miniBoss.Chapter:-1,animationClips=actor.VisualRig.AnimationClipCount,resource=actor.VisualRig.ResourcePath,hp=actor.health,feet=actor.transform.position,renderers=actor.GetComponentsInChildren<Renderer>().Length});manifest.views.Add(view);
     }
     add("enemy_showcase_group_"+(group+1),actors.Count==count,"Actual spawned "+actors.Count+";front and 55-degree native renders;"+title+";visual quality requires review");
    }
    File.WriteAllText(Path.Combine(output,"enemy-showcase.json"),JsonUtility.ToJson(manifest,true));
   }
   finally
   {
    foreach(var actor in actors)if(actor){G.enemies.Remove(actor);actor.gameObject.SetActive(false);UnityEngine.Object.Destroy(actor.gameObject);}if(root){root.SetActive(false);UnityEngine.Object.Destroy(root);}Time.timeScale=oldScale;RenderSettings.fog=oldFog;RenderSettings.ambientMode=oldAmbientMode;RenderSettings.ambientLight=oldAmbient;G.player.SetViewActive(true);G.player.target=null;G.player.enabled=oldPlayerEnabled;if(preview)preview.enabled=oldPreviewEnabled;G.qaSuppressAI=wasAI;G.SetState(RunState.Menu);
   }
  }
  static void Capture(Camera camera,string path)
  {
   var texture=new RenderTexture(1800,1000,24,RenderTextureFormat.ARGB32);Texture2D pixels=null;var previous=RenderTexture.active;
   try{texture.Create();RenderPipeline.SubmitRenderRequest(camera,new RenderPipeline.StandardRequest{destination=texture});RenderTexture.active=texture;pixels=new Texture2D(1800,1000,TextureFormat.RGBA32,false);pixels.ReadPixels(new Rect(0,0,1800,1000),0,0);pixels.Apply();File.WriteAllBytes(path,pixels.EncodeToPNG());}
   finally{RenderTexture.active=previous;texture.Release();UnityEngine.Object.Destroy(texture);if(pixels)UnityEngine.Object.Destroy(pixels);}
  }
 }
}
