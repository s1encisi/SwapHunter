using UnityEngine;

namespace SwapHunter
{
    public sealed partial class DemoGame
    {
        WeaponModelPreview weaponLibraryPreview;
        WeaponDisplayStats[] weaponLibraryStats;
        bool weaponLibraryWasVisible;
        internal WeaponModelPreview WeaponLibraryPreview => weaponLibraryPreview;
        internal void SelectWeaponForLibraryReview(int id) { if (WeaponCatalog.Valid(id)) expeditionSelectedWeapon = id; }

        void UpdateWeaponLibraryPresentation()
        {
            UpdateClassPresentation();
            bool visible = expeditionStore != null && ((state == RunState.Menu && expeditionBoard && expeditionTab == 1)
                || (expeditionActive && expeditionInventory));
            if (visible && !weaponLibraryWasVisible)
            {
                if (!weaponLibraryPreview)
                {
                    weaponLibraryPreview = gameObject.AddComponent<WeaponModelPreview>();
                    weaponLibraryPreview.Initialize(config.models);
                }
                weaponLibraryStats = WeaponDisplayStats.BuildAll(config);
            }
            if (weaponLibraryPreview)
            {
                weaponLibraryPreview.SetVisible(visible);
                if (visible && expeditionBoard && !expeditionInventory) weaponLibraryPreview.Select(expeditionSelectedWeapon);
                if (!visible || expeditionInventory) weaponLibraryPreview.AutoRotate = false;
            }
            weaponLibraryWasVisible = visible;
        }

        static string WeaponUnlockLabel(int id)
        {
            foreach (var level in ExpeditionCatalog.Levels)
                if (level.weaponUnlock == id) return "第 " + (level.id + 1) + " 章解锁";
            return "初始武器";
        }

        void WeaponTexture(Rect area, Texture texture, bool dim = false)
        {
            if (!texture) { Text("模型载入中", area, 13, Muted, TextAnchor.MiddleCenter); return; }
            Color saved = GUI.color;
            GUI.color = dim ? new Color(.58f, .63f, .70f, 1) : Color.white;
            GUI.DrawTexture(area, texture, ScaleMode.ScaleToFit, true);
            GUI.color = saved;
        }

        void WeaponText(string value, Rect rect, int size, Color color, TextAnchor anchor = TextAnchor.UpperLeft)
        {
            var clipping = textStyle.clipping;
            textStyle.clipping = TextClipping.Clip;
            Text(value, rect, size, color, anchor);
            textStyle.clipping = clipping;
        }

        void DrawWeaponLibrary()
        {
            var store = expeditionStore;
            var profile = store.Profile;
            float available = viewWidth - 96;
            int columns = available >= 1300 ? 3 : 2;
            int rows = WeaponCatalog.Count / columns;
            float listWidth = Mathf.Clamp(available * .40f, 360, 710);
            float detailX = 48 + listWidth + 24, detailWidth = viewWidth - detailX - 48;
            const float top = 274, bottom = 816;
            float cardTop = top + 32, cardHeight = (bottom - 28 - cardTop - (rows - 1) * 8) / rows;
            float cardWidth = (listWidth - (columns - 1) * 8) / columns;
            Text("武器库  /  12", new Rect(48, top, listWidth, 26), 18, Paper);
            for (int id = 0; id < WeaponCatalog.Count; id++)
            {
                var weapon = WeaponCatalog.Get(id);
                Rect card = new Rect(48 + id % columns * (cardWidth + 8), cardTop + id / columns * (cardHeight + 8), cardWidth, cardHeight);
                bool selected = expeditionSelectedWeapon == id;
                if (NavigationButton(weapon.name, card, selected, false)) expeditionSelectedWeapon = id;
                bool usable = store.CanEquip(id);
                string status = usable && store.Primary == id ? "主槽" : usable && store.Secondary == id ? "副槽" : store.IsTactical ? (profile.weapons[id]?"其他职业":TacticalPrototypeRules.WeaponListed(id,profile.cleared)?"可采购":"未上架") : usable ? "已解锁" : "未解锁";
                Text((id + 1).ToString("00"), new Rect(card.x + 10, card.y + 5, 30, 17), 12, selected ? Paper : Muted);
                WeaponText(status, new Rect(card.x + 44, card.y + 3, card.width - 54, 25), 11,
                    usable ? PhaseBlue : Muted, TextAnchor.UpperRight);
                Rect icon = new Rect(card.x + 10, card.y + 29, card.width - 20, card.height - 59);
                WeaponTexture(icon, weaponLibraryPreview ? weaponLibraryPreview.GetIcon(id) : null, !usable);
                WeaponText(weapon.name, new Rect(card.x + 10, card.yMax - 27, card.width - 20, 27),
                    columns == 3 ? 15 : 16, usable ? Paper : Muted, TextAnchor.MiddleCenter);
            }
            Text(store.IsTactical ? "武器由全档共享，按职业资格装配。\n全部模型可预览；上架后可直接采购。" : "全部武器均可预览；未解锁武器不能装备",
                new Rect(48, bottom - 18, listWidth, store.IsTactical ? 38 : 24), store.IsTactical ? 12 : 13, Muted);

            int selectedId = expeditionSelectedWeapon;
            var selectedWeapon = WeaponCatalog.Get(selectedId);
            bool selectedUsable = store.CanEquip(selectedId);
            bool canConfigure = !store.IsTactical || store.Profile.deployment == null;
            Rect panel = new Rect(detailX, top, detailWidth, bottom - top + 18);
            DrawUiPanel(panel, true);
            float x = panel.x + 22, width = panel.width - 44;
            WeaponText(selectedWeapon.name, new Rect(x, top + 15, width - 130, 43), 31, Paper);
            Text(store.IsTactical ? store.WeaponAvailability(selectedId) : selectedUsable ? "已解锁" : WeaponUnlockLabel(selectedId),
                new Rect(panel.xMax - 192, top + 13, 170, 47), 13, selectedUsable ? PhaseBlue : Muted, TextAnchor.MiddleRight);
            if (weaponLibraryStats == null) return;
            var stats = weaponLibraryStats[selectedId];
            Text(stats.MechanismLabel + "  ·  常规初始备弹 " + stats.BaseReserve,
                new Rect(x, top + 62, width, 23), 16, Muted);
            WeaponText(stats.FireCadenceExplanation, new Rect(x, top + 86, width, 26), 13, Muted);

            float contentTop = top + 122, modelWidth = (width - 24) * .53f, statsX = x + modelWidth + 24;
            float statsWidth = panel.xMax - 22 - statsX;
            Rect modelRect = new Rect(x, contentTop, modelWidth, 205);
            Fill(modelRect, new Color(.018f, .036f, .065f));
            Fill(new Rect(modelRect.x, modelRect.yMax - 1, modelRect.width, 1), new Color(.16f, .27f, .38f));
            var preview = weaponLibraryPreview;
            if (preview && !string.IsNullOrEmpty(preview.SelectedError))
                Text("模型预览暂不可用", modelRect, 17, Muted, TextAnchor.MiddleCenter);
            else WeaponTexture(modelRect, preview ? preview.SelectedTexture : null);
            Text("对应武器模型", new Rect(modelRect.x + 9, modelRect.y + 7, modelRect.width - 18, 18), 12, Muted);

            float controlsY = modelRect.yMax + 8, buttonWidth = (modelWidth - 18) / 4;
            if (Button("左转", new Rect(x, controlsY, buttonWidth, 31)) && preview) preview.RotateYaw(-30);
            if (Button("复位", new Rect(x + buttonWidth + 6, controlsY, buttonWidth, 31)) && preview) preview.ResetView();
            if (Button("右转", new Rect(x + (buttonWidth + 6) * 2, controlsY, buttonWidth, 31)) && preview) preview.RotateYaw(30);
            if (Button(preview && preview.AutoRotate ? "停止" : "自动", new Rect(x + (buttonWidth + 6) * 3, controlsY, buttonWidth, 31)) && preview) preview.AutoRotate = !preview.AutoRotate;
            WeaponText(selectedWeapon.description, new Rect(x, controlsY + 42, modelWidth, 42), 14, Muted);

            Text("基础属性  ·  统一刻度", new Rect(statsX, contentTop - 21, statsWidth, 20), 13, PhaseBlue);
            float rowHeight = 50;
            for (int rowIndex = 0; rowIndex < stats.Rows.Length; rowIndex++)
            {
                var row = stats.Rows[rowIndex]; float y = contentTop + rowIndex * rowHeight;
                WeaponText(row.Label, new Rect(statsX, y, statsWidth * .57f, 32), 13, Paper, TextAnchor.MiddleLeft);
                WeaponText(row.ValueText, new Rect(statsX + statsWidth * .46f, y, statsWidth * .54f, 32), 17, Paper, TextAnchor.MiddleRight);
                Rect track = new Rect(statsX, y + 33, statsWidth, 6);
                Fill(track, new Color(.10f, .16f, .23f));
                if (row.IsApplicable) Fill(new Rect(track.x, track.y, track.width * Mathf.Clamp01(row.Normalized), track.height),
                    rowIndex == 1 || rowIndex == 2 || rowIndex == 4 ? new Color(.91f, .65f, .36f) : PhaseBlue);
            }
            WeaponText(store.IsTactical ? "基础值未计配件与战斗加成；时长、散布越低越好" : "基础值未计配件、专精与战斗加成；条形表示数值大小", new Rect(x, bottom - 124, width, 23), 11, Muted, TextAnchor.MiddleLeft);
            float actionY = bottom - 100, slotWidth = Mathf.Min(125, width * .20f);
            for (int slot = 0; slot < 2; slot++)
                if (Button(slot == 0 ? "主武器槽" : "副武器槽", new Rect(x + slot * (slotWidth + 8), actionY, slotWidth, 36), expeditionWeaponSlot == slot)) expeditionWeaponSlot = slot;
            bool previous = GUI.enabled;
            GUI.enabled = previous && selectedUsable && canConfigure;
            float equipX = x + (slotWidth + 8) * 2;
            bool equipped = selectedUsable && (expeditionWeaponSlot == 0 ? store.Primary : store.Secondary) == selectedId;
            bool starterOffer=store.IsTactical&&!profile.weapons[selectedId]&&(selectedId==0||selectedId==1||selectedId==2||selectedId==3||selectedId==9);
            bool buyable=store.IsTactical&&!profile.weapons[selectedId]&&!starterOffer&&TacticalPrototypeRules.WeaponListed(selectedId,profile.cleared);
            int price=store.IsTactical?TacticalPrototypeRules.WeaponPrice(selectedId):0;
            GUI.enabled=previous&&canConfigure&&((selectedUsable&&!equipped)||starterOffer||(buyable&&profile.credits>=price));
            string actionLabel=equipped?"已装备至所选槽":selectedUsable?"装备至所选槽":starterOffer?"前往职业入门保障":buyable?"购买 / "+price+" 信用点":store.IsTactical?"当前不可装备":"完成章节后解锁";
            if(Button(actionLabel,new Rect(equipX,actionY,panel.xMax-22-equipX,36),true))
                ExpeditionAction(()=>{if(starterOffer){if(selectedId!=0)for(int cls=0;cls<4;cls++)if(TacticalPrototypeRules.StarterWeapon(cls)==selectedId){store.SelectClass(cls);break;}expeditionTab=2;}else if(buyable)store.BuyWeapon(System.Guid.NewGuid().ToString("N"),selectedId);else store.Equip(expeditionWeaponSlot,selectedId);});
            GUI.enabled = previous && selectedUsable && canConfigure;
            int attachment = store.AttachmentFor(selectedId);
            if (Button("组件：" + ExpeditionCatalog.Attachments[attachment] + "  ›", new Rect(x, bottom - 54, width, 33)))
                ExpeditionAction(() => expeditionStore.SetAttachment(selectedId, (attachment + 1) % ExpeditionCatalog.Attachments.Length));
            GUI.enabled = previous;
            WeaponText(ExpeditionCatalog.AttachmentRules[attachment] + (store.IsTactical ? "；关外免费装配，行动中锁定。" : "；部署前可免费调整。"), new Rect(x, bottom - 15, width, 24), 12, Muted);
        }

        void DrawInventoryWeaponCard(int slot, Rect card)
        {
            int id = player.EquippedWeapons[slot];
            DrawUiPanel(card, true);
            Text(slot == 0 ? "主武器" : "副武器", new Rect(card.x + 20, card.y + 15, card.width - 40, 23), 15, PhaseBlue);
            WeaponText(WeaponCatalog.Get(id).name, new Rect(card.x + 20, card.y + 48, card.width - 40, 36), 28, Paper);
            WeaponTexture(new Rect(card.x + 20, card.y + 94, card.width * .46f, 91), weaponLibraryPreview ? weaponLibraryPreview.GetIcon(id) : null);
            float x = card.x + card.width * .54f, width = card.width * .46f - 22;
            int capacity = player.Capacity(id), reserveLimit = ExpeditionReserveLimit(id);
            Text("弹匣 " + player.ammo[id] + " / " + capacity, new Rect(x, card.y + 98, width, 26), 20, Paper);
            Fill(new Rect(x, card.y + 130, width, 6), new Color(.10f, .16f, .23f));
            Fill(new Rect(x, card.y + 130, width * Mathf.Clamp01(player.ammo[id] / (float)Mathf.Max(1, capacity)), 6), PhaseBlue);
            Text("当前备弹 " + player.reserve[id], new Rect(x, card.y + 151, width, 23), 17, Muted);
            Fill(new Rect(x, card.y + 181, width, 4), new Color(.10f, .16f, .23f));
            Fill(new Rect(x, card.y + 181, width * Mathf.Clamp01(player.reserve[id] / (float)Mathf.Max(1, reserveLimit)), 4), PhaseBlue);
        }
    }
}
