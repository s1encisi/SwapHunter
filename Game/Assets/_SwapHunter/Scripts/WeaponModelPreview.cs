using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace SwapHunter
{
    // Read-only visual copies of the real weapon meshes/materials. No prefab behaviours, physics or combat actors are instantiated.
    [DefaultExecutionOrder(500)]
    public sealed class WeaponModelPreview : MonoBehaviour
    {
        const int PreviewLayer = 30, PreviewMask = 1 << PreviewLayer;
        const int IconWidth = 256, IconHeight = 128, PreviewWidth = 768, PreviewHeight = 448;
        static readonly Vector3 StagePosition = new Vector3(1024, -256, -1024);
        static readonly Vector3 ViewDirection = new Vector3(3, .7f, 1.25f).normalized;
        ModelLibrary models;
        readonly Texture2D[] icons = new Texture2D[WeaponCatalog.Count];
        readonly string[] errors = new string[WeaponCatalog.Count];
        readonly Bounds[] iconBounds = new Bounds[WeaponCatalog.Count];
        readonly int[] iconRendererCounts = new int[WeaponCatalog.Count], sourceIds = new int[WeaponCatalog.Count], iconRenderCounts = new int[WeaponCatalog.Count];
        GameObject stage;
        Transform iconPivot, selectedPivot;
        GameObject iconModel, selectedModel;
        Renderer[] iconRenderers, selectedRenderers;
        Camera previewCamera;
        Light keyLight, fillLight;
        RenderTexture iconTarget, selectedTarget;
        Bounds selectedBounds;
        int selectedId, builtSelectedId = -1, renderedId = -1, nextIcon;
        bool visible, dirty;
        float yaw, nextLargeRender;
        string rendererError;

        public bool AutoRotate { get; set; }
        public bool Visible => visible;
        public bool IsRendering { get; private set; }
        public int SelectedId => selectedId;
        public int RenderedId => renderedId;
        public int TotalRenderCount { get; private set; }
        public int SelectedModelBuildCount { get; private set; }
        public float Yaw => yaw;
        public string SelectedError => !string.IsNullOrEmpty(rendererError) ? rendererError : errors[selectedId];
        public RenderTexture SelectedTexture => visible && renderedId == selectedId ? selectedTarget : null;
        public Bounds SelectedModelBounds => builtSelectedId == selectedId ? selectedBounds : default;
        public int SelectedRendererCount => builtSelectedId == selectedId && selectedRenderers != null ? selectedRenderers.Length : 0;
        public int SelectedSourceInstanceId => sourceIds[selectedId];
        public int IconCount { get { int count = 0; foreach (var icon in icons) if (icon) count++; return count; } }
        public bool HasOwnedResources => stage || iconTarget || selectedTarget || IconCount > 0;
        public int ActivePreviewRendererCount => ActiveCount(selectedRenderers) + ActiveCount(iconRenderers);
        internal GameObject OwnedStageForReview => stage;
        internal GameObject SelectedModelForReview => selectedModel;
        internal RenderTexture IconTargetForReview => iconTarget;
        internal int PreviewLayerForReview => PreviewLayer;

        public void Initialize(ModelLibrary library)
        {
            if (!library) throw new ArgumentNullException(nameof(library));
            if (models == library) return;
            ReleaseOwnedResources(); models = library; rendererError = null; dirty = true;
        }
        public void SetVisible(bool value)
        {
            if (visible == value) return;
            visible = value;
            if (value) { rendererError = null; dirty = true; nextLargeRender = 0; }
            // Release is executed once in Update, never repeatedly from OnGUI Layout/Repaint.
        }
        public void Select(int id)
        {
            if (!WeaponCatalog.Valid(id)) throw new ArgumentOutOfRangeException(nameof(id));
            if (selectedId == id) return;
            selectedId = id; yaw = 0; renderedId = -1; dirty = true; nextLargeRender = 0;
        }
        public void ResetView() { yaw = 0; dirty = true; nextLargeRender = 0; }
        public void RotateYaw(float degrees)
        {
            if (float.IsNaN(degrees) || float.IsInfinity(degrees)) throw new ArgumentOutOfRangeException(nameof(degrees));
            AutoRotate = false; yaw = Mathf.Repeat(yaw + degrees, 360); dirty = true; nextLargeRender = 0;
        }
        public Texture2D GetIcon(int id) => WeaponCatalog.Valid(id) ? icons[id] : null;
        public string GetError(int id) => WeaponCatalog.Valid(id) ? errors[id] : "武器编号无效";
        public Bounds GetIconModelBounds(int id) => WeaponCatalog.Valid(id) ? iconBounds[id] : default;
        public int GetIconRendererCount(int id) => WeaponCatalog.Valid(id) ? iconRendererCounts[id] : 0;
        public int GetIconRenderCount(int id) => WeaponCatalog.Valid(id) ? iconRenderCounts[id] : 0;
        public int GetSourceInstanceId(int id) => WeaponCatalog.Valid(id) ? sourceIds[id] : 0;
        public static string SourceKey(int id) => WeaponVisualFactory.SourceKey(id);

        void Update()
        {
            if (!visible) { if (HasOwnedResources) ReleaseOwnedResources(); return; }
            if (!models || !string.IsNullOrEmpty(rendererError)) return;
            try
            {
                EnsureStage();
                if (builtSelectedId != selectedId) BuildSelected();
                if (AutoRotate && selectedModel) { yaw = Mathf.Repeat(yaw + Time.unscaledDeltaTime * 12, 360); dirty = true; }
                // Selection is serviced first; users never see the previous weapon next to a new name.
                if (dirty && selectedModel && Time.unscaledTime >= nextLargeRender)
                {
                    selectedPivot.localRotation = Quaternion.Euler(0, yaw, 0);
                    Render(selectedPivot, selectedBounds, selectedTarget);
                    renderedId = selectedId; dirty = false; nextLargeRender = Time.unscaledTime + 1f / 20;
                }
                // At most one icon capture per frame, independent of the number of OnGUI events.
                while (nextIcon < WeaponCatalog.Count && (icons[nextIcon] || !string.IsNullOrEmpty(errors[nextIcon]))) nextIcon++;
                if (nextIcon < WeaponCatalog.Count) CaptureIcon(nextIcon++);
            }
            catch (Exception error)
            {
                rendererError = "武器预览暂不可用：" + error.Message;
                StopStageRendering(); Debug.LogError(rendererError);
            }
        }
        void EnsureStage()
        {
            if (stage) return;
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) throw new InvalidOperationException("需要真实图形设备");
            stage = new GameObject("Weapon UI render-only stage") { hideFlags = HideFlags.DontSave };
            stage.transform.position = StagePosition;
            iconPivot = Child(stage.transform, "Icon model pivot"); selectedPivot = Child(stage.transform, "Selected model pivot");
            iconPivot.gameObject.SetActive(false); selectedPivot.gameObject.SetActive(false);
            var cameraObject = Child(stage.transform, "Weapon preview camera").gameObject;
            previewCamera = cameraObject.AddComponent<Camera>(); previewCamera.enabled = false; previewCamera.tag = "Untagged";
            previewCamera.cullingMask = PreviewMask; previewCamera.clearFlags = CameraClearFlags.SolidColor;
            previewCamera.backgroundColor = new Color(.035f, .055f, .075f, 1); previewCamera.orthographic = true;
            previewCamera.useOcclusionCulling = false;
            previewCamera.nearClipPlane = .01f; previewCamera.farClipPlane = 20; previewCamera.allowHDR = false; previewCamera.allowMSAA = false;
            var data = previewCamera.GetUniversalAdditionalCameraData(); data.renderType = CameraRenderType.Base;
            data.renderPostProcessing = false; data.renderShadows = false; data.requiresDepthTexture = false; data.requiresColorTexture = false;
            keyLight = CreateLight("Preview key", new Color(1, .94f, .84f), 3.2f, Quaternion.Euler(38, -42, 0));
            fillLight = CreateLight("Preview fill", new Color(.54f, .74f, 1), 1.0f, Quaternion.Euler(22, 132, 0));
            iconTarget = Target("Weapon icon target", IconWidth, IconHeight); selectedTarget = Target("Selected weapon target", PreviewWidth, PreviewHeight);
        }
        Light CreateLight(string name, Color color, float intensity, Quaternion rotation)
        {
            var root = Child(stage.transform, name); root.localRotation = rotation;
            var light = root.gameObject.AddComponent<Light>(); light.type = LightType.Directional; light.cullingMask = PreviewMask;
            light.color = color; light.intensity = intensity; light.shadows = LightShadows.None; light.enabled = false; return light;
        }
        static Transform Child(Transform parent, string name)
        {
            var root = new GameObject(name) { hideFlags = HideFlags.DontSave }; root.layer = PreviewLayer; root.transform.SetParent(parent, false); return root.transform;
        }
        static RenderTexture Target(string name, int width, int height)
        {
            var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
            { name = name, hideFlags = HideFlags.DontSave, antiAliasing = 1, useMipMap = false, autoGenerateMips = false };
            if (!target.Create()) { Destroy(target); throw new InvalidOperationException("无法创建预览纹理"); }
            return target;
        }
        GameObject ResolveAsset(int id)
        {
            var asset = WeaponVisualFactory.ResolveAsset(id);
            if (!asset) throw new InvalidOperationException("缺少对应武器模型（" + SourceKey(id) + "）");
            sourceIds[id] = asset.GetInstanceID(); return asset;
        }
        void BuildSelected()
        {
            DropModel(ref selectedModel, ref selectedRenderers); selectedPivot.localRotation = Quaternion.identity;
            builtSelectedId = selectedId; renderedId = -1; selectedBounds = default;
            try
            {
                selectedModel = CopyModel(ResolveAsset(selectedId), selectedPivot, out selectedBounds, out selectedRenderers);
                SelectedModelBuildCount++;
                errors[selectedId] = null; dirty = true;
            }
            catch (Exception error) { errors[selectedId] = "该武器模型无法显示：" + error.Message; }
            finally { selectedPivot.gameObject.SetActive(false); }
        }
        void CaptureIcon(int id)
        {
            try
            {
                iconPivot.localRotation = Quaternion.identity;
                iconModel = CopyModel(ResolveAsset(id), iconPivot, out var bounds, out iconRenderers);
                Render(iconPivot, bounds, iconTarget);
                RenderTexture previous = RenderTexture.active;
                try
                {
                    RenderTexture.active = iconTarget;
                    var icon = new Texture2D(IconWidth, IconHeight, TextureFormat.RGBA32, false)
                    { name = "Actual weapon icon " + id.ToString("00"), hideFlags = HideFlags.DontSave, filterMode = FilterMode.Bilinear };
                    icons[id] = icon; icon.ReadPixels(new Rect(0, 0, IconWidth, IconHeight), 0, 0); icon.Apply(false, true);
                }
                finally { RenderTexture.active = previous; }
                iconBounds[id] = bounds; iconRendererCounts[id] = iconRenderers.Length; iconRenderCounts[id]++;
            }
            catch (Exception error) { errors[id] = "该武器图标无法显示：" + error.Message; if (icons[id]) { Destroy(icons[id]); icons[id] = null; } }
            finally { iconPivot.gameObject.SetActive(false); DropModel(ref iconModel, ref iconRenderers); }
        }
        GameObject CopyModel(GameObject asset, Transform pivot, out Bounds bounds, out Renderer[] renderers)
        {
            pivot.gameObject.SetActive(true);
            var map = new Dictionary<Transform, Transform>();
            var root = CopyTransforms(asset.transform, pivot, map, true);
            var visibleRenderers = new List<Renderer>(); var block = new MaterialPropertyBlock();
            try
            {
                foreach (var original in asset.GetComponentsInChildren<Renderer>(true))
                {
                    if (!map.TryGetValue(original.transform, out var clone)) continue;
                    Renderer copy;
                    if (original is SkinnedMeshRenderer skin)
                    {
                        if (!skin.sharedMesh) continue;
                        var target = clone.gameObject.AddComponent<SkinnedMeshRenderer>(); target.sharedMesh = skin.sharedMesh;
                        var originalBones = skin.bones; var bones = new Transform[originalBones.Length];
                        for (int i = 0; i < bones.Length; i++)
                        {
                            if (!originalBones[i]) continue;
                            if (!map.TryGetValue(originalBones[i], out bones[i])) throw new InvalidOperationException("模型骨骼超出武器资源层级");
                        }
                        target.bones = bones; if (skin.rootBone && map.TryGetValue(skin.rootBone, out var rootBone)) target.rootBone = rootBone;
                        target.localBounds = skin.localBounds; target.updateWhenOffscreen = true;
                        for (int i = 0; i < skin.sharedMesh.blendShapeCount; i++) target.SetBlendShapeWeight(i, skin.GetBlendShapeWeight(i));
                        copy = target;
                    }
                    else if (original is MeshRenderer)
                    {
                        var filter = original.GetComponent<MeshFilter>(); if (!filter || !filter.sharedMesh) continue;
                        clone.gameObject.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh; copy = clone.gameObject.AddComponent<MeshRenderer>();
                    }
                    else continue; // No particles, weapon effects, animation, audio, colliders or gameplay components.
                    copy.sharedMaterials = original.sharedMaterials; copy.enabled = original.enabled;
                    copy.shadowCastingMode = ShadowCastingMode.Off; copy.receiveShadows = false;
                    copy.lightProbeUsage = LightProbeUsage.Off; copy.reflectionProbeUsage = ReflectionProbeUsage.Off;
                    original.GetPropertyBlock(block); copy.SetPropertyBlock(block); block.Clear();
                    if (copy.enabled && copy.gameObject.activeInHierarchy) visibleRenderers.Add(copy);
                }
                if (visibleRenderers.Count == 0) throw new InvalidOperationException("武器资源没有可见网格");
                Bounds world = visibleRenderers[0].bounds; for (int i = 1; i < visibleRenderers.Count; i++) world.Encapsulate(visibleRenderers[i].bounds);
                if (world.size.sqrMagnitude < .000001f) throw new InvalidOperationException("武器网格包围盒无效");
                root.position -= world.center - StagePosition;
                bounds = new Bounds(Vector3.zero, world.size); renderers = visibleRenderers.ToArray(); return root.gameObject;
            }
            catch { root.gameObject.SetActive(false); Destroy(root.gameObject); throw; }
        }
        Transform CopyTransforms(Transform original, Transform parent, Dictionary<Transform, Transform> map, bool isRoot)
        {
            Transform clone = Child(parent, original.name); map.Add(original, clone);
            // Match the new factory exactly; never apply the old ModelLibrary mirror to new resources.
            clone.localPosition = original.localPosition;
            clone.localRotation = original.localRotation;
            clone.localScale = original.localScale; // New asset carries its own export/import convention.
            clone.gameObject.SetActive(isRoot || original.gameObject.activeSelf);
            foreach (Transform child in original) CopyTransforms(child, clone, map, false);
            return clone;
        }
        void Render(Transform pivot, Bounds bounds, RenderTexture target)
        {
            float horizontalRadius = new Vector2(bounds.extents.x, bounds.extents.z).magnitude;
            float verticalRadius = bounds.extents.y * Mathf.Sqrt(1 - ViewDirection.y * ViewDirection.y) + horizontalRadius * Mathf.Abs(ViewDirection.y);
            previewCamera.aspect = target.width / (float)target.height;
            previewCamera.orthographicSize = Mathf.Max(.08f, Mathf.Max(verticalRadius, horizontalRadius / previewCamera.aspect) * 1.18f);
            float distance = Mathf.Max(2, bounds.extents.magnitude * 5);
            previewCamera.transform.position = StagePosition + ViewDirection * distance; previewCamera.transform.LookAt(StagePosition, Vector3.up);
            previewCamera.farClipPlane = distance + bounds.extents.magnitude * 3 + 1;
            var request = new RenderPipeline.StandardRequest { destination = target };
            if (!RenderPipeline.SupportsRenderRequest(previewCamera, request)) throw new InvalidOperationException("当前渲染管线不支持离屏预览");
            try
            {
                IsRendering = true; iconPivot.gameObject.SetActive(pivot == iconPivot); selectedPivot.gameObject.SetActive(pivot == selectedPivot);
                keyLight.enabled = fillLight.enabled = true;
                RenderPipeline.SubmitRenderRequest(previewCamera, request); TotalRenderCount++;
            }
            finally { StopStageRendering(); }
        }
        void StopStageRendering()
        {
            if (iconPivot) iconPivot.gameObject.SetActive(false); if (selectedPivot) selectedPivot.gameObject.SetActive(false);
            if (keyLight) keyLight.enabled = false; if (fillLight) fillLight.enabled = false;
            if (previewCamera) previewCamera.enabled = false; IsRendering = false;
        }
        static int ActiveCount(Renderer[] renderers)
        { int count = 0; if (renderers != null) foreach (var renderer in renderers) if (renderer && renderer.enabled && renderer.gameObject.activeInHierarchy) count++; return count; }
        static void DropModel(ref GameObject model, ref Renderer[] renderers)
        { if (model) { model.SetActive(false); Destroy(model); } model = null; renderers = null; }
        void ReleaseOwnedResources()
        {
            StopStageRendering(); DropModel(ref iconModel, ref iconRenderers); DropModel(ref selectedModel, ref selectedRenderers);
            if (stage) { stage.SetActive(false); Destroy(stage); } stage = null; previewCamera = null; keyLight = fillLight = null; iconPivot = selectedPivot = null;
            if (iconTarget) { iconTarget.Release(); Destroy(iconTarget); } iconTarget = null;
            if (selectedTarget) { selectedTarget.Release(); Destroy(selectedTarget); } selectedTarget = null;
            for (int i = 0; i < icons.Length; i++) { if (icons[i]) Destroy(icons[i]); icons[i] = null; errors[i] = null; iconBounds[i] = default; iconRendererCounts[i] = sourceIds[i] = 0; }
            nextIcon = 0; builtSelectedId = renderedId = -1; selectedBounds = default;
        }
        void OnDisable() { visible = false; ReleaseOwnedResources(); }
        void OnDestroy() { ReleaseOwnedResources(); }
    }
}
