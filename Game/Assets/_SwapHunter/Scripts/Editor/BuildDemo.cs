using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace SwapHunter.Editor
{
    public static class BuildDemo
    {
        public const string Version = "0.6.0";
        const string ScenePath = "Assets/_SwapHunter/Scenes/SwapHunter.unity";

        // Builds the authored project as it is. Regeneration is an explicit Prepare step.
        public static void Build()
        {
            try
            {
                if (PlayerSettings.bundleVersion != Version)
                    throw new InvalidOperationException("Project version must be " + Version + ". Update ProjectSettings before building.");
                MissionCatalogEditor.ValidateCatalog();
                if (!AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath))
                    throw new InvalidOperationException("Entry scene is missing. Restore it or explicitly run Tools/Prepare-Demo-v06.ps1.");
                var config = AssetDatabase.LoadAssetAtPath<DemoConfig>("Assets/_SwapHunter/Data/DemoConfig.asset");
                if (!config || !config.models || !config.uiFont)
                    throw new InvalidOperationException("Prepared DemoConfig/model library/font is missing. Run Prepare explicitly after reviewing source changes.");
                string[] args = Environment.GetCommandLineArgs();
                int outputIndex = Array.IndexOf(args, "-buildOutput");
                string output = Path.GetFullPath(outputIndex >= 0 && outputIndex + 1 < args.Length
                    ? args[outputIndex + 1] : "../Builds/SwapHunter-v0.6.0/SwapHunter.exe");
                string directory = Path.GetDirectoryName(output);
                if (Directory.Exists(directory) && Directory.GetFileSystemEntries(directory).Length > 0)
                    throw new IOException("Existing build preserved. Choose a new empty output directory: " + directory);
                Directory.CreateDirectory(directory);
                bool development = Array.IndexOf(args, "-development") >= 0;
                BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { ScenePath }, locationPathName = output,
                    target = BuildTarget.StandaloneWindows64,
                    options = development ? BuildOptions.Development : BuildOptions.None
                });
                Debug.Log("SWAPHUNTER_BUILD=" + report.summary.result + "; version=" + Version + "; bytes=" + report.summary.totalSize);
                EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 1);
            }
            catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
        }

        // Explicit, source-writing operation. Never called by Build().
        [MenuItem("SwapHunter/Prepare project assets and scene (writes source)")]
        public static void Prepare()
        {
            try
            {
                string[] args = Environment.GetCommandLineArgs();
                int at = Array.IndexOf(args, "-prepareEvidence");
                string evidenceDirectory = at >= 0 && at + 1 < args.Length ? Path.GetFullPath(args[at + 1])
                    : Path.GetFullPath("../Logs/V06/prepare-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmssfff") + "-" + Guid.NewGuid().ToString("N").Substring(0, 8));
                Directory.CreateDirectory(evidenceDirectory);
                Debug.Log("SWAPHUNTER_PREPARE_EVIDENCE=" + evidenceDirectory);
                PrepareAssetsAndScene(evidenceDirectory);
                Debug.Log("SWAPHUNTER_PREPARE=Succeeded; version=" + Version);
                if (Application.isBatchMode) EditorApplication.Exit(0);
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                if (Application.isBatchMode) EditorApplication.Exit(1);
                else throw;
            }
        }

        static void PrepareAssetsAndScene(string evidenceDirectory)
        {
            if (!File.Exists(MissionCatalogEditor.AssetPath)) MissionCatalogEditor.PrepareDefaultConfiguration();
            else MissionCatalogEditor.ValidateCatalog();
            Directory.CreateDirectory("Assets/_SwapHunter/Data"); Directory.CreateDirectory("Assets/_SwapHunter/Scenes"); AssetDatabase.Refresh();
            const string configPath = "Assets/_SwapHunter/Data/DemoConfig.asset";
            DemoConfig config = AssetDatabase.LoadAssetAtPath<DemoConfig>(configPath);
            if (!config) { config = ScriptableObject.CreateInstance<DemoConfig>(); AssetDatabase.CreateAsset(config, configPath); }
            config.models = AssetImportAudit.Prepare(evidenceDirectory); BlenderAssetAudit.Prepare(config.models, evidenceDirectory);
            if (config.tuningVersion < 2) { config.walkSpeed = 4.3f; config.sprintSpeed = 6.2f; config.jumpHeight = 1.0f; config.tuningVersion = 2; }
            if(config.tuningVersion<5){config.swapCooldown=3.5f;config.tuningVersion=5;}
            const string uiFontPath = "Assets/_SwapHunter/Fonts/NotoSansSC-Regular.otf";
            var fontImporter = AssetImporter.GetAtPath(uiFontPath) as TrueTypeFontImporter;
            if (fontImporter && (!fontImporter.includeFontData || fontImporter.fontTextureCase != FontTextureCase.Dynamic))
            {
                fontImporter.includeFontData = true; fontImporter.fontTextureCase = FontTextureCase.Dynamic; fontImporter.SaveAndReimport();
            }
            config.uiFont = AssetDatabase.LoadAssetAtPath<Font>(uiFontPath);
            if (!config.uiFont) throw new InvalidOperationException("Bundled Chinese UI font is missing");
            Color[] colors = {
                new Color(.085f,.12f,.15f), new Color(.16f,.23f,.28f), new Color(.57f,.38f,.22f), new Color(.25f,.31f,.35f),
                new Color(.1f,.85f,.77f), new Color(1,.53f,.16f), new Color(.98f,.18f,.12f), Color.white,
                new Color(.07f,.09f,.12f), new Color(.18f,.92f,.83f,.22f), new Color(.23f,.92f,.52f), new Color(.1f,.20f,.30f), new Color(.52f,.30f,.12f)
            };
            config.materials = new Material[colors.Length];
            for (int i = 0; i < colors.Length; i++)
            {
                string path = "Assets/_SwapHunter/Data/Material-" + i + ".mat";
                Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
                bool unlit = i == 4 || i == 5 || i == 6 || i == 7 || i == 9 || i == 10;
                Shader shader = Shader.Find(unlit ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit");
                if (!material) { material = new Material(shader); AssetDatabase.CreateAsset(material, path); }
                material.shader = shader; material.SetColor("_BaseColor", colors[i]); material.enableInstancing = true;
                if (!unlit) { material.SetFloat("_Smoothness", .28f); material.SetFloat("_Metallic", .3f); }
                if (i == 9)
                {
                    material.SetFloat("_Surface", 1); material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha); material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                    material.SetFloat("_ZWrite", 0); material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); material.renderQueue = (int)RenderQueue.Transparent;
                }
                EditorUtility.SetDirty(material); config.materials[i] = material;
            }
            EditorUtility.SetDirty(config);
            const string fontPath = "Assets/_SwapHunter/Data/WorldFont.mat";
            config.worldTextMaterial = AssetDatabase.LoadAssetAtPath<Material>(fontPath);
            if (!config.worldTextMaterial) { config.worldTextMaterial = new Material(Shader.Find("SwapHunter/WorldText")); AssetDatabase.CreateAsset(config.worldTextMaterial, fontPath); }
            const string floorPath = "Assets/_SwapHunter/Data/DeckTexture.asset";
            Texture2D deck = AssetDatabase.LoadAssetAtPath<Texture2D>(floorPath);
            if (!deck)
            {
                deck = new Texture2D(128, 128, TextureFormat.RGB24, true); deck.name = "Deck panel texture";
                Color[] pixels = new Color[128 * 128]; var noise = new System.Random(15);
                for (int y = 0; y < 128; y++) for (int x = 0; x < 128; x++) { float v = x % 32 < 1 || y % 32 < 1 ? .70f : .94f + (float)noise.NextDouble() * .06f; pixels[y * 128 + x] = new Color(v, v, v); }
                deck.SetPixels(pixels); deck.Apply(); deck.wrapMode = TextureWrapMode.Repeat; deck.anisoLevel = 4; AssetDatabase.CreateAsset(deck, floorPath);
            }
            config.materials[0].SetTexture("_BaseMap", deck); config.materials[0].SetTextureScale("_BaseMap", new Vector2(8, 8));
            UniversalRenderPipelineAsset pipeline = null;
            foreach (string guid in AssetDatabase.FindAssets("t:UniversalRenderPipelineAsset"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid); var candidate = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);
                if (!pipeline || path.Contains("PC_")) pipeline = candidate;
            }
            if (!pipeline) throw new InvalidOperationException("URP pipeline is missing");
            pipeline.renderScale = 1; pipeline.msaaSampleCount = 4; pipeline.shadowDistance = 65;
            GraphicsSettings.defaultRenderPipeline = pipeline;
            for (int level = 0; level < QualitySettings.names.Length; level++) { QualitySettings.SetQualityLevel(level, false); QualitySettings.renderPipeline = pipeline; }
            int pcQuality = Array.IndexOf(QualitySettings.names, "PC");
            if (pcQuality < 0) throw new InvalidOperationException("Prepare requires the PC quality profile");
            QualitySettings.SetQualityLevel(pcQuality, false); QualitySettings.vSyncCount = 1;
            EditorUtility.SetDirty(pipeline);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GameObject boot = new GameObject("SwapHunter · Demo director"); boot.AddComponent<DemoGame>().config = config;
            const string scenePath = "Assets/_SwapHunter/Scenes/SwapHunter.unity";
            EditorSceneManager.SaveScene(scene, scenePath); EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(scenePath, true) };
            PlayerSettings.companyName = "SwapHunter"; PlayerSettings.productName = "SwapHunter"; PlayerSettings.bundleVersion = Version;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64, new[] { GraphicsDeviceType.Direct3D11 });
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed; PlayerSettings.defaultScreenWidth = 1280; PlayerSettings.defaultScreenHeight = 720;
            PlayerSettings.runInBackground = true; PlayerSettings.resizableWindow = true;
            AssetDatabase.SaveAssets();
        }
    }
}
