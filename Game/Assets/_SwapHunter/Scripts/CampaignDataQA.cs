using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace SwapHunter
{
    [Serializable] public sealed class CampaignDataCheck
    {
        public string name, detail;
        public bool passed;
    }
    // Explicitly invoked by QA only. Every invocation creates a new isolated fixture folder.
    public static class CampaignDataQA
    {
        public static List<CampaignDataCheck> Run(CampaignCatalog catalog, string outputDirectory)
        {
            string root = Path.Combine(Path.GetFullPath(outputDirectory), "campaign-data-fixtures-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            var results = new List<CampaignDataCheck>();
            void Check(string name, Action action)
            {
                try { action(); results.Add(new CampaignDataCheck { name = name, passed = true, detail = "passed" }); }
                catch (Exception ex) { results.Add(new CampaignDataCheck { name = name, passed = false, detail = ex.GetType().Name + ": " + ex.Message }); }
            }
            CampaignStore Fresh(string name) => new CampaignStore(Path.Combine(root, name), catalog);
            var contract = catalog.contracts[0];

            Check("campaign_catalog_valid", () => { catalog.Validate(); Require(catalog.modules.Length == 6 && catalog.contracts.Length == 3, "Six modules and three contracts required"); });
            Check("campaign_catalog_duplicate_id_rejected", () =>
            {
                var broken = JsonUtility.FromJson<CampaignCatalog>(JsonUtility.ToJson(catalog)); broken.modules[1].id = broken.modules[0].id;
                MustThrow<InvalidDataException>(() => broken.Validate());
            });
            Check("campaign_new_profile", () =>
            {
                var s = Fresh("starter"); Require(s.Profile.credits == 0 && s.Profile.unlocked.Count == 2 && s.HasModule("guard") && s.HasModule("breach"), "Starter profile");
                var reload = new CampaignStore(Path.Combine(root, "starter"), catalog); Require(reload.Profile.equipped.Length == 2 && reload.Profile.credits == 0, "Starter persisted");
            });
            Check("campaign_invalid_contract_rejected", () =>
            {
                var s = Fresh("unknown"); MustThrow<ArgumentException>(() => s.Begin("invalid")); Require(s.Profile.activeRun == null, "No active run after rejection");
            });
            Check("campaign_running_loadout_locked", () =>
            {
                var s = Fresh("locked"); string id = s.Begin(contract.id);
                MustThrow<InvalidOperationException>(() => s.Begin(contract.id)); Require(!s.Equip("", 0, out _) && !s.Unlock("anchor", out _), "Active run modifications rejected");
                Require(s.Profile.activeRun.runId == id && s.Profile.activeRun.equipped[0] == "guard", "Snapshot unchanged");
            });
            Check("campaign_wrong_run_and_early_extract_rejected", () =>
            {
                var s = Fresh("early"); string id = s.Begin(contract.id);
                Require(!s.MarkMainComplete("not-this-run") && !s.MarkBonusComplete("not-this-run"), "Foreign run ignored");
                MustThrow<InvalidOperationException>(() => s.Settle(id, true)); Require(s.Profile.activeRun != null && s.Profile.credits == 0, "Early extraction has no reward");
            });
            Check("campaign_success_once_across_reload", () =>
            {
                var s = Fresh("success"); string id = s.Begin(contract.id);
                Require(s.MarkMainComplete(id) && !s.MarkMainComplete(id), "Main objective is idempotent");
                Require(s.MarkBonusComplete(id) && !s.MarkBonusComplete(id), "Bonus objective is idempotent");
                int expected = contract.reward + contract.bonusReward; var receipt = s.Settle(id, true);
                Require(receipt.success && receipt.totalReward == expected && s.Profile.credits == expected && s.Profile.activeRun == null, "Success reward");
                var reloaded = new CampaignStore(Path.Combine(root, "success"), catalog); var repeated = reloaded.Settle(id, false);
                Require(repeated.success && reloaded.Profile.credits == expected && reloaded.Profile.settlements.Count == 1, "Duplicate settlement returns original receipt");
            });
            Check("campaign_failure_without_main_zero", () =>
            {
                var s = Fresh("no-main"); string id = s.Begin(contract.id); s.MarkBonusComplete(id); var receipt = s.Settle(id, false);
                Require(receipt.totalReward == 0 && receipt.bonusReward == 0 && s.Profile.credits == 0, "Bonus cannot be cashed after failure");
            });
            Check("campaign_failure_main_compensation_only", () =>
            {
                var s = Fresh("main-fail"); string id = s.Begin(contract.id); s.MarkMainComplete(id); s.MarkBonusComplete(id); var receipt = s.Settle(id, false);
                Require(receipt.baseReward == contract.failureReward && receipt.bonusReward == 0 && s.Profile.credits == contract.failureReward, "Failure compensation only");
            });
            Check("campaign_interrupt_recovery_once", () =>
            {
                var s = Fresh("recovery"); string id = s.Begin(contract.id); s.MarkMainComplete(id); s.MarkBonusComplete(id);
                var reloaded = new CampaignStore(Path.Combine(root, "recovery"), catalog); var receipt = reloaded.RecoverInterrupted();
                Require(receipt != null && !receipt.success && receipt.runId == id && receipt.totalReward == contract.failureReward, "Recovery uses failure rule");
                var again = new CampaignStore(Path.Combine(root, "recovery"), catalog); Require(again.RecoverInterrupted() == null && again.Profile.credits == contract.failureReward, "Recovery persisted");
            });
            Check("campaign_unlock_and_energy_rules", () =>
            {
                var s = Fresh("equipment"); Require(!s.Unlock("anchor", out _) && !s.Equip("anchor", 0, out _), "Locked and unaffordable rejected");
                Require(!s.Equip("guard", 1, out _) && !s.Equip("", -1, out _), "Duplicate and invalid slot rejected");
                int budget = catalog.Module("anchor").cost + catalog.Module("overdrive").cost;
                Fund(s, contract, budget); int before = s.Profile.credits;
                Require(s.Unlock("anchor", out _) && s.Profile.credits == before - catalog.Module("anchor").cost, "Unlock spends exact cost");
                before = s.Profile.credits; Require(!s.Unlock("anchor", out _) && s.Profile.credits == before, "No duplicate charge");
                Require(s.Unlock("overdrive", out _), "Overdrive unlocked");
                Require(!s.Equip("overdrive", 0, out _) && s.Profile.equipped[0] == "guard", "Over-budget replacement rejected");
                Require(s.Equip("anchor", 1, out _) && s.Equip("overdrive", 0, out _), "Three plus one budget accepted");
                var reload = new CampaignStore(Path.Combine(root, "equipment"), catalog); Require(reload.HasModule("anchor") && reload.HasModule("overdrive"), "Loadout persisted");
            });
            Check("campaign_rewards_snapshot", () =>
            {
                var mutable = JsonUtility.FromJson<CampaignCatalog>(JsonUtility.ToJson(catalog)); var c = mutable.contracts[0]; int original = c.reward;
                var s = new CampaignStore(Path.Combine(root, "snapshot"), mutable); string id = s.Begin(c.id); c.reward += 1000; s.MarkMainComplete(id);
                Require(s.Settle(id, true).totalReward == original, "Active contract retains original offer");
            });
            Check("campaign_write_failure_preserves_memory", () =>
            {
                var s = Fresh("write-fail"); string id = s.Begin(contract.id); s.MarkMainComplete(id);
                string file = Path.Combine(root, "write-fail", "campaign-v3.json"); bool denied = false;
                using (var exclusive = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.None))
                {
                    try { s.Settle(id, true); } catch (IOException) { denied = true; } catch (UnauthorizedAccessException) { denied = true; }
                }
                Require(denied && s.Profile.credits == 0 && s.Profile.activeRun != null && s.Profile.settlements.Count == 0, "Failed replace changed neither memory nor receipt");
                Require(s.Settle(id, true).totalReward == contract.reward, "Retry can complete");
            });
            Check("campaign_corrupt_primary_recovers_preserves_original", () =>
            {
                var s = Fresh("backup"); string id = s.Begin(contract.id); s.MarkMainComplete(id); s.Settle(id, true); s.Begin(contract.id);
                string dir = Path.Combine(root, "backup"), file = Path.Combine(dir, "campaign-v3.json"); File.WriteAllText(file, "deliberately invalid QA fixture");
                var recovered = new CampaignStore(dir, catalog); Require(recovered.Profile.credits == contract.reward && !string.IsNullOrEmpty(recovered.LastWarning), "Valid backup loaded visibly");
                recovered.Begin(contract.id); Require(Directory.GetFiles(dir, "campaign-v3.json.corrupt-*").Length == 1, "Corrupted primary retained for inspection");
            });
            Check("campaign_double_corrupt_never_resets", () =>
            {
                string dir = Path.Combine(root, "both-bad"); Directory.CreateDirectory(dir); string file = Path.Combine(dir, "campaign-v3.json");
                File.WriteAllText(file, "bad-primary"); File.WriteAllText(file + ".bak", "bad-backup");
                MustThrow<InvalidDataException>(() => new CampaignStore(dir, catalog));
                Require(File.ReadAllText(file) == "bad-primary" && File.ReadAllText(file + ".bak") == "bad-backup", "Both originals preserved");
            });
            Check("campaign_each_contract_reward_matrix", () =>
            {
                foreach (var c in catalog.contracts)
                {
                    var s = Fresh("matrix-" + c.id); string id = s.Begin(c.id); s.MarkMainComplete(id); var baseOnly = s.Settle(id, true);
                    Require(baseOnly.totalReward == c.reward && baseOnly.bonusReward == 0, "Base-only reward " + c.id);
                    id = s.Begin(c.id); s.MarkMainComplete(id); s.MarkBonusComplete(id); var all = s.Settle(id, true);
                    Require(all.totalReward == c.reward + c.bonusReward, "Complete reward " + c.id);
                }
            });
            return results;
        }
        static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
        static void MustThrow<T>(Action action) where T : Exception
        {
            try { action(); } catch (T) { return; }
            throw new InvalidOperationException("Expected " + typeof(T).Name);
        }
        static void Fund(CampaignStore store, ContractDefinition contract, int amount)
        {
            int guard = 0;
            while (store.Profile.credits < amount && guard++ < 100)
            { string id = store.Begin(contract.id); store.MarkMainComplete(id); store.MarkBonusComplete(id); store.Settle(id, true); }
            Require(store.Profile.credits >= amount, "Fixture funding failed");
        }
    }
}
