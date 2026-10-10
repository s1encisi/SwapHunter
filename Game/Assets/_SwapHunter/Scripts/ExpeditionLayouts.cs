using UnityEngine;

namespace SwapHunter
{
    public static partial class ExpeditionWorld
    {
        static void Calibration()
        {
            Courtyard(30, 48);
            Cover(-6, 7); Cover(6, 7);
            Anchor(new Vector3(-4, .02f, 12));
            Spawn("calibration", 0, new Vector3(4, 0, 12), 180, true);
            Label("安全校准 / 交换位置后转身观察", new Vector3(0, 2.6f, 10), .48f);
            Shapes.Box("Training boundary", root, new Vector3(0, .025f, 17), new Vector3(28, .035f, .12f), 4);
            Tower(7, 31, 2.2f, 10, 8); Stairs(7, 22, 2.2f);
            Cover(-4, 26, 1.2f, 4); Cover(10, 33, 1.1f, 3, 2.2f);
            Anchor(new Vector3(4, 2.22f, 33));
            Spawn("combat", 0, new Vector3(8, 2.2f, 30), 185, true);
            Region("combat", new Vector3(0, 2, 30), new Vector3(28, 8, 24));
            Door("$main", new Vector3(0, 0, 40), 5, "解决实战守卫后开放");
            Solid("Exit left wall", new Vector3(-8.5f, 1.4f, 40), new Vector3(11, 2.8f, .6f));
            Solid("Exit right wall", new Vector3(8.5f, 1.4f, 40), new Vector3(11, 2.8f, .6f));
            Exit(new Vector3(0, 0, 44), "校准出口");
            Supply(new Vector3(-10, 0, 10));
            Label("侧路与楼梯 / 不依赖敌人存活", new Vector3(-10, 2, 25), .35f);
            map.walkingCheckpoints.Add(new Vector3(7, 2.2f, 29));
        }

        static void ShieldLine()
        {
            Courtyard(34, 56);
            Cover(0, 10, 1.2f, 4); Cover(-5, 17, 2.4f, 5);
            Cover(5, 24, 2.4f, 5); Cover(-9, 32, 1.2f, 4);
            Cover(3, 38, 2.4f, 5); Cover(10, 42, 1.2f, 3);
            Anchor(new Vector3(-11, .02f, 22)); Anchor(new Vector3(11, .02f, 47));
            Spawn("first", 0, new Vector3(1, 0, 18), 180);
            Spawn("second", 0, new Vector3(-1, 0, 45), 180);
            Spawn("second", 1, new Vector3(10, 0, 38), 260);
            Region("second", new Vector3(0, 2, 40), new Vector3(32, 7, 26));
            Door("$main", new Vector3(0, 0, 50), 5, "货检闸口 / 指定盾卫");
            Solid("Gate left wing", new Vector3(-9, 1.5f, 50), new Vector3(12, 3, .6f));
            Solid("Gate right wing", new Vector3(9, 1.5f, 50), new Vector3(12, 3, .6f));
            Exit(new Vector3(0, 0, 53), "货检出口");
            Supply(new Vector3(-13, 0, 28)); Supply(new Vector3(13, 0, 47));
            Label("先横移，再决定把盾送到哪里", new Vector3(-8, 2.5f, 13), .42f);
            Label("侧向火力 / 右侧检修回路", new Vector3(10, 2.5f, 33), .4f, true);
            map.walkingCheckpoints.Add(new Vector3(-12, 0, 21));
            map.walkingCheckpoints.Add(new Vector3(13, 0, 44));
        }

        static void VerticalRoute()
        {
            Courtyard(36, 52);
            Tower(-10, 25, 2.2f, 10, 12); Stairs(-10, 14, 2.2f);
            Tower(8, 39, 3.3f, 12, 16); Stairs(8, 23, 3.3f);
            // Keep a standing turn-in apron before the lower stair rails.
            // Roof north edge 11.5; rail exclusion starts 13.65 (previously only a 1.15m gap).
            Solid("Low maintenance roof", new Vector3(-10, 1.43f, 9), new Vector3(6, .26f, 5), 3);
            Solid("Vault lip", new Vector3(-2, .5f, 19), new Vector3(4, 1, 1), 2);
            Cover(6, 12); Cover(-12, 28, 1.1f, 3, 2.2f); Cover(11, 40, 1.1f, 3, 3.3f);
            Anchor(new Vector3(-7, 2.22f, 25)); Anchor(new Vector3(5, 3.32f, 37));
            Spawn("lower", 0, new Vector3(5, 0, 15), 180);
            Spawn("middle", 0, new Vector3(-10, 2.2f, 24), 175, true);
            Spawn("upper", 0, new Vector3(9, 3.3f, 37), 180, true);
            Region("middle", new Vector3(-10, 3.5f, 25), new Vector3(12, 3, 14));
            Region("upper", new Vector3(8, 4.8f, 39), new Vector3(14, 3.5f, 18));
            LaunchPad(new Vector3(0, .02f, 25));
            Objective("a", new Vector3(5, 3.3f, 43), "取得上层通行件");
            Exit(new Vector3(11, 3.3f, 44), "上层维护出口");
            Supply(new Vector3(-13, 2.2f, 29)); Supply(new Vector3(13, 0, 24));
            Label("低通道 / 蹲伏后还有步行楼梯", new Vector3(-10, 2.2f, 8), .38f);
            Label("先看落点，再使用大跳", new Vector3(0, 2.2f, 24), .4f);
            map.walkingCheckpoints.Add(new Vector3(-10, 2.2f, 24));
            map.walkingCheckpoints.Add(new Vector3(8, 3.3f, 36));
        }

        static void FirstExtraction()
        {
            Courtyard(36, 58);
            Cover(-5, 14, 2.2f, 5); Cover(5, 22, 1.2f, 5);
            Solid("Central shortcut wall", new Vector3(-7, 1.4f, 30), new Vector3(12, 2.8f, 1));
            Solid("Central shortcut wall", new Vector3(7, 1.4f, 30), new Vector3(12, 2.8f, 1));
            Door("a", new Vector3(0, 0, 30), 2, "凭据接通后开放中央捷径");
            Tower(11, 39, 2.2f, 8, 10); Stairs(11, 29, 2.2f);
            Cover(-9, 43); Cover(5, 47);
            Anchor(new Vector3(12, .02f, 21)); Anchor(new Vector3(0, .02f, 44));
            Spawn("initial", 0, new Vector3(0, 0, 18), 180);
            Spawn("initial", 1, new Vector3(-10, 0, 24), 140);
            Spawn("intercept", 0, new Vector3(-10, 0, 48), 120);
            Spawn("intercept", 1, new Vector3(10, 0, 51), 235);
            Objective("a", new Vector3(-4, 0, 26), "取得核心凭据");
            Bonus(new Vector3(11, 2.2f, 41), "暴露侧台数据 / 可放弃");
            Exit(new Vector3(0, 0, 54), "对侧撤离口 / 允许遗留敌人");
            Supply(new Vector3(-14, 0, 11)); Supply(new Vector3(14, 0, 49));
            Label("左侧外环 / 可步行绕行", new Vector3(-14, 2.3f, 28), .38f);
            Label("可选数据会延长暴露，无额外追兵", new Vector3(11, 4.8f, 38), .32f, true);
            map.walkingCheckpoints.Add(new Vector3(-15, 0, 31));
        }

        static void Crossfire()
        {
            Courtyard(40, 64);
            Tower(-12, 29, 4.4f, 10, 10); Stairs(-12, 13.5f, 4.4f);
            Tower(12, 42, 3.3f, 10, 10); Stairs(12, 29, 3.3f);
            Cover(-3, 13, 2.3f, 5); Cover(4, 22, 1.2f, 4);
            Cover(0, 34, 1.15f, 4); Cover(-16, 31, 1.1f, 2, 4.4f);
            Cover(16, 44, 1.1f, 2, 3.3f);
            Anchor(new Vector3(-9, 4.42f, 31)); Anchor(new Vector3(9, 3.32f, 43));
            Anchor(new Vector3(0, .02f, 29));
            Spawn("left", 0, new Vector3(-12, 4.4f, 28), 145, true);
            Spawn("left", 1, new Vector3(-8, 0, 20), 160);
            Spawn("right", 0, new Vector3(12, 3.3f, 41), 215, true);
            Spawn("right", 1, new Vector3(9, 0, 32), 210);
            Spawn("guard", 0, new Vector3(1, 0, 27), 180);
            Spawn("reinforce", 0, new Vector3(0, 0, 54), 180);
            Objective("a", new Vector3(-14, 4.4f, 32), "左塔火控节点");
            Objective("b", new Vector3(14, 3.3f, 45), "右塔火控节点");
            for (int side = -1; side <= 1; side += 2)
            {
                Solid("Sheltered route outer wall", new Vector3(side * 8.5f, 1.6f, 51), new Vector3(.5f, 3.2f, 14));
                Solid("Sheltered route inner wall", new Vector3(side * 4.5f, 1.6f, 51), new Vector3(.5f, 3.2f, 14));
            }
            Door("a", new Vector3(-6.5f, 0, 44), 3.5f, "左塔接通 / 遮蔽通道");
            Door("b", new Vector3(6.5f, 0, 44), 3.5f, "右塔接通 / 遮蔽通道");
            Exit(new Vector3(0, 0, 60), "中央货运出口");
            Bonus(new Vector3(-17, 0, 54), "远端数据 / 额外绕路");
            Supply(new Vector3(-17, 0, 24)); Supply(new Vector3(17, 0, 37));
            Label("观察另一座塔，再决定第一次换位", new Vector3(0, 3, 10), .45f);
            map.walkingCheckpoints.Add(new Vector3(-12, 4.4f, 27));
            map.walkingCheckpoints.Add(new Vector3(12, 3.3f, 40));
        }

        static void TransferDanger()
        {
            Courtyard(38, 62);
            Cover(-5, 10, 1.15f, 5); Cover(7, 17, 2.2f, 4);
            Cover(-8, 24, 1.2f, 4); Cover(0, 34, 2.2f, 4);
            Cover(8, 40, 1.2f, 4); Cover(-10, 48, 2.2f, 4);
            Anchor(new Vector3(11, .02f, 23)); Anchor(new Vector3(-12, .02f, 40));
            LaunchPad(new Vector3(13, .02f, 12));
            Spawn("first", 0, new Vector3(2, 0, 17), 180);
            Spawn("first", 1, new Vector3(-9, 0, 20), 140);
            Spawn("second", 0, new Vector3(9, 0, 36), 200);
            Spawn("second", 1, new Vector3(-6, 0, 36), 160);
            Spawn("third", 0, new Vector3(9, 0, 51), 210);
            Spawn("third", 1, new Vector3(-9, 0, 53), 140);
            Region("side", new Vector3(14, 2, 41), new Vector3(8, 6, 18));
            DamageTarget("a", new Vector3(-9, 0, 29), "封锁装置 A", 96);
            DamageTarget("b", new Vector3(9, 0, 45), "封锁装置 B", 144);
            Door("a", new Vector3(-4, 0, 32), 22, "装置 A / 货舱通路");
            Door("b", new Vector3(4, 0, 48), 22, "装置 B / 出口通路");
            Bonus(new Vector3(15, 0, 42), "侧仓数据 / 额外交战");
            Exit(new Vector3(0, 0, 58), "封锁出口");
            Supply(new Vector3(-15, 0, 13)); Supply(new Vector3(15, 0, 53));
            Label("手雷留在原处 / 留好跑离路线", new Vector3(0, 3, 13), .42f, true);
            Label("弹药耗尽时仍可近战破坏", new Vector3(-9, 3.5f, 29), .36f);
            map.walkingCheckpoints.Add(new Vector3(16, 0, 33));
            map.walkingCheckpoints.Add(new Vector3(-16, 0, 49));
        }

        static void RelayIslands()
        {
            map.spawn = new Vector3(0, .04f, 4);
            Floor(0, 8, 14, 14); Floor(-11, 25, 14, 14); Floor(11, 25, 14, 14);
            // The maintenance level is below the island decks with full standing clearance.
            Floor(0, 8, 26, 3, -3.3f);
            Floor(-11, 16, 3, 18, -3.3f); Floor(11, 16, 3, 18, -3.3f);
            Floor(0, 22, 4, 28, -3.3f);
            Stairs(0, 23, 3.3f, 3, -3.3f, 180);
            Stairs(-11, 10, 3.3f, 3, -3.3f);
            Stairs(11, 10, 3.3f, 3, -3.3f);
            Anchor(new Vector3(0, .02f, 8));
            Anchor(new Vector3(-7, .02f, 22));
            Anchor(new Vector3(7, .02f, 25));
            Socket("a", new Vector3(0, .02f, 11), "接入座 A / 站在此处搬锚");
            Socket("b", new Vector3(-7, .02f, 25), "接入座 B");
            Socket("c", new Vector3(7, .02f, 22), "接入座 C");
            Cover(-14, 23, 1.15f, 3); Cover(14, 28, 1.15f, 3);
            Spawn("island-b", 0, new Vector3(-12, 0, 28), 155, true);
            Spawn("island-b", 1, new Vector3(-15, 0, 20), 120);
            Spawn("island-c", 0, new Vector3(12, 0, 24), 235);
            Spawn("reinforce", 0, new Vector3(-15, 0, 29), 120);
            Spawn("reinforce", 1, new Vector3(15, 0, 20), 230);
            Bridge("a", -5.5f, 16.5f, 3, 4);
            Bridge("b", 0, 29, 9, 3);
            Bridge("c", 5.5f, 16.5f, 3, 4);
            Maintenance(new Vector3(-4, 0, 5));
            // The south approach (14, 0, 28.4) is inside C-island cover.
            // Author a real west-side interaction position; keep the checkpoint.
            Exit(new Vector3(14, 0, 30), "C 岛维护出口", new Vector3(12.2f, 0, 30));
            Bonus(new Vector3(16, 0, 25), "C 岛侧支线数据");
            Supply(new Vector3(-15, 0, 30)); Supply(new Vector3(14, 0, 19));
            Label("下层维护路线 / 锚点搬错仍可返回", new Vector3(0, -1, 24), .4f);
            Label("接入后可继续搬走锚点", new Vector3(0, 2.6f, 11), .4f);
            map.walkingCheckpoints.Add(new Vector3(0, -3.3f, 25));
            map.walkingCheckpoints.Add(new Vector3(-11, 0, 19));
            map.walkingCheckpoints.Add(new Vector3(11, 0, 19));
        }

        static void WarehouseChoices()
        {
            Courtyard(42, 52);
            // Separators leave a front cross-route, a rear cross-route and two genuine sight windows.
            foreach (float x in new[] { -7f, 7f })
            {
                Solid("Warehouse partition front", new Vector3(x, 1.6f, 18), new Vector3(.6f, 3.2f, 10));
                Solid("Warehouse window sill", new Vector3(x, .4f, 27), new Vector3(.6f, .8f, 8));
                Solid("Warehouse window lintel", new Vector3(x, 2.9f, 27), new Vector3(.6f, .6f, 8));
                Solid("Warehouse partition rear", new Vector3(x, 1.6f, 36), new Vector3(.6f, 3.2f, 10));
            }
            Cover(-14, 22, 2.2f, 6); Cover(-11, 33, 1.1f, 4);
            Cover(0, 20, 1.1f, 3); Cover(3, 35, 1.2f, 3);
            Cover(14, 19, 2.3f, 6); Cover(12, 30, 2.3f, 5); Cover(16, 37, 1.1f, 3);
            Solid("Optional crouch roof", new Vector3(17, 1.44f, 27), new Vector3(5, .25f, 4), 3);
            Anchor(new Vector3(-12, .02f, 28)); Anchor(new Vector3(0, .02f, 28)); Anchor(new Vector3(12, .02f, 27));
            Region("a", new Vector3(-14, 2, 28), new Vector3(13, 6, 30));
            Region("b", new Vector3(0, 2, 28), new Vector3(13, 6, 30));
            Region("c", new Vector3(14, 2, 28), new Vector3(13, 6, 30));
            Spawn("warehouse-a", 0, new Vector3(-14, 0, 29), 180);
            Spawn("warehouse-a", 1, new Vector3(-11, 0, 37), 190);
            Spawn("warehouse-b", 0, new Vector3(0, 0, 36), 180, true);
            Spawn("warehouse-b", 1, new Vector3(4, 0, 31), 200);
            Spawn("warehouse-c", 0, new Vector3(16, 0, 24), 210);
            Spawn("warehouse-c", 1, new Vector3(12, 0, 38), 180);
            Objective("a", new Vector3(-15, 0, 40), "A 仓数据 / 近距盾线");
            Objective("b", new Vector3(-2, 0, 40), "B 仓数据 / 长视线");
            Objective("c", new Vector3(15, 0, 40), "C 仓数据 / 折返");
            Exit(new Vector3(0, 0, 48), "汇合出口 / 任意两份数据");
            Supply(new Vector3(-18, 0, 17)); Supply(new Vector3(18, 0, 35));
            Label("任选两仓即可撤离 / 第三份为可选奖励", new Vector3(0, 3, 10), .43f);
            Label("A 近距盾线", new Vector3(-14, 3, 14), .4f);
            Label("B 长走廊", new Vector3(0, 3, 14), .4f);
            Label("C 折返与侧窗", new Vector3(14, 3, 14), .4f);
            map.walkingCheckpoints.Add(new Vector3(-14, 0, 43));
            map.walkingCheckpoints.Add(new Vector3(14, 0, 43));
        }

        static void TowerDescent()
        {
            Courtyard(42, 64);
            // The lower deck occupies x[-14,-2], z[14,26]; descend west of it.
            // Stair top still overlaps the 6.6m deck south edge at z42.
            Tower(-11, 50, 6.6f, 12, 16); Stairs(-16, 26, 6.6f, 3.5f);
            Tower(9, 38, 4.4f, 12, 16); Stairs(9, 19.5f, 4.4f, 3.5f);
            Tower(-8, 20, 2.2f, 12, 12); Stairs(-8, 9, 2.2f, 3.5f);
            map.spawn = new Vector3(-14, 6.64f, 46);
            Objective("a", new Vector3(-14, 6.6f, 54), "取得顶层核心");
            Bonus(new Vector3(-7, 6.6f, 56), "顶层侧路数据 / 增加下撤风险");
            Anchor(new Vector3(-8, 6.62f, 47)); Anchor(new Vector3(6, 4.42f, 35));
            Anchor(new Vector3(-5, 2.22f, 21)); Anchor(new Vector3(8, .02f, 13));
            Cover(-15, 51, 1.1f, 3, 6.6f); Cover(12, 40, 1.1f, 3, 4.4f);
            Cover(-11, 22, 1.1f, 3, 2.2f); Cover(6, 10, 1.2f, 4);
            Spawn("top", 0, new Vector3(-9, 6.6f, 54), 210, true);
            Spawn("top", 1, new Vector3(-15, 6.6f, 55), 190, true);
            Spawn("middle", 0, new Vector3(8, 4.4f, 38), 310, true);
            Spawn("middle", 1, new Vector3(13, 4.4f, 42), 275, true);
            Spawn("middle", 2, new Vector3(5, 4.4f, 42), 290, true);
            Spawn("bottom", 0, new Vector3(-8, 2.2f, 23), 20, true);
            Spawn("bottom", 1, new Vector3(3, 0, 9), 0);
            Spawn("bottom", 2, new Vector3(11, 0, 17), 340);
            Region("middle", new Vector3(0, 3, 35), new Vector3(40, 12, 19));
            Region("bottom", new Vector3(0, 3, 15), new Vector3(40, 12, 24));
            LaunchPad(new Vector3(15, .02f, 23));
            Supply(new Vector3(13, 4.4f, 35)); Supply(new Vector3(-13, 0, 9));
            Exit(new Vector3(0, 0, 4), "地面货运出口");
            Label("取核心后向下撤离 / 无需清理上层敌人", new Vector3(-11, 9.5f, 48), .4f);
            map.walkingCheckpoints.Add(new Vector3(-16, 0, 25));
            map.walkingCheckpoints.Add(new Vector3(9, 4.4f, 32));
            map.walkingCheckpoints.Add(new Vector3(-8, 2.2f, 18));
        }

        static void CutReinforcements()
        {
            Courtyard(40, 62);
            for (int i = 0; i < 3; i++)
            {
                Cover(i % 2 == 0 ? -5 : 5, 14 + i * 13, 1.25f, 7);
                Cover(i % 2 == 0 ? 10 : -10, 20 + i * 10, 2.2f, 5);
            }
            Objective("a", new Vector3(-15, 0, 26), "关闭左侧部署入口");
            Objective("b", new Vector3(15, 0, 33), "关闭右侧部署入口");
            Objective("c", new Vector3(0, 0, 54), "关闭后方部署入口");
            Spawn("initial", 0, new Vector3(0, 0, 18), 180);
            Spawn("initial", 1, new Vector3(-11, 0, 20), 150);
            Spawn("initial", 2, new Vector3(11, 0, 26), 210);
            Spawn("left", 0, new Vector3(-15, 0, 40), 145, true);
            Spawn("left", 1, new Vector3(-15, 0, 45), 145);
            Spawn("right", 0, new Vector3(15, 0, 43), 210);
            Spawn("right", 1, new Vector3(15, 0, 48), 210);
            Spawn("rear", 0, new Vector3(-5, 0, 56), 180, true);
            Spawn("rear", 1, new Vector3(5, 0, 56), 180);
            Anchor(new Vector3(-12, .02f, 32)); Anchor(new Vector3(12, .02f, 48));
            Supply(new Vector3(-16, 0, 12));
            Bonus(new Vector3(-17, 0, 52), "左站后方存储仓 / 额外绕路");
            Exit(new Vector3(0, 0, 58), "货运出口 / 已落地守卫可遗留");
            Label("关闭入口可取消尚未落地的有限增援", new Vector3(0, 3, 9), .45f, true);
            Label("唯一场地补给", new Vector3(-16, 2.8f, 12), .38f);
            map.walkingCheckpoints.Add(new Vector3(-17, 0, 30));
            map.walkingCheckpoints.Add(new Vector3(17, 0, 37));
        }

        static void LocalInterference()
        {
            Courtyard(44, 62);
            Cover(-8, 17, 2.3f, 5); Cover(8, 26, 2.3f, 5);
            Cover(-7, 36, 1.2f, 4); Cover(7, 46, 1.2f, 4);
            Cover(0, 43, 2.3f, 5);
            Jammer("field-left", new Vector3(-11, 1, 28), 6,
                new Vector3(-17, 0, 24), new Vector3(-10, 0, 31));
            Jammer("field-right", new Vector3(11, 1, 39), 6,
                new Vector3(17, 0, 35), new Vector3(11, 0, 42));
            Anchor(new Vector3(-18, .02f, 16)); Anchor(new Vector3(18, .02f, 48));
            Anchor(new Vector3(0, .02f, 32)); Anchor(new Vector3(-10, .02f, 47));
            Spawn("left", 0, new Vector3(-11, 0, 27), 180);
            Spawn("left", 1, new Vector3(-15, 0, 31), 155);
            Spawn("left", 2, new Vector3(-7, 0, 32), 200);
            Spawn("right", 0, new Vector3(14, 0, 44), 215, true);
            Spawn("right", 1, new Vector3(8, 0, 38), 190);
            Spawn("core", 0, new Vector3(-3, 0, 50), 170);
            Spawn("core", 1, new Vector3(6, 0, 53), 215);
            Region("left", new Vector3(-11, 2, 28), new Vector3(19, 7, 24));
            Region("right", new Vector3(11, 2, 39), new Vector3(19, 7, 24));
            Region("core", new Vector3(0, 2, 48), new Vector3(26, 7, 20));
            Objective("a", new Vector3(0, 0, 54), "取得核心权限");
            Bonus(new Vector3(12, 0, 36), "另一干扰区数据 / 可绕过");
            Supply(new Vector3(-19, 0, 14)); Supply(new Vector3(19, 0, 51));
            Exit(new Vector3(-18, 0, 57), "外环出口 / 无需关闭全部场区");
            Label("外环安全路线 / 干扰不阻断枪械", new Vector3(-19, 3, 35), .4f);
            map.walkingCheckpoints.Add(new Vector3(-19, 0, 40));
            map.walkingCheckpoints.Add(new Vector3(19, 0, 40));
        }

        static void CoreBreakthrough()
        {
            Courtyard(44, 66);
            Tower(-12, 33, 3.3f, 11, 12); Stairs(-12, 19, 3.3f, 4);
            Tower(12, 45, 4.4f, 11, 12); Stairs(12, 28.5f, 4.4f, 4);
            // Permanent low refuges supplement the tall platforms after all launch pads are used.
            Tower(-5, 28, 1.4f, 5, 5); Ramp(-5, 23.5f, 1.4f, 4, 4, guideSides: true);
            Ramp(-5, 32.5f, 1.4f, 4, 4, yaw: 180, guideSides: true);
            Ramp(-.5f, 28, 1.4f, 4, 3, yaw: 270, guideSides: true);
            Tower(5, 41, 1.4f, 5, 5); Ramp(5, 36.5f, 1.4f, 4, 4, guideSides: true);
            Ramp(5, 45.5f, 1.4f, 4, 4, yaw: 180, guideSides: true);
            Ramp(.5f, 41, 1.4f, 4, 4, yaw: 90, guideSides: true);
            Ramp(9.5f, 41, 1.4f, 4, 4, yaw: 270, guideSides: true);
            // The left tall deck is too low to fit a player above a side ramp underneath it.
            // A separate refuge in the outer lane avoids requiring that long detour.
            Tower(-19, 43, 1.4f, 4, 4); Ramp(-19, 39, 1.4f, 4, 3, guideSides: true);
            Ramp(-19, 47, 1.4f, 4, 3, yaw: 180, guideSides: true);
            Cover(0, 16, 1.2f, 5); Cover(-3, 47, 1.15f, 4); Cover(7, 55, 1.15f, 4);
            Cover(-15, 35, 1.1f, 3, 3.3f); Cover(15, 47, 1.1f, 3, 4.4f);
            Anchor(new Vector3(-9, 3.32f, 33)); Anchor(new Vector3(9, 4.42f, 45));
            Anchor(new Vector3(-16, .02f, 47)); Anchor(new Vector3(16, .02f, 25));
            LaunchPad(new Vector3(0, .02f, 12)); LaunchPad(new Vector3(-6, .02f, 40)); LaunchPad(new Vector3(6, .02f, 54));
            Spawn("boss", 0, new Vector3(0, 0, 37), 180, true);
            Spawn("phase-one", 0, new Vector3(-17, 0, 22), 150);
            Spawn("phase-one", 1, new Vector3(17, 0, 30), 220);
            Spawn("phase-two", 0, new Vector3(-16, 0, 49), 130);
            Spawn("phase-two", 1, new Vector3(17, 0, 52), 230);
            Spawn("phase-three", 0, new Vector3(-13, 3.3f, 37), 150, true);
            Spawn("phase-three", 1, new Vector3(17, 0, 59), 215);
            Objective("a", new Vector3(-15, 3.3f, 37), "左高位授权");
            Objective("b", new Vector3(15, 4.4f, 49), "右高位授权");
            Objective("c", new Vector3(0, 0, 59), "取得地面相位核心");
            Bonus(new Vector3(16, 4.4f, 42), "侧台观察数据 / 额外暴露");
            Supply(new Vector3(-18, 0, 17)); Supply(new Vector3(18, 0, 56));
            Exit(new Vector3(0, 0, 62), "对侧货运出口 / 指挥官必须击败");
            Label("永久高位可避地波 / A 盾不挡地波", new Vector3(-5, 4, 25), .42f);
            Label("授权顺序自由 / 随从不要求全清", new Vector3(0, 3, 52), .4f);
            map.walkingCheckpoints.Add(new Vector3(-12, 3.3f, 30));
            map.walkingCheckpoints.Add(new Vector3(12, 4.4f, 42));
            map.walkingCheckpoints.Add(new Vector3(-5, 1.4f, 28));
            map.walkingCheckpoints.Add(new Vector3(5, 1.4f, 41));
            map.walkingCheckpoints.Add(new Vector3(-19, 1.4f, 43));
        }
    }
}


