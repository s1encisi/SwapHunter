using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
namespace SwapHunter.Editor
{
 public static class FinalModelsImport
 {
  [Serializable] public sealed class Item{public string path,error;public bool passed;public int meshes,vertices,clips,skins;public string[] animationNames;}
  [Serializable] public sealed class Report{public bool passed;public string scope="Actual new model imports, shader/clip/marker contracts; no gameplay or human claim";public List<Item> items=new List<Item>();}
  public static void Run()
  {
   var args=Environment.GetCommandLineArgs();int at=Array.IndexOf(args,"-modelEvidence");if(at<0||at+1>=args.Length)throw new ArgumentException("-modelEvidence is required");
   string output=Path.GetFullPath(args[at+1]);if(File.Exists(output))throw new IOException("New report path required");
   var report=new Report();
   foreach(string path in Directory.GetFiles("Assets/_SwapHunter/Resources/VisualV2","*.glb",SearchOption.AllDirectories).OrderBy(p=>p))
   {
    var item=new Item{path=path.Replace('\\','/')};report.items.Add(item);
    try
    {
     AssetDatabase.ImportAsset(item.path,ImportAssetOptions.ForceSynchronousImport);
     var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(item.path);if(!prefab)throw new InvalidOperationException("Missing imported model");
     var renderers=prefab.GetComponentsInChildren<Renderer>(true);item.meshes=renderers.Length;
     foreach(var f in prefab.GetComponentsInChildren<MeshFilter>(true))if(f.sharedMesh)item.vertices+=f.sharedMesh.vertexCount;
     foreach(var skin in prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true)){item.skins++;if(skin.sharedMesh)item.vertices+=skin.sharedMesh.vertexCount;}
     if(item.meshes==0||item.vertices==0)throw new InvalidOperationException("Empty model");
     foreach(var renderer in renderers)foreach(var material in renderer.sharedMaterials)
      if(!material||!material.shader||material.shader.name.Contains("InternalError"))throw new InvalidOperationException("Missing/error material");
     var clips=AssetDatabase.LoadAllAssetsAtPath(item.path).OfType<AnimationClip>().ToArray();item.clips=clips.Length;item.animationNames=clips.Select(c=>c.name).ToArray();
     if(item.path.Contains("/Actors/"))
     {
      if(item.skins==0||clips.Length==0)throw new InvalidOperationException("Actor requires skin and animations");
      if(clips.Any(c=>c.legacy))throw new InvalidOperationException("Actor Playables require non-legacy clips");
     }
     if(item.path.Contains("/Characters/fps_arms")&&item.skins==0)throw new InvalidOperationException("New FPS arms require skin");
     if(item.path.Contains("/Weapons/weapon_"))foreach(string name in new[]{"Muzzle","Grip","SupportGrip"})
      if(!prefab.GetComponentsInChildren<Transform>(true).Any(t=>t.name==name))throw new InvalidOperationException("Missing weapon marker: "+name);
     item.passed=true;
    }
    catch(Exception error){item.error=error.Message;item.passed=false;}
   }
   report.passed=report.items.Count>=32&&report.items.All(i=>i.passed);
   Directory.CreateDirectory(Path.GetDirectoryName(output));File.WriteAllText(output,JsonUtility.ToJson(report,true));
   Debug.Log("FINAL_MODELS_IMPORT="+report.passed+"; models="+report.items.Count);EditorApplication.Exit(report.passed?0:1);
  }
 }
}
