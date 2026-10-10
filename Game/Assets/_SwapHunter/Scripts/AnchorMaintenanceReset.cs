using System;
using UnityEngine;

namespace SwapHunter
{
    // A repeatable maintenance action. Preflight all endpoints before moving anything.
    public sealed class AnchorMaintenanceReset : MonoBehaviour
    {
        PhaseAnchor[] anchors;
        Vector3[] origins;
        public string LastFailure { get; private set; } = "";

        public void Configure(PhaseAnchor[] targets, Vector3[] originalFeet)
        {
            if (targets == null || originalFeet == null || targets.Length == 0 || targets.Length != originalFeet.Length)
                throw new ArgumentException("Anchor reset requires matching nonempty anchor/origin arrays.");
            for (int i = 0; i < targets.Length; i++)
            {
                if (!targets[i]) throw new ArgumentException("Anchor reset target is missing.");
                Vector3 point = originalFeet[i];
                if (float.IsNaN(point.x) || float.IsNaN(point.y) || float.IsNaN(point.z) || float.IsInfinity(point.x) || float.IsInfinity(point.y) || float.IsInfinity(point.z))
                    throw new ArgumentException("Anchor reset origin must be finite.");
                for (int j = 0; j < i; j++) if (targets[i] == targets[j]) throw new ArgumentException("Anchor reset target is duplicated.");
            }
            anchors = (PhaseAnchor[])targets.Clone(); origins = (Vector3[])originalFeet.Clone();
        }
        public bool TryReset()
        {
            LastFailure = "";
            var game = DemoGame.I;
            if (!game || !game.IsPlaying || anchors == null) return Reject("当前无法复位");
            Physics.SyncTransforms();
            for (int i = 0; i < anchors.Length; i++)
            {
                if (!anchors[i] || !anchors[i].Alive) return Reject("锚点不可用");
                var anchor = anchors[i]; float radius = anchor.Radius;
                var a = origins[i] + Vector3.up * (radius + .06f);
                var b = origins[i] + Vector3.up * (anchor.Height - radius - .03f);
                foreach (var shape in Physics.OverlapCapsule(a, b, radius - .035f, Layers.CombatMask, QueryTriggerInteraction.Ignore))
                {
                    if (!BeingReset(shape)) return Reject("锚点原位受阻 · 请移开人物或障碍");
                }
                // Include authored collider dimensions, which may be wider than SwapTarget.Radius.
                Vector3 shift = origins[i] - anchor.transform.position;
                foreach (var shape in anchor.GetComponentsInChildren<Collider>())
                {
                    if (!shape.enabled || shape.isTrigger) continue;
                    Collider[] overlaps;
                    Vector3 scale = shape.transform.lossyScale;
                    if (shape is SphereCollider sphere)
                        overlaps = Physics.OverlapSphere(shape.transform.TransformPoint(sphere.center) + shift, sphere.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Max(Mathf.Abs(scale.y), Mathf.Abs(scale.z))) - .01f, Layers.CombatMask, QueryTriggerInteraction.Ignore);
                    else if (shape is BoxCollider box)
                    {
                        Vector3 half = Vector3.Scale(box.size, new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z))) * .5f - Vector3.one * .01f;
                        half = new Vector3(Mathf.Max(.01f, half.x), Mathf.Max(.01f, half.y), Mathf.Max(.01f, half.z));
                        overlaps = Physics.OverlapBox(shape.transform.TransformPoint(box.center) + shift, half, shape.transform.rotation, Layers.CombatMask, QueryTriggerInteraction.Ignore);
                    }
                    else if (shape is CapsuleCollider capsule)
                    {
                        Vector3 axis = capsule.direction == 0 ? Vector3.right : capsule.direction == 1 ? Vector3.up : Vector3.forward;
                        float axisScale = Mathf.Abs(capsule.direction == 0 ? scale.x : capsule.direction == 1 ? scale.y : scale.z);
                        float widthScale = capsule.direction == 0 ? Mathf.Max(Mathf.Abs(scale.y), Mathf.Abs(scale.z)) : capsule.direction == 1 ? Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z)) : Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y));
                        float actualRadius = capsule.radius * widthScale;
                        Vector3 center = shape.transform.TransformPoint(capsule.center) + shift;
                        Vector3 offset = shape.transform.TransformDirection(axis).normalized * Mathf.Max(0, capsule.height * axisScale * .5f - actualRadius);
                        overlaps = Physics.OverlapCapsule(center - offset, center + offset, Mathf.Max(.01f, actualRadius - .01f), Layers.CombatMask, QueryTriggerInteraction.Ignore);
                    }
                    else return Reject("锚点碰撞体不支持安全复位");
                    foreach (var overlap in overlaps) if (!BeingReset(overlap)) return Reject("锚点原位受阻 · 请移开人物或障碍");
                    for (int j = 0; j < i; j++)
                        foreach (var previousShape in anchors[j].GetComponentsInChildren<Collider>())
                            if (previousShape.enabled && !previousShape.isTrigger && Physics.ComputePenetration(shape, shape.transform.position + shift, shape.transform.rotation, previousShape, previousShape.transform.position + origins[j] - anchors[j].transform.position, previousShape.transform.rotation, out _, out float depth) && depth > .01f)
                                return Reject("锚点原位重叠");
                }
                for (int j = 0; j < i; j++)
                {
                    float low = origins[j].y + anchors[j].Radius + .06f;
                    float high = origins[j].y + anchors[j].Height - anchors[j].Radius - .03f;
                    float gap = Mathf.Max(0, Mathf.Max(a.y - high, low - b.y));
                    Vector3 flat = Vector3.ProjectOnPlane(origins[i] - origins[j], Vector3.up);
                    float combined = radius + anchors[j].Radius - .07f;
                    if (flat.sqrMagnitude + gap * gap < combined * combined) return Reject("锚点原位重叠");
                }
            }
            var previous = new Vector3[anchors.Length];
            for (int i = 0; i < anchors.Length; i++) previous[i] = anchors[i].transform.position;
            for (int i = 0; i < anchors.Length; i++)
            {
                if (anchors[i].MoveTo(origins[i])) continue;
                for (int j = 0; j <= i; j++) anchors[j].MoveTo(previous[j]);
                Physics.SyncTransforms(); return Reject("锚点复位失败");
            }
            Physics.SyncTransforms(); game.Record("anchor_maintenance_reset", "count=" + anchors.Length);
            return true;
        }
        bool BeingReset(Collider shape)
        {
            foreach (var other in anchors)
                if (other && (shape.transform == other.transform || shape.transform.IsChildOf(other.transform))) return true;
            return false;
        }
        bool Reject(string reason)
        {
            LastFailure = reason;
            var game = DemoGame.I;
            if (game) { game.Toast(reason, 2); game.Record("anchor_reset_rejected", reason); }
            return false;
        }
    }
}
