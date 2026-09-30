using UnityEngine;

namespace SwapHunter
{
    public sealed partial class DemoGame
    {
        static readonly Color Cyan = new Color(.40f, .78f, 1f), Muted = new Color(.72f, .78f, .88f), Ink = new Color(.016f, .026f, .055f, .98f);
        GUIStyle textStyle, buttonStyle;
        float viewWidth, uiScaleFactor=1, uiOffsetY;
        Matrix4x4 lastUiMatrix=Matrix4x4.identity;
        Rect FullScreenGuiRect => new Rect(0,-uiOffsetY/uiScaleFactor,viewWidth,Screen.height/uiScaleFactor);
        Vector2 ViewportToUi(Vector3 point) => new Vector2(point.x*viewWidth,((1-point.y)*Screen.height-uiOffsetY)/uiScaleFactor);
        internal Vector2 HudMarkerScreenPosition(Vector3 worldPoint)
        {
            Vector2 point=ViewportToUi(player.cameraView.WorldToViewportPoint(worldPoint));
            Vector3 screen=lastUiMatrix.MultiplyPoint3x4(new Vector3(point.x,point.y,0));return new Vector2(screen.x,screen.y);
        }
        internal Rect HudScreenMaskBounds
        {
            get { Rect r=FullScreenGuiRect;Vector3 a=lastUiMatrix.MultiplyPoint3x4(new Vector3(r.xMin,r.yMin,0)),b=lastUiMatrix.MultiplyPoint3x4(new Vector3(r.xMax,r.yMax,0));return Rect.MinMaxRect(a.x,a.y,b.x,b.y); }
        }
        void Styles()
        {
            if (textStyle != null) return;
            textStyle = new GUIStyle(GUI.skin.label) { font = font, wordWrap = true, richText = false, clipping = TextClipping.Overflow };
            buttonStyle = new GUIStyle(GUI.skin.button) { font = font, fontSize = 20, alignment = TextAnchor.MiddleLeft, padding = new RectOffset(20, 16, 8, 8) };
            buttonStyle.normal.background = Texture2D.whiteTexture; buttonStyle.hover.background = Texture2D.whiteTexture; buttonStyle.active.background = Texture2D.whiteTexture;
            buttonStyle.hover.background = UITexture(new Color(.12f,.29f,.55f)); buttonStyle.active.background = UITexture(new Color(.08f,.39f,.94f));
            buttonStyle.normal.textColor = Color.white; buttonStyle.hover.textColor = Color.white; buttonStyle.active.textColor = Color.white;
        }
        static Texture2D UITexture(Color color) { var t = new Texture2D(1,1); t.SetPixel(0,0,color); t.Apply(); return t; }
        void Fill(Rect r, Color color) { Color old = GUI.color; GUI.color = color; GUI.DrawTexture(r, Texture2D.whiteTexture); GUI.color = old; }
        void Text(string value, Rect r, int size = 20, Color? color = null, TextAnchor align = TextAnchor.UpperLeft)
        {
            textStyle.fontSize = size; textStyle.normal.textColor = color ?? Color.white; textStyle.alignment = align; GUI.Label(r, value, textStyle);
        }
        // Shared by settings, campaign and expedition screens.
        void DrawUiPanel(Rect r, bool accent = false)
        {
            Fill(r,new Color(.025f,.045f,.09f,.94f));
            Fill(new Rect(r.x,r.y,r.width,1),new Color(.20f,.29f,.43f,.75f));
            if(accent)Fill(new Rect(r.x,r.y,4,r.height),Cyan);
        }
        void SlashBand(Rect r, Color color, float angle)
        {
            Matrix4x4 saved=GUI.matrix;
            GUIUtility.RotateAroundPivot(angle,r.center); Fill(r,color); GUI.matrix=saved;
        }
        bool Button(string value, Rect r, bool primary = false)=>NavigationButton(value,r,primary);
        void OnGUI()
        {
            if (!font) return; Styles();BeginUiNavigation();
            Matrix4x4 previous=GUI.matrix; bool previousEnabled=GUI.enabled;
            float scale=Screen.height/900f*options.uiScale;float margin=(Screen.height-900*scale)*.5f;
            if(margin>0&&(state==RunState.Menu||settingsOpen||expeditionInventory))
            {Fill(new Rect(0,0,Screen.width,margin),Ink);Fill(new Rect(0,Screen.height-margin,Screen.width,margin),Ink);}
            GUI.matrix=Matrix4x4.TRS(new Vector3(0,margin,0),Quaternion.identity,new Vector3(scale,scale,1));viewWidth=Screen.width/scale;
            uiScaleFactor=scale;uiOffsetY=margin;lastUiMatrix=GUI.matrix;
            try
            {
                if(state==RunState.Loading)
                {
                    Fill(FullScreenGuiRect,Ink);Fill(new Rect(0,440,viewWidth*.38f,8),Cyan);
                    Text("正在接入相位信标",new Rect(64,345,800,70),38);
                    Text("准备行动区域…",new Rect(68,468,800,36),18,Muted);return;
                }
                if(settingsOpen)GUI.enabled=false;
                bool expeditionScreen=DrawExpeditionScreen();
                if(!expeditionScreen)
                {
                    if(state==RunState.Menu){if(campaignBoard)CampaignBoardUI();else Menu();}
                    else if(state==RunState.Playing||state==RunState.Paused&&!settlementPending||state==RunState.Dead&&lastSettlement==null)Hud();
                    if(state==RunState.Paused||state==RunState.Dead||state==RunState.Victory){if(!DrawTutorialCompletion())Overlay();}
                }
                GUI.enabled=previousEnabled;
                if(settingsOpen)Settings();
            }
            finally { GUI.enabled=previousEnabled;GUI.matrix=previous; }
        }
        void Menu(){BrandedMenu();}
        void Hud()
        {
            if (!player) return;
            DrawUiPanel(new Rect(20,20,Mathf.Min(700,viewWidth*.49f),141),true);
            DrawUiPanel(new Rect(viewWidth-302,22,282,88));
            Fill(new Rect(32, 30, 5, 68), Cyan);
            Text(expeditionActive?"相位行动  /  "+(expeditionLevel+1).ToString("00"):stage.ToString("00")+"  /  "+areaNames[stage],new Rect(49,29,540,28),16,Cyan);
            Text(tutorialCourse&&tutorialCourse.Active?tutorialCourse.Title:expeditionActive ? ExpeditionTitle : campaignActive ? activeContract.name : stageNames[stage], new Rect(49, 57, 500, 40), 30);
            Text(Objective, new Rect(49, 109, Mathf.Min(644, viewWidth * .49f - 54), 60), 19, new Color(.84f, .90f, .92f));
            Text(FormatTime(runSeconds), new Rect(viewWidth - 195, 32, 150, 35), 25, Color.white, TextAnchor.MiddleRight);
            if(expeditionActive) Text("必需 "+ExpeditionRequiredEnemies+" / 数据追兵 "+ExpeditionOptionalEnemies,new Rect(viewWidth-345,74,300,30),16,Muted,TextAnchor.MiddleRight);
            else if (AliveEnemies>0) Text("在场敌人  "+AliveEnemies,new Rect(viewWidth-275,74,230,30),16,Muted,TextAnchor.MiddleRight);

            float x = viewWidth / 2, y = 450;
            Color reticle = player.target && player.swapReason == "可换位" ? Cyan : Color.white;
            DrawCrosshair(x, y, options, player.CurrentSpreadDegrees);
            if (player.hitFlash > 0 && options.hitMarkers) Text(player.killFlash > 0 ? "×" : "+", new Rect(x - 20, y - 27, 40, 54), 36, player.killFlash > 0 ? new Color(1, .67f, .24f) : Color.white, TextAnchor.MiddleCenter);
            if (player.blockFlash > 0)
            {
                Fill(new Rect(x + 85, 432, 245, 36), new Color(.025f, .035f, .04f, .95f));
                Text("格挡 · 未造成伤害", new Rect(x + 95, 434, 225, 32), 18, Amber, TextAnchor.MiddleCenter);
            }
            if(player.hitFlash>0&&!string.IsNullOrEmpty(player.HitRegionLabel)&&options.hitMarkers)
                Text(player.HitRegionLabel,new Rect(x+40,y-13,145,30),16,player.killFlash>0?Amber:Color.white);
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
                    Vector2 projected=ViewportToUi(point);float sx=projected.x,sy=projected.y;
                    Text("[       ]", new Rect(sx - 58, sy - 31, 116, 62), 42, reticle, TextAnchor.MiddleCenter);
                    Text(player.swapReason == "可换位" ? KeyName(6) + " 交换位置" : player.swapReason, new Rect(sx - 135, sy + 45, 270, 32), 17, reticle, TextAnchor.MiddleCenter);
                }
            }
            foreach (var enemy in enemies)
            {
                if (!enemy || !enemy.Alive || Vector3.Distance(player.transform.position, enemy.transform.position) > 28 || Physics.Linecast(player.Eye.position, enemy.AimPoint, Layers.WorldMask)) continue;
                Vector3 point = player.cameraView.WorldToViewportPoint(enemy.transform.position + Vector3.up * (enemy.Height + .35f));
                if (point.z <= 0 || point.x < 0 || point.x > 1 || point.y < 0 || point.y > 1) continue;
                Vector2 projected=ViewportToUi(point);float sx=projected.x,sy=projected.y;
                Fill(new Rect(sx - 103, sy - 25, 206, 26), new Color(.018f, .025f, .03f, .85f));
                Text(enemy.Label + (enemy.boss ? (enemy.boss.Energy>0 ? " · 能量护层" : " · 核心暴露") : enemy.HasPhysicalShield ? " · 盾牌挡弹" : ""), new Rect(sx - 100, sy - 25, 200, 26), 16, enemy.kind == EnemyKind.Elite ? new Color(1, .72f, .43f) : Muted, TextAnchor.MiddleCenter);
                Fill(new Rect(sx - 40, sy + 3, 80, 4), new Color(.02f, .03f, .04f, .7f));
                Fill(new Rect(sx - 40, sy + 3, 80 * enemy.health / enemy.maximumHealth, 4), enemy.stunLeft > 0 ? Cyan : new Color(1, .46f, .25f));
                if (enemy.warningLeft > 0) Text("狙击锁定", new Rect(sx - 80, sy - 52, 160, 26), 15, new Color(1, .3f, .24f), TextAnchor.MiddleCenter);
            }
            DrawUiPanel(new Rect(32,778,280,94),true);
            Text("生命", new Rect(49, 790, 70, 25), 15, Muted);
            Text(Mathf.CeilToInt(player.health).ToString("000"), new Rect(48, 811, 105, 44), 33);
            Fill(new Rect(163, 830, 126, 8), new Color(.2f, .27f, .29f));
            Fill(new Rect(163, 830, 126 * player.health / config.playerHealth, 8), player.health < 30 ? new Color(1, .3f, .22f) : Cyan);
            DrawUiPanel(new Rect(viewWidth-348,778,316,94),true);
            Text(player.CurrentWeaponName,new Rect(viewWidth-329,790,278,25),16,Muted);
            Text(player.ammo[player.weapon].ToString("00"), new Rect(viewWidth - 329, 814, 95, 44), 34);
            Text("/ " + player.reserve[player.weapon], new Rect(viewWidth - 229, 828, 170, 30), 19, Muted);
            if (player.reloadLeft > 0) Text("装填中…", new Rect(viewWidth - 300, 735, 260, 32), 19, Cyan, TextAnchor.MiddleRight);
            float abilityX = x - 139;
            DrawUiPanel(new Rect(abilityX,796,278,77),true);
            Text(KeyName(6) + "  相位换位", new Rect(abilityX + 17, 808, 200, 26), 20, Cyan);
            Text(player.cooldown > 0 ? player.cooldown.ToString("F1") + " s" : "就绪", new Rect(abilityX + 185, 808, 75, 27), 19, Color.white, TextAnchor.MiddleRight);
            Fill(new Rect(abilityX + 17, 848, 244, 5), new Color(.20f, .27f, .29f));
            Fill(new Rect(abilityX + 17, 848, 244 * Mathf.Clamp01(1 - player.cooldown / Mathf.Max(.01f,CampaignCooldown*ExpeditionCooldownMultiplier)), 5), Cyan);
            if (ToastVisible)
            {
                bool bossPresent=false;
                foreach(var enemy in enemies)if(enemy&&enemy.Alive&&enemy.boss){bossPresent=true;break;}
                Rect notice=bossPresent?new Rect(32,178,Mathf.Clamp(viewWidth-692,240,650),64):new Rect(x-350,campaignActive?273:195,700,53);
                Fill(notice, new Color(.02f, .045f, .06f, .9f));
                Text(toast, new Rect(notice.x+17,notice.y+10,notice.width-34,notice.height-20),bossPresent?18:19,Color.white,TextAnchor.MiddleCenter);
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
                Rect bounds=FullScreenGuiRect;Fill(new Rect(0,bounds.yMin,25,bounds.height),damage);Fill(new Rect(viewWidth-25,bounds.yMin,25,bounds.height),damage);Fill(new Rect(0,bounds.yMax-25,viewWidth,25),damage);
            }
            if (player.phaseFlash > 0) { Color phase = new Color(.1f, 1, .8f, player.phaseFlash * 1.2f); Rect bounds=FullScreenGuiRect;Fill(new Rect(0,bounds.yMin,12,bounds.height),phase);Fill(new Rect(viewWidth-12,bounds.yMin,12,bounds.height),phase); }
            CampaignHudUI();DrawBossHud();DrawTacticsHud();
            if(!string.IsNullOrEmpty(player.ActionLabel))Text(player.ActionLabel,new Rect(33,738,330,32),18,Cyan);
            if(player.ChargeProgress>0){Fill(new Rect(x-72,y+35,144,4),new Color(.1f,.16f,.24f));Fill(new Rect(x-72,y+35,144*player.ChargeProgress,4),Cyan);}
            Text(KeyName(10) + " / " + KeyName(11) + " 切枪     " + KeyName(7) + " 装填     Esc 暂停", new Rect(35, 875, 600, 22), 16, Muted, TextAnchor.MiddleLeft);
        }
        void Overlay()
        {
            if(expeditionActive){DrawExpeditionOverlay();return;}
            if (settlementPending)
            {
                Fill(FullScreenGuiRect, Ink); float pendingX = viewWidth / 2 - 320;
                Text("行动已结束 · 等待保存", new Rect(pendingX, 270, 680, 70), 36, Amber);
                Text(campaignNotice, new Rect(pendingX, 362, 640, 135), 21);
                Text("战斗已冻结。保存成功后显示结算，不会重复发奖。", new Rect(pendingX, 509, 640, 70), 19, Muted);
                if (Button("重试保存结算", new Rect(pendingX, 612, 640, 58), true)) FinishContract(false);
                return;
            }
            if (lastSettlement != null && (state == RunState.Dead || state == RunState.Victory)) { CampaignSettlementUI(); return; }
            Fill(FullScreenGuiRect,new Color(.009f,.015f,.035f,.89f)); float x=Mathf.Max(60,viewWidth*.17f);
            SlashBand(new Rect(viewWidth*.68f,-90,90,1100),new Color(.07f,.27f,.70f,.48f),-12);
            Fill(new Rect(x,183,52,5),state==RunState.Dead?Amber:Cyan);
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
        public string KeyName(int i) => BindingInput.Label(options.actions!=null&&i>=0&&i<options.actions.Length&&options.actions[i]!=null?options.actions[i].primary:"").Replace("鼠标左键", "M1").Replace("鼠标右键", "M2").Replace("鼠标侧键 ", "M").Replace("鼠标中键", "M3");
        static string FormatTime(float seconds) => ((int)seconds / 60).ToString("00") + ":" + ((int)seconds % 60).ToString("00");
    }
}









