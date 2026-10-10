#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace SwapHunter
{
    public static class MissionCatalogEditor
    {
        public const string AssetPath = "Assets/_SwapHunter/Resources/MissionConfiguration.asset";

        [MenuItem("SwapHunter/v0.6/准备默认任务配置（仅首次创建）")]
        public static void PrepareDefaultConfiguration()
        {
            try
            {
                if (File.Exists(AssetPath))
                {
                    var existing = AssetDatabase.LoadAssetAtPath<MissionConfiguration>(AssetPath);
                    if (!existing) throw new InvalidOperationException("现有任务配置无法读取；原文件保留，不会覆盖：" + AssetPath);
                    existing.Validate();
                    MissionCatalog.ResetCache();
                    Debug.Log("任务配置已存在并通过校验，保留全部用户编辑：" + AssetPath);
                    return;
                }
                Directory.CreateDirectory(Path.GetDirectoryName(AssetPath));
                AssetDatabase.Refresh();
                var asset = ScriptableObject.CreateInstance<MissionConfiguration>();
                asset.missions = MissionCatalog.CreateDefaultDefinitions();
                try { asset.Validate(); }
                catch { UnityEngine.Object.DestroyImmediate(asset); throw; }
                AssetDatabase.CreateAsset(asset, AssetPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.ImportAsset(AssetPath, ImportAssetOptions.ForceSynchronousImport);
                MissionCatalog.ResetCache();
                ValidateCatalog();
                Debug.Log("首次创建十二关任务配置成功：" + AssetPath);
            }
            catch (Exception error)
            {
                Debug.LogError("准备任务配置失败，未使用默认值掩盖错误：" + error.Message);
                throw; // executeMethod/batch callers receive a real failure.
            }
        }

        [MenuItem("SwapHunter/v0.6/验证任务配置")]
        public static void ValidateCatalog()
        {
            try
            {
                var asset = AssetDatabase.LoadAssetAtPath<MissionConfiguration>(AssetPath);
                if (!asset) throw new InvalidOperationException("缺少或无法读取任务配置：" + AssetPath + "。请先显式运行准备默认任务配置。");
                asset.Validate();
                MissionCatalog.ResetCache();
                // Verify the actual Resources lookup too, not just a detached asset.
                MissionCatalog.Validate();
                if (MissionCatalog.Configuration != asset) throw new InvalidOperationException("Resources 下存在同名任务配置冲突，运行时读取的不是规范路径资产。");
                foreach (var definition in asset.missions)
                    Debug.Log("任务配置 第" + (definition.level + 1) + "关 | 总预算=" + definition.TotalBudget
                        + " | 指定必清=" + definition.RequiredBudget + " | 主目标=" + definition.requiredObjectives
                        + "/" + definition.objectives.Length + " | 最少Q=" + definition.minimumSwaps
                        + " | 出口=" + definition.extraction);
                Debug.Log("十二关任务配置全部通过校验。普通构建只读取该资产，不重建配置。");
            }
            catch (Exception error)
            {
                Debug.LogError("任务配置验证失败：" + error.Message);
                throw;
            }
        }
    }
}
#endif
