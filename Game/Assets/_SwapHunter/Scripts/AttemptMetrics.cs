using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace SwapHunter
{
    [Serializable] public sealed class AttemptMetricsRecord
    {
        public string runId, mode, startedUtc, endedUtc, outcome;
        public int attempt, chapter, rulesVersion;
        public bool complete, evidenceComplete = true;
        public float activeSeconds, pausedSeconds, cumulativeActiveSeconds, cumulativePausedSeconds;
        public int medkitsUsed, ammoPacksUsed, armorPacksUsed, actualAmmoGranted;
        public float effectiveHealing, armorAbsorbed;
    }
    [Serializable] public sealed class AttemptMetricsEvent
    {
        public int schemaVersion = 1;
        public string kind, utc, detail;
        public AttemptMetricsRecord attempt;
        public DemoGame.ExpeditionResourceTelemetry resources;
        public ExpeditionReceipt settlement;
    }
    // A journal is evidence, never the authority for credits or inventory. An unmatched
    // attempt_start is an interrupted/incomplete attempt, not a successful or zero-cost run.
    public sealed class AttemptMetricsJournal
    {
        public readonly string PathName;
        public AttemptMetricsRecord Current { get; private set; }
        public string Notice { get; private set; } = "";
        string cumulativeRun = "";
        float cumulativeActive, cumulativePaused;
        bool failed;
        public AttemptMetricsJournal(string root)
        { PathName = Path.Combine(root, "Playtests", "attempts-" + Guid.NewGuid().ToString("N") + ".jsonl"); }
        public void Begin(string run, int attempt, int chapter, string mode, int rulesVersion,
            DemoGame.ExpeditionResourceTelemetry resources)
        {
            if (Current != null && !Current.complete) End("interrupted", resources);
            if (cumulativeRun != run) { cumulativeRun = run; cumulativeActive = cumulativePaused = 0; }
            Current = new AttemptMetricsRecord { runId=run, attempt=attempt, chapter=chapter,
                mode=mode, rulesVersion=rulesVersion, startedUtc=DateTime.UtcNow.ToString("o"),
                evidenceComplete=!failed, cumulativeActiveSeconds=cumulativeActive, cumulativePausedSeconds=cumulativePaused };
            Write("attempt_start", "Missing attempt_end means incomplete evidence.", resources);
        }
        public void Tick(float elapsed, bool playing, bool paused)
        {
            if (Current == null || Current.complete) return;
            if (playing) Current.activeSeconds += elapsed;
            else if (paused) Current.pausedSeconds += elapsed;
            Current.cumulativeActiveSeconds = cumulativeActive + Current.activeSeconds;
            Current.cumulativePausedSeconds = cumulativePaused + Current.pausedSeconds;
        }
        public void Consume(int type, float effective, DemoGame.ExpeditionResourceTelemetry resources)
        {
            if (Current == null || Current.complete) return;
            if (type == 0) { Current.medkitsUsed++; Current.effectiveHealing += effective; }
            else if (type == 1) { Current.ammoPacksUsed++; Current.actualAmmoGranted += Mathf.RoundToInt(effective); }
            else if (type == 2) Current.armorPacksUsed++;
            else throw new ArgumentOutOfRangeException(nameof(type));
            Write("consumable_use", TacticalPrototypeRules.ConsumableNames[type], resources);
        }
        public void Absorb(float amount)
        { if (Current != null && !Current.complete) Current.armorAbsorbed += amount; }
        public void Event(string kind, string detail, DemoGame.ExpeditionResourceTelemetry resources)
        { if (Current != null && !Current.complete) Write(kind, detail, resources); }
        public void End(string outcome, DemoGame.ExpeditionResourceTelemetry resources)
        {
            if (Current == null || Current.complete) return;
            Current.complete = true; Current.outcome = outcome; Current.endedUtc = DateTime.UtcNow.ToString("o");
            cumulativeActive += Current.activeSeconds; cumulativePaused += Current.pausedSeconds;
            Current.cumulativeActiveSeconds=cumulativeActive; Current.cumulativePausedSeconds=cumulativePaused;
            Write("attempt_end", "Attempt use is not permanent inventory debit.", resources);
        }
        public void Settlement(ExpeditionReceipt receipt, DemoGame.ExpeditionResourceTelemetry resources)
        {
            if (Current == null || Current.runId != receipt.runId) return;
            Write("settlement_committed", "Only emitted after ExpeditionStore persisted the receipt.", resources, receipt);
        }
        void Write(string kind, string detail, DemoGame.ExpeditionResourceTelemetry resources, ExpeditionReceipt receipt=null)
        {
            if (failed) return;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(PathName));
                File.AppendAllText(PathName, JsonUtility.ToJson(new AttemptMetricsEvent { kind=kind,
                    utc=DateTime.UtcNow.ToString("o"), detail=detail, attempt=Current, resources=resources,
                    settlement=receipt }) + Environment.NewLine, new UTF8Encoding(false));
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
            {
                failed=true; if (Current != null) Current.evidenceComplete=false;
                Notice="本次行动记录不完整：" + error.Message + "。资产以结算收据为准。";
                Debug.LogWarning(Notice);
            }
        }
    }
    public sealed partial class DemoGame
    {
        AttemptMetricsJournal attemptJournal;
        public AttemptMetricsRecord CurrentAttemptMetrics => attemptJournal?.Current;
        public string AttemptMetricsNotice => attemptJournal?.Notice ?? "";
        public string AttemptMetricsPath => attemptJournal?.PathName ?? "";
        public string AttemptMetricsSummary
        {
            get
            {
                var a=CurrentAttemptMetrics; if(a==null)return "";
                return "第"+a.attempt+"次尝试：有效用时 "+a.activeSeconds.ToString("0.0")+"秒 / 暂停 "+a.pausedSeconds.ToString("0.0")+
                    "秒；本行动累计 "+a.cumulativeActiveSeconds.ToString("0.0")+"秒\n"+
                    "本次使用：医疗 "+a.medkitsUsed+"（实回 "+a.effectiveHealing.ToString("0")+"） / 弹药 "+a.ammoPacksUsed+
                    "（实补 "+a.actualAmmoGranted+"发） / 护甲 "+a.armorPacksUsed+"（实吸 "+a.armorAbsorbed.ToString("0")+"）"+
                    (a.outcome=="death"?"；失败不永久扣除道具；重试时恢复出发库存。":"");
            }
        }
        ExpeditionResourceTelemetry AttemptResourceState()
        {
            bool has=player; var equipped=has?player.EquippedWeapons:new int[0];
            var weapons=new WeaponResourceTelemetry[equipped.Length];
            for(int i=0;i<equipped.Length;i++){int w=equipped[i];weapons[i]=new WeaponResourceTelemetry{slot=i,weaponId=w,magazine=player.ammo[w],reserve=player.reserve[w]};}
            return new ExpeditionResourceTelemetry{runId=expeditionRun,level=expeditionLevel,hasPlayer=has,
                health=has?player.health:0,temporaryArmor=has?player.TemporaryArmor:0,medkits=expeditionMedkits,
                ammoPacks=expeditionAmmoPacks,armorPacks=expeditionArmorPacks,weapons=weapons,
                committed=expeditionReceipt!=null,success=expeditionReceipt!=null&&expeditionReceipt.success};
        }
        void BeginAttemptMetrics()
        {
            if(!TacticalExpedition)return;
            if(attemptJournal==null)attemptJournal=new AttemptMetricsJournal(RunStorage.Root);
            var d=expeditionStore.Profile.deployment;
            attemptJournal.Begin(d.runId,d.attempts,d.level+1,RunStorage.IsValidation?"QA":RunStorage.IsDemonstration?"Trial":"Normal",
                d.rulesVersion,AttemptResourceState());
        }
        void TickAttemptMetrics()
        { attemptJournal?.Tick(Time.unscaledDeltaTime,IsPlaying,state==RunState.Paused); }
        void EndAttemptMetrics(string outcome)
        { if(attemptJournal!=null)attemptJournal.End(outcome,AttemptResourceState()); }
        void RecordAttemptConsumable(int type,float effective)
        { if(attemptJournal!=null)attemptJournal.Consume(type,effective,AttemptResourceState()); }
        public void RecordAttemptArmorAbsorbed(float amount)
        { attemptJournal?.Absorb(amount); }
        void RecordAttemptSettlement(ExpeditionReceipt receipt)
        { if(attemptJournal!=null)attemptJournal.Settlement(receipt,AttemptResourceState()); }
        void RecordAttemptEvent(string kind,string detail)
        {
            if(attemptJournal==null)return;
            // Resource snapshots at actual branch/objective events allow paired analysis;
            // they do not estimate an unplayed alternative route.
            if(kind=="mission_region"||kind=="mission_objective"||kind=="mission_bonus"||kind=="encounter_trigger")
                attemptJournal.Event(kind,detail,AttemptResourceState());
        }
    }
}
