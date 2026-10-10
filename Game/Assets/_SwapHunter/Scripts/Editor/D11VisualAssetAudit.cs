using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
namespace SwapHunter.Editor
{
    public static class D11VisualAssetAudit
    {
        [Serializable] public sealed class Item {public string path,guid;public int renderers,vertices,materials;public Vector3 size;public bool passed;public string error;}
        [Serializable] public sealed class Report {public string unityVersion,kind="Actual Unity import/asset audit; not native player rendering, navigation, frame-rate or human review";public bool passed;public List<Item> items=new List<Item>();}
        public static void Run()
        {
            var args=Environment.GetCommandLineArgs();int at=Array.IndexOf(args,"-visualEvidence");
            if(at<0||at+1>=args.Length)throw new ArgumentException("Pass a new -visualEvidence report path");
            string output=Path.GetFullPath(args[at+1]);if(File.Exists(output))throw new IOException("Existing visual evidence preserved");
            var report=new Report{unityVersion=Application.unityVersion};bool passed=true;
            foreach(string name in new[]{"Column_Slim","FloorTile_Basic","Props_ContainerFull","Props_ComputerSmall","Wall_1"})
            {
                string path="Assets/_SwapHunter/Resources/VisualD11/Environment/"+name+".glb";var item=new Item{path=path};report.items.Add(item);
                try
                {
                    AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);var model=AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if(!model)throw new InvalidOperationException("GLB did not import");
                    item.guid=AssetDatabase.AssetPathToGUID(path);var renderers=model.GetComponentsInChildren<Renderer>(true);item.renderers=renderers.Length;
                    bool first=true;Bounds bounds=default;var materials=new HashSet<Material>();
                    foreach(var renderer in renderers)
                    {
                        if(first){bounds=renderer.bounds;first=false;}else bounds.Encapsulate(renderer.bounds);
                        foreach(var material in renderer.sharedMaterials)
                        {if(!material||!material.shader||material.shader.name.Contains("InternalError"))throw new InvalidOperationException("Missing/invalid imported material");materials.Add(material);}
                    }
                    foreach(var mesh in model.GetComponentsInChildren<MeshFilter>(true))if(mesh.sharedMesh)item.vertices+=mesh.sharedMesh.vertexCount;
                    item.materials=materials.Count;item.size=bounds.size;
                    item.passed=item.renderers>0&&item.vertices>0&&item.materials>0&&model.GetComponentsInChildren<Collider>(true).Length==0&&model.GetComponentsInChildren<MonoBehaviour>(true).Length==0;
                }
                catch(Exception error){item.error=error.Message;item.passed=false;}passed&=item.passed;
            }
            foreach(string name in new[]{"circle_01","flare_01","spark_01"})
            {
                string path="Assets/_SwapHunter/Resources/VisualD11/Particles/"+name+".png";var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                var item=new Item{path=path,guid=AssetDatabase.AssetPathToGUID(path),size=texture?new Vector3(texture.width,texture.height,0):Vector3.zero,
                    passed=texture&&texture.width==512&&texture.height==512};report.items.Add(item);passed&=item.passed;
            }
            report.passed=passed;Directory.CreateDirectory(Path.GetDirectoryName(output));File.WriteAllText(output,JsonUtility.ToJson(report,true));
            Debug.Log("D11_VISUAL_IMPORT="+passed+";assets="+report.items.Count);EditorApplication.Exit(passed?0:1);
        }
    }
}
