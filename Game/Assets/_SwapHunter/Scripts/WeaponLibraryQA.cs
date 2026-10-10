using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace SwapHunter
{
    // Scripted integration of the real UI preview and display data. Never an automated aesthetic approval.
    public sealed class WeaponLibraryQA : MonoBehaviour
    {
        [Serializable] public sealed class Check { public string name, detail; public bool passed; }
        [Serializable] public sealed class Frame
        {
            public int weapon, width, height, variedPixels, sampledPixels;
            public string file, kind, source;
            public bool includesScreenSpaceGui, humanReviewRequired = true;
        }
        [Serializable] public sealed class StatRow
        {
            public string label, valueText, directionHint;
            public float value, maximum, normalized;
            public bool isApplicable;
        }
        [Serializable] public sealed class Stats
        {
            public int weapon, magazine, pellets, baseReserve;
            public string name, scope;
            public List<StatRow> rows = new List<StatRow>();
        }
        [Serializable] public sealed class Release
        {
            public string scope;
            public int tracked, destroyed, inactive, alive;
            public bool destroyedMeansNativeObjectUnavailable = true;
        }
        [Serializable] public sealed class Report
        {
            public string version, buildGuid, graphicsDevice, graphicsApi, fullGuiCapture;
            public string kind = "Native weapon-library preview/config/lifecycle fixture. Real preview output with explicit scripted selection, not mouse usability, human visual approval, gameplay completion or FPS evidence.";
            public string ownership = "Only preview-owned stage objects and two render targets plus icon textures are required to die. Shared source meshes/materials must survive. No global zero-growth threshold, forced GC or UnloadUnusedAssets.";
            public bool batchMode, passed, complete, fullGuiCaptureRequested, fullGuiCaptureCompleted, humanVisualReviewCompleted;
            public List<Check> checks = new List<Check>();
            public List<Frame> frames = new List<Frame>();
            public List<Stats> stats = new List<Stats>();
            public List<Release> releases = new List<Release>();
        }

        readonly Report report = new Report();
        readonly List<Object> sharedAssets = new List<Object>();
        static readonly int[] RepresentativeIds = { 0, 5, 8, 9, 10, 11 };
        string output;
        float began;
        bool done, abortRequested;
        DemoGame G => DemoGame.I;
        WeaponModelPreview Preview => G ? G.WeaponLibraryPreview : null;

        void Awake()
        {
            began = Time.realtimeSinceStartup;
            var args = Environment.GetCommandLineArgs(); int i = Array.IndexOf(args, "-qaOutput");
            output = i >= 0 && i + 1 < args.Length ? args[i + 1]
                : Path.Combine(Path.GetTempPath(), "SwapHunterWeaponLibraryQA", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(output);
            if (File.Exists(Path.Combine(output, "weapon-library-report.json")))
                throw new InvalidOperationException("Existing weapon-library evidence is preserved; use a new -qaOutput directory.");
            report.version = Application.version; report.buildGuid = Application.buildGUID;
            report.batchMode = Application.isBatchMode; report.graphicsDevice = SystemInfo.graphicsDeviceName;
            report.graphicsApi = SystemInfo.graphicsDeviceType.ToString();
            report.fullGuiCaptureRequested = Array.IndexOf(args, "-weaponLibraryCaptureGUI") >= 0;
            report.fullGuiCapture = Application.isBatchMode ? "skipped_background: camera/texture captures never include IMGUI"
                : report.fullGuiCaptureRequested ? "pending_explicit_foreground_capture" : "not_requested";
            Application.logMessageReceived += OnLog;
        }
        void OnLog(string message, string stack, LogType type)
        {
            if (!done && (type == LogType.Error || type == LogType.Exception || type == LogType.Assert))
            { Add("runtime_error", false, message + "\n" + stack); abortRequested = true; }
        }
        void Update()
        {
            if (done) return;
            if (abortRequested) { Finish(); return; }
            if (Time.realtimeSinceStartup - began > 180)
            { Add("watchdog", false, "180 real seconds; no unconditional SplashScreen or EndOfFrame wait in batch mode"); Finish(); }
        }
        void Add(string name, bool passed, string detail = "")
        {
            report.checks.Add(new Check { name = name, passed = passed, detail = detail });
            Debug.Log("WEAPON_LIBRARY_QA " + name + " " + passed + " " + detail);
        }

        IEnumerator Start()
        {
            Add("isolated_storage_flag", RunStorage.IsValidation, "Register -swapHunterWeaponLibraryQA before DemoGame initializes persistence.");
            Add("real_graphics_device", SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null, report.graphicsApi + "; -batchmode is supported, -nographics is not");
            if (!RunStorage.IsValidation || SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) { Finish(); yield break; }
            float deadline = Time.realtimeSinceStartup + 15;
            while ((!G || !G.player || G.expeditionStore == null) && Time.realtimeSinceStartup < deadline) yield return null;
            bool ready = G && G.player && G.expeditionStore != null;
            Add("game_initialized", ready, "Actual game/player/profile objects are required");
            if (!ready) { Finish(); yield break; }
            Add("profile_is_in_validation_root", IsBelow(G.expeditionStore.SavePath, RunStorage.Root), G.expeditionStore.SavePath);
            if (!IsBelow(G.expeditionStore.SavePath, RunStorage.Root)) { Finish(); yield break; }
            G.qaSuppressAI = true;
            G.OpenExpeditionBoard(); G.SetExpeditionTabForReview(1); G.SelectWeaponForLibraryReview(0);
            yield return WaitReady(0, true, "initial");
            if (!IsReady(0, true)) { Finish(); yield break; }

            string balanceBefore = JsonUtility.ToJson(G.config), profileBefore = JsonUtility.ToJson(G.expeditionStore.Profile);
            int weaponBefore = G.player.weapon;
            int[] ammoBefore = (int[])G.player.ammo.Clone(), reserveBefore = (int[])G.player.reserve.Clone();
            int[] equipmentBefore = (int[])G.player.EquippedWeapons.Clone();
            WeaponDisplayStatsChecks.Run(G.config, Add);
            RecordStats();
            var preview = Preview; preview.AutoRotate = false;
            var iconIds = new HashSet<int>();
            for (int id = 0; id < WeaponCatalog.Count; id++)
            {
                var asset = SourceAsset(id); RememberSharedAssets(asset);
                var icon = preview.GetIcon(id); if (icon) iconIds.Add(icon.GetInstanceID());
                Add("icon_" + id.ToString("00") + "_own_source", asset && icon && preview.GetSourceInstanceId(id) == asset.GetInstanceID()
                    && preview.GetIconRendererCount(id) > 0 && preview.GetIconModelBounds(id).size.sqrMagnitude > .000001f,
                    WeaponModelPreview.SourceKey(id) + "; renderers=" + preview.GetIconRendererCount(id) + "; " + preview.GetError(id));
                Add("icon_" + id.ToString("00") + "_captured_once", preview.GetIconRenderCount(id) == 1, "captures=" + preview.GetIconRenderCount(id));
                CaptureTexture(icon, id, "icon-" + id.ToString("00") + ".png", "actual preview icon");
            }
            Add("twelve_distinct_icon_textures", preview.IconCount == 12 && iconIds.Count == 12, "cached=" + preview.IconCount + "; distinct=" + iconIds.Count);

            foreach (int id in RepresentativeIds)
            {
                G.SelectWeaponForLibraryReview(id); yield return WaitReady(id, true, "selected_" + id.ToString("00"));
                if (!IsReady(id, true)) { Finish(); yield break; }
                Add("selected_" + id.ToString("00") + "_source", preview.SelectedSourceInstanceId == SourceAsset(id).GetInstanceID()
                    && preview.SelectedRendererCount > 0, WeaponModelPreview.SourceKey(id));
                InspectReadOnlyStage("selected_" + id.ToString("00"), preview);
                CaptureTexture(preview.SelectedTexture, id, "selected-" + id.ToString("00") + ".png", "actual selected-model render texture");
            }

            // Observe stable scope IDs as well as production counters; no global resource threshold.
            yield return null; yield return null;
            var stable = Owned(preview); var stableIds = Ids(stable);
            int builds = preview.SelectedModelBuildCount, renders = preview.TotalRenderCount;
            int selectedTextureId = preview.SelectedTexture.GetInstanceID();
            for (int repeat = 0; repeat < 4; repeat++)
            {
                G.SelectWeaponForLibraryReview(11); preview.Select(11);
                for (int id = 0; id < 12; id++) preview.GetIcon(id);
                yield return null;
            }
            Add("same_selection_reuses_owned_objects_and_textures", stableIds.SetEquals(Ids(Owned(preview)))
                && builds == preview.SelectedModelBuildCount && selectedTextureId == preview.SelectedTexture.GetInstanceID(),
                "scope objects=" + stable.Count + "; model builds=" + builds);
            Add("idle_preview_has_no_capture_work", renders == preview.TotalRenderCount && !preview.IsRendering
                && preview.ActivePreviewRendererCount == 0, "render requests=" + renders + " -> " + preview.TotalRenderCount);

            preview.RotateYaw(37); yield return WaitRenderAfter(renders);
            Add("manual_rotation_updates_existing_model", Mathf.Abs(preview.Yaw - 37) < .01f && preview.TotalRenderCount > renders
                && preview.SelectedModelBuildCount == builds && preview.SelectedTexture.GetInstanceID() == selectedTextureId,
                "yaw=" + preview.Yaw + "; textures/model identity retained");
            renders = preview.TotalRenderCount; preview.ResetView(); yield return WaitRenderAfter(renders);
            Add("reset_view_restores_rotation", Mathf.Abs(preview.Yaw) < .01f && preview.TotalRenderCount > renders, "yaw=" + preview.Yaw);
            renders = preview.TotalRenderCount; float yaw = preview.Yaw; preview.AutoRotate = true;
            yield return new WaitForSecondsRealtime(.2f); preview.AutoRotate = false; yield return null;
            Add("auto_rotation_changes_view_without_rebuild", Mathf.Abs(Mathf.DeltaAngle(yaw, preview.Yaw)) > .1f
                && preview.TotalRenderCount > renders && preview.SelectedModelBuildCount == builds, "yaw=" + preview.Yaw + "; no FPS inference");
            InspectReadOnlyStage("idle_after_controls", preview);
            Add("browsing_preserves_equipment_ammo_profile_and_balance", weaponBefore == G.player.weapon
                && Equal(ammoBefore, G.player.ammo) && Equal(reserveBefore, G.player.reserve) && Equal(equipmentBefore, G.player.EquippedWeapons)
                && balanceBefore == JsonUtility.ToJson(G.config) && profileBefore == JsonUtility.ToJson(G.expeditionStore.Profile),
                "Previewing locked weapons and rotating models must not equip/unlock/fire/save or edit configuration");

            // Normal UI transition must destroy this component's resources, rather than just hide them.
            var leaving = Owned(preview); G.SetExpeditionTabForReview(0);
            yield return null; yield return null; yield return null;
            ObserveRelease("leave_weapon_tab", leaving);
            Add("tab_exit_clears_component_ownership", !preview.Visible && !preview.HasOwnedResources && preview.IconCount == 0 && !preview.SelectedTexture, "Component may remain; owned textures and stage may not");
            G.SetExpeditionTabForReview(1); G.SelectWeaponForLibraryReview(0); yield return WaitReady(0, true, "reenter");
            if (!IsReady(0, true)) { Finish(); yield break; }
            Add("tab_reentry_reuses_component_with_fresh_resources", Preview == preview && !stableIds.Overlaps(Ids(Owned(preview))), "Prior stage/texture instances were not resurrected");

            // Independently exercise the real OnDestroy path while closing the tab.
            var destroying = Owned(preview); G.SetExpeditionTabForReview(0); Destroy(preview);
            yield return null; yield return null; yield return null;
            ObserveRelease("component_destroy", destroying);
            Add("component_native_destroyed", !preview, "Unity native component no longer available");
            G.SetExpeditionTabForReview(1); G.SelectWeaponForLibraryReview(0); yield return WaitReady(0, true, "new_component");
            if (!IsReady(0, true)) { Finish(); yield break; }
            preview = Preview;
            Add("shared_meshes_materials_survive_disposal", AllAlive(sharedAssets), "shared references=" + sharedAssets.Count + "; these assets are not owned by the preview");

            // Screen-space GUI is captured only when separately requested in an actual foreground run.
            if (report.fullGuiCaptureRequested && !Application.isBatchMode) yield return CaptureGui("weapon-tab-gui.png");
            bool beganChapter = G.BeginExpedition(0);
            Add("inventory_fixture_begins_real_chapter", beganChapter, "Only chapter-1 UI state setup; no combat completion claim");
            if (beganChapter)
            {
                G.expeditionInventory = true; G.SetState(RunState.Paused); yield return null; yield return null;
                Add("inventory_uses_equipped_weapon_icons", preview.Visible && preview.GetIcon(G.player.EquippedWeapons[0])
                    && preview.GetIcon(G.player.EquippedWeapons[1]) && !preview.AutoRotate, "Explicit inventory UI-state fixture; not keyboard/mouse usability evidence");
                InspectReadOnlyStage("inventory_idle", preview);
                if (report.fullGuiCaptureRequested && !Application.isBatchMode) yield return CaptureGui("inventory-gui.png");
                var inventoryScope = Owned(preview); G.expeditionInventory = false; G.SetState(RunState.Playing);
                yield return null; yield return null; yield return null;
                ObserveRelease("inventory_close", inventoryScope);
                Add("inventory_exit_releases_owned_resources", !preview.Visible && !preview.HasOwnedResources, "Closing inventory returns to gameplay without preview rendering work");
                G.FinishExpedition(false); G.OpenExpeditionBoard(); G.SetExpeditionTabForReview(0);
            }
            int iconsSaved = report.frames.FindAll(frame => frame.kind == "actual preview icon").Count;
            int selectedSaved = report.frames.FindAll(frame => frame.kind == "actual selected-model render texture").Count;
            Add("required_preview_frames_saved", iconsSaved == 12 && selectedSaved == 6, "icons=" + iconsSaved + "; selected=" + selectedSaved);
            if (report.fullGuiCaptureRequested && !Application.isBatchMode)
            {
                report.fullGuiCaptureCompleted = report.frames.FindAll(frame => frame.includesScreenSpaceGui).Count == 2;
                report.fullGuiCapture = report.fullGuiCaptureCompleted ? "captured_for_human_review" : "incomplete";
                Add("explicit_gui_frames_saved", report.fullGuiCaptureCompleted, "Real ScreenCapture after EndOfFrame; no automated layout/aesthetic approval");
            }
            report.complete = true; Finish();
        }

        bool IsReady(int id, bool allIcons) => Preview && Preview.Visible && Preview.SelectedId == id && Preview.RenderedId == id
            && Preview.SelectedTexture && string.IsNullOrEmpty(Preview.SelectedError) && (!allIcons || Preview.IconCount == 12);
        IEnumerator WaitReady(int id, bool allIcons, string label)
        {
            float deadline = Time.realtimeSinceStartup + 20;
            while (!IsReady(id, allIcons) && Time.realtimeSinceStartup < deadline) yield return null;
            Add(label + "_preview_ready", IsReady(id, allIcons), Preview
                ? "selected=" + Preview.SelectedId + "; rendered=" + Preview.RenderedId + "; icons=" + Preview.IconCount + "; error=" + Preview.SelectedError + IconErrors(Preview)
                : "DemoGame UI preview component is missing");
        }
        static string IconErrors(WeaponModelPreview preview)
        {
            string errors = "";
            for (int id = 0; id < 12; id++)
                if (!string.IsNullOrEmpty(preview.GetError(id))) errors += "; icon " + id + ": " + preview.GetError(id);
            return errors;
        }
        IEnumerator WaitRenderAfter(int count)
        { float deadline = Time.realtimeSinceStartup + 3; while (Preview && Preview.TotalRenderCount <= count && Time.realtimeSinceStartup < deadline) yield return null; }
        static bool IsBelow(string file, string root) => Path.GetFullPath(file).StartsWith(Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        static bool Equal(int[] a, int[] b)
        { if (a.Length != b.Length) return false; for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false; return true; }
        GameObject SourceAsset(int id) => id == 0 ? G.config.models.carbine : id == 1 ? G.config.models.shotgun : Resources.Load<GameObject>("Weapons/weapon_" + id.ToString("00"));

        void RecordStats()
        {
            foreach (var entry in WeaponDisplayStats.BuildAll(G.config))
            {
                var record = new Stats { weapon = entry.WeaponId, name = entry.Name, scope = entry.ScopeNote,
                    magazine = entry.Magazine, pellets = entry.Pellets, baseReserve = entry.BaseReserve };
                foreach (var row in entry.Rows)
                    record.rows.Add(new StatRow { label = row.Label, valueText = row.ValueText, directionHint = row.DirectionHint,
                        value = row.Value, maximum = row.Maximum, normalized = row.Normalized, isApplicable = row.IsApplicable });
                report.stats.Add(record);
            }
            Add("serializable_twelve_six_axis_snapshot", report.stats.Count == 12 && report.stats.TrueForAll(item => item.rows.Count == 6), "Explicit value DTOs; no empty JsonUtility serialization of properties");
        }
        void RememberSharedAssets(GameObject asset)
        {
            if (!asset) return;
            foreach (var renderer in asset.GetComponentsInChildren<Renderer>(true))
            {
                foreach (var material in renderer.sharedMaterials) if (material && !sharedAssets.Contains(material)) sharedAssets.Add(material);
                Mesh mesh = renderer is SkinnedMeshRenderer skin ? skin.sharedMesh : renderer.GetComponent<MeshFilter>() ? renderer.GetComponent<MeshFilter>().sharedMesh : null;
                if (mesh && !sharedAssets.Contains(mesh)) sharedAssets.Add(mesh);
            }
        }
        static bool AllAlive(List<Object> objects) { foreach (var item in objects) if (!item) return false; return objects.Count > 0; }
        static List<Object> Owned(WeaponModelPreview preview)
        {
            var objects = new List<Object>();
            if (preview.OwnedStageForReview)
                foreach (var t in preview.OwnedStageForReview.GetComponentsInChildren<Transform>(true)) objects.Add(t.gameObject);
            if (preview.IconTargetForReview) objects.Add(preview.IconTargetForReview);
            if (preview.SelectedTexture) objects.Add(preview.SelectedTexture);
            for (int id = 0; id < 12; id++) if (preview.GetIcon(id)) objects.Add(preview.GetIcon(id));
            return objects;
        }
        static HashSet<int> Ids(List<Object> objects)
        { var ids = new HashSet<int>(); foreach (var item in objects) if (item) ids.Add(item.GetInstanceID()); return ids; }
        void ObserveRelease(string scope, List<Object> objects)
        {
            var observation = new Release { scope = scope, tracked = objects.Count };
            foreach (var item in objects)
            {
                if (!item) observation.destroyed++;
                else if (item is GameObject go && !go.activeInHierarchy) observation.inactive++;
                else observation.alive++;
            }
            report.releases.Add(observation);
            Add(scope + "_owned_native_objects_destroyed", observation.tracked > 14 && observation.destroyed == observation.tracked,
                "tracked=" + observation.tracked + "; destroyed=" + observation.destroyed + "; merelyInactive=" + observation.inactive + "; alive=" + observation.alive);
        }
        void InspectReadOnlyStage(string label, WeaponModelPreview preview)
        {
            GameObject stage = preview.OwnedStageForReview, selected = preview.SelectedModelForReview;
            bool pure = selected;
            if (selected)
                foreach (var component in selected.GetComponentsInChildren<Component>(true))
                    pure &= component is Transform || component is MeshFilter || component is MeshRenderer || component is SkinnedMeshRenderer;
            Add(label + "_no_combat_physics_animation", pure, "Selected hierarchy contains render-only copies, without prefab behaviours or collision");
            bool quiet = stage && !preview.IsRendering && preview.ActivePreviewRendererCount == 0;
            if (stage)
            {
                foreach (var camera in stage.GetComponentsInChildren<Camera>(true)) quiet &= !camera.enabled && camera.cullingMask == (1 << preview.PreviewLayerForReview);
                foreach (var light in stage.GetComponentsInChildren<Light>(true)) quiet &= !light.enabled && light.cullingMask == (1 << preview.PreviewLayerForReview);
                foreach (var t in stage.GetComponentsInChildren<Transform>(true))
                    if (t != stage.transform) quiet &= t.gameObject.layer == preview.PreviewLayerForReview;
            }
            Add(label + "_isolated_idle_render_stage", quiet, "Camera/light/renderers inactive between explicit render requests; dedicated layer only");
        }

        void CaptureTexture(Texture source, int id, string filename, string kind)
        {
            RenderTexture temporary = null, previous = RenderTexture.active; Texture2D pixels = null;
            try
            {
                if (!source) throw new InvalidOperationException("Preview texture is missing");
                // Production icons can discard CPU pixels. This QA-owned readback does not change that contract.
                temporary = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32);
                Graphics.Blit(source, temporary); RenderTexture.active = temporary;
                pixels = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
                pixels.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0); pixels.Apply(false, false);
                SavePixels(pixels, id, filename, kind, false);
            }
            catch (Exception error) { Add(filename + "_capture", false, error.ToString()); }
            finally
            {
                RenderTexture.active = previous;
                if (pixels) Destroy(pixels); if (temporary) RenderTexture.ReleaseTemporary(temporary);
            }
        }
        void SavePixels(Texture2D pixels, int id, string filename, string kind, bool fullGui)
        {
            var colors = pixels.GetPixels32(); Color32 background = colors[0]; int varied = 0, samples = 0;
            for (int i = 0; i < colors.Length; i += 7)
            {
                samples++;
                if (Math.Abs(colors[i].r - background.r) + Math.Abs(colors[i].g - background.g) + Math.Abs(colors[i].b - background.b) > 18) varied++;
            }
            byte[] bytes = pixels.EncodeToPNG(); string path = Path.Combine(output, filename);
            using (var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write)) file.Write(bytes, 0, bytes.Length);
            Add(filename + "_nonempty_pixels", varied > Mathf.Max(16, samples / 200) && bytes.Length > 512,
                "varied=" + varied + "/" + samples + "; png bytes=" + bytes.Length + "; nonempty pixels do not establish visual quality");
            report.frames.Add(new Frame { weapon = id, file = filename, kind = kind, source = fullGui ? "Real ScreenCapture after WaitForEndOfFrame" : WeaponModelPreview.SourceKey(id),
                width = pixels.width, height = pixels.height, variedPixels = varied, sampledPixels = samples, includesScreenSpaceGui = fullGui });
        }
        IEnumerator CaptureGui(string filename)
        {
            // Caller requires both an explicit switch and non-batch execution. Never entered during background validation.
            yield return new WaitForEndOfFrame(); Texture2D pixels = null;
            try { pixels = ScreenCapture.CaptureScreenshotAsTexture(); SavePixels(pixels, -1, filename, "full screen UI for human review", true); }
            catch (Exception error) { Add(filename + "_capture", false, error.ToString()); }
            finally { if (pixels) Destroy(pixels); }
        }
        void Finish()
        {
            if (done) return; done = true; StopAllCoroutines();
            report.passed = report.complete && report.checks.Count > 0 && report.checks.TrueForAll(item => item.passed);
            File.WriteAllText(Path.Combine(output, "weapon-library-report.json"), JsonUtility.ToJson(report, true));
            Debug.Log("WEAPON_LIBRARY_QA_RESULT=" + report.passed + ";checks=" + report.checks.Count + ";frames=" + report.frames.Count);
            Application.Quit(report.passed ? 0 : 1);
        }
        void OnDestroy() { Application.logMessageReceived -= OnLog; }
    }
}
