using System.Collections.Generic;
using UnityEngine;

namespace SwapHunter
{
    // A per-effects-lifetime cache. The bound applies only to inactive retention, never to live projectiles.
    public sealed class BoltPool : MonoBehaviour
    {
        public const int MaximumRetained = 256;
        static BoltPool current;
        readonly Stack<Bolt> idle = new Stack<Bolt>(MaximumRetained);
        Transform effects;
        bool closing;
        public int RetainedCount => idle.Count;
        public Transform Effects => effects;
        public static BoltPool Current => current && current.isActiveAndEnabled && !current.closing ? current : null;

        internal static BoltPool For(Transform effects)
        {
            if (Current && current.effects == effects) return current;
            var root = new GameObject("Inactive enemy projectiles"); root.layer = Layers.Effects;
            root.transform.SetParent(effects, false);
            current = root.AddComponent<BoltPool>(); current.effects = effects;
            return current;
        }
        internal Bolt Take()
        {
            while (idle.Count > 0)
            {
                var bolt = idle.Pop();
                if (bolt) return bolt;
            }
            return null;
        }
        internal bool Retain(Bolt bolt)
        {
            if (closing || !isActiveAndEnabled || !effects || idle.Count >= MaximumRetained) return false;
            bolt.transform.SetParent(transform, false); idle.Push(bolt); return true;
        }
        void OnDisable()
        {
            // Existing effects cleanup disables then destroys every child. Do not repopulate a dying container.
            closing = true;
            while (idle.Count > 0) { var bolt = idle.Pop(); if (bolt) Destroy(bolt.gameObject); }
            if (current == this) current = null;
        }
        void OnDestroy() { if (current == this) current = null; idle.Clear(); }
    }
}
