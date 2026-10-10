using System;
using System.Collections.Generic;
using UnityEngine;

namespace SwapHunter
{
    public enum EncounterTrigger { Start, Region, Objective, ObjectiveCount, Elapsed, BossReady, BossRifle, BossCombined }
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
        public bool escort;
        public EncounterRole[] roles;
    }

    [Serializable]
    public sealed class MissionDefinition
    {
        public int level, requiredObjectives, minimumSwaps, maximumEscorts;
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
        static EncounterRole A(bool grenade = false) => new EncounterRole(EnemyKind.Assault, false, false, grenade);
        static EncounterRole S(bool required = false) => new EncounterRole(EnemyKind.Shield, required);
        static EncounterRole N() => new EncounterRole(EnemyKind.Sniper, false, true);
        static ObjectiveDefinition O(string id, string label) => new ObjectiveDefinition(id, label);
        static EncounterGroup G(string id, string label, EncounterTrigger trigger, EncounterRole[] roles,
            string region = null, string objective = null, int count = 0, float seconds = 0, float warning = 2.2f,
            string after = null, string alternative = null, string cancel = null, bool escort = false)
        {
            return new EncounterGroup { id = id, label = label, trigger = trigger, roles = roles, region = region,
                objective = objective, objectiveCount = count, triggerSeconds = seconds, warningSeconds = warning,
                afterDefeated = after, alternativeRegion = alternative, cancelOnObjective = cancel, escort = escort };
        }
        static MissionDefinition M(int level, string instruction, string extraction, string risk,
            ObjectiveDefinition[] objectives, EncounterGroup[] groups, int required = -1, int swaps = 0, int escorts = 0)
        {
            return new MissionDefinition { level = level, instruction = instruction, extraction = extraction,
                bonusRisk = risk, objectives = objectives, groups = groups,
                requiredObjectives = required < 0 ? objectives.Length : required, minimumSwaps = swaps, maximumEscorts = escorts };
        }

        public static readonly MissionDefinition[] Defaults = {
            M(0, "安全区完成一次双向换位，再解决侧台实战守卫", "前方校准出口", "入门章无额外数据",
                new ObjectiveDefinition[0], new[] {
                    G("calibration", "安全校准训练机", EncounterTrigger.Start, new[] { new EncounterRole(EnemyKind.Training) }),
                    G("combat", "侧台实战守卫", EncounterTrigger.Region, new[] { new EncounterRole(EnemyKind.Assault, true) }, region:"combat")
                }, swaps:1),
            M(1, "穿过两段盾线，解决两名指定盾卫；侧向突击兵可留在场内", "后方货检闸口", "引导章无额外数据",
                new ObjectiveDefinition[0], new[] {
                    G("first", "第一盾线", EncounterTrigger.Start, new[] { S(true) }),
                    G("second", "第二盾线与侧向火力", EncounterTrigger.Region, new[] { S(true), A() }, region:"second", after:"first")
                }),
            M(2, "取得上层通行件，再从上层通道离开；无需清空各层敌人", "上层通行出口", "引导章无额外数据",
                new[] { O("a", "上层通行件") }, new[] {
                    G("lower", "低位守卫", EncounterTrigger.Start, new[] { A() }),
                    G("middle", "中层楼梯守卫", EncounterTrigger.Region, new[] { A() }, region:"middle"),
                    G("upper", "上层目标守卫", EncounterTrigger.Region, new[] { A() }, region:"upper")
                }),
            M(3, "取得核心凭据，中央捷径开放后前往对侧撤离；无需全清", "对侧撤离口", "暴露侧台：绕路与截击射线",
                new[] { O("a", "核心凭据") }, new[] {
                    G("initial", "前段防线", EncounterTrigger.Start, new[] { S(), A() }),
                    G("intercept", "撤离侧入口截击", EncounterTrigger.Objective, new[] { A(), A() }, objective:"a", warning:3.2f)
                }),
            M(4, "夺取左右火控节点，分别打开对应遮蔽通路", "中央货运出口", "塔侧观察位：另一座塔的暴露射线",
                new[] { O("a", "左塔火控"), O("b", "右塔火控") }, new[] {
                    G("left", "左塔枪线与塔根", EncounterTrigger.Start, new[] { N(), A() }, warning:2.8f),
                    G("right", "右塔枪线与塔根", EncounterTrigger.Start, new[] { N(), A() }, seconds:1.2f, warning:3.4f),
                    G("guard", "中央盾卫", EncounterTrigger.Start, new[] { S() }),
                    G("reinforce", "首节点侧方增援", EncounterTrigger.ObjectiveCount, new[] { A() }, count:1, warning:3.2f)
                }),
            M(5, "用枪械、真实爆炸或近战摧毁两处封锁装置", "破开的货舱出口", "侧仓：额外交战和弹药支出",
                new[] { O("a", "封锁装置 A"), O("b", "封锁装置 B") }, new[] {
                    G("first", "投雷观察区", EncounterTrigger.Start, new[] { A(true), A() }, warning:2.8f),
                    G("second", "第二封锁防线", EncounterTrigger.Objective, new[] { A(), A() }, objective:"a"),
                    G("third", "侧仓防线", EncounterTrigger.Objective, new[] { A(), A() }, objective:"b", alternative:"side")
                }),
            M(6, "把任务锚点送入三处接入座；接入锁存，可使用维护复位", "C 岛出口", "C 岛侧支线：更长返程与暴露",
                new[] { O("a", "A 岛接入"), O("b", "B 岛接入"), O("c", "C 岛接入") }, new[] {
                    G("island-b", "B 岛狙击与侧卫", EncounterTrigger.Start, new[] { N(), A() }, warning:3),
                    G("island-c", "C 岛盾卫", EncounterTrigger.Start, new[] { S() }),
                    G("reinforce", "双接入后的本岛增援", EncounterTrigger.ObjectiveCount, new[] { A(), A() }, count:2, warning:3.4f)
                }),
            M(7, "A 窄廊、B 长线、C 折返：取任意两匣即可撤离，第三匣 +40", "仓区汇合出口", "第三分支：额外路线与独立交战",
                new[] { O("a", "A 仓数据"), O("b", "B 仓数据"), O("c", "C 仓数据") }, new[] {
                    G("warehouse-a", "A 仓窄口", EncounterTrigger.Region, new[] { S(), A() }, region:"a"),
                    G("warehouse-b", "B 仓长线", EncounterTrigger.Region, new[] { N(), A() }, region:"b"),
                    G("warehouse-c", "C 仓折返", EncounterTrigger.Region, new[] { A(), A() }, region:"c")
                }, required:2),
            M(8, "取得顶层核心，沿永久楼梯或换位路线下撤；上层敌人可遗留", "地面货运出口", "顶层侧路：额外暴露和下撤距离",
                new[] { O("a", "顶层核心") }, new[] {
                    G("top", "顶层核心防线", EncounterTrigger.Start, new[] { N(), A() }),
                    G("middle", "中层截击", EncounterTrigger.Region, new[] { S(), A(), A() }, region:"middle", objective:"a", warning:3),
                    G("bottom", "地面出口火网", EncounterTrigger.Region, new[] { N(), S(), A() }, region:"bottom", objective:"a", warning:3)
                }),
            M(9, "关闭三个控制站，取消对应入口尚未落地的预算；活敌不会消失", "控制站货运出口", "已关闭站点后方仓库：折返与资源消耗",
                new[] { O("a", "左控制站"), O("b", "右控制站"), O("c", "后控制站") }, new[] {
                    G("initial", "初始封锁", EncounterTrigger.Start, new[] { S(), A(), A() }),
                    G("left", "左入口增援", EncounterTrigger.Elapsed, new[] { N(), A() }, seconds:15, warning:4, cancel:"a"),
                    G("right", "右入口增援", EncounterTrigger.Elapsed, new[] { S(), A() }, seconds:30, warning:4, cancel:"b"),
                    G("rear", "后入口增援", EncounterTrigger.Elapsed, new[] { N(), A() }, seconds:45, warning:4, cancel:"c")
                }),
            M(10, "从外环取得核心权限并撤离；两处局部干扰器可分别关闭，无需全部关闭", "安全外环出口", "非必经干扰区：额外渗透风险",
                new[] { O("a", "核心权限") }, new[] {
                    G("left", "左干扰侧廊", EncounterTrigger.Region, new[] { S(), A(), A() }, region:"left"),
                    G("right", "右区长射线", EncounterTrigger.Region, new[] { N(), A() }, region:"right"),
                    G("core", "核心权限守卫", EncounterTrigger.Region, new[] { S(), A() }, region:"core")
                }),
            M(11, "取得双高台授权和地面核心，击败指挥官后从对侧离开；随从无需全清", "对侧货运出口", "侧台观察点：战斗中额外停留与暴露",
                new[] { O("a", "左高台授权"), O("b", "右高台授权"), O("c", "地面核心") }, new[] {
                    G("boss", "封锁指挥官", EncounterTrigger.Start, new[] { new EncounterRole(EnemyKind.Elite, true, true) }, warning:3),
                    G("phase-one", "能量阶段随从", EncounterTrigger.BossReady, new[] { A(), A() }, escort:true),
                    G("phase-two", "步枪阶段随从", EncounterTrigger.BossRifle, new[] { S(), A() }, warning:3, escort:true),
                    G("phase-three", "半血阶段随从", EncounterTrigger.BossCombined, new[] { N(), A() }, warning:3, escort:true)
                }, escorts:2)
        };

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
                if (level == 11 ? d.maximumEscorts < 1 || d.maximumEscorts > 2 : d.maximumEscorts != 0)
                    errors.Add(context + "仅 Boss 关可配置随从并发，且必须为 1 或 2。");

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
                    if (g.trigger == EncounterTrigger.Region && !StableId(g.region)) errors.Add(encounter + "区域触发必须填写区域 id。");
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
                        if (level == 11 && r.kind == EnemyKind.Elite)
                        {
                            bosses++;
                            if (g.id != "boss" || !r.required || g.escort) errors.Add(encounter + "指挥官须在 boss 组且为独立必清对象。");
                        }
                    }
                }
                var expectedGroups = new HashSet<string>();
                foreach (var g in Defaults[level].groups) expectedGroups.Add(g.id);
                if (!expectedGroups.SetEquals(groups.Keys)) errors.Add(context + "遭遇 id 与出生点绑定契约不一致；保留稳定键后可调整编制、触发和时机。");
                if (budget <= 0) errors.Add(context + "总敌人预算必须大于 0。");
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
