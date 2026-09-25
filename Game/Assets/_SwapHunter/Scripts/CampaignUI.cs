using UnityEngine;

namespace SwapHunter
{
    public sealed partial class DemoGame
    {
        static readonly Color Amber = new Color(1, .70f, .34f), Panel = new Color(.045f, .075f, .095f, .98f);
        void RuleLine(float x, float y, float width) { Fill(new Rect(x, y, width, 1), new Color(.20f, .30f, .34f)); }
        void CampaignBoardUI()
        {
            Fill(new Rect(0, 0, viewWidth, 900), new Color(.018f, .032f, .045f, .98f));
            float left = 48, gap = 28, lw = (viewWidth - 124) * .37f, right = left + lw + gap, rw = viewWidth - right - 48;
            Text("SWAPHUNTER  /  OPERATIONS", new Rect(left, 35, 700, 28), 16, Cyan);
            Text("行动准备", new Rect(left, 70, 600, 65), 43);
            Text("选择目标，配置你的换位方式。", new Rect(left, 132, 650, 35), 20, Muted);
            if (Button("返回", new Rect(viewWidth - 180, 48, 132, 45))) campaignBoard = false;
            if (campaignStore == null || catalog == null)
            { Text(campaignNotice, new Rect(left, 230, viewWidth - 96, 240), 23, Amber); return; }
            Text("研究点  " + campaignStore.Profile.credits, new Rect(viewWidth - 440, 111, 390, 36), 24, Cyan, TextAnchor.MiddleRight);
            RuleLine(left, 184, viewWidth - 96);
            Text("01   选择合约", new Rect(left, 207, lw, 34), 23);
            for (int i = 0; i < catalog.contracts.Length; i++)
            {
                var contract = catalog.contracts[i]; float y = 258 + i * 137;
                bool selected = selectedContract == contract.id;
                Fill(new Rect(left, y, lw, 122), selected ? new Color(.065f, .18f, .19f) : Panel);
                Fill(new Rect(left, y, 4, 122), selected ? Cyan : new Color(.15f, .24f, .28f));
                if (GUI.Button(new Rect(left, y, lw, 122), GUIContent.none, GUIStyle.none)) { selectedContract = contract.id; sound.Play("confirm"); }
                Text(contract.name, new Rect(left + 19, y + 12, lw - 40, 34), 24, selected ? Cyan : Color.white);
                Text(contract.brief, new Rect(left + 19, y + 51, lw - 38, 40), 17, new Color(.78f, .85f, .87f));
                Text("撤离 +" + contract.reward + "   /   可选数据 +" + contract.bonusReward, new Rect(left + 19, y + 91, lw - 38, 27), 16, Amber);
            }
            var chosen = catalog.Contract(selectedContract);
            Text("结算规则", new Rect(left, 688, lw, 30), 19, Cyan);
            Text("主目标完成后需返回入口撤离。\n数据终端会呼叫增援，额外奖励仅撤离兑现。\n失败：主目标未完成为 0，已完成保留 " + chosen.failureReward + "。", new Rect(left, 725, lw, 107), 18, new Color(.78f, .85f, .87f));
            Text("02   战术模块", new Rect(right, 207, rw, 34), 23);
            int energy = 0;
            for (int slot = 0; slot < 2; slot++)
            {
                string id = campaignStore.Profile.equipped[slot]; var module = string.IsNullOrEmpty(id) ? null : catalog.Module(id);
                if (module != null) energy += module.energy;
                float w = (rw - 14) / 2;
                if (Button((moduleSlot == slot ? "● " : "○ ") + "槽位 " + (slot + 1) + " / " + (module == null ? "空" : module.name), new Rect(right + slot * (w + 14), 254, w, 48), moduleSlot == slot)) moduleSlot = slot;
            }
            Text("能量 " + energy + " / " + catalog.energyBudget + "   ·   先选槽位，再装备模块", new Rect(right, 313, rw - 160, 30), 18, Cyan);
            if (Button("清空此槽", new Rect(right + rw - 142, 308, 142, 38)))
            { try { if (!campaignStore.Equip("", moduleSlot, out var error)) campaignNotice = error; else campaignNotice = "槽位已清空"; } catch (System.Exception e) { campaignNotice = "档案写入失败：" + e.Message; } }
            float cardW = (rw - 14) / 2;
            for (int i = 0; i < catalog.modules.Length; i++)
            {
                var module = catalog.modules[i]; float x = right + (i % 2) * (cardW + 14), y = 362 + (i / 2) * 145;
                bool owned = campaignStore.Profile.unlocked.Contains(module.id);
                bool equipped = System.Array.IndexOf(campaignStore.Profile.equipped, module.id) >= 0;
                Fill(new Rect(x, y, cardW, 133), equipped ? new Color(.065f, .145f, .16f) : Panel);
                Text(module.name + "  / " + module.energy + " 能量", new Rect(x + 13, y + 9, cardW - 25, 30), 21, equipped ? Cyan : Color.white);
                Text(module.description, new Rect(x + 13, y + 43, cardW - 25, 39), 16, new Color(.86f, .9f, .91f));
                Text(module.tradeoff, new Rect(x + 13, y + 83, cardW - 139, 40), 15, Amber);
                bool enabled = GUI.enabled; GUI.enabled = !equipped && (owned || campaignStore.Profile.credits >= module.cost);
                string label = equipped ? "已装备" : owned ? "装备" : module.cost + " 解锁";
                if (Button(label, new Rect(x + cardW - 119, y + 88, 106, 34), owned && !equipped))
                {
                    try
                    {
                        bool ok = owned ? campaignStore.Equip(module.id, moduleSlot, out var error) : campaignStore.Unlock(module.id, out error);
                        campaignNotice = ok ? owned ? "模块已装备" : "解锁完成，再点装备放入选中的槽位" : error;
                        if (ok) sound.Play("confirm");
                        Record(owned ? "module_equip" : "module_unlock", module.id + ":" + campaignNotice);
                    }
                    catch (System.Exception e) { campaignNotice = "档案写入失败：" + e.Message; }
                }
                GUI.enabled = enabled;
            }
            var firstUnlock = catalog.Module("anchor");
            Text(string.IsNullOrEmpty(campaignNotice) && campaignStore.Profile.credits < firstUnlock.cost ? "本合约带出数据可得 " + (chosen.reward + chosen.bonusReward) + " 研究点；信标耦合需 " + firstUnlock.cost + "。" : campaignNotice, new Rect(right, 805, rw - 278, 69), 17, Amber);
            if (Button("部署行动   →", new Rect(right + rw - 262, 809, 262, 58), true)) BeginContract(selectedContract);
        }

        void CampaignSettlementUI()
        {
            Fill(new Rect(0, 0, viewWidth, 900), new Color(.018f, .035f, .045f, 1));
            float x = viewWidth / 2 - 440;
            Text("OPERATIONS  /  AFTER ACTION", new Rect(x, 126, 880, 32), 16, Cyan);
            Text(lastSettlement.success ? "行动完成" : "行动中断", new Rect(x, 179, 880, 73), 49, lastSettlement.success ? Color.white : Amber);
            Text(catalog.Contract(lastSettlement.contractId).name, new Rect(x, 268, 880, 40), 25, Muted);
            RuleLine(x, 329, 880);
            Text("主目标", new Rect(x, 359, 280, 32), 20, Muted);
            Text(lastSettlement.mainComplete ? "已完成" : "未完成", new Rect(x + 290, 359, 220, 32), 23, lastSettlement.mainComplete ? Cyan : Muted);
            Text("数据回收", new Rect(x, 412, 280, 32), 20, Muted);
            Text(lastSettlement.bonusComplete ? lastSettlement.success ? "已带出" : "行动失败，未带出" : "未领取", new Rect(x + 290, 412, 430, 32), 23, Amber);
            Text((lastSettlement.success ? "主目标奖励  +" : "失败补偿  +") + lastSettlement.baseReward + "     可选目标  +" + lastSettlement.bonusReward, new Rect(x, 478, 880, 40), 23);
            Fill(new Rect(x, 537, 880, 93), Panel);
            Text("研究点收入  +" + lastSettlement.totalReward, new Rect(x + 22, 561, 500, 50), 30, Cyan);
            Text("余额  " + lastSettlement.creditsAfter, new Rect(x + 550, 568, 300, 40), 23, Color.white, TextAnchor.MiddleRight);
            Text("用时 " + FormatTime(runSeconds) + "   ·   换位 " + totalSwaps + "   ·   击破 " + totalKills + "\n奖励已存入本地档案，重复打开结算不会重复发放。", new Rect(x, 657, 880, 74), 19, Muted);
            if (Button("调整配置，开始下一份合约   →", new Rect(x, 761, 560, 58), true)) OpenCampaignBoard();
            if (Button("主菜单", new Rect(x + 584, 761, 296, 58))) { ClearCampaignStations(); SetState(RunState.Menu); }
        }
        void CampaignHudUI()
        {
            if (!campaignActive) return;
            float x = viewWidth / 2;
            Fill(new Rect(32, 180, 430, 80), new Color(.025f, .04f, .05f, .82f));
            Text("可选数据   " + (campaignBonus ? "已取得 · 撤离后兑现" : "+" + activeContract.bonusReward + "  /  会呼叫增援"), new Rect(48, 192, 396, 27), 18, Amber);
            Text("主目标 " + (campaignMain ? "已完成" : "进行中") + "   /   预计撤离收入 " + (activeContract.reward + (campaignBonus ? activeContract.bonusReward : 0)), new Rect(48, 225, 396, 27), 17, Muted);
            DrawStationMarker(extractionStation, campaignMain ? "撤离" : "入口 / 完成目标后撤离", Cyan);
            if (!campaignBonus) DrawStationMarker(bonusStation, "可选数据 / 增援风险", Amber);
            var station = FindCampaignStation();
            if (station) Text(KeyName(8) + "  " + (station.extract ? campaignMain ? "确认撤离" : "完成主目标后可撤离" : "读取数据并呼叫增援"), new Rect(x - 270, 606, 540, 39), 22, station.extract ? Cyan : Amber, TextAnchor.MiddleCenter);
            string activeModules = GuardReady && BreachReady ? "护幕生效  /  近距强化：下一枪" : GuardReady ? "相位护幕生效" : BreachReady ? "近距强化：下一枪" : string.Join(" / ", System.Array.ConvertAll(campaignStore.Profile.equipped, id => string.IsNullOrEmpty(id) ? "空槽" : catalog.Module(id).name));
            Text(activeModules, new Rect(x - 260, 752, 520, 30), 17, Cyan, TextAnchor.MiddleCenter);
        }
        void DrawStationMarker(CampaignStation station, string label, Color color)
        {
            if (!station) return;
            Vector3 direction = station.Point - player.Eye.position;
            Vector3 v = player.cameraView.WorldToViewportPoint(station.Point + Vector3.up * 1.1f);
            if (v.z < 0 || v.x < .08f || v.x > .92f || v.y < .15f || v.y > .78f)
            {
                if (!station.extract || !campaignMain) return;
                float angle = Vector3.SignedAngle(player.transform.forward, Vector3.ProjectOnPlane(direction, Vector3.up), Vector3.up);
                Text((angle < 0 ? "← " : "→ ") + "入口撤离  " + direction.magnitude.ToString("F0") + " m", new Rect(viewWidth / 2 - 190, 350, 380, 34), 20, color, TextAnchor.MiddleCenter); return;
            }
            Text(label + "\n" + direction.magnitude.ToString("F0") + " m", new Rect(v.x * viewWidth - 155, (1 - v.y) * 900 - 30, 310, 60), 17, color, TextAnchor.MiddleCenter);
        }
    }
}
