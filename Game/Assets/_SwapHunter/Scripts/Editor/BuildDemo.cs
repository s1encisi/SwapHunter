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
        public static void Build()
        {
            try
            {
                Directory.CreateDirectory("Assets/_SwapHunter/Data"); Directory.CreateDirectory("Assets/_SwapHunter/Scenes"); AssetDatabase.Refresh();
                const string configPath = "Assets/_SwapHunter/Data/DemoConfig.asset";
                DemoConfig config = AssetDatabase.LoadAssetAtPath<DemoConfig>(configPath);
                if (!config) { config = ScriptableObject.CreateInstance<DemoConfig>(); AssetDatabase.CreateAsset(config, configPath); }
                config.models = AssetImportAudit.Prepare(); BlenderAssetAudit.Prepare(config.models);
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
                QualitySettings.SetQualityLevel(2, false); QualitySettings.vSyncCount = 1;
                EditorUtility.SetDirty(pipeline);
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                GameObject boot = new GameObject("SwapHunter · Demo director"); boot.AddComponent<DemoGame>().config = config;
                const string scenePath = "Assets/_SwapHunter/Scenes/SwapHunter.unity";
                EditorSceneManager.SaveScene(scene, scenePath); EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(scenePath, true) };
                PlayerSettings.companyName = "SwapHunter"; PlayerSettings.productName = "SwapHunter"; PlayerSettings.bundleVersion = "0.5.2";
                PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
                PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64, false);
                PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64, new[] { GraphicsDeviceType.Direct3D11 });
                PlayerSettings.fullScreenMode = FullScreenMode.Windowed; PlayerSettings.defaultScreenWidth = 1280; PlayerSettings.defaultScreenHeight = 720;
                PlayerSettings.runInBackground = true; PlayerSettings.resizableWindow = true;
                AssetDatabase.SaveAssets();
                string[] args = Environment.GetCommandLineArgs(); int outputIndex = Array.IndexOf(args,"-buildOutput");
                string output = Path.GetFullPath(outputIndex>=0 && outputIndex+1<args.Length ? args[outputIndex+1] : "../Builds/SwapHunter-v0.5.2/SwapHunter.exe"); Directory.CreateDirectory(Path.GetDirectoryName(output));
                bool development = Array.IndexOf(Environment.GetCommandLineArgs(), "-development") >= 0;
                BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = new[] { scenePath }, locationPathName = output, target = BuildTarget.StandaloneWindows64, options = development ? BuildOptions.Development : BuildOptions.None });
                Debug.Log("SWAPHUNTER_BUILD=" + report.summary.result + "; bytes=" + report.summary.totalSize);
                UnityEditor.EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 1);
            }
            catch (Exception error) { Debug.LogException(error); UnityEditor.EditorApplication.Exit(1); }
        }
    }
}



