using System;
using System.Collections.Generic;
using UnityEngine;

namespace SwapHunter
{
    // Completion is latched; the connected anchor remains a freely movable Q target.
    public sealed class PhaseAnchorSocket : MonoBehaviour
    {
        public string Id { get; private set; }
        public float Radius { get; private set; }
        public bool Connected { get; private set; }
        Action onConnected;
        bool configured;

        public void Configure(string id, float radius, Action connected)
        {
            if (configured) throw new InvalidOperationException("Anchor socket cannot be configured twice.");
            if (string.IsNullOrWhiteSpace(id) || radius <= 0 || float.IsNaN(radius) || float.IsInfinity(radius))
                throw new ArgumentException("Anchor socket requires an id and finite positive radius.");
            Id = id; Radius = radius; onConnected = connected; configured = true;
        }
        void Update()
        {
            var game = DemoGame.I;
            if (!configured || Connected || !game || !game.IsPlaying) return;
            if (HasAnchor(game.anchors) || game.expeditionActive && game.expeditionMap != null && HasAnchor(game.expeditionMap.anchors))
            {
                Connected = true;
                game.Record("anchor_socket_connected", Id + ";position=" + transform.position);
                var callback = onConnected; onConnected = null;
                callback?.Invoke();
            }
        }
        bool HasAnchor(List<PhaseAnchor> anchors)
        {
            foreach (var anchor in anchors)
                if (anchor && anchor.Alive && (anchor.transform.position - transform.position).sqrMagnitude <= Radius * Radius) return true;
            return false;
        }
    }
}
