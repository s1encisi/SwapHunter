using System;
using System.IO;
using System.Collections;
using System.Text;
using UnityEngine;

namespace SwapHunter
{
    // A demo profile is an explicit content-preview seed, never fake completion
    // history. Gameplay still uses normal input, damage, objectives and settlement.
    public static class DemonstrationSession
    {
        public const string Capability = "SWAPHUNTER_ISOLATED_DEMO_V1";
        public const string Badge = "演示档 · 不写正式进度";
        public static int Level { get; private set; } = 1; // UI default: chapter 2.

        public static bool TryParseLevel(string[] arguments, out int level, out string error)
        {
            level = 1; error = "";
            int at = Array.IndexOf(arguments, "-demoLevel");
            if (at < 0) return true;
            if (at + 1 >= arguments.Length || !int.TryParse(arguments[at + 1], out int chapter) || chapter < 1 || chapter > 12)
            { error = "-demoLevel 必须为 1..12 的整数，例如 -swapHunterDemo -demoLevel 2。"; return false; }
            if (Array.IndexOf(arguments, "-demoLevel", at + 1) >= 0)
            { error = "-demoLevel 不能重复指定。"; return false; }
            level = chapter - 1; return true;
        }

        public static ExpeditionProfile CreateSeedProfile()
        {
            var profile = new ExpeditionProfile { highestUnlocked = 11, knifeRank = 3 };
            for (int i = 0; i < profile.weapons.Length; i++) profile.weapons[i] = true;
            // Leave cleared[], bestSeconds[], receipts, credits and specialization
            // ranks at their honest defaults. No synthetic Settle calls.
            ExpeditionStore.Validate(profile);
            return profile;
        }

        [Serializable] sealed class Manifest
        {
            public bool isDemonstration = true;
            public string build = Application.version;
            public string createdUtc = DateTime.UtcNow.ToString("o");
            public int selectedChapter;
            public string seed = "全武器、飞刀3级预置；无预置通关/成绩/收据/研究点，专精默认0阶。";
            public string rules = "正常输入、伤害、任务与结算规则；本进程全部档案和设置独立保存在临时目录。";
        }

        public static void Prepare()
        {
            if (!RunStorage.IsDemonstration) throw new InvalidOperationException("未启用独立演示模式，拒绝生成演示种子。");
            if (RunStorage.IsValidation) throw new InvalidOperationException("演示模式不能与 QA/性能测试参数混用；请单独启动。");
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-demoSmoke") >= 0 && !Application.isBatchMode)
                throw new InvalidOperationException("演示档案自检仅接受 -batchmode，不打开交互窗口。");
            if (!TryParseLevel(Environment.GetCommandLineArgs(), out int level, out string error))
                throw new ArgumentException(error);
            Level = RunStorage.IsDirectedTrial ? D2TrialCatalog.LevelFor(DirectedTrialBootstrap.Request.scenario) : level;
            bool tactical=RunStorage.IsTacticalDemo;
            string folder=Path.Combine(RunStorage.Root,tactical?"Expedition-Tactical-v6":"Expedition-v4");
            Directory.CreateDirectory(folder);
            var seed=tactical?TacticalDemoProfile.Create():CreateSeedProfile();
            if(tactical)
            {
                var args=Environment.GetCommandLineArgs();int at=Array.IndexOf(args,"-demoClass");
                if(at>=0){if(at+1>=args.Length||!int.TryParse(args[at+1],out int cls)||!TacticalPrototypeRules.ValidClass(cls))throw new ArgumentException("-demoClass需为0..3");seed.activeClass=cls;}
                at=Array.IndexOf(args,"-demoSkill");if(at>=0){if(at+1>=args.Length||!int.TryParse(args[at+1],out int choice)||choice<0||choice>1)throw new ArgumentException("-demoSkill需为0或1");seed.classLoadouts[seed.activeClass].classSkillId=TacticalPrototypeRules.SkillIds[seed.activeClass*2+choice];}
                seed.primary=seed.classLoadouts[seed.activeClass].primary;seed.secondary=seed.classLoadouts[seed.activeClass].secondary;
                ExpeditionStore.Validate(seed);
            }
            if(RunStorage.IsDirectedTrial)seed=DirectedTrialBootstrap.CreateProfile();
            WriteNew(Path.Combine(folder,tactical?"expedition-v6.json":"expedition-v4.json"),JsonUtility.ToJson(seed,true));
            WriteNew(Path.Combine(RunStorage.Root, "demonstration-session.json"), JsonUtility.ToJson(new Manifest { selectedChapter = Level + 1, seed=RunStorage.IsDirectedTrial?"定向商品与请求等级；其余技能1级、合法双枪、基础配件、零携带道具；情境="+DirectedTrialBootstrap.Request.scenario:tactical?"四职业全武器/技能3级、4医疗/2弹药/2护甲为展示预置；0信用点，无通关或收据。":"全武器、飞刀3级预置；无预置通关/成绩/收据/研究点，专精默认0阶。" }, true));
        }
        static void WriteNew(string path, string text)
        {
            byte[] bytes = new UTF8Encoding(false).GetBytes(text);
            using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            { stream.Write(bytes, 0, bytes.Length); stream.Flush(true); }
        }
    }

    public sealed partial class DemoGame
    {
        bool demonstrationPrepared;
        bool PrepareDemonstration()
        {
            try
            {
                DemonstrationSession.Prepare(); demonstrationPrepared = true;
                Debug.Log(DemonstrationSession.Capability);
                Debug.Log("SWAPHUNTER_DEMONSTRATION_ROOT=" + RunStorage.Root);
                return true;
            }
            catch (Exception error)
            {
                Debug.LogError("独立演示启动失败：" + error.Message);
                return false;
            }
        }
        void BeginDemonstration()
        {
            if(!RunStorage.IsDirectedTrial&&RunStorage.IsTacticalDemo&&Array.IndexOf(Environment.GetCommandLineArgs(),"-demoMenu")>=0){OpenExpeditionBoard();expeditionTab=2;Toast(DemonstrationSession.Badge+"；所有职业、武器和技能为展示预置。",8);return;}
            Record("demonstration_seed", (RunStorage.IsDirectedTrial?"directed-trial-product-level="+DirectedTrialBootstrap.Request.level+";scenario="+DirectedTrialBootstrap.Request.scenario+";":RunStorage.IsTacticalDemo?"tactical-all-skills-level3;":"legacy;")+"all-weapons;knife-rank=3;no-completion-history;temporary-profile;chapter=" + (DemonstrationSession.Level + 1));
            if (!BeginExpedition(DemonstrationSession.Level))
            {
                Record("demonstration_start_failed", expeditionNotice);
                Debug.LogError("演示章节部署失败：" + expeditionNotice);
                Application.Quit(2);
                return;
            }
            if(RunStorage.IsDirectedTrial)BeginD2TrialScenario();
            Toast(DemonstrationSession.Badge + (RunStorage.IsTacticalDemo?"；四职业武器、技能和道具为展示预置，正常伤害与任务规则。":"；武器/飞刀为演示预置，任务与伤害规则不变。"), 8);
            Record("demonstration_started", "chapter=" + (DemonstrationSession.Level + 1));
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-demoSmoke") >= 0)
                StartCoroutine(VerifyDemonstrationStorage());
        }

        [Serializable] sealed class DemonstrationSmokeReport
        {
            public string kind = "Native startup/storage isolation smoke; no UI, input skill or player-experience conclusion";
            public string root, formalRoot, profilePath, settingsPath;
            public bool passed, pathsIsolated, memoryCheckpoint, seedHasNoCompletions, normalInputMode, realChapterStarted;
            public bool savedDemoSettings, settledDemoFailure;
            public int chapter;
        }

        IEnumerator VerifyDemonstrationStorage()
        {
            yield return new WaitForSecondsRealtime(.15f);
            var report = new DemonstrationSmokeReport {
                root = RunStorage.Root, formalRoot = Application.persistentDataPath,
                profilePath = expeditionStore.SavePath, settingsPath = SettingsStore.FilePath,
                chapter = expeditionLevel + 1, normalInputMode = !qaMode,
                realChapterStarted = expeditionActive && mission != null && expeditionLevel == DemonstrationSession.Level
            };
            string prefix = Path.GetFullPath(report.root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            report.pathsIsolated = RunStorage.IsDemonstration && RunStorage.IsIsolated && !RunStorage.IsValidation
                && !string.Equals(Path.GetFullPath(report.root), Path.GetFullPath(report.formalRoot), StringComparison.OrdinalIgnoreCase)
                && Path.GetFullPath(report.profilePath).StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                && Path.GetFullPath(report.settingsPath).StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
            report.seedHasNoCompletions = expeditionStore.Profile.credits == 0 && expeditionStore.Profile.receipts.Count == 0;
            foreach (bool cleared in expeditionStore.Profile.cleared) report.seedHasNoCompletions &= !cleared;
            RunStorage.Checkpoint = 7;
            report.memoryCheckpoint = RunStorage.Checkpoint == 7 && RunStorage.HasCheckpoint;
            RunStorage.ClearCheckpoint(); report.memoryCheckpoint &= !RunStorage.HasCheckpoint;
            report.settledDemoFailure = FinishExpedition(false) && expeditionReceipt != null && !expeditionReceipt.success;
            report.savedDemoSettings = SaveSettings();
            report.passed = report.pathsIsolated && report.memoryCheckpoint && report.seedHasNoCompletions
                && report.normalInputMode && report.realChapterStarted && report.settledDemoFailure && report.savedDemoSettings;
            File.WriteAllText(Path.Combine(report.root, "demonstration-smoke-report.json"), JsonUtility.ToJson(report, true));
            Debug.Log("SWAPHUNTER_DEMO_SMOKE=" + report.passed + "; root=" + report.root);
            Application.Quit(report.passed ? 0 : 1);
        }

        void DrawDemonstrationBadge()
        {
            if (!RunStorage.IsDemonstration) return;
            bool enabled = GUI.enabled; GUI.enabled = true;
            float width = Mathf.Min(460, viewWidth - 24);
            Rect bounds = new Rect((viewWidth - width) * .5f, FullScreenGuiRect.yMin, width, 19);
            Fill(bounds, new Color(.025f, .10f, .13f, .96f));
            Text(DemonstrationSession.Badge, bounds, 13, new Color(.4f, 1, .9f), TextAnchor.MiddleCenter);
            GUI.enabled = enabled;
        }
    }
}
