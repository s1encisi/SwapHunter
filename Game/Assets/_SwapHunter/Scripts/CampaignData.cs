using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace SwapHunter
{
    [Serializable] public sealed class ModuleDefinition
    {
        public string id, name, description, tradeoff;
        public int cost, energy;
        public float effectMultiplier = 1, penaltyMultiplier = 1, duration, range;
    }
    [Serializable] public sealed class ContractDefinition
    {
        public string id, name, brief;
        public int stage, reward, bonusReward, failureReward;
    }
    [Serializable] public sealed class CampaignCatalog
    {
        public int version = 1, energyBudget = 4;
        public ModuleDefinition[] modules;
        public ContractDefinition[] contracts;
        public static CampaignCatalog Load()
        {
            var asset = Resources.Load<TextAsset>("CampaignCatalog");
            if (!asset) throw new InvalidDataException("Missing Resources/CampaignCatalog.json");
            var data = JsonUtility.FromJson<CampaignCatalog>(asset.text);
            if (data == null) throw new InvalidDataException("Campaign catalog could not be read");
            data.Validate(); return data;
        }
        public ModuleDefinition Module(string id) { if (modules != null) foreach (var m in modules) if (m.id == id) return m; return null; }
        public ContractDefinition Contract(string id) { if (contracts != null) foreach (var c in contracts) if (c.id == id) return c; return null; }
        public void Validate()
        {
            if (version != 1 || energyBudget <= 0 || modules == null || contracts == null || modules.Length == 0 || contracts.Length == 0)
                throw new InvalidDataException("Unsupported or empty campaign catalog");
            var ids = new HashSet<string>();
            foreach (var m in modules)
                if (m == null || string.IsNullOrEmpty(m.id) || !ids.Add(m.id) || string.IsNullOrEmpty(m.name) || m.cost < 0 || m.energy < 1 || m.energy > energyBudget
                    || !Finite(m.effectMultiplier) || m.effectMultiplier <= 0 || !Finite(m.penaltyMultiplier) || m.penaltyMultiplier <= 0 || !Finite(m.duration) || m.duration < 0 || !Finite(m.range) || m.range < 0)
                    throw new InvalidDataException("Invalid module definition");
            if (Module("guard") == null || Module("breach") == null || Module("guard").energy + Module("breach").energy > energyBudget)
                throw new InvalidDataException("Starter loadout is invalid");
            ids.Clear();
            foreach (var c in contracts)
                if (c == null || string.IsNullOrEmpty(c.id) || !ids.Add(c.id) || string.IsNullOrEmpty(c.name) || c.stage < 1 || c.stage > 3 || c.reward <= 0 || c.bonusReward < 0 || c.failureReward < 0 || c.failureReward >= c.reward || (long)c.reward + c.bonusReward > int.MaxValue)
                    throw new InvalidDataException("Invalid contract definition");
        }
        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
    [Serializable] public sealed class CampaignRun
    {
        public string runId, contractId, startedUtc;
        public string[] equipped;
        public bool mainComplete, bonusComplete;
        public int reward, bonusReward, failureReward;
    }
    [Serializable] public sealed class Settlement
    {
        public string runId, contractId, finishedUtc;
        public bool success, mainComplete, bonusComplete;
        public int baseReward, bonusReward, totalReward, creditsAfter;
    }
    [Serializable] public sealed class CampaignProfile
    {
        public int version = 1, credits;
        public List<string> unlocked = new List<string> { "guard", "breach" };
        public string[] equipped = { "guard", "breach" };
        public CampaignRun activeRun;
        public List<Settlement> settlements = new List<Settlement>();
    }

    // Write a complete successor before replacing the in-memory profile.
    // A local backup recovers accidental damage, not deliberate save tampering.
    public sealed class CampaignStore
    {
        readonly string directory, file, backup;
        readonly CampaignCatalog catalog;
        bool recoveredBackup;
        public CampaignProfile Profile { get; private set; }
        public string SavePath => file;
        public string LastWarning { get; private set; } = "";
        public CampaignStore(string directory, CampaignCatalog catalog)
        {
            if (string.IsNullOrEmpty(directory)) throw new ArgumentException("A campaign save directory is required");
            this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog)); catalog.Validate();
            this.directory = Path.GetFullPath(directory);
            file = Path.Combine(this.directory, "campaign-v3.json"); backup = file + ".bak";
            Directory.CreateDirectory(this.directory);
            if (File.Exists(file) || File.Exists(backup))
            {
                Exception primaryError = null;
                try { Profile = Read(file); }
                catch (Exception ex) when (ex is IOException || ex is ArgumentException || ex is InvalidDataException)
                { primaryError = ex; }
                if (Profile == null)
                {
                    try { Profile = Read(backup); recoveredBackup = true; LastWarning = "主存档无法读取，已加载上一份有效备份；最近一次操作可能回退。原文件将保留供检查。"; }
                    catch (Exception ex) when (ex is IOException || ex is ArgumentException || ex is InvalidDataException)
                    { throw new InvalidDataException("主存档与备份均无法读取。文件已保留，请勿覆盖；可先进入教学或自由试招。", primaryError ?? ex); }
                }
            }
            else { var fresh = new CampaignProfile(); Persist(fresh); Profile = fresh; }
        }
        CampaignProfile Read(string path)
        {
            string json = File.ReadAllText(path);
            if (!json.Contains("\"version\"") || !json.Contains("\"unlocked\"") || !json.Contains("\"settlements\"")) throw new InvalidDataException("Incomplete campaign save");
            var data = JsonUtility.FromJson<CampaignProfile>(json); NormalizeEmptyRun(data); Validate(data); return data;
        }
        CampaignProfile Copy() { var clone = JsonUtility.FromJson<CampaignProfile>(JsonUtility.ToJson(Profile)); NormalizeEmptyRun(clone); return clone; }
        static void NormalizeEmptyRun(CampaignProfile profile)
        {
            // Unity serializes a null inline serializable class as its empty default value.
            // Normalize only that exact empty representation; partially corrupted runs still fail validation.
            var run = profile?.activeRun;
            if (run != null && string.IsNullOrEmpty(run.runId) && string.IsNullOrEmpty(run.contractId) && string.IsNullOrEmpty(run.startedUtc)
                && (run.equipped == null || run.equipped.Length == 0) && !run.mainComplete && !run.bonusComplete && run.reward == 0 && run.bonusReward == 0 && run.failureReward == 0)
                profile.activeRun = null;
        }
        void Validate(CampaignProfile p)
        {
            if (p == null || p.version != 1 || p.credits < 0 || p.unlocked == null || p.equipped == null || p.equipped.Length != 2 || p.settlements == null)
                throw new InvalidDataException("Invalid campaign profile");
            var unlocked = new HashSet<string>();
            foreach (string id in p.unlocked) if (catalog.Module(id) == null || !unlocked.Add(id)) throw new InvalidDataException("Unknown or duplicate unlocked module");
            if (!unlocked.Contains("guard") || !unlocked.Contains("breach")) throw new InvalidDataException("Starter modules missing");
            ValidateLoadout(p.equipped, unlocked);
            var runs = new HashSet<string>();
            foreach (var s in p.settlements)
                if (s == null || string.IsNullOrEmpty(s.runId) || !runs.Add(s.runId) || catalog.Contract(s.contractId) == null || s.baseReward < 0 || s.bonusReward < 0 || (long)s.baseReward + s.bonusReward != s.totalReward || s.creditsAfter < 0 || s.success && !s.mainComplete || !s.success && s.bonusReward != 0)
                    throw new InvalidDataException("Invalid settlement history");
            if (p.activeRun != null)
            {
                var r = p.activeRun;
                if (string.IsNullOrEmpty(r.runId) || runs.Contains(r.runId) || catalog.Contract(r.contractId) == null || r.reward <= 0 || r.failureReward < 0 || r.failureReward >= r.reward || r.bonusReward < 0 || (long)r.reward + r.bonusReward > int.MaxValue)
                    throw new InvalidDataException("Invalid active contract");
                ValidateLoadout(r.equipped, unlocked);
            }
        }
        void ValidateLoadout(string[] slots, HashSet<string> unlocked)
        {
            if (slots == null || slots.Length != 2) throw new InvalidDataException("Loadout requires two slots");
            int energy = 0; var used = new HashSet<string>();
            foreach (string id in slots)
            {
                if (string.IsNullOrEmpty(id)) continue;
                var module = catalog.Module(id);
                if (module == null || !unlocked.Contains(id) || !used.Add(id)) throw new InvalidDataException("Loadout contains a locked or duplicate module");
                energy += module.energy;
            }
            if (energy > catalog.energyBudget) throw new InvalidDataException("Loadout exceeds energy budget");
        }
        void Commit(CampaignProfile next) { Validate(next); Persist(next); Profile = next; }
        void Persist(CampaignProfile next)
        {
            Validate(next);
            string temporary = Path.Combine(directory, "campaign-v3." + Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                byte[] bytes = System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(next, true));
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None)) { stream.Write(bytes, 0, bytes.Length); stream.Flush(true); }
                if (File.Exists(file))
                {
                    if (recoveredBackup)
                    {
                        File.Copy(file, file + ".corrupt-" + DateTime.UtcNow.ToString("yyyyMMddTHHmmssfff") + "-" + Guid.NewGuid().ToString("N"), false);
                        File.Replace(temporary, file, null);
                    }
                    else File.Replace(temporary, file, backup);
                }
                else File.Move(temporary, file);
                recoveredBackup = false;
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
        public bool HasModule(string id) => Array.IndexOf(Profile.activeRun != null ? Profile.activeRun.equipped : Profile.equipped, id) >= 0;
        public string Begin(string contractId)
        {
            if (Profile.activeRun != null) throw new InvalidOperationException("已有合约尚未结算");
            var c = catalog.Contract(contractId) ?? throw new ArgumentException("未知合约");
            var next = Copy();
            next.activeRun = new CampaignRun { runId = Guid.NewGuid().ToString("N"), contractId = c.id, startedUtc = DateTime.UtcNow.ToString("o"), equipped = (string[])next.equipped.Clone(), reward = c.reward, bonusReward = c.bonusReward, failureReward = c.failureReward };
            Commit(next); return Profile.activeRun.runId;
        }
        public bool MarkMainComplete(string runId)
        {
            if (Profile.activeRun == null || Profile.activeRun.runId != runId || Profile.activeRun.mainComplete) return false;
            var next = Copy(); next.activeRun.mainComplete = true; Commit(next); return true;
        }
        public bool MarkBonusComplete(string runId)
        {
            if (Profile.activeRun == null || Profile.activeRun.runId != runId || Profile.activeRun.bonusComplete) return false;
            var next = Copy(); next.activeRun.bonusComplete = true; Commit(next); return true;
        }
        public Settlement Settle(string runId, bool success)
        {
            foreach (var previous in Profile.settlements) if (previous.runId == runId) return JsonUtility.FromJson<Settlement>(JsonUtility.ToJson(previous));
            if (Profile.activeRun == null || Profile.activeRun.runId != runId) throw new InvalidOperationException("没有可结算的合约");
            var next = Copy(); var run = next.activeRun;
            if (success && !run.mainComplete) throw new InvalidOperationException("完成主要目标后才能撤离结算");
            var receipt = new Settlement { runId = run.runId, contractId = run.contractId, finishedUtc = DateTime.UtcNow.ToString("o"), success = success, mainComplete = run.mainComplete, bonusComplete = run.bonusComplete, baseReward = success ? run.reward : run.mainComplete ? run.failureReward : 0, bonusReward = success && run.bonusComplete ? run.bonusReward : 0 };
            receipt.totalReward = checked(receipt.baseReward + receipt.bonusReward); next.credits = checked(next.credits + receipt.totalReward); receipt.creditsAfter = next.credits;
            next.settlements.Add(receipt); next.activeRun = null; Commit(next); return JsonUtility.FromJson<Settlement>(JsonUtility.ToJson(receipt));
        }
        public Settlement RecoverInterrupted() => Profile.activeRun == null ? null : Settle(Profile.activeRun.runId, false);
        public bool Unlock(string id, out string error)
        {
            error = ""; var module = catalog.Module(id);
            if (Profile.activeRun != null) { error = "合约进行中不能解锁模块"; return false; }
            if (module == null) { error = "未知模块"; return false; }
            if (Profile.unlocked.Contains(id)) { error = "模块已解锁"; return false; }
            if (Profile.credits < module.cost) { error = "研究点不足"; return false; }
            var next = Copy(); next.credits -= module.cost; next.unlocked.Add(id); Commit(next); return true;
        }
        public bool Equip(string id, int slot, out string error)
        {
            error = ""; id = id ?? "";
            if (Profile.activeRun != null) { error = "合约进行中不能更改装配"; return false; }
            if (slot < 0 || slot > 1) { error = "装备槽无效"; return false; }
            if (id.Length > 0 && !Profile.unlocked.Contains(id)) { error = "请先解锁模块"; return false; }
            if (id.Length > 0 && Profile.equipped[1 - slot] == id) { error = "同一模块不能重复装备"; return false; }
            var next = Copy(); next.equipped[slot] = id;
            try { ValidateLoadout(next.equipped, new HashSet<string>(next.unlocked)); }
            catch (InvalidDataException) { error = "超过能量上限 " + catalog.energyBudget + "，请先卸下另一个模块"; return false; }
            Commit(next); return true;
        }
    }
}
