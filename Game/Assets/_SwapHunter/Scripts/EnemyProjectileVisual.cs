using UnityEngine;
using UnityEngine.Rendering;

namespace SwapHunter
{
    public enum EnemyProjectileStyle { Auto, OmniRing, OmniAimed }
    // Geometry is created once; all shot-specific state is reapplied while its Bolt is still inactive.
    public sealed class EnemyProjectileVisual : MonoBehaviour
    {
        Bolt bolt;
        Transform core;
        Renderer coreRenderer;
        LineRenderer tail;
        MaterialPropertyBlock color;
        Vector3 origin;
        float coreWidth, coreLength, tailLength;

        public static EnemyProjectileVisual Attach(Bolt projectile)
        {
            var visual = projectile.gameObject.AddComponent<EnemyProjectileVisual>();
            visual.BuildGeometry(projectile); return visual;
        }
        void BuildGeometry(Bolt projectile)
        {
            bolt = projectile;
            // This is explicit runtime initialization, never a MonoBehaviour field initializer/constructor.
            color = new MaterialPropertyBlock();
            core = Shapes.Make("Tracer core", PrimitiveType.Cube, transform, Vector3.zero, Vector3.one, 7, false, Layers.Effects).transform;
            coreRenderer = core.GetComponent<Renderer>(); coreRenderer.shadowCastingMode = ShadowCastingMode.Off; coreRenderer.receiveShadows = false;
            var lineObject = new GameObject("Short projectile tail"); lineObject.layer = Layers.Effects; lineObject.transform.SetParent(transform, false);
            tail = lineObject.AddComponent<LineRenderer>(); tail.useWorldSpace = false; tail.positionCount = 2;
            tail.sharedMaterial = Shapes.Mat(7); tail.alignment = LineAlignment.View;
            tail.shadowCastingMode = ShadowCastingMode.Off; tail.receiveShadows = false;
        }
        public void ResetForShot()
        {
            origin = transform.position;
            var source = bolt.owner;
            bool boss = source && source.boss;
            bool aimed = bolt.VisualStyle == EnemyProjectileStyle.OmniAimed;
            bool omni = bolt.VisualStyle == EnemyProjectileStyle.OmniRing || bolt.VisualStyle == EnemyProjectileStyle.Auto
                && boss && source.boss.Skill == BossSkill.OmniFire && source.boss.SkillStep == BossSkillStep.Execute;
            bool sniper = source && source.kind == EnemyKind.Sniper;
            coreWidth = aimed ? .028f : omni ? .030f : boss ? .023f : sniper ? .020f : .018f;
            coreLength = aimed ? .30f : omni ? .15f : boss ? .12f : sniper ? .14f : .10f;
            tailLength = aimed ? 1.05f : omni ? .58f : boss ? .46f : sniper ? .62f : .38f;
            float tailWidth = aimed ? .024f : omni ? .020f : boss ? .016f : sniper ? .014f : .012f;
            Color tint = aimed ? new Color(1, .30f, .78f) : omni ? new Color(1, .39f, .14f) : boss ? new Color(1, .62f, .28f) :
                sniper ? new Color(1, .93f, .72f) : source && source.kind == EnemyKind.Training ? new Color(.60f, .85f, 1) : new Color(1, .82f, .48f);
            enabled = true; coreRenderer.enabled = true; core.localRotation = Quaternion.identity;
            color.Clear(); color.SetColor("_BaseColor", Color.Lerp(tint, Color.white, .55f));
            color.SetColor("_Color", Color.Lerp(tint, Color.white, .55f)); coreRenderer.SetPropertyBlock(color);
            tail.transform.localPosition = Vector3.zero; tail.transform.localRotation = Quaternion.identity; tail.transform.localScale = Vector3.one;
            tail.positionCount = 2; tail.startWidth = tailWidth; tail.endWidth = .0015f;
            tail.startColor = Color.white; tail.endColor = new Color(.4f, .4f, .4f, 1);
            color.Clear(); color.SetColor("_BaseColor", tint); color.SetColor("_Color", tint); tail.SetPropertyBlock(color);
            tail.SetPosition(0, Vector3.zero); tail.SetPosition(1, Vector3.zero); tail.enabled = false;
            UpdateVisual();
        }
        void LateUpdate() { if (bolt && !bolt.IsRetired) UpdateVisual(); }
        void UpdateVisual()
        {
            transform.rotation = bolt.velocity.sqrMagnitude > .0001f ? Quaternion.LookRotation(bolt.velocity) : Quaternion.identity;
            float traveled = Vector3.Distance(origin, transform.position);
            float length = Mathf.Min(coreLength, Mathf.Max(coreWidth, traveled));
            core.localScale = new Vector3(coreWidth, coreWidth, length); core.localPosition = Vector3.back * (length * .5f);
            tail.enabled = traveled > .01f; tail.SetPosition(0, Vector3.zero);
            tail.SetPosition(1, Vector3.back * Mathf.Min(tailLength, traveled));
        }
    }
}
