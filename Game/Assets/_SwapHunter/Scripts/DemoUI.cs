using UnityEngine;

namespace SwapHunter
{
    public sealed partial class DemoGame
    {
        static readonly Color Cyan = new Color(.29f, .94f, .84f), Muted = new Color(.76f, .82f, .86f), Ink = new Color(.035f, .055f, .07f, .98f);
        GUIStyle textStyle, buttonStyle;
        float viewWidth;
        void Styles()
        {
            if (textStyle != null) return;
            textStyle = new GUIStyle(GUI.skin.label) { font = font, wordWrap = true, richText = false, clipping = TextClipping.Overflow };
            buttonStyle = new GUIStyle(GUI.skin.button) { font = font, fontSize = 20, alignment = TextAnchor.MiddleLeft, padding = new RectOffset(20, 16, 8, 8) };
            buttonStyle.normal.background = Texture2D.whiteTexture; buttonStyle.hover.background = Texture2D.whiteTexture; buttonStyle.active.background = Texture2D.whiteTexture;
            buttonStyle.hover.background = UITexture(new Color(.19f,.34f,.36f)); buttonStyle.active.background = UITexture(new Color(.10f,.58f,.51f));
            buttonStyle.normal.textColor = Color.white; buttonStyle.hover.textColor = Color.white; buttonStyle.active.textColor = Color.white;
        }
        static Texture2D UITexture(Color color) { var t = new Texture2D(1,1); t.SetPixel(0,0,color); t.Apply(); return t; }
        void Fill(Rect r, Color color) { Color old = GUI.color; GUI.color = color; GUI.DrawTexture(r, Texture2D.whiteTexture); GUI.color = old; }
        void Text(string value, Rect r, int size = 20, Color? color = null, TextAnchor align = TextAnchor.UpperLeft)
        {
            textStyle.fontSize = size; textStyle.normal.textColor = color ?? Color.white; textStyle.alignment = align; GUI.Label(r, value, textStyle);
        }
        bool Button(string value, Rect r, bool primary = false)
        {
            bool enabled = GUI.enabled;
            bool hover = r.Contains(Event.current.mousePosition) && enabled;
            GUI.enabled = true;
            Color background = primary ? new Color(.07f, .43f, .39f) : new Color(.11f, .16f, .19f);
            if (hover) background = Color.Lerp(background, Cyan, .24f);
            if (!enabled) background *= .6f;
            Fill(r, background);
            Fill(new Rect(r.x, r.yMax - 2, r.width, 2), hover ? Cyan : new Color(.16f, .25f, .28f));
            Text(value, new Rect(r.x + 12, r.y, r.width - 24, r.height), r.width < 160 ? 16 : 20, enabled ? Color.white : Muted, TextAnchor.MiddleLeft);
            GUI.enabled = enabled;
            return GUI.Button(r, GUIContent.none, GUIStyle.none);
        }
        void OnGUI()
        {
            if (!font) return; Styles();
            Matrix4x4 previous = GUI.matrix; float scale = Screen.height / 900f; GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1)); viewWidth = Screen.width / scale;
            if (state == RunState.Loading) { Fill(new Rect(0, 0, viewWidth, 900), Ink); Text("正在准备相位装置…", new Rect(80, 390, 700, 60), 32); GUI.matrix = previous; return; }
            bool uiEnabled = GUI.enabled;
            if (settingsOpen) GUI.enabled = false;
            if (state == RunState.Menu) { if (campaignBoard) CampaignBoardUI(); else Menu(); }
            else if (state == RunState.Playing || state == RunState.Paused && !settlementPending || state == RunState.Dead && lastSettlement == null) Hud();
            if (state == RunState.Paused || state == RunState.Dead || state == RunState.Victory) Overlay();
            GUI.enabled = uiEnabled;
            if (settingsOpen) Settings();
            GUI.matrix = previous;
        }
        void Menu()
        {
            Fill(new Rect(0, 0, 660, 900), new Color(.025f, .045f, .060f, .95f));
            Fill(new Rect(65, 74, 45, 4), Cyan);
            Text("SWAPHUNTER   /   OPERATIONS DEMO 0.3.1", new Rect(65, 95, 520, 32), 16, Cyan);
            Text("换位猎手", new Rect(59, 172, 560, 92), 68);
            Text("抢的不是火力，\n而是开火的位置。", new Rect(65, 276, 510, 95), 28, new Color(.82f, .88f, .89f));
            Text("穿过空中货运港，夺取相位核心。\n首次行动，建议先完成基础教学。", new Rect(65, 395, 520, 80), 19, Muted);
            if (Button("合约行动 / 配置与成长     →", new Rect(65, 493, 450, 58), true)) OpenCampaignBoard();
            if (Button("基础教学与完整战役", new Rect(65, 564, 450, 48))) StartRun();
            if (RunStorage.Checkpoint > 0 && Button("继续上次检查点", new Rect(65, 623, 450, 42))) StartRun(false, true);
            float y = RunStorage.Checkpoint > 0 ? 677 : 625;
            if (Button("自由试招", new Rect(65, y, 214, 48))) StartRun(true);
            if (Button("设置", new Rect(294, y, 221, 48))) OpenSettings();
            if (Button("退出", new Rect(65, y + 62, 450, 44))) Application.Quit();
            Text(KeyName(0) + "/" + KeyName(1) + "/" + KeyName(2) + "/" + KeyName(3) + " 移动    " + KeyName(12) + " 开火\n" + KeyName(6) + " 换位    " + KeyName(7) + " 装填    " + KeyName(10) + "/" + KeyName(11) + " 切枪    " + KeyName(8) + " 交互", new Rect(65, 797, 560, 64), 16, Muted);
            Text("选择合约 / 战术装配 / 撤离成长", new Rect(viewWidth - 460, 832, 395, 30), 16, Color.white, TextAnchor.MiddleRight);
        }
        void Hud()
        {
            if (!player) return;
            Fill(new Rect(20, 20, Mathf.Min(700, viewWidth * .49f), 141), new Color(.025f, .04f, .05f, .65f));
            Fill(new Rect(viewWidth - 382, 22, 362, 100), new Color(.025f, .04f, .05f, .65f));
            Fill(new Rect(32, 30, 5, 68), Cyan);
            Text(stage.ToString("00") + "  /  " + areaNames[stage], new Rect(49, 29, 540, 28), 16, Cyan);
            Text(campaignActive ? activeContract.name : stageNames[stage], new Rect(49, 57, 500, 40), 30);
            Text(Objective, new Rect(49, 109, Mathf.Min(644, viewWidth * .49f - 54), 60), 19, new Color(.84f, .90f, .92f));
            Text(FormatTime(runSeconds), new Rect(viewWidth - 195, 32, 150, 35), 25, Color.white, TextAnchor.MiddleRight);
            if (stage > 0 && stage < 4) Text("在场敌人  " + AliveEnemies + "    /    第 " + wave + " 波", new Rect(viewWidth - 365, 77, 320, 36), 16, Muted, TextAnchor.MiddleRight);

            float x = viewWidth / 2, y = 450;
            Color reticle = player.target && player.swapReason == "可换位" ? Cyan : Color.white;
            DrawCrosshair(x, y, options, player.CurrentSpreadDegrees);
            if (player.hitFlash > 0 && options.hitMarkers) Text(player.killFlash > 0 ? "×" : "+", new Rect(x - 20, y - 27, 40, 54), 36, player.killFlash > 0 ? new Color(1, .67f, .24f) : Color.white, TextAnchor.MiddleCenter);
            if (player.blockFlash > 0 && options.hitMarkers)
            {
                Fill(new Rect(x + 85, 432, 245, 36), new Color(.025f, .035f, .04f, .95f));
                Text("格挡 · 未造成伤害", new Rect(x + 95, 434, 225, 32), 18, Amber, TextAnchor.MiddleCenter);
            }
            if (player.AimingAtShield)
            {
                Fill(new Rect(x - 290, 704, 580, 39), new Color(.025f, .035f, .04f, .94f));
                Text("盾牌不可击碎 · 绕侧，或 " + KeyName(6) + " 换位后转身射击身体", new Rect(x - 278, 709, 556, 30), 18, Amber, TextAnchor.MiddleCenter);
            }
            if (player.target && player.target.Alive)
            {
                Vector3 point = player.cameraView.WorldToViewportPoint(player.target.AimPoint);
                if (point.z > 0)
                {
                    float sx = point.x * viewWidth, sy = (1 - point.y) * 900;
                    Text("[       ]", new Rect(sx - 58, sy - 31, 116, 62), 42, reticle, TextAnchor.MiddleCenter);
                    Text(player.swapReason == "可换位" ? KeyName(6) + " 交换位置" : player.swapReason, new Rect(sx - 135, sy + 45, 270, 32), 17, reticle, TextAnchor.MiddleCenter);
                }
            }
            foreach (var enemy in enemies)
            {
                if (!enemy || !enemy.Alive || Vector3.Distance(player.transform.position, enemy.transform.position) > 28 || Physics.Linecast(player.Eye.position, enemy.AimPoint, Layers.WorldMask)) continue;
                Vector3 point = player.cameraView.WorldToViewportPoint(enemy.transform.position + Vector3.up * (enemy.Height + .35f));
                if (point.z <= 0 || point.x < 0 || point.x > 1 || point.y < 0 || point.y > 1) continue;
                float sx = point.x * viewWidth, sy = (1 - point.y) * 900;
                Fill(new Rect(sx - 103, sy - 25, 206, 26), new Color(.018f, .025f, .03f, .85f));
                Text(enemy.Label + (enemy.kind == EnemyKind.Shield || enemy.kind == EnemyKind.Elite ? " · 盾牌挡弹" : ""), new Rect(sx - 100, sy - 25, 200, 26), 16, enemy.kind == EnemyKind.Elite ? new Color(1, .72f, .43f) : Muted, TextAnchor.MiddleCenter);
                Fill(new Rect(sx - 40, sy + 3, 80, 4), new Color(.02f, .03f, .04f, .7f));
                Fill(new Rect(sx - 40, sy + 3, 80 * enemy.health / enemy.maximumHealth, 4), enemy.stunLeft > 0 ? Cyan : new Color(1, .46f, .25f));
                if (enemy.warningLeft > 0) Text("狙击锁定", new Rect(sx - 80, sy - 52, 160, 26), 15, new Color(1, .3f, .24f), TextAnchor.MiddleCenter);
            }
            Fill(new Rect(32, 778, 280, 94), new Color(.025f, .04f, .05f, .82f));
            Text("生命", new Rect(49, 790, 70, 25), 15, Muted);
            Text(Mathf.CeilToInt(player.health).ToString("000"), new Rect(48, 811, 105, 44), 33);
            Fill(new Rect(163, 830, 126, 8), new Color(.2f, .27f, .29f));
            Fill(new Rect(163, 830, 126 * player.health / config.playerHealth, 8), player.health < 30 ? new Color(1, .3f, .22f) : Cyan);
            Fill(new Rect(viewWidth - 308, 778, 276, 94), new Color(.025f, .04f, .05f, .82f));
            Text(player.weapon == 0 ? "01 / 脉冲卡宾枪" : "02 / 破门霰弹枪", new Rect(viewWidth - 289, 790, 240, 25), 15, Muted);
            Text(player.ammo[player.weapon].ToString("00"), new Rect(viewWidth - 289, 814, 80, 44), 34);
            Text("/ " + player.reserve[player.weapon], new Rect(viewWidth - 208, 828, 120, 30), 19, Muted);
            if (player.reloadLeft > 0) Text("装填中…", new Rect(viewWidth - 300, 735, 260, 32), 19, Cyan, TextAnchor.MiddleRight);
            float abilityX = x - 139;
            Fill(new Rect(abilityX, 796, 278, 77), new Color(.025f, .04f, .05f, .82f));
            Text(KeyName(6) + "  相位换位", new Rect(abilityX + 17, 808, 200, 26), 20, Cyan);
            Text(player.cooldown > 0 ? player.cooldown.ToString("F1") + " s" : "就绪", new Rect(abilityX + 185, 808, 75, 27), 19, Color.white, TextAnchor.MiddleRight);
            Fill(new Rect(abilityX + 17, 848, 244, 5), new Color(.20f, .27f, .29f));
            Fill(new Rect(abilityX + 17, 848, 244 * Mathf.Clamp01(1 - player.cooldown / CampaignCooldown), 5), Cyan);
            if (ToastVisible)
            {
                Fill(new Rect(x - 350, campaignActive ? 273 : 195, 700, 53), new Color(.02f, .045f, .06f, .9f));
                Text(toast, new Rect(x - 333, campaignActive ? 283 : 205, 666, 35), 19, Color.white, TextAnchor.MiddleCenter);
            }
            if (nearestTerminal && !nearestTerminal.activated)
                Text(KeyName(8) + "  " + (stage == 2 ? "接通继电器" : stage == 3 ? "获取核心" : stage == 4 ? "确认撤离" : "启动控制台"), new Rect(x - 190, 609, 380, 38), 23, Cyan, TextAnchor.MiddleCenter);
            if (player.targetArrowTime > 0)
            {
                Vector3 direction = player.lastSwapTarget - player.transform.position;
                float angle = Vector3.SignedAngle(player.transform.forward, Vector3.ProjectOnPlane(direction, Vector3.up), Vector3.up);
                Text(Mathf.Abs(angle) > 110 ? "原目标在身后  ↶" : angle > 20 ? "原目标在右侧  →" : angle < -20 ? "←  原目标在左侧" : "原目标在前方", new Rect(x - 220, 666, 440, 32), 18, Cyan, TextAnchor.MiddleCenter);
            }
            if (player.hurtFlash > 0)
            {
                Vector3 threat = Vector3.ProjectOnPlane(player.lastThreat - player.transform.position, Vector3.up);
                float direction = Vector3.SignedAngle(player.transform.forward, threat, Vector3.up);
                string label = Mathf.Abs(direction) > 135 ? "后方受击" : direction > 35 ? "右侧受击" : direction < -35 ? "左侧受击" : "前方受击";
                Text(label, new Rect(x - 100, y + 95, 200, 34), 18, new Color(1,.55f,.4f), TextAnchor.MiddleCenter);
                Color damage = new Color(1, .1f, .07f, player.hurtFlash * .6f);
                Fill(new Rect(0, 0, 25, 900), damage); Fill(new Rect(viewWidth - 25, 0, 25, 900), damage); Fill(new Rect(0, 875, viewWidth, 25), damage);
            }
            if (player.phaseFlash > 0) { Color phase = new Color(.1f, 1, .8f, player.phaseFlash * 1.2f); Fill(new Rect(0, 0, 12, 900), phase); Fill(new Rect(viewWidth - 12, 0, 12, 900), phase); }
            CampaignHudUI();
            Text(KeyName(10) + " / " + KeyName(11) + " 切枪     " + KeyName(7) + " 装填     Esc 暂停", new Rect(35, 875, 600, 22), 16, Muted, TextAnchor.MiddleLeft);
        }
        void Overlay()
        {
            if (settlementPending)
            {
                Fill(new Rect(0, 0, viewWidth, 900), Ink); float pendingX = viewWidth / 2 - 320;
                Text("行动已结束 · 等待保存", new Rect(pendingX, 270, 680, 70), 36, Amber);
                Text(campaignNotice, new Rect(pendingX, 362, 640, 135), 21);
                Text("战斗已冻结。保存成功后显示结算，不会重复发奖。", new Rect(pendingX, 509, 640, 70), 19, Muted);
                if (Button("重试保存结算", new Rect(pendingX, 612, 640, 58), true)) FinishContract(false);
                return;
            }
            if (lastSettlement != null && (state == RunState.Dead || state == RunState.Victory)) { CampaignSettlementUI(); return; }
            Fill(new Rect(0, 0, viewWidth, 900), new Color(.018f, .035f, .045f, .88f)); float x = viewWidth / 2 - 255;
            Text(state == RunState.Paused ? "暂停行动" : state == RunState.Dead ? "相位信号中断" : "撤离成功", new Rect(x, 224, 550, 73), 46, state == RunState.Dead ? new Color(1, .56f, .4f) : Color.white);
            Text(state == RunState.Paused ? "下一次换位，先想好落点。" : state == RunState.Dead ? "检查点已保留。观察威胁，换一个位置再来。" : "相位核心已回收。你带走了开火的位置。", new Rect(x, 312, 540, 65), 19, Muted);
            if (state == RunState.Victory)
            {
                Text("行动时间  " + FormatTime(runSeconds) + "    击破  " + totalKills + "\n换位  " + totalSwaps + " 次    重试  " + deaths + " 次", new Rect(x, 401, 530, 82), 25, Cyan);
                if (Button("再执行一次", new Rect(x, 523, 510, 58), true)) StartRun();
            }
            else if (Button(state == RunState.Paused ? "继续行动" : "从检查点重试", new Rect(x, 422, 510, 58), true)) { if (state == RunState.Paused) SetState(RunState.Playing); else Retry(); }
            float bottom = state == RunState.Victory ? 597 : 496;
            if (state == RunState.Paused && Button("设置", new Rect(x, bottom, 510, 50))) OpenSettings();
            if (Button(campaignActive ? (abandonConfirm ? "确认结束行动并按失败结算" : "放弃合约 / 查看结算") : "返回主菜单", new Rect(x, state == RunState.Paused ? bottom + 65 : bottom, 510, 50)))
            { if (campaignActive) { if (abandonConfirm) FinishContract(false); else abandonConfirm = true; } else SetState(RunState.Menu); }
            if (campaignActive && abandonConfirm) Text("未完成主目标没有收益；额外数据不会带出。", new Rect(x, bottom + 126, 560, 64), 18, Amber);
            if (campaignActive && !string.IsNullOrEmpty(campaignNotice)) Text(campaignNotice, new Rect(x, bottom + 190, 560, 70), 17, Amber);
        }
        void Settings() { DrawSettingsV2(); }
        public string KeyName(int i) => BindingInput.Label(options.actions[i].primary).Replace("鼠标左键", "M1").Replace("鼠标右键", "M2").Replace("鼠标侧键 ", "M").Replace("鼠标中键", "M3");
        static string FormatTime(float seconds) => ((int)seconds / 60).ToString("00") + ":" + ((int)seconds % 60).ToString("00");
    }
}


