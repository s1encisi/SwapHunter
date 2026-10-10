using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace SwapHunter
{
    // Native storage fixtures only: no player saves, no screenshots or human-test claim.
    public sealed class StorageV06QA : MonoBehaviour
    {
        [Serializable] public sealed class Report
        {
            public string version, fixtureRoot;
            public string kind = "Native isolated settings persistence and reused CampaignDataQA fault fixtures; not human playtesting";
            public bool passed, batchMode;
            public List<CampaignDataCheck> checks = new List<CampaignDataCheck>();
        }

        readonly Report report = new Report();
        string output, fixture;
        bool done;

        void Awake() { Application.logMessageReceived += RuntimeError; }
        void OnDestroy() { Application.logMessageReceived -= RuntimeError; }
        void RuntimeError(string message, string trace, LogType kind)
        {
            if (done || (kind != LogType.Error && kind != LogType.Exception && kind != LogType.Assert)) return;
            report.checks.Add(new CampaignDataCheck { name = "runtime_error", passed = false, detail = message + "\n" + trace });
        }

        IEnumerator Start()
        {
            yield return null;
            string[] arguments = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(arguments, "-qaOutput");
            output = Path.GetFullPath(index >= 0 && index + 1 < arguments.Length ? arguments[index + 1] : RunStorage.Root);
            report.version = Application.version;
            report.batchMode = Application.isBatchMode;
            Directory.CreateDirectory(output);
            bool isolated = RunStorage.IsValidation && DemoGame.I && DemoGame.I.qaMode
                && Inside(RunStorage.Root, output) && Inside(SettingsStore.FilePath, RunStorage.Root)
                && Array.IndexOf(arguments, "-swapHunterV06StorageQA") >= 0;
            Check("storage_v06_flag_and_root_isolated", () => Require(isolated, "The v0.6 flag must be registered before DemoGame loads or saves settings"));
            if (!isolated) { Finish(); yield break; }

            // Keep Mono/Windows fixture paths short, including corrupt archives.
            // The output directory is already unique; collision never reuses old evidence.
            fixture = Path.Combine(output, "s-" + Guid.NewGuid().ToString("N").Substring(0, 12));
            if (Directory.Exists(fixture) || File.Exists(fixture)) throw new IOException("Existing storage fixture preserved: " + fixture);
            Directory.CreateDirectory(fixture);
            report.fixtureRoot = fixture;
            RunSettingsChecks();
            Check("campaign_storage_uses_same_isolated_root", () =>
                Require(DemoGame.I.campaignStore != null && Inside(DemoGame.I.campaignStore.SavePath, RunStorage.Root), "Campaign store must not use the player directory"));
            try
            {
                // Reuse existing corrupt-main/backup, failed-replace, idempotent-reward and migration fixtures.
                foreach (CampaignDataCheck result in CampaignDataQA.RunInNewDirectory(DemoGame.I.catalog, Path.Combine(fixture, "campaign")))
                {
                    result.name = "reused_" + result.name;
                    report.checks.Add(result);
                }
            }
            catch (Exception error)
            {
                report.checks.Add(new CampaignDataCheck { name = "campaign_fixture_setup", passed = false, detail = error.ToString() });
            }
            Finish();
        }

        void RunSettingsChecks()
        {
            Check("settings_atomic_replace_and_valid_backup", () =>
            {
                string path = PathFor("replace");
                var first = new GameOptions { sensitivity = 1.7f, volume = .31f };
                first.actions[0] = new ActionBinding("K:UpArrow", "M:4");
                SettingsStore.SaveToPath(first, path);
                string firstText = File.ReadAllText(path);
                var second = first.Clone(); second.sensitivity = 2.4f;
                SettingsStore.SaveToPath(second, path);
                Require(Mathf.Approximately(SettingsStore.LoadFromPath(path).sensitivity, 2.4f), "New complete document committed");
                Require(File.ReadAllText(path + ".bak") == firstText, "Previous complete document is .bak");
                Require(Directory.GetFiles(Path.GetDirectoryName(path), "*.tmp-*").Length == 0, "Successful atomic writes consume their temp files");
                Require(string.IsNullOrEmpty(SettingsStore.LastWarning), "Success has no stale failure warning");
            });
            Check("settings_failed_write_preserves_primary_backup_and_draft", () =>
            {
                string path = PathFor("locked");
                SettingsStore.SaveToPath(new GameOptions { sensitivity = 1.2f }, path);
                SettingsStore.SaveToPath(new GameOptions { sensitivity = 1.6f }, path);
                string primary = File.ReadAllText(path), backup = File.ReadAllText(path + ".bak");
                var draft = new GameOptions { sensitivity = 100 };
                bool denied = false;
                using (var held = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None))
                {
                    try { SettingsStore.SaveToPath(draft, path); }
                    catch (IOException) { denied = true; }
                    catch (UnauthorizedAccessException) { denied = true; }
                }
                Require(denied && !string.IsNullOrEmpty(SettingsStore.LastWarning), "Failure must propagate with a Chinese warning");
                Require(File.ReadAllText(path) == primary && File.ReadAllText(path + ".bak") == backup, "Neither prior file was modified");
                Require(draft.sensitivity == 100, "Failed persistence must not mutate the caller's editable draft");
                SettingsStore.SaveToPath(new GameOptions { sensitivity = 2.7f }, path);
                Require(Mathf.Approximately(SettingsStore.LoadFromPath(path).sensitivity, 2.7f), "Retry succeeds after lock release");
            });
            Check("settings_replace_failure_preserves_old_document", () =>
            {
                string path = PathFor("backup-directory");
                SettingsStore.SaveToPath(new GameOptions { sensitivity = 1.3f }, path);
                string primary = File.ReadAllText(path);
                Directory.CreateDirectory(path + ".bak");
                bool rejected = false;
                try { SettingsStore.SaveToPath(new GameOptions { sensitivity = 3 }, path); }
                catch (IOException) { rejected = true; }
                catch (UnauthorizedAccessException) { rejected = true; }
                Require(rejected && File.ReadAllText(path) == primary, "A failed atomic replacement cannot truncate the primary");
                Require(!string.IsNullOrEmpty(SettingsStore.LastWarning), "Replacement failure is visible");
            });
            Check("settings_corrupt_main_recovers_verified_backup", () =>
            {
                string path = PathFor("recover");
                SettingsStore.SaveToPath(new GameOptions { sensitivity = 1.9f }, path);
                SettingsStore.SaveToPath(new GameOptions { sensitivity = 2.9f }, path);
                string backup = File.ReadAllText(path + ".bak");
                const string damaged = "{damaged-settings";
                File.WriteAllText(path, damaged);
                var recovered = SettingsStore.LoadFromDirectory(Path.GetDirectoryName(path));
                Require(Mathf.Approximately(recovered.sensitivity, 1.9f) && SettingsStore.LastWarning.Contains("备份"), "Valid backup restored visibly");
                Require(File.ReadAllText(path) == damaged && File.ReadAllText(path + ".bak") == backup, "Load does not rewrite either original");
                Require(Archived(path, damaged), "Damaged primary has a separate preserved archive");
                SettingsStore.SaveToPath(recovered, path);
                Require(File.ReadAllText(path + ".bak") == backup, "Explicit recovery save does not replace a good backup with damaged data");
                Require(Mathf.Approximately(SettingsStore.LoadFromPath(path).sensitivity, 1.9f), "Explicit save commits recovered options");
            });
            Check("settings_double_corruption_preserves_both_originals", () =>
            {
                string path = PathFor("both-bad");
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, "bad-primary"); File.WriteAllText(path + ".bak", "bad-backup");
                var loaded = SettingsStore.LoadFromDirectory(Path.GetDirectoryName(path));
                Require(Mathf.Approximately(loaded.sensitivity, 1) && !string.IsNullOrEmpty(SettingsStore.LastWarning), "Defaults are explicit, not a successful recovery claim");
                Require(File.ReadAllText(path) == "bad-primary" && File.ReadAllText(path + ".bak") == "bad-backup", "Both originals remain untouched");
                Require(Archived(path, "bad-primary") && Archived(path + ".bak", "bad-backup"), "Both damaged records archived without replacing originals");
            });
            Check("settings_missing_main_can_read_backup_without_writing", () =>
            {
                string path = PathFor("backup-only");
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                string text = JsonUtility.ToJson(new GameOptions { sensitivity = 2.1f });
                File.WriteAllText(path + ".bak", text);
                var loaded = SettingsStore.LoadFromDirectory(Path.GetDirectoryName(path));
                Require(Mathf.Approximately(loaded.sensitivity, 2.1f) && !File.Exists(path), "Backup read does not create a new primary implicitly");
                Require(File.ReadAllText(path + ".bak") == text && SettingsStore.LastWarning.Contains("备份"), "Backup remains unchanged and recovery is visible");
            });
            Check("settings_v4_migration_keeps_old_file_and_bindings", () => Migration(4));
            Check("settings_v2_migration_keeps_old_file_and_bindings", () => Migration(2));
            Check("settings_empty_or_partial_documents_are_not_valid_backups", () =>
            {
                string path = PathFor("invalid-schema"); Directory.CreateDirectory(Path.GetDirectoryName(path));
                foreach (string text in new[] { "{}", "{\"version\":5}", "{\"version\":5,\"actions\":[]}" })
                {
                    File.WriteAllText(path, text); bool rejected = false;
                    try { SettingsStore.LoadFromPath(path); } catch (InvalidDataException) { rejected = true; }
                    Require(rejected, "Partial object must not silently reset all bindings: " + text);
                }
            });
        }

        void Migration(int version)
        {
            string path = PathFor("migration-v" + version), directory = Path.GetDirectoryName(path);
            Directory.CreateDirectory(directory);
            var previous = new GameOptions { version = version, sensitivity = 1.8f, resolution = 2, quality = 2, effectsVolume = .27f, feedbackVolume = .38f };
            var oldBindings = new ActionBinding[17]; Array.Copy(previous.actions, oldBindings, oldBindings.Length);
            previous.actions = oldBindings; previous.actions[0] = new ActionBinding("K:UpArrow", "M:4");
            string text = JsonUtility.ToJson(previous, true);
            string legacy = Path.Combine(directory, "settings-v" + version + ".json"); File.WriteAllText(legacy, text);
            var loaded = SettingsStore.LoadFromDirectory(directory);
            Require(loaded.version == GameOptions.CurrentVersion && loaded.actions.Length == GameOptions.DefaultBindings().Length, "Schema and new action slots migrated");
            Require(loaded.actions[0].primary == "K:UpArrow" && loaded.actions[0].secondary == "M:4", "Custom bindings preserved");
            if (version == 2) Require(loaded.resolutionWidth == 1920 && loaded.resolutionHeight == 1080 && Mathf.Approximately(loaded.ambientVolume, .27f), "v2 display/audio conversion preserved");
            SettingsStore.SaveToPath(loaded, path);
            Require(File.ReadAllText(legacy) == text && SettingsStore.LoadFromPath(path).actions[0].primary == "K:UpArrow", "Old document stays intact beside current settings");
        }

        string PathFor(string name) => Path.Combine(fixture, name, "settings-v5.json");
        static bool Inside(string path, string directory)
        {
            string prefix = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            return Path.GetFullPath(path).StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        }
        static bool Archived(string path, string content)
        {
            foreach (string archived in Directory.GetFiles(Path.GetDirectoryName(path), Path.GetFileName(path) + ".backup-*"))
                if (File.ReadAllText(archived) == content) return true;
            return false;
        }
        static void Require(bool passed, string message) { if (!passed) throw new InvalidOperationException(message); }
        void Check(string name, Action action)
        {
            try { action(); report.checks.Add(new CampaignDataCheck { name = name, passed = true, detail = "passed" }); }
            catch (Exception error) { report.checks.Add(new CampaignDataCheck { name = name, passed = false, detail = error.ToString() }); }
        }
        void Finish()
        {
            if (done) return;
            done = true;
            report.passed = report.checks.Count > 0 && report.checks.TrueForAll(check => check.passed);
            File.WriteAllText(Path.Combine(output, "storage-v06-report.json"), JsonUtility.ToJson(report, true));
            Debug.Log("STORAGE_V06_QA passed=" + report.passed + "; checks=" + report.checks.Count);
            Application.Quit(report.passed ? 0 : 1);
        }
    }
}
