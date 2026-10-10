using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace SwapHunter
{
    public sealed class VisualV06QA : MonoBehaviour
    {
        [Serializable] public sealed class Check { public string name, detail; public bool passed; }
        [Serializable] public sealed class Frame
        {
            public string id, file, kind, setup, chapterName, swapReason;
            public int chapter, width, height, changedPixels;
            public Vector3 cameraPosition, cameraEuler, playerFeet;
            public bool includesWeaponCamera, includesScreenSpaceHud = false, humanReviewRequired = true;
        }
        [Serializable] public sealed class Report
        {
            public string kind = "Native URP offscreen camera captures; AI suppressed and viewpoints explicitly placed. Not real-time performance, human playtesting or unassisted completion.";
            public string renderApi = "RenderPipeline.StandardRequest + SubmitRenderRequest; full base-camera stack";
            public string version, graphicsDevice, graphicsApi;
            public bool batchMode, passed, humanVisualReviewCompleted = false;
            public List<Check> checks = new List<Check>();
            public List<Frame> frames = new List<Frame>();
        }
        readonly Report report = new Report();
        string output;
        Camera overview;
        float started;
        bool done;
        const int Width = 1600, Height = 900;
        readonly HashSet<int> representative = new HashSet<int> { 1, 5, 6, 10, 11 };
        DemoGame G => DemoGame.I;
        PlayerMotor P => G.player;

        void Awake()
        {
            started = Time.realtimeSinceStartup;
            var args = Environment.GetCommandLineArgs(); int index = Array.IndexOf(args, "-qaOutput");
            output = index >= 0 && index + 1 < args.Length ? args[index + 1] : RunStorage.Root;
            Directory.CreateDirectory(output); report.version = Application.version; report.batchMode = Application.isBatchMode;
            report.graphicsDevice = SystemInfo.graphicsDeviceName; report.graphicsApi = SystemInfo.graphicsDeviceType.ToString();
            Application.logMessageReceived += Error;
        }
        void Error(string message, string stack, LogType type)
        { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) Add("runtime_error", false, message + "\n" + stack); }
        void Update() { if (!done && Time.realtimeSinceStartup - started > 360) { Add("watchdog", false, "360 real seconds"); Finish(); } }
        void Add(string name, bool passed, string detail)
        { report.checks.Add(new Check { name = name, passed = passed, detail = detail }); Debug.Log("V06_VISUAL " + name + " " + passed + " " + detail); }
        void Aim(Vector3 target)
        { Vector3 d = target - P.Eye.position; P.SetLook(Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg, -Mathf.Atan2(d.y, new Vector2(d.x, d.z).magnitude) * Mathf.Rad2Deg); }
        void Place(Vector3 feet, Vector3 look)
        { P.CancelActions(); P.SetFeet(feet); P.verticalSpeed = 0; P.cooldown = 0; Aim(look); Physics.SyncTransforms(); }
        IEnumerator Start()
        {
            while (!Application.isBatchMode && !UnityEngine.Rendering.SplashScreen.isFinished) yield return null;
            Add("isolated_validation_storage", RunStorage.IsValidation, RunStorage.Root);
            Add("graphics_device_available", SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null, report.graphicsApi + "; do not use -nographics for this suite");
            if (!RunStorage.IsValidation || SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) { Finish(); yield break; }
            var profile = new ExpeditionStore(Path.Combine(RunStorage.Root, "v06-visual-fixture"));
            for (int level = profile.Profile.highestUnlocked; level < 11; level++)
            { string run = profile.Begin(level); profile.Settle(run, true, false, 30); }
            G.expeditionStore = profile; G.qaSuppressAI = true;
            overview = new GameObject("Offscreen chapter inspection camera").AddComponent<Camera>();
            overview.CopyFrom(P.cameraView); overview.enabled = false; overview.tag = "Untagged";
            overview.cullingMask &= ~(1 << Layers.ViewModel); overview.fieldOfView = 48; overview.nearClipPlane = .1f; overview.farClipPlane = 300;
            var data = overview.GetUniversalAdditionalCameraData(); data.renderPostProcessing = false; data.renderType = CameraRenderType.Base;
            for (int level = 0; level < 12; level++)
            {
                if (G.expeditionActive) { G.FinishExpedition(false); G.OpenExpeditionBoard(); yield return null; }
                bool began = G.BeginExpedition(level); Add("chapter_" + (level + 1).ToString("00") + "_begin", began, G.expeditionNotice);
                if (!began || G.expeditionMap == null) continue;
                yield return new WaitForSeconds(2.3f);
                Bounds bounds = WalkableBounds();
                overview.transform.position = bounds.center + new Vector3(.65f, .85f, -1).normalized * Mathf.Max(48, bounds.size.magnitude * .94f);
                overview.transform.LookAt(bounds.center + Vector3.up * .5f);
                Capture(overview, level, "chapter" + (level + 1).ToString("00") + "-overview", "chapter overview", "Real generated chapter, initial encounter deployment; explicit inspection camera; no objectives completed by this capture.");
                if (representative.Contains(level))
                {
                    switch (level)
                    {
                        case 1: Place(new Vector3(13, .04f, 33), new Vector3(-1, 1.1f, 45)); break;
                        case 5: Place(new Vector3(-13, .04f, 24), new Vector3(-9, .7f, 29)); break;
                        case 6: Place(new Vector3(0, .04f, 11), new Vector3(-7, .97f, 22)); break;
                        case 10: Place(new Vector3(-18, .04f, 20), new Vector3(0, .97f, 32)); break;
                        case 11: Place(new Vector3(-8, .04f, 21), new Vector3(-9, 4.27f, 33)); break;
                    }
                    yield return new WaitForSeconds(2.1f);
                    P.GetComponent<SwapEndpointPreview>().Refresh();
                    Capture(P.cameraView, level, "chapter" + (level + 1).ToString("00") + "-player", "player camera", "Actual first-person base camera plus equipment overlay; explicit feet placement, AI suppressed. Screen-space IMGUI is not rendered by camera requests.");
                }
                if (level == 5) yield return DamageViews(level);
                if (level == 6) yield return SwapViews(level);
                if (level == 10) yield return FieldViews(level);
            }
            int overviewCount = 0, playerCount = 0;
            foreach (var frame in report.frames) { if (frame.kind == "chapter overview") overviewCount++; if (frame.kind == "player camera") playerCount++; }
            Add("all_twelve_chapter_overviews", overviewCount == 12, "saved=" + overviewCount);
            Add("five_representative_player_views", playerCount == 5, "saved=" + playerCount);
            Finish();
        }
        Bounds WalkableBounds()
        {
            Bounds bounds = new Bounds(G.expeditionMap.spawn, Vector3.one);
            foreach (var collider in G.expeditionMap.root.GetComponentsInChildren<Collider>())
                if (collider.enabled && !collider.isTrigger && collider.gameObject.layer == Layers.World) bounds.Encapsulate(collider.bounds);
            return bounds;
        }
        IEnumerator DamageViews(int level)
        {
            var objectives = G.expeditionMap.root.GetComponentsInChildren<DamageableObjective>();
            Add("damage_visual_fixture_exists", objectives.Length > 0, "Native chapter 6 destructible objective");
            if (objectives.Length == 0) yield break;
            var objective = objectives[0]; var visual = objective.GetComponent<ObjectiveDamagePresentation>();
            Place(new Vector3(-13, .04f, 24), objective.AimPoint); P.SetLoadout(0, 1); yield return new WaitForSeconds(.25f);
            float before = objective.Health; bool fired = P.Fire(); visual.Refresh();
            Add("durability_changes_on_real_weapon_hit", fired && objective.Health < before && visual.DisplayedFraction < 1 && visual.HitFlashing, "health=" + objective.Health + ";label=" + visual.StatusText);
            Capture(P.cameraView, level, "chapter06-objective-hit", "objective feedback", "One actual carbine shot; durability and native hit flash, no direct damage injection.");
            for (int shot = 0; shot < 16 && objective.Alive; shot++)
            { yield return new WaitForSeconds(.16f); Aim(objective.AimPoint); P.Fire(); }
            yield return new WaitForSeconds(.16f); visual.Refresh();
            Add("destroyed_prop_has_visible_state", objective.Destroyed && visual.StatusText == "已破坏" && visual.DisplayedFraction == 0, visual.StatusText);
            Capture(P.cameraView, level, "chapter06-objective-destroyed", "objective feedback", "Same objective after actual weapon destruction; mission callback and opened world state retained.");
        }
        IEnumerator SwapViews(int level)
        {
            var anchor = G.expeditionMap.anchors.Find(a => a && Vector3.Distance(a.transform.position, new Vector3(-7, .02f, 22)) < 1);
            Add("swap_preview_anchor_exists", anchor, "Native chapter 7 movable anchor"); if (!anchor) yield break;
            Place(new Vector3(0, .04f, 11), anchor.AimPoint); yield return null;
            var preview = P.GetComponent<SwapEndpointPreview>();
            var grenade = GrenadeActor.Spawn(P.transform.position + Vector3.up * .2f + Vector3.right * .7f, P.transform.position, 8);
            preview.Refresh();
            Add("preview_matches_real_endpoints", preview.Visible && Vector3.Distance(preview.SourceFeet, P.transform.position) < .01f && Vector3.Distance(preview.DestinationFeet, anchor.transform.position) < .01f, preview.Result.ToString());
            Add("preview_reports_actual_nearby_grenade", preview.SourceGrenadeNearby && !preview.DestinationGrenadeNearby, "Native live grenade explicitly placed near source as a visual fixture; no general safety claim.");
            overview.transform.position = new Vector3(13, 15, 4); overview.transform.LookAt(new Vector3(-3, .5f, 17));
            Capture(overview, level, "chapter07-both-endpoints", "swap geometry inspection", "Real source/destination ring geometry and preserved heading arrows; explicit side camera and one disclosed live-grenade fixture.");
            Capture(P.cameraView, level, "chapter07-before-swap", "swap player sequence", "Actual first-person view before Q; placed live grenade remains at the source.");
            int swaps = P.swapCount; bool requested = P.RequestSwap(anchor); yield return new WaitForSeconds(.18f); Aim(anchor.AimPoint); preview.Refresh();
            Add("visual_sequence_uses_actual_q", requested && P.swapCount == swaps + 1, "swaps=" + P.swapCount);
            Capture(P.cameraView, level, "chapter07-after-swap", "swap player sequence", "Actual Q completed; anchor and grenade remain at old player location, socket progress may latch naturally.");
            if (grenade) Destroy(grenade.gameObject);
        }
        IEnumerator FieldViews(int level)
        {
            var fields = G.expeditionMap.root.GetComponentsInChildren<PhaseInterferenceField>();
            Add("two_runtime_field_boundaries", fields.Length == 2, "Each configured sphere owns runtime renderers and no blocker collider");
            if (fields.Length != 2) yield break;
            foreach (var field in fields)
            {
                var visual = field.GetComponent<PhaseInterferencePresentation>(); visual.Refresh();
                Add("field_visible_" + field.Id, visual.VisibleBoundaryCount >= 3 && field.GetComponentsInChildren<Collider>().Length == 0, visual.StatusText);
                float surfaceError = 0;
                foreach (var line in field.GetComponentsInChildren<LineRenderer>())
                    if (line.enabled)
                        for (int point = 0; point < line.positionCount; point++)
                            surfaceError = Mathf.Max(surfaceError, Mathf.Abs(Vector3.Distance(line.GetPosition(point), field.transform.position) - field.Radius));
                Add("field_drawn_sphere_matches_rule_" + field.Id, surfaceError < .002f, "maximum surface distance error=" + surfaceError);
            }
            var anchor = G.expeditionMap.anchors.Find(a => a && Vector3.Distance(a.transform.position, new Vector3(0, .02f, 32)) < 1);
            Add("field_preview_anchor_exists", anchor, "Native outer-ring endpoint"); if (!anchor) yield break;
            Place(new Vector3(-13, .04f, 24), anchor.AimPoint); yield return null;
            var preview = P.GetComponent<SwapEndpointPreview>(); preview.Refresh();
            Add("interference_preview_uses_chinese_reason", preview.Result.Code == SwapFailureReason.Interference && !preview.DestinationText.Contains("field-"), preview.DestinationText);
            Capture(P.cameraView, level, "chapter11-inside-field", "interference state", "Player feet inside the left sphere; invalid Q preview is driven by real validation.");
            var control = G.expeditionMap.nodes.Find(n => n.kind == ExpeditionNodeKind.Jammer && n.label.Contains("左侧"));
            Add("left_field_control_exists", control, "Native mission control"); if (!control) yield break;
            Place(control.transform.position + Vector3.back * 1.5f, control.Point); bool used = G.UseExpeditionNode(control); yield return null;
            int active = 0; bool closedLabel = false;
            foreach (var field in fields)
            {
                if (field.Active) active++;
                var visual = field.GetComponent<PhaseInterferencePresentation>(); visual.Refresh();
                if (!field.Active) closedLabel |= visual.StatusText == "此处干扰已关闭";
            }
            Add("closed_field_desaturates_independently", used && active == 1 && closedLabel, "Exactly one native field remains active; closed sphere has a distinct state label");
            Aim(new Vector3(-8, 2.2f, 32));
            Capture(P.cameraView, level, "chapter11-one-field-closed", "interference state", "Native E control closed the left field; left boundary gray, right field remains amber.");
        }
        void Capture(Camera camera, int level, string id, string kind, string setup)
        {
            RenderTexture target = null; Texture2D pixels = null; RenderTexture previous = RenderTexture.active;
            var labels = new List<TextMesh>(G.expeditionMap.root.GetComponentsInChildren<TextMesh>());
            labels.AddRange(P.GetComponentsInChildren<TextMesh>());
            var rotations = new Quaternion[labels.Count];
            for (int i = 0; i < labels.Count; i++) rotations[i] = labels[i].transform.rotation;
            float oldAspect = camera.aspect;
            try
            {
                target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 1, name = "v06-offscreen-" + id };
                target.Create(); camera.aspect = Width / (float)Height;
                for (int i = 0; i < labels.Count; i++) labels[i].transform.rotation = camera.transform.rotation;
                var request = new RenderPipeline.StandardRequest { destination = target };
                bool supported = RenderPipeline.SupportsRenderRequest(camera, request);
                Add(id + "_render_request_supported", supported, camera.name);
                if (!supported) return;
                RenderPipeline.SubmitRenderRequest(camera, request);
                RenderTexture.active = target; pixels = new Texture2D(Width, Height, TextureFormat.RGB24, false);
                pixels.ReadPixels(new Rect(0, 0, Width, Height), 0, 0); pixels.Apply(false, false);
                var colors = pixels.GetPixels32(); int changed = 0; Color32 first = colors[0];
                for (int i = 0; i < colors.Length; i += 97)
                    if (Math.Abs(colors[i].r - first.r) + Math.Abs(colors[i].g - first.g) + Math.Abs(colors[i].b - first.b) > 18) changed++;
                byte[] png = pixels.EncodeToPNG(); string filename = id + ".png"; File.WriteAllBytes(Path.Combine(output, filename), png);
                Add(id + "_native_pixels_saved", changed > 100 && png.Length > 20000, "1600x900; varied samples=" + changed + ";bytes=" + png.Length + "; layout quality awaits human review");
                report.frames.Add(new Frame { id = id, file = filename, kind = kind, setup = setup, chapter = level + 1, chapterName = ExpeditionCatalog.Levels[level].name,
                    width = Width, height = Height, changedPixels = changed, cameraPosition = camera.transform.position, cameraEuler = camera.transform.eulerAngles,
                    playerFeet = P.transform.position, includesWeaponCamera = camera == P.cameraView, swapReason = P.ValidateSwap(P.FindTarget()).ToString() });
            }
            catch (Exception error) { Add(id + "_capture_exception", false, error.ToString()); }
            finally
            {
                camera.aspect = oldAspect; RenderTexture.active = previous;
                for (int i = 0; i < labels.Count; i++) if (labels[i]) labels[i].transform.rotation = rotations[i];
                if (pixels) Destroy(pixels); if (target) { target.Release(); Destroy(target); }
            }
        }
        void Finish()
        {
            if (done) return; done = true; Time.timeScale = 1;
            report.passed = report.checks.Count > 0 && report.checks.TrueForAll(check => check.passed);
            File.WriteAllText(Path.Combine(output, "visual-v06-report.json"), JsonUtility.ToJson(report, true));
            Debug.Log("VISUAL_V06_QA_RESULT=" + report.passed + ";frames=" + report.frames.Count);
            Application.Quit(report.passed ? 0 : 1);
        }
        void OnDestroy() { Application.logMessageReceived -= Error; if (overview) Destroy(overview.gameObject); }
    }
}
