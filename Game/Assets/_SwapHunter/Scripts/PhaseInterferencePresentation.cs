using UnityEngine;
using UnityEngine.Rendering;

namespace SwapHunter
{
    // Two meridians, one equator and the feet-height slice describe the actual rule sphere.
    public sealed class PhaseInterferencePresentation : MonoBehaviour
    {
        PhaseInterferenceField field;
        Transform visualRoot;
        readonly LineRenderer[] shell = new LineRenderer[3];
        LineRenderer footSlice;
        Material boundaryMaterial;
        V06WorldCaption caption;
        MaterialPropertyBlock colors;
        void Awake() { colors = new MaterialPropertyBlock(); }
        public int VisibleBoundaryCount { get; private set; }
        public float LastFootSliceHeight { get; private set; }
        public string StatusText => caption ? caption.Value : "";
        public void Initialize(PhaseInterferenceField owner)
        {
            field = owner;
            visualRoot = new GameObject("Interference sphere boundary").transform; visualRoot.SetParent(transform, false);
            // The shared trace material is opaque; use a local transparent variant so alpha really attenuates the wires.
            boundaryMaterial = new Material(Shapes.Mat(9)); boundaryMaterial.name = "Local interference boundary";
            boundaryMaterial.SetFloat("_Surface", 1); boundaryMaterial.SetFloat("_Blend", 0);
            boundaryMaterial.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha); boundaryMaterial.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            boundaryMaterial.SetFloat("_SrcBlendAlpha", (float)BlendMode.One); boundaryMaterial.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            boundaryMaterial.SetFloat("_ZWrite", 0); boundaryMaterial.SetOverrideTag("RenderType", "Transparent");
            boundaryMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); boundaryMaterial.renderQueue = (int)RenderQueue.Transparent;
            for (int i = 0; i < shell.Length; i++) shell[i] = Line(.012f);
            footSlice = Line(.03f);
            caption = V06WorldCaption.Create(visualRoot, Vector3.up * 2, .027f);
            caption.ConfigureVisibility(16, "interference"); Refresh();
        }
        LineRenderer Line(float width)
        {
            var line = Shapes.Line(visualRoot, Vector3.zero, Vector3.zero, Color.white, width);
            line.sharedMaterial = boundaryMaterial; line.positionCount = 65; line.loop = true; line.startWidth = line.endWidth = width; return line;
        }
        void LateUpdate() { Refresh(); }
        public void Refresh()
        {
            if (!field || !visualRoot) return;
            visualRoot.gameObject.SetActive(field.isActiveAndEnabled); VisibleBoundaryCount = 0;
            if (!field.isActiveAndEnabled) return;
            var game = DemoGame.I;
            var player = game ? game.player : null;
            Vector3 eye = player ? player.Eye.position : field.transform.position;
            float boundaryDistance = Mathf.Abs(Vector3.Distance(eye, field.transform.position) - field.Radius);
            float widthFactor = Mathf.Clamp(boundaryDistance / 2, .25f, 1);
            float rangeFade = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(22, 38, boundaryDistance));
            caption.transform.localPosition = Vector3.up * Mathf.Min(2, field.Radius * .45f);
            Color color = field.Active ? new Color(.95f, .5f, .14f, .26f * rangeFade) : new Color(.42f, .49f, .52f, .16f * rangeFade);
            for (int i = 0; i < shell.Length; i++)
            {
                var line = shell[i]; line.startWidth = line.endWidth = .012f * widthFactor; ApplyColor(line, color);
                for (int sample = 0; sample < line.positionCount; sample++)
                {
                    float angle = sample * Mathf.PI * 2 / (line.positionCount - 1); Vector3 offset;
                    if (i < 2)
                    {
                        float heading = i * Mathf.PI * .5f;
                        offset = new Vector3(Mathf.Cos(heading) * Mathf.Cos(angle), Mathf.Sin(angle), Mathf.Sin(heading) * Mathf.Cos(angle)) * field.Radius;
                    }
                    else offset = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * field.Radius;
                    line.SetPosition(sample, field.transform.position + offset);
                }
                line.enabled = rangeFade > 0; if (line.enabled) VisibleBoundaryCount++;
            }
            float feetHeight = player ? player.transform.position.y : field.transform.position.y;
            float delta = feetHeight - field.transform.position.y;
            footSlice.enabled = rangeFade > 0 && Mathf.Abs(delta) < field.Radius; LastFootSliceHeight = feetHeight;
            if (footSlice.enabled)
            {
                float radius = Mathf.Sqrt(field.Radius * field.Radius - delta * delta);
                for (int i = 0; i < footSlice.positionCount; i++)
                {
                    float angle = i * Mathf.PI * 2 / (footSlice.positionCount - 1);
                    footSlice.SetPosition(i, new Vector3(field.transform.position.x + Mathf.Cos(angle) * radius, feetHeight, field.transform.position.z + Mathf.Sin(angle) * radius));
                }
                footSlice.startWidth = footSlice.endWidth = .03f * widthFactor;
                ApplyColor(footSlice, field.Active ? new Color(.97f, .57f, .2f, .65f * rangeFade) : new Color(.46f, .53f, .56f, .28f * rangeFade)); VisibleBoundaryCount++;
            }
            caption.Set(field.Active ? "局部干扰 · 源与目标须在场外" : "此处干扰已关闭", field.Active ? new Color(1, .71f, .36f) : new Color(.61f, .68f, .7f));
        }
        void ApplyColor(LineRenderer line, Color color)
        { line.startColor = line.endColor = color; colors.SetColor("_BaseColor", color); colors.SetColor("_Color", color); line.SetPropertyBlock(colors); }
        void OnDisable() { if (visualRoot) visualRoot.gameObject.SetActive(false); VisibleBoundaryCount = 0; }
        void OnDestroy() { if (boundaryMaterial) Destroy(boundaryMaterial); }
    }
}
