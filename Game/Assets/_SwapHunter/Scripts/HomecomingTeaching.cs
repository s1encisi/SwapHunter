using UnityEngine;
namespace SwapHunter
{
    // Chapter one owns its teaching. Progress observes completed gameplay actions,
    // survives out-of-order practice, and never replaces the chapter settlement.
    public sealed partial class DemoGame
    {
        readonly bool[] homecomingLessons = new bool[8];
        Vector3 teachingPrevious;
        float teachingDistance;
        bool teachingReloadPending;
        int teachingReloadWeapon, teachingReloadAmmo, teachingItems;
        int teachingStep = -1;
        public bool HomecomingTeachingActive => expeditionActive && expeditionLevel == 0 && storyLevel == 0 && teachingStep >= 0 && teachingStep < 8;
        public string HomecomingTeachingTitle => "归航第一课  " + (Mathf.Clamp(teachingStep, 0, 7) + 1) + " / 8";
        public int HomecomingTrainingAmmoGrant { get; private set; }
        public int HomecomingTeachingCompleted { get { int count = 0; foreach (bool done in homecomingLessons) if (done) count++; return count; } }
        public string HomecomingTeachingInstruction
        {
            get
            {
                if (!player) return "进入检修站，接通救援专线";
                switch (teachingStep)
                {
                    case 0: return KeyName(0)+" / "+KeyName(1)+" / "+KeyName(2)+" / "+KeyName(3)+" 移动，鼠标观察；沿地面引导线走进校准区";
                    case 1: return KeyName(13)+" 瞄准左前方训练机，"+KeyName(12)+" 射击；命中后再推进";
                    case 2: return KeyName(7)+" 换弹，等待武器回到待发姿势；先在掩体后补足弹匣";
                    case 3: return "瞄准机器人或青色锚点，按 "+KeyName(6)+" 换位；回头确认双方原来的位置";
                    case 4:
                        int skill = player.classSkill ? player.classSkill.EquippedSkill : -1;
                        return skill == 3 ? "先射击并按 "+KeyName(7)+" 换弹，再按 "+KeyName(24)+" 体验快速装填"
                            : skill == 7 ? "瞄准近处空地，按 "+KeyName(24)+" 释放区域封锁，随后离开作用范围"
                            : skill >= 0 ? KeyName(24)+" 使用“"+player.classSkill.AbilityName+"”；技能效果结束后等待冷却"
                            : "职业技能需在行动地图中装配；当前先练习 "+KeyName(20)+" 定向屏障或 "+KeyName(22)+" 飞刀";
                    case 5: return KeyName(26)+" 选择道具，"+KeyName(14)+" / "+KeyName(15)+" 切到弹药包，"+KeyName(27)+" 使用；完成换弹后可补充备用弹药";
                    case 6: return "侧台守卫已接管闸口。先借掩体接近，再用换位取得侧面射角；"+KeyName(21)+" 近战解围";
                    default: return "击败检疫监理，沿灯带到前方撤离口；靠近后按 "+KeyName(8)+" 离开，进入货检通道";
                }
            }
        }
        void BeginHomecomingTeaching()
        {
            teachingStep = -1; HomecomingTrainingAmmoGrant = 0;
            if (storyLevel != 0 || !player || RunStorage.IsDirectedTrial) return;
            System.Array.Clear(homecomingLessons, 0, homecomingLessons.Length);
            teachingPrevious = player.transform.position; teachingDistance = 0; teachingReloadPending = false;
            // A local training supply is never charged to or committed into the profile.
            if (expeditionAmmoPacks < 1) { expeditionAmmoPacks = 1; HomecomingTrainingAmmoGrant = 1; Record("teaching_supply", "one local ammunition pack; no inventory mutation"); }
            teachingItems = expeditionMedkits + expeditionAmmoPacks + expeditionArmorPacks;
            teachingStep = 0;
            Record("homecoming_teaching", "begin;chapter=1");
        }
        void TickHomecomingTeaching()
        {
            if (!HomecomingTeachingActive || !IsPlaying || !player) return;
            Vector3 now = player.transform.position;
            float distance = Vector3.ProjectOnPlane(now - teachingPrevious, Vector3.up).magnitude;
            if (distance < 1) teachingDistance += distance;
            teachingPrevious = now;
            homecomingLessons[0] |= teachingDistance >= 4;
            homecomingLessons[1] |= totalHits > 0;
            if (player.ReloadV2Pending && (!teachingReloadPending || player.weapon != teachingReloadWeapon))
            {
                teachingReloadPending = true; teachingReloadWeapon = player.weapon; teachingReloadAmmo = player.ammo[player.weapon];
            }
            if (teachingReloadPending && player.weapon == teachingReloadWeapon && !player.ReloadV2Pending)
            {
                homecomingLessons[2] |= player.ammo[teachingReloadWeapon] > teachingReloadAmmo;
                teachingReloadPending = false;
            }
            homecomingLessons[3] |= totalSwaps > 0;
            homecomingLessons[4] |= player.classSkill && !string.IsNullOrEmpty(player.classSkill.LastOutcome);
            if (!player.classSkill || player.classSkill.EquippedSkill < 0)
                homecomingLessons[4] |= player.tactics && player.tactics.ShieldActive || player.combatTools && player.combatTools.Throws > 0;
            int items = expeditionMedkits + expeditionAmmoPacks + expeditionArmorPacks;
            homecomingLessons[5] |= items < teachingItems; teachingItems = items;
            if (mission != null) foreach (var member in mission.Members)
            {
                if (member.group.id == "combat" && member.role.required && member.Defeated) homecomingLessons[6] = true;
                if (member.group.id == "mini-boss" && member.Defeated) homecomingLessons[7] = true;
            }
            if (player.transform.position.z > 9 && player.transform.position.z < 29) StoryEmit("learn-advanced");
            if (mission != null && mission.RegionVisited("combat")) StoryEmit("learn-tools");
            int next = 0; while (next < homecomingLessons.Length && homecomingLessons[next]) next++;
            if (next == teachingStep) return;
            int previous = teachingStep; teachingStep = next;
            Record("homecoming_teaching", "step="+next+";completed="+HomecomingTeachingCompleted);
            // One response per actual checkpoint keeps radio pacing readable.
            if (previous < 6) StoryEmit("learn-"+previous);
            if (next == 8) StoryEmit("learn-complete");
        }
        void DrawHomecomingTeaching()
        {
            if (!HomecomingTeachingActive || !IsPlaying || StoryModal) return;
            float width = Mathf.Min(550, viewWidth - 48), x = 24;
            Fill(new Rect(x, 140, width, 90), new Color(.025f, .042f, .050f, .94f));
            Text(HomecomingTeachingTitle, new Rect(x+16, 149, width-32, 20), 15, Cyan);
            string instruction=KeyHints.Resolve(HomecomingTeachingInstruction);int size=17;
            textStyle.fontSize=size;
            while(size>15&&textStyle.CalcHeight(new GUIContent(instruction),width-32)>54)textStyle.fontSize=--size;
            Text(instruction, new Rect(x+16, 173, width-32, 54), size, Paper);
        }
    }
}
