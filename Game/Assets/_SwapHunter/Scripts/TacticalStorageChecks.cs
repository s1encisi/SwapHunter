using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
namespace SwapHunter
{
    public static class TacticalStorageChecks
    {
        [Serializable] sealed class EconomyReport
        {
            public string kind="Synthetic isolated account transactions; no replay/optional income; not combat or human playtesting";
            public int rulesVersion=TacticalPrototypeRules.Version;
            public bool complete;
            public List<TacticalEconomyRow> rows;
        }
        public static void Run(string fixtureRoot,Action<string,bool,string> check)
        {
            var rows=TacticalEconomyChecks.Run(fixtureRoot,check);
            string output=Path.Combine(fixtureRoot,"economy-paths-"+Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(output);
            var report=new EconomyReport{complete=rows.Count==48,rows=rows};
            File.WriteAllText(Path.Combine(output,"economy-paths.json"),JsonUtility.ToJson(report,true),new UTF8Encoding(false));
            var text=new StringBuilder("# 四职业逐章经济路径\n\n生产 Store API 的隔离合成交易；不是实战通关或真人平衡测试。无重玩、无可选收益；首章空道具，后续每章采购并在结算中消耗两医疗包与一弹药包（65信用点）。\n\n");
            text.Append("| 职业 | 章节 | 入账前余额 | 补给 | 成长支出 | 入场余额 | 首通收入 | 结余 | 武器ID | 技能等级 |\n|---|---:|---:|---:|---:|---:|---:|---:|---|---|\n");
            foreach(var r in rows)text.Append("| ").Append(TacticalPrototypeRules.ClassNames[r.classId]).Append(" | ").Append(r.chapter).Append(" | ").Append(r.opening).Append(" | ").Append(r.supplyCost).Append(" | ").Append(r.purchaseCost).Append(" | ").Append(r.beforeDeployment).Append(" | ").Append(r.income).Append(" | ").Append(r.closing).Append(" | ").Append(r.primary).Append("/").Append(r.secondary).Append(" | ").Append(r.classSkillLevel).Append("/").Append(r.generalSkillLevel).Append(" |\n");
            text.Append("\ncomplete=").Append(report.complete).Append("；失败详情以集中检查报告为准。每行操作：\n\n");
            foreach(var r in rows)if(!string.IsNullOrEmpty(r.purchases))text.Append("- ").Append(TacticalPrototypeRules.ClassNames[r.classId]).Append("第").Append(r.chapter).Append("章入场前：").Append(r.purchases).Append("。\n");
            File.WriteAllText(Path.Combine(output,"economy-paths.md"),text.ToString(),new UTF8Encoding(false));
            check("economy_paths_artifacts",report.complete,output);
        }
    }
}