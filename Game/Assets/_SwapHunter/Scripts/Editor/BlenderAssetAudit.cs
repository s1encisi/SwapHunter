using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
namespace SwapHunter.Editor
{
 public static class BlenderAssetAudit
 {
  [Serializable] public sealed class Item {public string name;public int vertices,triangles;public Vector3 correction,bounds;}
  [Serializable] public sealed class Report {public string pipeline="Blender -> GLB -> glTFast -> Unity prefab";public List<Item> assets=new List<Item>();}
  public static void Prepare(ModelLibrary library)
  {
   string source="Assets/_SwapHunter/Art/BlenderModels";if(!Directory.Exists(source))return;
   Directory.CreateDirectory("Assets/_SwapHunter/Resources/Weapons");Directory.CreateDirectory("Assets/_SwapHunter/Resources/BlenderActors");
   var report=new Report();
   foreach(string file in Directory.GetFiles(source,"*.glb"))
   {
    string path=file.Replace('\\','/');AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
    var asset=AssetDatabase.LoadAssetAtPath<GameObject>(path);if(!asset)throw new InvalidOperationException("Blender GLB import failed: "+path);
    var right=ImportedModels.Find(asset.transform,"AxisRight");var forward=ImportedModels.Find(asset.transform,"AxisForward");
    if(!right||!forward)throw new InvalidOperationException("Blender axis probes missing: "+path);
    Vector3 r=asset.transform.InverseTransformPoint(right.position),f=asset.transform.InverseTransformPoint(forward.position);
    Vector3 correction=new Vector3(Mathf.Sign(r.x),1,Mathf.Sign(f.z));
    if(Vector3.Distance(Vector3.Scale(r,correction),Vector3.right)>.001f||Vector3.Distance(Vector3.Scale(f,correction),Vector3.forward)>.001f)throw new InvalidOperationException("Invalid Blender axes: "+path);
    string name=Path.GetFileNameWithoutExtension(path);var item=new Item{name=name,correction=correction};
    foreach(var filter in asset.GetComponentsInChildren<MeshFilter>(true))
    {if(!filter.sharedMesh)continue;item.vertices+=filter.sharedMesh.vertexCount;for(int j=0;j<filter.sharedMesh.subMeshCount;j++)item.triangles+=(int)filter.sharedMesh.GetIndexCount(j)/3;}
    if(item.triangles<=0)throw new InvalidOperationException("Empty Blender mesh: "+path);
    var wrapper=new GameObject(name);var instance=(GameObject)PrefabUtility.InstantiatePrefab(asset);instance.transform.SetParent(wrapper.transform,false);
    instance.transform.localScale=Vector3.Scale(correction,library.coordinateCorrection);
    var renderers=wrapper.GetComponentsInChildren<Renderer>();if(renderers.Length>0){Bounds b=renderers[0].bounds;foreach(var renderer in renderers)b.Encapsulate(renderer.bounds);item.bounds=b.size;}
    string target="Assets/_SwapHunter/Resources/"+(name.StartsWith("weapon_")?"Weapons/":"BlenderActors/")+name+".prefab";
    var prefab=PrefabUtility.SaveAsPrefabAsset(wrapper,target);UnityEngine.Object.DestroyImmediate(wrapper);
    if(name.StartsWith("weapon_"))
    {
     foreach(string marker in new[]{"Muzzle","AimRear","AimFront","magazine","charging_handle"})
      if(!ImportedModels.Find(prefab.transform,marker))throw new InvalidOperationException(name+" missing "+marker);
    }
    if(name=="operative-arms-v04")library.arms=prefab;
    if(name=="robot-assault-v04")library.assault=prefab;
    if(name=="robot-sniper-v04")library.sniper=prefab;
    if(name=="robot-shield-v04")library.shield=prefab;
    if(name=="robot-commander-v04")library.commander=prefab;
    report.assets.Add(item);
   }
   EditorUtility.SetDirty(library);AssetDatabase.SaveAssets();
   string folder=Path.GetFullPath("../Logs/V052");Directory.CreateDirectory(folder);File.WriteAllText(Path.Combine(folder,"blender-import.json"),JsonUtility.ToJson(report,true));
   Debug.Log("BLENDER_MODELS_IMPORTED="+report.assets.Count);
  }
 }
}


