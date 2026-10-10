using System;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

namespace SwapHunter
{
    // Owns only data synchronously created here on a fresh runtime surface.
    // A disabled classic world must retain its data so it can be re-enabled.
    [DisallowMultipleComponent]
    public sealed class RuntimeNavigationOwner : MonoBehaviour
    {
        NavMeshSurface surface;
        NavMeshData ownedData;
        public NavMeshData OwnedData => ownedData;

        public static RuntimeNavigationOwner BuildFresh(NavMeshSurface target)
        {
            if (!target) throw new ArgumentNullException(nameof(target));
            // Refuse existing/baked/shared data; this is not a general ownership transfer API.
            if (target.navMeshData || target.GetComponent<RuntimeNavigationOwner>())
                throw new InvalidOperationException("Runtime navigation ownership requires a new surface without existing data.");
            var owner = target.gameObject.AddComponent<RuntimeNavigationOwner>();
            owner.surface = target;
            try { target.BuildNavMesh(); }
            finally { owner.ownedData = target.navMeshData; }
            if (!owner.ownedData) throw new InvalidOperationException("Runtime NavMesh build produced no data.");
            return owner;
        }

        // Do not release in OnDisable: chapter entry temporarily disables the classic world.
        void OnDestroy()
        {
            if (!ownedData) return;
            // MissionLayout may still be updating this same data asynchronously.
            UnityEngine.AI.NavMeshBuilder.Cancel(ownedData);
            if (surface && surface.navMeshData == ownedData)
            {
                surface.RemoveData();
                surface.navMeshData = null;
            }
            Destroy(ownedData);
            ownedData = null;
            surface = null;
        }
    }
}
