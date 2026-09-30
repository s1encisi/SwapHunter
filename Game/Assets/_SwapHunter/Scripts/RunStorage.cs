using System;
using System.IO;
using UnityEngine;

namespace SwapHunter
{
    // Validation runs must never save over a player who is trying the demo concurrently.
    public static class RunStorage
    {
        static readonly string[] Arguments = Environment.GetCommandLineArgs();
        public static readonly bool IsValidation = Array.IndexOf(Arguments, "-swapHunterNaturalBossQA") >= 0 || Array.IndexOf(Arguments, "-swapHunterInteractiveReview") >= 0 || Array.IndexOf(Arguments, "-swapHunterBossPerformanceQA") >= 0 || Array.IndexOf(Arguments, "-swapHunterShowcaseCapture") >= 0 || Array.IndexOf(Arguments, "-swapHunterBattlefieldQA") >= 0 || Array.IndexOf(Arguments, "-swapHunterTutorialQA") >= 0 || Array.IndexOf(Arguments, "-swapHunterShopUIQA") >= 0 || Array.IndexOf(Arguments, "-swapHunterDeploymentQA") >= 0 || Array.IndexOf(Arguments, "-swapHunterTacticalAIQA") >= 0 || Array.IndexOf(Arguments, "-swapHunterCombatToolsQA") >= 0 || Array.IndexOf(Arguments, "-swapHunterBossSkillsQA") >= 0 || Array.IndexOf(Arguments, "-swapHunterTacticsQA") >= 0 || Array.IndexOf(Arguments, "-swapHunterBossQA") >= 0 || Array.IndexOf(Arguments, "-swapHunterMotionQA") >= 0 || Array.IndexOf(Arguments, "-swapHunterExpeditionBenchmark") >= 0 || Array.IndexOf(Arguments, "-swapHunterRouteQA") >= 0 || Array.IndexOf(Arguments, "-swapHunterExpeditionQA") >= 0 || Array.IndexOf(Arguments, "-swapHunterQA") >= 0
            || Array.IndexOf(Arguments, "-swapHunterGunplayQA") >= 0
            || Array.IndexOf(Arguments, "-swapHunterReviewFixQA") >= 0
            || Array.IndexOf(Arguments, "-swapHunterCampaignQA") >= 0
            || Array.IndexOf(Arguments, "-swapHunterBenchmark") >= 0
            || Array.IndexOf(Arguments, "-swapHunterAVReview") >= 0
            || Array.IndexOf(Arguments, "-swapHunterAudioReview") >= 0;
        static string root;
        static int testCheckpoint;
        static bool testHasCheckpoint;
        public static string Root
        {
            get
            {
                if (root != null) return root;
                if (!IsValidation) return root = Application.persistentDataPath;
                int index = Array.IndexOf(Arguments, "-qaOutput");
                string output = index >= 0 && index + 1 < Arguments.Length ? Arguments[index + 1] : Path.Combine(Path.GetTempPath(), "SwapHunterValidation");
                return root = Path.Combine(Path.GetFullPath(output), "runtime-fixture-" + Guid.NewGuid().ToString("N"));
            }
        }
        public static int Checkpoint
        {
            get => IsValidation ? testCheckpoint : PlayerPrefs.GetInt("SH.checkpoint", 0);
            set
            {
                if (IsValidation) { testCheckpoint = value; testHasCheckpoint = true; }
                else { PlayerPrefs.SetInt("SH.checkpoint", value); PlayerPrefs.Save(); }
            }
        }
        public static bool HasCheckpoint => IsValidation ? testHasCheckpoint : PlayerPrefs.HasKey("SH.checkpoint");
        public static void ClearCheckpoint()
        {
            if (IsValidation) { testCheckpoint = 0; testHasCheckpoint = false; }
            else { PlayerPrefs.DeleteKey("SH.checkpoint"); PlayerPrefs.Save(); }
        }
    }
}



