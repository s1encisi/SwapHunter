using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace SwapHunter.Editor
{
    public static class AssetImportAudit
    {
        [Serializable] public sealed class Item { public string name; public int meshes, vertices, triangles; public Vector3 boundsSize, rightAxis, forwardAxis; }
        [Serializable] public sealed class Report { public string pipeline = "Three.js GLTFExporter -> GLB -> Unity glTFast editor import"; public Vector3 correction; public List<Item> assets = new List<Item>(); }
        public static ModelLibrary Prepare()
        {
            string[] names = { "cx24-carbine", "s12-scattergun", "operative-arms", "operative-body", "assault-sentinel", "sniper-sentinel", "shield-sentinel", "commander-sentinel", "cargo-container", "deck-module", "bulkhead-module", "control-terminal", "phase-beacon", "supply-case", "security-gate" };
            var assets = new List<GameObject>(); var report = new Report();
            foreach (string name in names)
            {
                string path = "Assets/_SwapHunter/Art/ThreeModels/" + name + ".glb";
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (!asset) throw new InvalidOperationException("GLB did not import to a Unity model: " + path);
                var right = ImportedModels.Find(asset.transform, "AxisRight"); var forward = ImportedModels.Find(asset.transform, "AxisForward");
                if (!right || !forward) throw new InvalidOperationException("Coordinate probes missing: " + name);
                var item = new Item { name = name, rightAxis = asset.transform.InverseTransformPoint(right.position), forwardAxis = asset.transform.InverseTransformPoint(forward.position) };
                Bounds bounds = default; bool initialized = false;
                foreach (var filter in asset.GetComponentsInChildren<MeshFilter>())
                {
                    if (!filter.sharedMesh) continue; item.meshes++; item.vertices += filter.sharedMesh.vertexCount;
                    for (int s = 0; s < filter.sharedMesh.subMeshCount; s++) item.triangles += (int)filter.sharedMesh.GetIndexCount(s) / 3;
                    var renderer = filter.GetComponent<Renderer>(); if (renderer) { if (!initialized) { bounds = renderer.bounds; initialized = true; } else bounds.Encapsulate(renderer.bounds); }
                }
                if (item.meshes == 0 || item.triangles == 0) throw new InvalidOperationException("Empty imported mesh: " + name);
                item.boundsSize = bounds.size; report.assets.Add(item); assets.Add(asset);
            }
            const string libraryPath = "Assets/_SwapHunter/Data/ModelLibrary.asset";
            var library = AssetDatabase.LoadAssetAtPath<ModelLibrary>(libraryPath);
            if (!library) { library = ScriptableObject.CreateInstance<ModelLibrary>(); AssetDatabase.CreateAsset(library, libraryPath); }
            library.coordinateCorrection = new Vector3(Mathf.Sign(report.assets[0].rightAxis.x), 1, Mathf.Sign(report.assets[0].forwardAxis.z));
            library.carbine = assets[0]; library.shotgun = assets[1]; library.arms = assets[2]; library.playerBody = assets[3];
            library.assault = assets[4]; library.sniper = assets[5]; library.shield = assets[6]; library.commander = assets[7];
            library.cargo = assets[8]; library.deck = assets[9]; library.bulkhead = assets[10]; library.terminal = assets[11]; library.beacon = assets[12]; library.supply = assets[13]; library.gate = assets[14];
            report.correction = library.coordinateCorrection;
            foreach (var item in report.assets)
                if (Vector3.Distance(Vector3.Scale(item.rightAxis, report.correction), Vector3.right) > .001f || Vector3.Distance(Vector3.Scale(item.forwardAxis, report.correction), Vector3.forward) > .001f)
                    throw new InvalidOperationException("Inconsistent imported axes: " + item.name);
            EditorUtility.SetDirty(library); AssetDatabase.SaveAssets();
            string folder = Path.GetFullPath("../Logs/V03"); Directory.CreateDirectory(folder); File.WriteAllText(Path.Combine(folder, "asset-import.json"), JsonUtility.ToJson(report, true));
            Debug.Log("THREE_MODELS_IMPORTED=" + assets.Count + "; correction=" + library.coordinateCorrection);
            return library;
        }
        public static void Run()
        {
            try { Prepare(); UnityEditor.EditorApplication.Exit(0); }
            catch (Exception error) { Debug.LogException(error); UnityEditor.EditorApplication.Exit(1); }
        }
    }
}

