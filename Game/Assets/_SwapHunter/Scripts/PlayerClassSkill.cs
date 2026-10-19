using UnityEngine;

namespace SwapHunter
{
    public enum PlayerDamageKind { Normal, Spatial, Environment }

    // Prototype tuning is shared by the real effects and their HUD; levels are 1..3.
    public static class ClassSkillTuning
    {
        public const float ShockRange = 4, ShockHalfAngle = 45;
        public const float FortifyDuration = 4, AreaRadius = 3, FloorTolerance = .8f;
        public const float PrecisionWindow = 4, ScanRange = 20, PlacementRange = 12;
        public const float RemoteArmTime = .5f, RemoteLifetime = 8, RemoteCooldown = 18;
        public const float RemoteSpeed = 12, RemoteLift = 3, RemoteGravity = 12, RemoteEdgeScale = .2f, RemoteSelfScale = 1;
        public const float DenialDuration = 4, DenialTick = .25f, DenialCooldown = 18;
        static readonly float[] cooldowns = { 12, 10, 16, 14, 12, 14, 18, 18 };
        static readonly string[] names = { "战术推进", "快速机动", "阵地加固", "快速装填", "战术扫描", "精确射击", "遥控爆破", "区域封锁" };
        public static float Cooldown(int skill) => skill >= 0 && skill < cooldowns.Length ? cooldowns[skill] : 0;
        public static string Name(int skill) => skill >= 0 && skill < names.Length ? names[skill] : "未装配职业技能";
        public static float ShockSeconds(int level) => .4f + .2f * Mathf.Clamp(level, 1, 3);
        public static float MobilitySeconds(int level) => 1.5f + .5f * Mathf.Clamp(level, 1, 3);
        public static float FortifyReduction(int level) => .1f + .05f * Mathf.Clamp(level, 1, 3);
        public static float ReloadRemaining(int level) => .8f - .1f * Mathf.Clamp(level, 1, 3);
        public static float ScanSeconds(int level) => 2 + Mathf.Clamp(level, 1, 3);
        public static float PrecisionBonus(int level) => .1f + .05f * Mathf.Clamp(level, 1, 3);
        public static float RemoteDamage(int level) => 3 * (48 + 16 * Mathf.Clamp(level, 1, 3));
        public static float DenialDps(int level) => 3 * (12 + 4 * Mathf.Clamp(level, 1, 3));
        public static float ShieldCooldown(int level) => 11 - Mathf.Clamp(level, 1, 3);
        public static string Describe(int skill, int level)
        {
            level=Mathf.Clamp(level,1,3);
            switch(skill)
            {
                case 0:return "沿移动方向推进8米，约0.30秒；地面使用，碰撞停止，无无敌。\n冷却 "+(11-level)+"秒；击杀、换位或补给均不刷新。";
                case 1:return "地面移速 +25%，持续 "+MobilitySeconds(level).ToString("0.0")+" 秒；不增加跳高。\n开枪、近战、投刀或引爆后结束；允许 Q。冷却 10 秒。";
                case 2:return "固定半径 3 米阵地持续 4 秒，同层且无遮挡时减伤 "+(FortifyReduction(level)*100).ToString("0")+"%。\n离开失效，返回恢复；不减免地波等空间机制。冷却 16 秒。";
                case 3:return "正在装填时使用，剩余时间减少 "+((1-ReloadRemaining(level))*100).ToString("0")+"%。\n不凭空补弹；Q 成功会中断未完成装填。冷却 14 秒。";
                case 4:return "扫描 20 米内敌人的当前位置和朝向，持续 "+ScanSeconds(level).ToString("0")+" 秒。\n可显示遮挡后信息，不允许隔墙 Q。冷却 12 秒。";
                case 5:return "4 秒准备窗口，下一发实际枪弹伤害 +"+(PrecisionBonus(level)*100).ToString("0")+"%。\n射空或命中盾也消耗，三连发只强化首发；可配合 Q。冷却 14 秒。";
                case 6:return "投出一枚装置，0.5 秒后再次使用引爆，8 秒超时失效。\n3 米爆炸基准 "+RemoteDamage(level).ToString("0")+"，距离衰减、受墙阻挡且有自伤；结束后冷却 18 秒。";
                case 7:return "瞄准 12 米内地面，3 米封锁区持续 4 秒，每秒 "+DenialDps(level).ToString("0")+" 伤害。\n每 0.25 秒按同层停留结算，受墙阻挡且伤及自己。冷却 18 秒。";
                default:return "未装配职业技能";
            }
        }
    }

    // One equipped skill, advanced by PlayerMotor on the scaled gameplay clock.
    public sealed class PlayerClassSkill : MonoBehaviour
    {
        public const int InputAction = 24;
        public const float Duration = 2, Cooldown = 10, GroundSpeedMultiplier = 1.25f;
        public float MobilityLeft { get; private set; }
        public float CooldownLeft { get; private set; }
        public float FortifyLeft { get; private set; }
        public float ScanLeft { get; private set; }
        public float PrecisionLeft { get; private set; }
        public string LastFailure { get; private set; } = "";
        public string LastOutcome { get; private set; } = "";
        public bool MobilityActive => LivingTactical && MobilityLeft > 0;
        public bool ScanActive => LivingTactical && ScanLeft > 0;
        public bool PrecisionReady => LivingTactical && PrecisionLeft > 0;
        public bool FortifyActive => LivingTactical && FortifyLeft > 0;
        public Vector3 FortifyCenter => fortifyCenter;
        public RemoteClassCharge RemoteCharge => remote;
        public ClassDenialField DenialField => denial;
        public int EquippedSkill => IsTacticalFlow ? DemoGame.I.expeditionStore.EquippedClassSkill : -1;
        public int EquippedLevel => IsTacticalFlow ? DemoGame.I.expeditionStore.EquippedClassSkillLevel : 0;
        public string AbilityName => ClassSkillTuning.Name(EquippedSkill);
        public float ActiveSeconds => remote ? remote.LifetimeLeft : denial ? denial.SecondsLeft : Mathf.Max(MobilityLeft, Mathf.Max(FortifyLeft, Mathf.Max(ScanLeft, PrecisionLeft)));
        public string Status => remote ? (remote.Armed ? "已就绪 · 再按引爆 " : "启动中 ") + remote.LifetimeLeft.ToString("F1") + "秒"
            : ActiveSeconds > 0 ? "持续 " + ActiveSeconds.ToString("F1") + "秒"
            : CooldownLeft > 0 ? "冷却 " + CooldownLeft.ToString("F1") + "秒" : EquippedLevel > 0 ? "就绪" : "未装配";
        public static bool IsTacticalFlow
        {
            get { var game = DemoGame.I; return game && game.expeditionActive && game.expeditionStore != null && game.expeditionStore.IsTactical; }
        }
        bool LivingTactical => owner && owner.health > 0 && IsTacticalFlow;
        PlayerMotor owner;
        Vector3 fortifyCenter;
        float fortifyReduction, precisionBonus;
        LineRenderer fortifyRing;
        RemoteClassCharge remote;
        ClassDenialField denial;

        public void Initialize(PlayerMotor player) { owner = player; ResetCombat(); }
        public void ResetCombat()
        {
            MobilityLeft = CooldownLeft = FortifyLeft = ScanLeft = PrecisionLeft = 0;
            LastFailure = LastOutcome = ""; fortifyReduction = precisionBonus = 0;
            if (fortifyRing) Destroy(fortifyRing.gameObject); fortifyRing = null;
            var old = remote; remote = null; if (old) old.Cancel();
            if (denial) denial.Cancel(); denial = null;
        }
        internal void Tick(float dt)
        {
            var game = DemoGame.I;
            if (!LivingTactical || game.state == RunState.Dead || game.state == RunState.Victory || game.state == RunState.Menu) { ResetCombat(); return; }
            if (!game.IsPlaying) return;
            MobilityLeft = Mathf.Max(0, MobilityLeft - dt); CooldownLeft = Mathf.Max(0, CooldownLeft - dt);
            FortifyLeft = Mathf.Max(0, FortifyLeft - dt); ScanLeft = Mathf.Max(0, ScanLeft - dt); PrecisionLeft = Mathf.Max(0, PrecisionLeft - dt);
            if (FortifyLeft <= 0 && fortifyRing) { Destroy(fortifyRing.gameObject); fortifyRing = null; }
        }
        internal void ProcessInput()
        {
            if (DemoGame.I.Pressed(InputAction)) ActivateEquipped();
        }
        public bool ActivateMobility()
        {
            if (EquippedSkill != TacticalPrototypeRules.MobilitySkillIndex) return Reject("当前未装配快速机动");
            return ActivateEquipped();
        }
        public bool ActivateEquipped()
        {
            var game = DemoGame.I;
            if (!LivingTactical || !game.IsPlaying) return Reject("当前无法使用职业技能");
            int skill = EquippedSkill, level = EquippedLevel;
            if (skill < 0 || level < 1) return Reject("未装配职业技能，请在关外领取保障或装配");
            if (skill == TacticalPrototypeRules.RemoteSkillIndex && remote)
            {
                if (!remote.Armed) return Reject("爆破装置尚未启动，请稍候再引爆");
                if (!remote.TryDetonate()) return Reject("爆破装置已失效");
                LastFailure = ""; LastOutcome = "遥控爆破已引爆"; return true;
            }
            if (owner.SwapActionsLocked)
            {
                if(skill==TacticalPrototypeRules.ReloadSkillIndex)return Reject("换位准备期间装填已挂起，暂不能快速装填");
                if(skill==TacticalPrototypeRules.ShockSkillIndex || skill==TacticalPrototypeRules.FortifySkillIndex || skill==TacticalPrototypeRules.RemoteSkillIndex || skill==TacticalPrototypeRules.DenialSkillIndex)
                    return Reject("换位准备期间不能开始新的攻击或区域施放");
            }
            if (CooldownLeft > 0) return Reject(AbilityName + "正在冷却");
            string outcome;
            switch (skill)
            {
                case TacticalPrototypeRules.ShockSkillIndex:
                    if(!owner.BeginTacticalAdvance())return Reject("需要落地、空闲的推进起点");outcome="战术推进 · 固定方向8米，不刷新冷却";break;
                case TacticalPrototypeRules.MobilitySkillIndex:
                    MobilityLeft = ClassSkillTuning.MobilitySeconds(level); outcome = "快速机动 · 地面移速 +25%，攻击后结束"; break;
                case TacticalPrototypeRules.FortifySkillIndex:
                    if (!TryGround(owner.transform.position + Vector3.up * .2f, Vector3.down, 3, out fortifyCenter)) return Reject("阵地加固需要附近可用地面");
                    FortifyLeft = ClassSkillTuning.FortifyDuration; fortifyReduction = ClassSkillTuning.FortifyReduction(level);
                    fortifyRing = Shapes.Ring(game.effects, fortifyCenter + Vector3.up * .04f, ClassSkillTuning.AreaRadius, new Color(.2f,.75f,1), .055f);
                    outcome = "阵地已加固 · 留在蓝圈同层区域获得减伤"; break;
                case TacticalPrototypeRules.ReloadSkillIndex:
                    if (!owner.AccelerateCurrentReload(ClassSkillTuning.ReloadRemaining(level))) return Reject("需要正在进行的有效装填；满匣或无备弹不能使用");
                    outcome = "快速装填 · 已缩短本次剩余装填时间"; break;
                case TacticalPrototypeRules.ScanSkillIndex:
                    ScanLeft = ClassSkillTuning.ScanSeconds(level); outcome = "战术扫描 · 显示 20 米内当前位置与朝向"; break;
                case TacticalPrototypeRules.PrecisionSkillIndex:
                    PrecisionLeft = ClassSkillTuning.PrecisionWindow; precisionBonus = ClassSkillTuning.PrecisionBonus(level);
                    outcome = "精确射击已准备 · 下一发枪弹强化，射空也消耗"; break;
                case TacticalPrototypeRules.RemoteSkillIndex:
                    remote = RemoteClassCharge.Create(this, owner, ClassSkillTuning.RemoteDamage(level));
                    outcome = "爆破装置已投出 · 启动后再次使用引爆，超时失效"; break;
                case TacticalPrototypeRules.DenialSkillIndex:
                    if (!TryGround(owner.Eye.position, owner.Eye.forward, ClassSkillTuning.PlacementRange, out var point)) return Reject("请瞄准 12 米内可见地面投放封锁区");
                    denial = ClassDenialField.Create(owner, point, ClassSkillTuning.DenialDps(level));
                    outcome = "封锁区已建立 · 持续伤害也会伤及自己"; break;
                default: return Reject("当前职业技能不可用");
            }
            CooldownLeft = skill == TacticalPrototypeRules.RemoteSkillIndex ? 0 : skill==TacticalPrototypeRules.ShockSkillIndex?11-level:ClassSkillTuning.Cooldown(skill);
            LastFailure = ""; LastOutcome = outcome;
            game.Record("class_skill_activate", "skill=" + skill + ";level=" + level + ";cooldown=" + CooldownLeft);
            game.sound.Play("confirm"); game.Toast(outcome, 2.5f); return true;
        }
        static bool TryGround(Vector3 start, Vector3 direction, float range, out Vector3 point)
        {
            point = default;
            if (!Physics.Raycast(start, direction, out var hit, range, Layers.WorldMask, QueryTriggerInteraction.Ignore) || hit.normal.y < .65f) return false;
            point = hit.point; return true;
        }
        string Shock(int level)
        {
            int controlled = 0, bossProtected = 0, shieldBlocked = 0;
            var game = DemoGame.I;
            foreach (var enemy in game.enemies)
            {
                if (!enemy || !enemy.Alive) continue;
                Vector3 delta = enemy.AimPoint - owner.Eye.position;
                if (delta.magnitude > ClassSkillTuning.ShockRange || Vector3.Dot(delta.normalized, owner.Eye.forward) < Mathf.Cos(ClassSkillTuning.ShockHalfAngle * Mathf.Deg2Rad)) continue;
                if (!CombatRay.Cast(owner.Eye.position, delta.normalized, delta.magnitude + .05f, owner.transform, out var hit) || hit.collider.GetComponentInParent<EnemyActor>() != enemy) continue;
                if (enemy.IsShieldCollider(hit.collider)) { shieldBlocked++; continue; }
                if (enemy.boss) { if (enemy.boss.InterruptBasicBurst()) controlled++; else bossProtected++; }
                else if (enemy.ApplyOrdinaryStagger(ClassSkillTuning.ShockSeconds(level))) controlled++;
            }
            Vector3 forward = Vector3.ProjectOnPlane(owner.Eye.forward, Vector3.up).normalized;
            if (forward.sqrMagnitude < .1f) forward = owner.transform.forward;
            Vector3 start = owner.transform.position + Vector3.up * .08f;
            for (int side = -1; side <= 1; side += 2)
                Shapes.Line(game.effects, start, start + Quaternion.AngleAxis(side * ClassSkillTuning.ShockHalfAngle, Vector3.up) * forward * ClassSkillTuning.ShockRange, new Color(.5f,.85f,1), .04f).gameObject.AddComponent<TimedEffect>().life = .25f;
            return "震荡已释放 · 生效 " + controlled + (shieldBlocked > 0 ? " · 实体盾阻挡 " + shieldBlocked : "") + (bossProtected > 0 ? " · 指挥官当前技能不可打断" : "");
        }
        public float FortifyDamageMultiplier()
        {
            if (!FortifyActive || !ClassSkillArea.Contains(fortifyCenter, owner.transform.position, owner.transform.position + Vector3.up * .9f)) return 1;
            return 1 - fortifyReduction;
        }
        internal float ConsumePrecisionShot()
        {
            if (!PrecisionReady) return 1;
            PrecisionLeft = 0; DemoGame.I.Record("precision_shot_consumed", "next actual shot"); return 1 + precisionBonus;
        }
        internal void EndForAttack(string action)
        {
            if (!MobilityActive) return;
            MobilityLeft = 0; DemoGame.I.Record("class_skill_end", "mobility;attack=" + action);
        }
        internal void RemoteEnded(RemoteClassCharge charge, bool detonated)
        {
            if (remote != charge) return;
            remote = null; CooldownLeft = ClassSkillTuning.RemoteCooldown;
            if (DemoGame.I) { DemoGame.I.Record("remote_charge_end", detonated ? "detonated" : "expired"); if (!detonated) DemoGame.I.Toast("爆破装置已失效，开始冷却", 2); }
        }
        bool Reject(string reason) { LastFailure = reason; if (DemoGame.I) DemoGame.I.Toast(reason, 2); return false; }
        void OnDisable() { ResetCombat(); if (owner) owner.ClearArmor(); }
    }
}