using UnityEngine;
using UnityEngine.Rendering;

namespace SwapHunter
{
    // Render-only attachment. Bolt remains the sole owner of movement and hit logic.
    public sealed class EnemyProjectileVisual : MonoBehaviour
    {
        Bolt bolt;
        Transform core;
        LineRenderer tail;
        Vector3 origin;
        float coreWidth, coreLength, tailLength;

        public static void Attach(Bolt projectile)
        {
            var visual = projectile.gameObject.AddComponent<EnemyProjectileVisual>();
            visual.Initialize(projectile);
        }

        void Initialize(Bolt projectile)
        {
            bolt = projectile; origin = transform.position;
            var source = bolt.owner;
            bool boss = source && source.boss;
            bool omni = boss && source.boss.Skill == BossSkill.OmniFire && source.boss.SkillStep == BossSkillStep.Execute;
            bool sniper = source && source.kind == EnemyKind.Sniper;
            coreWidth = omni ? .030f : boss ? .023f : sniper ? .020f : .018f;
            coreLength = omni ? .15f : boss ? .12f : sniper ? .14f : .10f;
            tailLength = omni ? .58f : boss ? .46f : sniper ? .62f : .38f;
            float tailWidth = omni ? .020f : boss ? .016f : sniper ? .014f : .012f;
            Color tint = omni ? new Color(1, .39f, .14f) : boss ? new Color(1, .62f, .28f) :
                sniper ? new Color(1, .93f, .72f) : source && source.kind == EnemyKind.Training ? new Color(.60f, .85f, 1) : new Color(1, .82f, .48f);

            core = Shapes.Make("Tracer core", PrimitiveType.Cube, transform, Vector3.zero, Vector3.one, 7, false, Layers.Effects).transform;
            var renderer = core.GetComponent<Renderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            var color = new MaterialPropertyBlock();
            color.SetColor("_BaseColor", Color.Lerp(tint, Color.white, .55f));
            color.SetColor("_Color", Color.Lerp(tint, Color.white, .55f)); renderer.SetPropertyBlock(color);

            var lineObject = new GameObject("Short projectile tail"); lineObject.layer = Layers.Effects; lineObject.transform.SetParent(transform, false);
            tail = lineObject.AddComponent<LineRenderer>(); tail.useWorldSpace = false; tail.positionCount = 2;
            tail.sharedMaterial = Shapes.Mat(7); tail.alignment = LineAlignment.View;
            tail.startWidth = tailWidth; tail.endWidth = .0015f;
            tail.startColor = Color.white; tail.endColor = new Color(.4f, .4f, .4f, 1);
            tail.shadowCastingMode = ShadowCastingMode.Off; tail.receiveShadows = false;
            color.Clear(); color.SetColor("_BaseColor", tint); color.SetColor("_Color", tint); tail.SetPropertyBlock(color);
            UpdateVisual();
        }

        void LateUpdate() { if (bolt) UpdateVisual(); }
        void UpdateVisual()
        {
            if (bolt.velocity.sqrMagnitude > .0001f) transform.rotation = Quaternion.LookRotation(bolt.velocity);
            float traveled = Vector3.Distance(origin, transform.position);
            float length = Mathf.Min(coreLength, Mathf.Max(coreWidth, traveled));
            core.localScale = new Vector3(coreWidth, coreWidth, length);
            core.localPosition = Vector3.back * (length * .5f);
            // A capped segment has no lingering trail objects and never extends ahead
            // of the ray-tested projectile or back beyond its muzzle position.
            tail.enabled = traveled > .01f;
            tail.SetPosition(0, Vector3.zero);
            tail.SetPosition(1, Vector3.back * Mathf.Min(tailLength, traveled));
        }
    }
}
