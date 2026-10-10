using UnityEngine;

namespace SwapHunter
{
    public sealed class ObjectiveDamagePresentation : MonoBehaviour
    {
        DamageableObjective objective;
        Renderer[] bodyRenderers;
        Color[] bodyColors;
        V06WorldCaption caption;
        Transform bar;
        Renderer barRenderer;
        float flashUntil;
        MaterialPropertyBlock block;
        void Awake() { block = new MaterialPropertyBlock(); }
        public string StatusText => caption ? caption.Value : "";
        public float DisplayedFraction { get; private set; }
        public bool HitFlashing => Time.time < flashUntil;
        public void Initialize(DamageableObjective owner)
        {
            objective = owner; bodyRenderers = GetComponentsInChildren<Renderer>(); bodyColors = new Color[bodyRenderers.Length];
            for (int i = 0; i < bodyRenderers.Length; i++)
            {
                var material = bodyRenderers[i].sharedMaterial;
                bodyColors[i] = material && material.HasProperty("_BaseColor") ? material.GetColor("_BaseColor") : Color.white;
            }
            var status = new GameObject("Objective durability").transform; status.SetParent(transform, false);
            status.position = objective.AimPoint + Vector3.up * .95f;
            caption = V06WorldCaption.Create(status, Vector3.up * .16f, .033f);
            var background = Shapes.Box("Durability bar background", status, new Vector3(0, -.1f, 0), new Vector3(1.25f, .065f, .018f), 3);
            background.layer = Layers.Effects;
            bar = Shapes.Box("Remaining durability", status, new Vector3(0, -.1f, -.016f), new Vector3(1.2f, .036f, .01f), 7).transform;
            bar.gameObject.layer = Layers.Effects; barRenderer = bar.GetComponent<Renderer>();
            Refresh();
        }
        public void NotifyHit() { flashUntil = Time.time + .14f; Refresh(); }
        void LateUpdate() { Refresh(); }
        public void Refresh()
        {
            if (!objective || !caption) return;
            DisplayedFraction = Mathf.Clamp01(objective.Health / objective.MaximumHealth);
            Color status = objective.Destroyed ? new Color(.52f, .9f, .65f) : HitFlashing ? Color.white : new Color(1, .7f, .29f);
            caption.Set(objective.Destroyed ? "已破坏" : "耐久 " + Mathf.CeilToInt(objective.Health) + " / " + Mathf.CeilToInt(objective.MaximumHealth), status);
            bar.localScale = new Vector3(1.2f * DisplayedFraction, .036f, .01f);
            bar.localPosition = new Vector3(-.6f * (1 - DisplayedFraction), -.1f, -.016f);
            SetColor(barRenderer, status);
            var game = DemoGame.I;
            if (game && game.player) caption.transform.parent.rotation = game.player.cameraView.transform.rotation;
            for (int i = 0; i < bodyRenderers.Length; i++)
                if (bodyRenderers[i]) SetColor(bodyRenderers[i], objective.Destroyed ? new Color(.2f, .26f, .28f) : HitFlashing ? new Color(1, .89f, .59f) : bodyColors[i]);
        }
        void SetColor(Renderer renderer, Color color)
        { block.Clear(); block.SetColor("_BaseColor", color); block.SetColor("_Color", color); renderer.SetPropertyBlock(block); }
    }
}
