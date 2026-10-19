using System;
using System.IO;
using UnityEngine;

namespace SwapHunter
{
    // Validation runs must never save over a player who is trying the demo concurrently.
    public static class RunStorage
    {
        static readonly string[] Arguments = Environment.GetCommandLineArgs();
        public static readonly bool IsDirectedTrial = Array.IndexOf(Arguments, "-swapHunterTrial") >= 0;
        public static readonly bool IsSynergyValidation = Array.IndexOf(Arguments, "-swapHunterSynergyQA") >= 0;
        public static readonly bool IsModelsValidation = Array.IndexOf(Arguments, "-swapHunterModelsFinalQA") >= 0;
        public static readonly bool IsUiValidation = Array.IndexOf(Arguments, "-swapHunterTacticalUIQA") >= 0;
        public static readonly bool IsFullValidation = Array.IndexOf(Arguments, "-swapHunterFullQA") >= 0;
        public static readonly bool IsStoryD2Validation = Array.IndexOf(Arguments, "-swapHunterStoryD2QA") >= 0;
        public static readonly bool IsFocusValidation=Array.IndexOf(Arguments,"-swapHunterFocusQA")>=0;
        public static readonly bool IsMapV2Validation=Array.IndexOf(Arguments,"-swapHunterMapV2QA")>=0;
        public static readonly bool IsTacticalValidation = IsFocusValidation || IsMapV2Validation || IsStoryD2Validation || IsSynergyValidation || IsUiValidation || IsFullValidation || Array.IndexOf(Arguments, "-swapHunterT01QA") >= 0;
        public static readonly bool IsValidation = IsModelsValidation || IsTacticalValidation || Array.IndexOf(Arguments, "-swapHunterWeaponLibraryQA") >= 0 || Array.IndexOf(Arguments, "-swapHunterV06LifecycleQA") >= 0 || Array.IndexOf(Arguments, "-swapHunterV06PoolQA") >= 0 || Array.IndexOf(Arguments, "-swapHunterV06BossEscapeQA") >= 0 || Array.IndexOf(Arguments, "-swapHunterV06AllocationQA") >= 0 || Array.IndexOf(Arguments, "-swapHunterV06CoreQA") >= 0 || Array.IndexOf(Arguments, "-swapHunterV06MissionQA") >= 0 || Array.IndexOf(Arguments, "-swapHunterV06RouteQA") >= 0 || Array.IndexOf(Arguments, "-swapHunterV06StorageQA") >= 0 || Array.IndexOf(Arguments, "-swapHunterV06VisualQA") >= 0 || Array.IndexOf(Arguments, "-swapHunterV06PerformanceQA") >= 0 || Array.IndexOf(Arguments, "-swapHunterNaturalBossQA") >= 0 || Array.IndexOf(Arguments, "-swapHunterInteractiveReview") >= 0 || Array.IndexOf(Arguments, "-swapHunterBossPerformanceQA") >= 0 || Array.IndexOf(Arguments, "-swapHunterShowcaseCapture") >= 0 || Array.IndexOf(Arguments, "-swapHunterBattlefieldQA") >= 0 || Array.IndexOf(Arguments, "-swapHunterTutorialQA") >= 0 || Array.IndexOf(Arguments, "-swapHunterShopUIQA") >= 0 || Array.IndexOf(Arguments, "-swapHunterDeploymentQA") >= 0 || Array.IndexOf(Arguments, "-swapHunterTacticalAIQA") >= 0 || Array.IndexOf(Arguments, "-swapHunterCombatToolsQA") >= 0 || Array.IndexOf(Arguments, "-swapHunterBossSkillsQA") >= 0 || Array.IndexOf(Arguments, "-swapHunterTacticsQA") >= 0 || Array.IndexOf(Arguments, "-swapHunterBossQA") >= 0 || Array.IndexOf(Arguments, "-swapHunterMotionQA") >= 0 || Array.IndexOf(Arguments, "-swapHunterExpeditionBenchmark") >= 0 || Array.IndexOf(Arguments, "-swapHunterRouteQA") >= 0 || Array.IndexOf(Arguments, "-swapHunterExpeditionQA") >= 0 || Array.IndexOf(Arguments, "-swapHunterQA") >= 0
            || Array.IndexOf(Arguments, "-swapHunterGunplayQA") >= 0
            || Array.IndexOf(Arguments, "-swapHunterReviewFixQA") >= 0
            || Array.IndexOf(Arguments, "-swapHunterCampaignQA") >= 0
            || Array.IndexOf(Arguments, "-swapHunterBenchmark") >= 0
            || Array.IndexOf(Arguments, "-swapHunterAVReview") >= 0
            || Array.IndexOf(Arguments, "-swapHunterAudioReview") >= 0;
        public static readonly bool IsTacticalDemo = IsDirectedTrial || Array.IndexOf(Arguments, "-swapHunterTacticalDemo") >= 0;
        public static readonly bool IsFullPreview = IsTacticalDemo && !IsDirectedTrial && Array.IndexOf(Arguments,"-demoAllUnlocked")>=0;
        public static readonly bool IsDemonstration = IsTacticalDemo || Array.IndexOf(Arguments, "-swapHunterDemo") >= 0;
        public static bool IsIsolated => IsValidation || IsDemonstration;
        static string root;
        static int testCheckpoint;
        static bool testHasCheckpoint;
        public static string Root
        {
            get
            {
                if (root != null) return root;
                if (!IsIsolated) return root = Application.persistentDataPath;
                // A demo never accepts a caller-supplied profile/output directory.
                // Keep Mono file paths short and allocate a fresh root per process.
                if (IsDemonstration) return root = Path.Combine(Path.GetTempPath(), "SHD", Guid.NewGuid().ToString("N").Substring(0, 12));
                int index = Array.IndexOf(Arguments, "-qaOutput");
                string output = index >= 0 && index + 1 < Arguments.Length ? Arguments[index + 1] : Path.Combine(Path.GetTempPath(), "SwapHunterValidation");
                return root = Path.Combine(Path.GetFullPath(output), "runtime-fixture-" + Guid.NewGuid().ToString("N"));
            }
        }
        public static int Checkpoint
        {
            get => IsIsolated ? testCheckpoint : PlayerPrefs.GetInt("SH.checkpoint", 0);
            set
            {
                if (IsIsolated) { testCheckpoint = value; testHasCheckpoint = true; }
                else { PlayerPrefs.SetInt("SH.checkpoint", value); PlayerPrefs.Save(); }
            }
        }
        public static bool HasCheckpoint => IsIsolated ? testHasCheckpoint : PlayerPrefs.HasKey("SH.checkpoint");
        public static void ClearCheckpoint()
        {
            if (IsIsolated) { testCheckpoint = 0; testHasCheckpoint = false; }
            else { PlayerPrefs.DeleteKey("SH.checkpoint"); PlayerPrefs.Save(); }
        }
    }
}



