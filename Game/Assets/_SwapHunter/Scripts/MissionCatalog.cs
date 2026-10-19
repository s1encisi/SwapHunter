using System;
using System.Collections.Generic;
using UnityEngine;

namespace SwapHunter
{
    public enum EncounterTrigger { Start, Region, Objective, ObjectiveCount, Elapsed, BossReady, BossRifle, BossCombined, MiniBoss, RegionElapsed }
    public enum EncounterBudgetState { Untriggered, Warning, Deployed, Cancelled }

    [Serializable]
    public sealed class ObjectiveDefinition
    {
        public string id, label;
        public ObjectiveDefinition(string id, string label) { this.id = id; this.label = label; }
    }

    [Serializable]
    public sealed class EncounterRole
    {
        public EnemyKind kind;
        public bool required, fixedPost, firstGrenade;
        public EncounterRole(EnemyKind kind, bool required = false, bool fixedPost = false, bool firstGrenade = false)
        { this.kind = kind; this.required = required; this.fixedPost = fixedPost; this.firstGrenade = firstGrenade; }
    }

    [Serializable]
    public sealed class EncounterGroup
    {
        public string id, label, region, objective, afterDefeated, alternativeRegion, cancelOnObjective;
        public EncounterTrigger trigger;
        public float triggerSeconds, warningSeconds = 2.2f;
        public int objectiveCount;
        public bool escort, miniEscort;
        public string miniBossId;
        public EncounterRole[] roles;
    }

    [Serializable]
    public sealed class MissionDefinition
    {
        public int level, requiredObjectives, minimumSwaps, maximumEscorts, liveLimit, miniEscortLimit;
        public string instruction, extraction, bonusRisk;
        public ObjectiveDefinition[] objectives;
        public EncounterGroup[] groups;
        public int TotalBudget { get { int n = 0; foreach (var g in groups) n += g.roles.Length; return n; } }
        public int RequiredBudget { get { int n = 0; foreach (var g in groups) foreach (var r in g.roles) if (r.required) n++; return n; } }
    }

    // Stable chapter IDs deliberately remain 0..11. World coordinates are bound by
    // ExpeditionWorld; mission rules, budgets and triggers live in this one catalog.
    public static class MissionCatalog
    {
        public static readonly MissionDefinition[] Defaults = MapV2MissionDefinitions.Create();

        static MissionConfiguration configuration;
        public const string ResourceName = "MissionConfiguration";
        public static MissionConfiguration Configuration
        {
            get
            {
                if (!configuration)
                {
                    var loaded = Resources.Load<MissionConfiguration>(ResourceName);
                    if (!loaded) throw new InvalidOperationException("缺少 Resources/MissionConfiguration.asset。请先执行显式的任务配置 Prepare；正常运行不会自动生成或回退默认配置。");
                    loaded.Validate();
                    configuration = loaded;
                }
                return configuration;
            }
        }
        public static MissionDefinition[] All => Configuration.missions;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void ResetCache() { configuration = null; }

        [Serializable] sealed class DefinitionContainer { public MissionDefinition[] missions; }
        public static MissionDefinition[] CreateDefaultDefinitions()
        {
            // A newly prepared asset owns a deep copy. Inspector edits must never
            // mutate the built-in reference definitions used by validation/QA.
            var copy = JsonUtility.FromJson<DefinitionContainer>(
                JsonUtility.ToJson(new DefinitionContainer { missions = Defaults }));
            return copy.missions;
        }

        public static MissionDefinition Get(int level)
        {
            var definitions = All;
            if (level < 0 || level >= definitions.Length) throw new ArgumentOutOfRangeException(nameof(level));
            return definitions[level];
        }

        public static void Validate() { Configuration.Validate(); }
        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        static bool StableId(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            foreach (char c in id) if (!(c >= 'a' && c <= 'z') && !(c >= '0' && c <= '9') && c != '-') return false;
            return true;
        }

        public static void Validate(MissionDefinition[] definitions)
        {
            var errors = ValidationErrors(definitions);
            if (errors.Count > 0) throw new InvalidOperationException("任务配置校验失败（" + errors.Count + "项）：\n" + string.Join("\n", errors));
        }

        // All chapters are checked before reporting. Authoring errors are never
        // silently repaired: the asset is the source of truth during runtime.
        public static List<string> ValidationErrors(MissionDefinition[] definitions)
        {
            var errors = new List<string>();
            if (definitions == null || definitions.Length != 12)
            { errors.Add("必须保留稳定章节 0..11，共 12 条定义。"); return errors; }
            for (int level = 0; level < definitions.Length; level++)
            {
                var d = definitions[level];
                string context = "第 " + (level + 1) + " 关：";
                if (d == null) { errors.Add(context + "定义为空。"); continue; }
                if (d.level != level) errors.Add(context + "稳定 level 必须为 " + level + "，不得重排章节或重置成长索引。");
                if (d.objectives == null || d.groups == null) { errors.Add(context + "目标或遭遇列表为空引用。"); continue; }
                if (d.requiredObjectives < 0 || d.requiredObjectives > d.objectives.Length) errors.Add(context + "主目标数量必须在 0 与现有目标总数之间。");
                if (d.minimumSwaps != (level == 0 ? 1 : 0)) errors.Add(context + "仅第 1 关要求一次 Q，其他关必须为 0。");
                if (string.IsNullOrWhiteSpace(d.instruction) || string.IsNullOrWhiteSpace(d.extraction) || string.IsNullOrWhiteSpace(d.bonusRisk))
                    errors.Add(context + "任务提示、出口文案和可选风险说明必须填写。");
                if (level == 11 ? d.maximumEscorts < 1 || d.maximumEscorts > 4 : d.maximumEscorts != 0)
                    errors.Add(context + "仅 Boss 关可配置随从并发，且必须为 1 至 4。");

                var objectives = new HashSet<string>();
                foreach (var o in d.objectives)
                    if (o == null || !StableId(o.id) || !objectives.Add(o.id) || string.IsNullOrWhiteSpace(o.label))
                        errors.Add(context + "目标必须有唯一稳定 id（小写字母、数字、连字符）和名称。");
                var expectedObjectives = new HashSet<string>();
                foreach (var o in Defaults[level].objectives) expectedObjectives.Add(o.id);
                if (!objectives.SetEquals(expectedObjectives)) errors.Add(context + "目标 id 与地图绑定契约不一致；新增目标需要同步地图，不能只改名字。");

                var groups = new Dictionary<string, EncounterGroup>();
                int bosses = 0, budget = 0;
                foreach (var g in d.groups)
                {
                    if (g == null) { errors.Add(context + "遭遇定义为空。"); continue; }
                    string encounter = context + "遭遇 " + (g.id ?? "<空>") + "：";
                    if (!StableId(g.id) || groups.ContainsKey(g.id)) errors.Add(encounter + "id 无效或重复。");
                    else groups.Add(g.id, g);
                    if (string.IsNullOrWhiteSpace(g.label)) errors.Add(encounter + "必须填写预警来源名称。");
                    if (!Enum.IsDefined(typeof(EncounterTrigger), g.trigger)) errors.Add(encounter + "触发类型无效。");
                    if (!Finite(g.warningSeconds) || g.warningSeconds < 2) errors.Add(encounter + "预警必须为至少 2 秒的有限值。");
                    if (!Finite(g.triggerSeconds) || g.triggerSeconds < 0) errors.Add(encounter + "触发延时必须为非负有限值。");
                    if ((g.trigger == EncounterTrigger.Region || g.trigger == EncounterTrigger.RegionElapsed || g.trigger == EncounterTrigger.MiniBoss) && !StableId(g.region)) errors.Add(encounter + "区域触发必须填写区域 id。");
                    if (!string.IsNullOrEmpty(g.alternativeRegion) && !StableId(g.alternativeRegion)) errors.Add(encounter + "备用区域 id 无效。");
                    if (g.trigger == EncounterTrigger.Objective && string.IsNullOrEmpty(g.objective)) errors.Add(encounter + "目标触发必须指定目标 id。");
                    if (!string.IsNullOrEmpty(g.objective) && !objectives.Contains(g.objective)) errors.Add(encounter + "触发目标不存在。");
                    if (!string.IsNullOrEmpty(g.cancelOnObjective) && !objectives.Contains(g.cancelOnObjective)) errors.Add(encounter + "取消预算的控制目标不存在。");
                    if (g.trigger == EncounterTrigger.ObjectiveCount && (g.objectiveCount <= 0 || g.objectiveCount > d.objectives.Length))
                        errors.Add(encounter + "触发目标数必须大于 0 且不超过目标总数。");
                    bool phaseTrigger = g.trigger == EncounterTrigger.BossReady || g.trigger == EncounterTrigger.BossRifle || g.trigger == EncounterTrigger.BossCombined;
                    if (phaseTrigger && level != 11 || g.escort && level != 11) errors.Add(encounter + "Boss 阶段与随从仅用于第 12 关。");
                    if (g.roles == null || g.roles.Length == 0) { errors.Add(encounter + "敌人预算不能为空。"); continue; }
                    budget += g.roles.Length;
                    foreach (var r in g.roles)
                    {
                        if (r == null || !Enum.IsDefined(typeof(EnemyKind), r.kind)) { errors.Add(encounter + "兵种无效。"); continue; }
                        if (r.kind == EnemyKind.Training && r.required) errors.Add(encounter + "训练机不能作为必清威胁。");
                        if (r.firstGrenade && r.kind != EnemyKind.Assault) errors.Add(encounter + "首次投雷编排仅支持现有突击兵。");
                        if (r.required && (!string.IsNullOrEmpty(g.cancelOnObjective) || g.escort)) errors.Add(encounter + "可取消预算或随从不能标记必清，否则可能永久卡关。");
                        if (level == 11 && r.kind == EnemyKind.Elite && string.IsNullOrEmpty(g.miniBossId) && g.id == "boss")
                        {
                            bosses++;
                            if (g.id != "boss" || !r.required || g.escort) errors.Add(encounter + "指挥官须在 boss 组且为独立必清对象。");
                        }
                    }
                }
                var expectedGroups = new HashSet<string>();
                foreach (var g in Defaults[level].groups) expectedGroups.Add(g.id);
                if (!expectedGroups.SetEquals(groups.Keys)) errors.Add(context + "遭遇 id 与出生点绑定契约不一致；保留稳定键后可调整编制、触发和时机。");
                int ordinary = 0, miniCount = 0; foreach (var q in d.groups) { if (!string.IsNullOrEmpty(q.miniBossId)) miniCount += q.roles.Length; else if (q.id != "boss") ordinary += q.roles.Length; }
                int[] expected = {10,15,15,20,30,30,25,30,40,45,35,30};
                if (ordinary != expected[level]) errors.Add(context + "普通预算必须恰好为 " + expected[level]);
                if (miniCount != 1) errors.Add(context + "必须且只能配置一名独立小Boss。");
                if (d.liveLimit < 6 || d.liveLimit > 18) errors.Add(context + "局部并发上限须在6至18。");
                if (level == 11 && bosses != 1) errors.Add(context + "必须且只能有一名指挥官。");
                foreach (var g in groups.Values)
                {
                    if (!string.IsNullOrEmpty(g.afterDefeated) && !groups.ContainsKey(g.afterDefeated))
                    { errors.Add(context + g.id + " 的前置遭遇不存在。"); continue; }
                    var path = new HashSet<string>();
                    var current = g;
                    while (current != null && !string.IsNullOrEmpty(current.afterDefeated))
                    {
                        if (!path.Add(current.id)) { errors.Add(context + g.id + " 的击败前置形成循环。"); break; }
                        if (!groups.TryGetValue(current.afterDefeated, out current)) break;
                    }
                }
            }
            return errors;
        }
    }
}
