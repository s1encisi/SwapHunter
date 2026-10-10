using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace SwapHunter
{
    // Contextual world labels; hidden text never leaves a floating durability bar behind.
    public sealed class V06WorldCaption : MonoBehaviour
    {
        static readonly List<V06WorldCaption> active = new List<V06WorldCaption>();
        public TextMesh Text { get; private set; }
        public string Value => Text ? Text.text : "";
        public bool Visible { get; private set; }
        MeshRenderer textRenderer;
        Renderer[] linkedRenderers;
        string group;
        float maximumDistance = 24;
        Color displayColor = Color.white;

        public static V06WorldCaption Create(Transform parent, Vector3 localPosition, float characterSize)
        {
            var go = new GameObject("World status label"); go.layer = Layers.Effects;
            go.transform.SetParent(parent, false); go.transform.localPosition = localPosition;
            var label = go.AddComponent<V06WorldCaption>(); var game = DemoGame.I;
            label.Text = go.AddComponent<TextMesh>(); label.Text.font = game.font; label.Text.fontSize = 64;
            label.Text.characterSize = characterSize; label.Text.anchor = TextAnchor.MiddleCenter;
            label.Text.alignment = TextAlignment.Center; label.Text.lineSpacing = 1.15f;
            label.textRenderer = label.Text.GetComponent<MeshRenderer>();
            label.textRenderer.shadowCastingMode = ShadowCastingMode.Off; label.textRenderer.receiveShadows = false;
            if (go.GetComponentInParent<ObjectiveDamagePresentation>()) { label.group = "durability"; label.maximumDistance = 16; }
            if (game.config.worldTextMaterial)
            {
                var material = new Material(game.config.worldTextMaterial); material.mainTexture = game.font.material.mainTexture;
                label.textRenderer.sharedMaterial = material; go.AddComponent<WorldFontAtlas>().Initialize(game.font, material);
            }
            return label;
        }
        public void ConfigureVisibility(float distance, string priorityGroup)
        { maximumDistance = Mathf.Max(1, distance); group = priorityGroup; }
        public void Set(string value, Color color)
        {
            if (Text.text != value) { Text.text = value; Text.font.RequestCharactersInTexture(value, Text.fontSize); }
            displayColor = color; Text.color = color;
        }
        void OnEnable() { if (!active.Contains(this)) active.Add(this); RenderPipelineManager.beginCameraRendering += BeginCamera; }
        void OnDisable() { active.Remove(this); RenderPipelineManager.beginCameraRendering -= BeginCamera; SetVisible(false); }
        void BeginCamera(ScriptableRenderContext context, Camera camera)
        {
            if ((camera.cullingMask & (1 << Layers.Effects)) != 0) RefreshForCamera(camera);
        }
        void LateUpdate()
        {
            var game = DemoGame.I;
            if (game && game.player && game.player.cameraView) RefreshForCamera(game.player.cameraView);
        }
        public void RefreshForCamera(Camera camera)
        {
            if (!Text || !textRenderer || !camera) return;
            transform.rotation = camera.transform.rotation;
            bool visible = Eligible(camera, out Vector3 viewport, out float distance);
            if (visible && !string.IsNullOrEmpty(group))
            {
                float priority = Priority(viewport, distance);
                foreach (var other in active)
                {
                    if (!other || other == this || other.group != group || !other.Eligible(camera, out var otherViewport, out float otherDistance)) continue;
                    // One nearby field explanation is enough; durability labels only compete when they overlap visually.
                    bool competes = group == "interference" || Mathf.Abs(viewport.x - otherViewport.x) < .15f && Mathf.Abs(viewport.y - otherViewport.y) < .13f;
                    float otherPriority = Priority(otherViewport, otherDistance);
                    if (competes && (otherPriority < priority - .001f || Mathf.Abs(otherPriority - priority) <= .001f && other.GetInstanceID() < GetInstanceID())) { visible = false; break; }
                }
            }
            SetVisible(visible);
            if (visible)
            {
                float fade = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(maximumDistance * .78f, maximumDistance, distance));
                var color = displayColor; color.a *= fade; Text.color = color;
            }
        }
        bool Eligible(Camera camera, out Vector3 viewport, out float distance)
        {
            viewport = default; distance = 0;
            if (!isActiveAndEnabled || !Text || string.IsNullOrEmpty(Text.text)) return false;
            Vector3 point = transform.position; distance = Vector3.Distance(camera.transform.position, point);
            if (distance < .65f || distance >= maximumDistance) return false;
            viewport = camera.WorldToViewportPoint(point);
            if (viewport.z <= 0 || viewport.x < .055f || viewport.x > .945f || viewport.y < .065f || viewport.y > .93f) return false;
            return !Physics.Linecast(camera.transform.position, point, Layers.WorldMask, QueryTriggerInteraction.Ignore);
        }
        static float Priority(Vector3 viewport, float distance)
        { return Vector2.Distance(new Vector2(viewport.x, viewport.y), new Vector2(.5f, .55f)) * 10 + distance * .03f; }
        void SetVisible(bool value)
        {
            Visible = value;
            if (textRenderer) textRenderer.enabled = value;
            if (linkedRenderers == null && Text && group == "durability") linkedRenderers = transform.parent.GetComponentsInChildren<Renderer>();
            if (linkedRenderers != null) foreach (var renderer in linkedRenderers) if (renderer) renderer.enabled = value;
        }
    }
}
