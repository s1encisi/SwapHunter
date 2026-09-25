using System;
using System.IO;
using UnityEngine;
using UnityEngine.AI;

namespace SwapHunter
{
    public sealed partial class DemoGame
    {
        public CampaignCatalog catalog;
        public CampaignStore campaignStore;
        public Settlement lastSettlement;
        public bool campaignActive, campaignBoard;
        public string selectedContract, campaignRunId, campaignNotice = "";
        public ContractDefinition activeContract;
        public bool campaignMain, campaignBonus;
        public CampaignStation bonusStation, extractionStation;
        float guardUntil, breachUntil, anchorUntil;
        int moduleSlot;
        bool abandonConfirm;
        public bool settlementPending;
        bool pendingSuccess;
        ModuleDefinition Module(string id) => catalog.Module(id);
        public float CampaignCooldown => config.swapCooldown * (HasModule("overdrive") ? Module("overdrive").effectMultiplier : 1);
        public bool HasModule(string id) => campaignActive && campaignStore != null && campaignStore.HasModule(id);

        void InitializeCampaign()
        {
            try
            {
                catalog = CampaignCatalog.Load();
                selectedContract = catalog.contracts[0].id;
                string directory = Path.Combine(Application.persistentDataPath, "Campaign-v3");
                if (qaMode)
                {
                    var args = Environment.GetCommandLineArgs(); int at = Array.IndexOf(args, "-qaOutput");
                    directory = Path.Combine(at >= 0 && at + 1 < args.Length ? args[at + 1] : RunStorage.Root, "campaign-test-" + Guid.NewGuid().ToString("N"));
                }
                campaignStore = new CampaignStore(directory, catalog);
                lastSettlement = campaignStore.RecoverInterrupted();
                selectedContract = catalog.contracts[0].id;
                campaignNotice = campaignStore.LastWarning + (lastSettlement != null ? " 上次中断的行动已按失败规则结算一次，收入 " + lastSettlement.totalReward + "。" : "");
            }
            catch (Exception e) { campaignStore = null; campaignNotice = "行动档案暂不可用：" + e.Message; Debug.LogWarning(campaignNotice); }
        }
        public bool BeginContract(string id)
        {
            if (campaignActive || campaignStore == null) return false;
            try
            {
                var definition = catalog.Contract(id);
                string run = campaignStore.Begin(id);
                ClearCampaignStations(); campaignRunId = run; activeContract = definition;
                campaignActive = true; campaignMain = campaignBonus = false; practice = false;
                campaignBoard = false; abandonConfirm = false; lastSettlement = null;
                guardUntil = breachUntil = anchorUntil = 0;
                totalKills = totalSwaps = totalHits = totalShots = deaths = 0; runSeconds = 0;
                EnterStage(definition.stage, true);
                // Confine a contract to its arena; classic progression retains its own checkpoints.
                foreach (var gate in gates) gate.SetOpen(false);
                float entryZ = stage == 1 ? 33 : stage == 2 ? 77 : 121;
                Vector3 bonus = stage == 1 ? new Vector3(12, 4, 56) : stage == 2 ? new Vector3(-12, 4, 98) : new Vector3(12, 3.5f, 147);
                bonusStation = CampaignStation.Create(this, bonus, false);
                extractionStation = CampaignStation.Create(this, new Vector3(2, 0, entryZ + 1), true);
                SetState(RunState.Playing);
                campaignNotice = "";
                Toast("合约已开始 · 可选数据会呼叫两名增援，撤离后才兑现奖励", 5);
                Record("contract_start", id + ":" + run + ":" + string.Join(",", campaignStore.Profile.equipped));
                return true;
            }
            catch (Exception e)
            {
                campaignNotice = "未能开始行动：" + e.Message;
                if (campaignStore.Profile.activeRun != null)
                {
                    campaignRunId = campaignStore.Profile.activeRun.runId;
                    activeContract = catalog.Contract(campaignStore.Profile.activeRun.contractId);
                    campaignActive = true;
                    FinishContract(false);
                }
                return false;
            }
        }
        void ClearCampaignStations()
        {
            if (bonusStation) { bonusStation.gameObject.SetActive(false); Destroy(bonusStation.gameObject); }
            if (extractionStation) { extractionStation.gameObject.SetActive(false); Destroy(extractionStation.gameObject); }
            bonusStation = extractionStation = null;
        }
        public void OpenCampaignBoard()
        {
            if (campaignActive) return;
            ClearCampaignStations(); SetState(RunState.Menu); campaignBoard = true; abandonConfirm = false;
        }
        public CampaignStation FindCampaignStation()
        {
            if (!campaignActive || !player) return null;
            CampaignStation nearest = null; float distance = 2.8f;
            foreach (var station in new[] { bonusStation, extractionStation })
            {
                if (!station || !station.gameObject.activeSelf || !station.extract && campaignBonus) continue;
                Vector3 delta = station.Point - player.Eye.position;
                if (delta.magnitude >= distance || Vector3.Dot(delta.normalized, player.Eye.forward) < .15f) continue;
                if (Physics.Linecast(player.Eye.position, station.Point, out var sight, Layers.WorldMask)
                    && sight.collider.GetComponentInParent<CampaignStation>() != station) continue;
                nearest = station; distance = delta.magnitude;
            }
            return nearest;
        }
        public bool UseCampaignStation(CampaignStation station)
        {
            if (!campaignActive || !IsPlaying || !station || FindCampaignStation() != station) return false;
            if (station.extract)
            {
                if (!campaignMain) { Toast("先完成主目标，再回此处撤离", 2); return false; }
                return FinishContract(true);
            }
            if (campaignBonus) return false;
            try
            {
                campaignStore.MarkBonusComplete(campaignRunId); campaignBonus = true;
                float z = stage == 1 ? 57 : stage == 2 ? 103 : 146;
                EnemyActor.Spawn(EnemyKind.Assault, new Vector3(5, 0, z), stage);
                EnemyActor.Spawn(EnemyKind.Assault, new Vector3(-8, 0, z - 6), stage);
                sound.Play("warning", station.Point); Toast("数据已取得 · 两名增援抵达 · 成功撤离才获得额外收益", 4);
                Record("contract_bonus", activeContract.id); return true;
            }
            catch (Exception e) { Toast("保存失败，数据未领取：" + e.Message, 4); return false; }
        }
        bool CompleteContractMain()
        {
            try
            {
                campaignStore.MarkMainComplete(campaignRunId); campaignMain = true; stageComplete = true;
                Toast(campaignBonus ? "主目标与数据已取得 · 返回入口撤离" : "主目标已完成 · 返回入口撤离，或挑战橙色数据终端", 5);
                Record("contract_main", activeContract.id); return true;
            }
            catch (Exception e) { Toast("保存失败，请再次操作终端：" + e.Message, 4); return false; }
        }
        public bool FinishContract(bool success)
        {
            if (!campaignActive) return false;
            if (settlementPending) success = pendingSuccess;
            else { settlementPending = true; pendingSuccess = success; }
            try
            {
                lastSettlement = campaignStore.Settle(campaignRunId, success);
                Record("contract_settlement", JsonUtility.ToJson(lastSettlement));
                campaignActive = false; abandonConfirm = false; settlementPending = false; campaignNotice = ""; SetState(success ? RunState.Victory : RunState.Dead);
                return true;
            }
            catch (Exception e) { campaignNotice = "结算保存失败，可重试：" + e.Message; SetState(RunState.Paused); return false; }
        }
        public string CampaignObjective => campaignMain ? "主目标完成 · 返回入口的青色终端撤离" : ObjectiveClassic;
        public void OnCampaignSwap(SwapTarget target)
        {
            if (!campaignActive) return;
            if (HasModule("guard")) guardUntil = Time.time + Module("guard").duration;
            if (HasModule("breach")) breachUntil = Time.time + Module("breach").duration;
            if (HasModule("anchor") && target is PhaseAnchor) anchorUntil = Time.time + Module("anchor").duration;
        }
        public float ModuleDamageTaken => (HasModule("overdrive") ? Module("overdrive").penaltyMultiplier : 1) * (HasModule("guard") && Time.time < guardUntil ? Module("guard").effectMultiplier : 1);
        public float ModuleMovement => (HasModule("guard") ? Module("guard").penaltyMultiplier : 1) * (HasModule("anchor") && Time.time < anchorUntil ? Module("anchor").effectMultiplier : 1);
        public float ModuleReload => HasModule("scavenger") ? Module("scavenger").penaltyMultiplier : 1;
        public float ModuleAmmoSupply => HasModule("scavenger") ? Module("scavenger").effectMultiplier : 1;
        public float ModuleSpread => HasModule("stabilizer") && player && player.IsAiming ? Module("stabilizer").effectMultiplier : 1;
        public float ModuleAimMovement => HasModule("stabilizer") && player && player.IsAiming ? Module("stabilizer").penaltyMultiplier : 1;
        public float ModuleShotDamage(float distance) => HasModule("breach") && Time.time < breachUntil && distance <= Module("breach").range ? Module("breach").effectMultiplier : 1;
        public void ConsumeModuleShot() { breachUntil = 0; }
        public bool BreachReady => HasModule("breach") && Time.time < breachUntil;
        public bool GuardReady => HasModule("guard") && Time.time < guardUntil;
    }

    public sealed class CampaignStation : MonoBehaviour
    {
        public bool extract;
        public Vector3 Point => transform.position + Vector3.up;
        public Vector3 Approach => transform.position + Vector3.back * 2;
        public static CampaignStation Create(DemoGame game, Vector3 position, bool extraction)
        {
            var go = new GameObject(extraction ? "Contract extraction" : "Optional data / reinforcements");
            go.transform.SetParent(game.world); go.transform.position = position;
            var station = go.AddComponent<CampaignStation>(); station.extract = extraction;
            ImportedModels.Create(game.config.models.terminal, go.transform, Vector3.zero);
            go.layer = Layers.World;
            var body = go.AddComponent<BoxCollider>(); body.center = new Vector3(0, .7f, 0); body.size = new Vector3(1.02f, 1.4f, .8f);
            var obstacle = go.AddComponent<NavMeshObstacle>(); obstacle.shape = NavMeshObstacleShape.Box;
            obstacle.center = body.center; obstacle.size = body.size; obstacle.carving = true; obstacle.carveOnlyStationary = false;
            Shapes.Ring(go.transform, position + Vector3.up * .04f, 1, extraction ? new Color(.2f, .95f, .8f) : new Color(1, .65f, .23f), .055f);
            return station;
        }
    }
}

