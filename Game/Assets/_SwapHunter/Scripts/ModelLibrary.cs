using UnityEngine;

namespace SwapHunter
{
    public sealed class ModelLibrary : ScriptableObject
    {
        public GameObject carbine, shotgun, arms, playerBody, assault, sniper, shield, commander;
        public GameObject cargo, deck, bulkhead, terminal, beacon, supply, gate;
        public Vector3 coordinateCorrection = Vector3.one;
    }
    public static class ImportedModels
    {
        public static ModelLibrary Library;
        public static Transform Find(Transform root, string name)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true)) if (child.name == name) return child;
            return null;
        }
        public static GameObject Create(GameObject asset, Transform parent, Vector3 position, Vector3? scale = null)
        {
            if (!asset) throw new System.InvalidOperationException("Required imported Three.js model is missing");
            var root = new GameObject(asset.name + " [Three.js]"); root.transform.SetParent(parent, false); root.transform.localPosition = position;
            root.transform.localScale = scale ?? Vector3.one;
            var model = Object.Instantiate(asset, root.transform, false); model.transform.localPosition = Vector3.zero; model.transform.localRotation = Quaternion.identity;
            model.transform.localScale = Library.coordinateCorrection;
            foreach (var collider in root.GetComponentsInChildren<Collider>()) { collider.enabled = false; Object.Destroy(collider); }
            return root;
        }
        public static void ReplaceBox(GameObject physicalProxy, GameObject asset, Vector3 nominalSize)
        {
            var renderer = physicalProxy.GetComponent<Renderer>(); if (renderer) renderer.enabled = false;
            Create(asset, physicalProxy.transform, new Vector3(0, -.5f, 0), new Vector3(1 / nominalSize.x, 1 / nominalSize.y, 1 / nominalSize.z));
        }
        public static void SetLayer(Transform root, int layer) { foreach (Transform t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer; }
    }
}
