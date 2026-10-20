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
                weaponLibraryPreview.AutoRotate=visible&&!expeditionInventory&&armoryAutoRotate;
            }
            if(knifePreview)knifePreview.SetVisible(visible&&!expeditionInventory&&expeditionWeaponSlot==2);
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

        int selectedArmoryKnife;
        bool armoryAutoRotate=true;
        void DrawWeaponLibrary()
        {
            var store=expeditionStore;var profile=store.Profile;
            float available=viewWidth-96,slotWidth=(available-24)/3;
            int equippedKnife=profile.classLoadouts[profile.activeClass].knifeId;
            for(int slot=0;slot<3;slot++)
            {
                string caption=slot==0?"1  主武器":slot==1?"2  副武器":"3  近战武器";
                string name=slot==2?KnifeV2Visual.Names[Mathf.Clamp(equippedKnife,0,5)]:WeaponCatalog.Get(slot==0?store.Primary:store.Secondary).name;
                Rect box=new Rect(48+slot*(slotWidth+12),274,slotWidth,74);
                if(NavigationButton(caption,box,expeditionWeaponSlot==slot,false))
                {
                    expeditionWeaponSlot=slot;
                    if(slot==2)selectedArmoryKnife=Mathf.Clamp(equippedKnife,0,5);
                    else expeditionSelectedWeapon=slot==0?store.Primary:store.Secondary;
                }
                Text(caption,new Rect(box.x+16,box.y+8,box.width-32,24),15,expeditionWeaponSlot==slot?Amber:Muted);
                WeaponText(name,new Rect(box.x+16,box.y+35,box.width-32,30),21,Paper);
            }
            float listWidth=Mathf.Clamp(available*.40f,360,650),detailX=48+listWidth+24;
            Rect panel=new Rect(detailX,374,viewWidth-detailX-48,455);
            Text(expeditionWeaponSlot==2?"近战刀具 / 6":"枪械 / 12",new Rect(48,363,listWidth,28),17,Muted);
            DrawUiPanel(panel,true);
            if(expeditionWeaponSlot==2)
            { DrawUnifiedKnifeArmory(listWidth,panel);return; }
            int columns=3,rows=(WeaponCatalog.Count+columns-1)/columns;
            float cardWidth=(listWidth-16)/columns,cardHeight=(422-(rows-1)*8)/rows;
            for(int id=0;id<WeaponCatalog.Count;id++)
            {
                var item=WeaponCatalog.Get(id);bool usable=store.CanEquip(id),selected=expeditionSelectedWeapon==id;
                Rect card=new Rect(48+id%columns*(cardWidth+8),397+id/columns*(cardHeight+8),cardWidth,cardHeight);
                if(NavigationButton(item.name,card,selected,false))expeditionSelectedWeapon=id;
                string status=store.Primary==id?"主槽":store.Secondary==id?"副槽":usable?"可装备":profile.weapons[id]?"其他职业":"未拥有";
                WeaponText(status,new Rect(card.x+8,card.y+4,card.width-16,20),11,selected?Amber:Muted,TextAnchor.MiddleRight);
                WeaponTexture(new Rect(card.x+8,card.y+23,card.width-16,card.height-49),weaponLibraryPreview?weaponLibraryPreview.GetIcon(id):null,!usable);
                WeaponText(item.name,new Rect(card.x+8,card.yMax-27,card.width-16,25),14,Paper,TextAnchor.MiddleCenter);
            }
            int selectedId=expeditionSelectedWeapon;
            var weapon=WeaponCatalog.Get(selectedId);bool usableSelected=store.CanEquip(selectedId);
            float x=panel.x+20,y=panel.y+17,width=panel.width-40;
            WeaponText(weapon.name,new Rect(x,y,width*.65f,40),29,Paper);
            WeaponText(store.IsTactical?store.WeaponAvailability(selectedId):usableSelected?"已解锁":WeaponUnlockLabel(selectedId),new Rect(x+width*.65f,y,width*.35f,43),13,usableSelected?Cyan:Muted,TextAnchor.MiddleRight);
            if(weaponLibraryStats==null)return;
            var stats=weaponLibraryStats[selectedId];
            WeaponText(stats.MechanismLabel+" · "+stats.FireCadenceExplanation,new Rect(x,y+48,width,36),14,Muted);
            float modelWidth=width*.55f,statsX=x+modelWidth+20,statsWidth=width-modelWidth-20;
            Rect modelRect=new Rect(x,y+93,modelWidth,190);
            DrawArmoryPreview(modelRect,weaponLibraryPreview?weaponLibraryPreview.SelectedTexture:null);
            WeaponText(weapon.description,new Rect(x,modelRect.yMax+8,modelWidth,40),13,Muted);
            for(int index=0;index<stats.Rows.Length;index++)
            {
                var row=stats.Rows[index];float rowY=y+86+index*36;
                WeaponText(row.Label,new Rect(statsX,rowY,statsWidth*.52f,25),12,Muted,TextAnchor.MiddleLeft);
                WeaponText(row.ValueText,new Rect(statsX+statsWidth*.49f,rowY,statsWidth*.51f,25),15,Paper,TextAnchor.MiddleRight);
                Fill(new Rect(statsX,rowY+26,statsWidth,3),new Color(.18f,.25f,.27f));
                if(row.IsApplicable)Fill(new Rect(statsX,rowY+26,statsWidth*Mathf.Clamp01(row.Normalized),3),index%2==0?Cyan:Amber);
            }
            bool canConfigure=!store.IsTactical||profile.deployment==null;
            int attachment=store.AttachmentFor(selectedId);
            WeaponText("配件："+ExpeditionCatalog.Attachments[attachment]+" · "+ExpeditionCatalog.AttachmentRules[attachment],new Rect(x,panel.yMax-101,width,24),12,Muted);
            bool enabled=GUI.enabled;
            float attachWidth=(width-12)/ExpeditionCatalog.Attachments.Length;
            GUI.enabled=enabled&&usableSelected&&canConfigure;
            for(int i=0;i<ExpeditionCatalog.Attachments.Length;i++)
            { int choice=i;if(Button(ExpeditionCatalog.Attachments[i],new Rect(x+i*attachWidth,panel.yMax-75,attachWidth-4,29),attachment==i))ExpeditionAction(()=>store.SetAttachment(selectedId,choice)); }
            GUI.enabled=enabled;
            float controlY=panel.yMax-40,rotateWidth=Mathf.Min(185,width*.37f);
            if(Button(armoryAutoRotate?"自动旋转预览 · 开":"自动旋转预览 · 关",new Rect(x,controlY,rotateWidth,32)))armoryAutoRotate=!armoryAutoRotate;
            bool equipped=usableSelected&&(expeditionWeaponSlot==0?store.Primary:store.Secondary)==selectedId;
            bool starter=store.IsTactical&&!profile.weapons[selectedId]&&(selectedId==0||selectedId==1||selectedId==2||selectedId==3||selectedId==9);
            bool buyable=store.IsTactical&&!profile.weapons[selectedId]&&!starter&&TacticalPrototypeRules.WeaponListed(selectedId,profile.cleared);
            int price=store.IsTactical?TacticalPrototypeRules.WeaponPrice(selectedId):0;
            GUI.enabled=enabled&&canConfigure&&((usableSelected&&!equipped)||starter||(buyable&&profile.credits>=price));
            string label=equipped?"已装备":usableSelected?"装备至 "+(expeditionWeaponSlot+1)+" 号槽":starter?"领取入门武器":buyable?"购买并装配 / "+price:"尚未解锁";
            if(Button(label,new Rect(x+rotateWidth+12,controlY,width-rotateWidth-12,32),true))
                ExpeditionAction(()=>{
                    if(starter){if(selectedId!=0)for(int cls=0;cls<4;cls++)if(TacticalPrototypeRules.StarterWeapon(cls)==selectedId){store.SelectClass(cls);break;}expeditionTab=2;}
                    else{if(buyable)store.BuyWeapon(System.Guid.NewGuid().ToString("N"),selectedId);if(store.CanEquip(selectedId))store.Equip(expeditionWeaponSlot,selectedId);}
                });
            GUI.enabled=enabled;
        }
        void DrawArmoryPreview(Rect rect,Texture texture)
        {
            Fill(rect,new Color(.045f,.080f,.098f));
            WeaponTexture(rect,texture);
            Text("装配预览",new Rect(rect.x+10,rect.y+8,rect.width-20,20),12,Muted);
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
