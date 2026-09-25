using System;
using System.IO;
using UnityEngine;

namespace SwapHunter
{
    // Validation runs must never save over a player who is trying the demo concurrently.
    public static class RunStorage
    {
        static readonly string[] Arguments = Environment.GetCommandLineArgs();
        public static readonly bool IsValidation = Array.IndexOf(Arguments, "-swapHunterQA") >= 0
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
