using UnityEngine;

namespace SwapHunter
{
    // Geometry preview only. It deliberately makes no promise that the destination is tactically safe.
    public sealed class SwapEndpointPreview : MonoBehaviour
    {
        PlayerMotor owner;
        Transform visuals;
        LineRenderer sourceRing, destinationRing, sourceArrow, destinationArrow, invalidCross;
        V06WorldCaption sourceLabel, destinationLabel;
        MaterialPropertyBlock block;
        void Awake() { block = new MaterialPropertyBlock(); }
        public bool Visible { get; private set; }
        public Vector3 SourceFeet { get; private set; }
        public Vector3 DestinationFeet { get; private set; }
        public SwapValidationResult Result { get; private set; }
        public bool SourceGrenadeNearby { get; private set; }
        public bool DestinationGrenadeNearby { get; private set; }
        public string DestinationText => destinationLabel ? destinationLabel.Value : "";
        public void Initialize(PlayerMotor player)
        {
            owner = player;
            visuals = new GameObject("Swap endpoint preview").transform; visuals.SetParent(transform, false);
            sourceRing = Ring(); destinationRing = Ring();
            sourceArrow = Line(); destinationArrow = Line(); invalidCross = Line();
            sourceArrow.positionCount = destinationArrow.positionCount = 5; invalidCross.positionCount = 4;
            sourceLabel = V06WorldCaption.Create(visuals, Vector3.zero, .029f);
            destinationLabel = V06WorldCaption.Create(visuals, Vector3.zero, .035f);
            Refresh();
        }
        LineRenderer Line()
        {
            var line = Shapes.Line(visuals, Vector3.zero, Vector3.zero, Color.white, .045f);
            line.startWidth = line.endWidth = .045f; return line;
        }
        LineRenderer Ring() { var line = Line(); line.loop = true; line.positionCount = 49; return line; }
        void LateUpdate() { Refresh(); }
        public void Refresh()
        {
            var game = DemoGame.I;
            var target = owner && game && game.IsPlaying && owner.health > 0 ? owner.FindTarget() : null;
            Visible = target && target.Alive && (target is EnemyActor || target is PhaseAnchor);
            if (!visuals) return;
            visuals.gameObject.SetActive(Visible);
            if (!Visible) return;
            SourceFeet = owner.transform.position; DestinationFeet = target.transform.position; Result = owner.ValidateSwap(target);
            Color color = Result.IsValid ? new Color(.17f, .96f, .84f) : new Color(1, .4f, .24f);
            DrawRing(sourceRing, SourceFeet + Vector3.up * .055f, target.Radius + .14f, color);
            DrawRing(destinationRing, DestinationFeet + Vector3.up * .055f, .47f, color);
            DrawArrow(sourceArrow, SourceFeet, target.transform.forward, color);
            DrawArrow(destinationArrow, DestinationFeet, owner.transform.forward, color);
            invalidCross.enabled = !Result.IsValid;
            if (invalidCross.enabled)
            {
                Vector3 p = DestinationFeet + Vector3.up * .065f;
                invalidCross.SetPosition(0, p + new Vector3(-.35f, 0, -.35f)); invalidCross.SetPosition(1, p + new Vector3(.35f, 0, .35f));
                invalidCross.SetPosition(2, p + new Vector3(-.35f, 0, .35f)); invalidCross.SetPosition(3, p + new Vector3(.35f, 0, -.35f)); SetColor(invalidCross, color);
            }
            SourceGrenadeNearby = GrenadeNearby(SourceFeet, out float sourceSeconds);
            DestinationGrenadeNearby = GrenadeNearby(DestinationFeet, out float destinationSeconds);
            sourceLabel.transform.position = SourceFeet + Vector3.up * .6f;
            destinationLabel.transform.position = DestinationFeet + Vector3.up * (Mathf.Max(1.8f, target.Height) + .48f);
            sourceLabel.Set("目标将到这里 · 朝向保留" + (SourceGrenadeNearby ? "\n附近有手雷 " + sourceSeconds.ToString("0.0") + " 秒" : ""), SourceGrenadeNearby ? new Color(1, .64f, .2f) : color);
            string status = Result.IsValid ? "你将到这里 · 朝向保留" : "落点预览 · " + Result.ToDisplayText(game.config.swapRange);
            status += DestinationGrenadeNearby ? "\n附近有手雷 " + destinationSeconds.ToString("0.0") + " 秒 · 仍需观察火线" : "\n仍需观察火线与手雷";
            destinationLabel.Set(status, DestinationGrenadeNearby ? new Color(1, .64f, .2f) : color);
        }
        bool GrenadeNearby(Vector3 feet, out float seconds)
        {
            seconds = float.MaxValue; var game = DemoGame.I;
            if (!game || !game.effects) return false;
            foreach (Transform effect in game.effects)
            {
                var grenade = effect.GetComponent<GrenadeActor>();
                if (grenade && grenade.Live && Vector3.Distance(feet + Vector3.up * .4f, grenade.BlastCenter) < game.config.grenadeRadius)
                    seconds = Mathf.Min(seconds, grenade.remaining);
                var ordnance = effect.GetComponent<PlayerOrdnance>();
                if (ordnance && ordnance.Live && Vector3.Distance(feet + Vector3.up * .4f, ordnance.transform.position) < PlayerOrdnance.BlastRadius)
                    seconds = Mathf.Min(seconds, ordnance.FuseLeft);
            }
            return seconds != float.MaxValue;
        }
        void DrawRing(LineRenderer line, Vector3 center, float radius, Color color)
        {
            for (int i = 0; i < line.positionCount; i++)
            { float angle = i * Mathf.PI * 2 / (line.positionCount - 1); line.SetPosition(i, center + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * radius); }
            SetColor(line, color);
        }
        void DrawArrow(LineRenderer line, Vector3 feet, Vector3 forward, Color color)
        {
            forward = Vector3.ProjectOnPlane(forward, Vector3.up).normalized;
            Vector3 side = Vector3.Cross(Vector3.up, forward), start = feet + Vector3.up * .06f, tip = start + forward * 1.18f;
            line.SetPosition(0, start); line.SetPosition(1, tip); line.SetPosition(2, tip - forward * .3f + side * .2f);
            line.SetPosition(3, tip); line.SetPosition(4, tip - forward * .3f - side * .2f); SetColor(line, color);
        }
        void SetColor(LineRenderer line, Color color)
        { line.startColor = line.endColor = color; block.SetColor("_BaseColor", color); block.SetColor("_Color", color); line.SetPropertyBlock(block); }
        void OnDisable() { if (visuals) visuals.gameObject.SetActive(false); Visible = false; }
    }
}
