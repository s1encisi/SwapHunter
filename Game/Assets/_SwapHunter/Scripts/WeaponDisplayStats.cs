using System;
using System.Globalization;
using UnityEngine;

namespace SwapHunter
{
    /// <summary>One physical quantity. Longer bars mean larger raw values, not a score.</summary>
    public sealed class WeaponDisplayStatRow
    {
        public readonly string Label, ValueText, DirectionHint;
        public readonly float Value, Maximum, Normalized;
        public readonly bool IsApplicable;
        public WeaponDisplayStatRow(string label, string text, float value, float maximum, string hint, bool applicable = true)
        {
            Label = label; ValueText = text; Value = value; Maximum = maximum;
            DirectionHint = hint; IsApplicable = applicable;
            Normalized = applicable && maximum > 0 ? Mathf.Clamp01(value / maximum) : 0;
        }
    }

    /// <summary>
    /// Read-only, deterministic armory values. No scene objects, timers, profile writes,
    /// effective DPS estimates, or live combat multipliers are involved.
    /// </summary>
    public sealed class WeaponDisplayStats
    {
        public int WeaponId { get; private set; }
        public string Name { get; private set; }
        public string MechanismLabel { get; private set; }
        public string DamageText { get; private set; }
        public string DamageExplanation { get; private set; }
        public string FalloffExplanation { get; private set; }
        public string FireCadenceExplanation { get; private set; }
        public string ConfigOverrideNotes { get; private set; }
        public string ScopeNote => "基础值；未计配件、专精、部位与距离影响。条长表示数值大小，不是综合评分。";
        public string SpreadExplanation => UsesSpread
            ? "静止、站立、未瞄准且未积累连射散布时的基础角度；移动、腾空、蹲伏、瞄准和连续射击会改变它。"
            : "榴弹沿视线方向发射后受重力影响；当前发射流程没有应用目录中的散布角。";
        public string ReserveExplanation => "相位行动常规入场的基础备弹；扩容配件另加25%，第10章入场再减半。场地补给可能超过此数，不是总携弹上限。";
        public int BaseReserve { get; private set; }
        public int Magazine { get; private set; }
        public int Pellets { get; private set; }
        public float BaseDamage { get; private set; }
        public float BaseIntervalSeconds { get; private set; }
        public float ReloadSeconds { get; private set; }
        public float HipSpreadDegrees { get; private set; }
        public float MovementMultiplier { get; private set; }
        public float MaximumRange { get; private set; }
        public float ChargeSeconds { get; private set; }
        public int BurstRounds { get; private set; }
        public float BurstPostRecoverySeconds { get; private set; }
        public bool UsesSpread { get; private set; }
        public bool IsShotgun { get; private set; }
        public bool IsExplosive { get; private set; }
        public WeaponDisplayStatRow[] Rows { get; private set; }

        static string Number(float value) => value.ToString("0.###", CultureInfo.InvariantCulture);

        public static WeaponDisplayStats Build(int weaponId, DemoConfig config)
        {
            if (!WeaponCatalog.Valid(weaponId)) throw new ArgumentOutOfRangeException(nameof(weaponId));
            return BuildAll(config)[weaponId];
        }

        // Build/cache once when armory data changes, rather than allocating on every OnGUI event.
        // The six scales are shared by all twelve entries from this same configuration.
        public static WeaponDisplayStats[] BuildAll(DemoConfig config)
        {
            if (!config) throw new ArgumentNullException(nameof(config));
            var result = new WeaponDisplayStats[WeaponCatalog.Count];
            var maxima = new float[6];
            for (int id = 0; id < result.Length; id++)
            {
                result[id] = Read(id, config);
                for (int row = 0; row < maxima.Length; row++)
                    if (row != 4 || result[id].UsesSpread) maxima[row] = Mathf.Max(maxima[row], result[id].RawValue(row));
            }
            foreach (var value in result)
            {
                value.Rows = new[] {
                    new WeaponDisplayStatRow(value.IsShotgun ? "单丸伤害" : value.IsExplosive ? "爆炸伤害" : "基础伤害",
                        value.DamageText, value.BaseDamage, maxima[0], value.IsShotgun ? "长＝单丸更高" : "长＝基准更高"),
                    new WeaponDisplayStatRow("基础击发间隔", Number(value.BaseIntervalSeconds) + " 秒",
                        value.BaseIntervalSeconds, maxima[1], "短更快"),
                    new WeaponDisplayStatRow("装填耗时", Number(value.ReloadSeconds) + " 秒",
                        value.ReloadSeconds, maxima[2], "短更快"),
                    new WeaponDisplayStatRow("装弹容量", value.Magazine.ToString(CultureInfo.InvariantCulture) + " 发",
                        value.Magazine, maxima[3], "长＝装弹更多"),
                    new WeaponDisplayStatRow("静止腰射散布", value.UsesSpread ? Number(value.HipSpreadDegrees) + "°" : "不适用",
                        value.UsesSpread ? value.HipSpreadDegrees : 0, maxima[4], value.UsesSpread ? "短更集中" : "按弧线弹道", value.UsesSpread),
                    new WeaponDisplayStatRow("持枪移动倍率", Number(value.MovementMultiplier * 100) + "%",
                        value.MovementMultiplier * 100, maxima[5], "长更快")
                };
            }
            return result;
        }

        float RawValue(int row)
        {
            switch (row)
            {
                case 0: return BaseDamage;
                case 1: return BaseIntervalSeconds;
                case 2: return ReloadSeconds;
                case 3: return Magazine;
                case 4: return HipSpreadDegrees;
                case 5: return MovementMultiplier * 100;
                default: throw new ArgumentOutOfRangeException(nameof(row));
            }
        }

        static WeaponDisplayStats Read(int id, DemoConfig config)
        {
            var definition = WeaponCatalog.Get(id);
            var stats = EffectiveWeaponStats.Read(id, config);
            float rate = stats.Rate;
            var value = new WeaponDisplayStats {
                WeaponId = id, Name = definition.name,
                BaseDamage = stats.Damage,
                BaseIntervalSeconds = 1 / Mathf.Max(.1f, rate), // Same floor as PlayerMotor.ShootRound.
                ReloadSeconds = stats.Reload,
                Magazine = stats.Magazine,
                BaseReserve = stats.Reserve, // SetLoadout uses catalog reserve, including weapon IDs 0/1.
                Pellets = stats.Pellets,
                HipSpreadDegrees = stats.Spread,
                MovementMultiplier = stats.Movement,
                MaximumRange = stats.Range,
                IsShotgun = definition.Shotgun,
                IsExplosive = definition.mechanism == FireMechanism.Grenade,
                UsesSpread = definition.mechanism != FireMechanism.Grenade,
                ChargeSeconds = definition.mechanism == FireMechanism.Charge ? definition.chargeTime : 0,
                BurstRounds = definition.mechanism == FireMechanism.Burst ? 3 : 1
            };
            value.DamageText = value.IsShotgun
                ? Number(value.BaseDamage) + " × " + value.Pellets.ToString(CultureInfo.InvariantCulture) + " 丸"
                : Number(value.BaseDamage);
            value.DamageExplanation = value.IsShotgun
                ? "数字为每丸基础伤害 × 每次发射的弹丸数；伤害条只取单丸值，不把全中总和当作保证伤害。"
                : value.IsExplosive
                    ? "爆炸基准伤害，实际伤害按爆心距离与遮挡计算；不是直接命中伤害，也不套用射线命中的头部/肢体倍率。"
                    : "单发基础命中伤害；未计部位、标记及战斗增益，距离规则单独说明。";
            value.FalloffExplanation = value.IsExplosive
                ? "初速 "+Number(definition.projectileSpeed)+" 米/秒，引信 "+Number(definition.projectileFuse)+" 秒，离发射点最远 "+Number(stats.Range)+" 米；实际落点受仰角、重力和遮挡影响。爆炸半径3.1米，爆心距离由100%降至20%，基础伤害不变。"
                : definition.minimumDamageMultiplier>=1
                    ? Number(stats.Range)+" 米射线检测范围内不做距离衰减；仍受遮挡与命中部位影响。"
                    : (value.IsShotgun?"每丸":"单发")+"在 "+Number(definition.falloffStart)+" 米内保持基础伤害，"+Number(definition.falloffStart)+"—"+Number(definition.falloffEnd)+" 米降至 "+Number(definition.minimumDamageMultiplier*100)+"%；射线最远检测 "+Number(stats.Range)+" 米。";
            value.ConfigOverrideNotes = id == 0
                ? "伤害、击发间隔、装填与容量读当前 DemoConfig；静止散布读 stationarySpread；入场备弹读 WeaponCatalog。"
                : id == 1
                    ? "每丸伤害、弹丸数、击发间隔、装填与容量读当前 DemoConfig；基础散布和入场备弹读 WeaponCatalog。"
                    : "基础属性读 WeaponCatalog；数值按实际发射机制解释，不换算为持续 DPS。";
            string interval = Number(value.BaseIntervalSeconds);
            switch (definition.mechanism)
            {
                case FireMechanism.Automatic:
                    value.MechanismLabel = "全自动";
                    value.FireCadenceExplanation = "按住连续开火；基础间隔 " + interval + " 秒，装填与动作限制另计。";
                    break;
                case FireMechanism.Pump:
                    value.MechanismLabel = "泵动单发";
                    value.FireCadenceExplanation = "每次扣扳机发射一枚霰弹；泵动窗口计入 " + interval + " 秒基础间隔。";
                    break;
                case FireMechanism.Burst:
                    value.MechanismLabel = "三连发";
                    value.BurstPostRecoverySeconds = Mathf.Max(value.BaseIntervalSeconds, .28f);
                    value.FireCadenceExplanation = "一次触发最多3发；组内约 " + Number(1 / definition.rate)
                        + " 秒，末发后至少 " + Number(value.BurstPostRecoverySeconds) + " 秒；再次按下才开下一组。";
                    break;
                case FireMechanism.SemiAutomatic:
                    value.MechanismLabel = "半自动";
                    value.FireCadenceExplanation = "需要逐次扣扳机；基础间隔 " + interval + " 秒，不自动连续发射。";
                    break;
                case FireMechanism.DoubleBarrel:
                    value.MechanismLabel = "双管·逐发";
                    value.FireCadenceExplanation = "每次触发只发射一管；两次触发后需装填，基础间隔 " + interval + " 秒。";
                    break;
                case FireMechanism.BoltAction:
                    value.MechanismLabel = "栓动单发";
                    value.FireCadenceExplanation = "每次扣扳机单发；拉栓窗口计入 " + interval + " 秒基础间隔。";
                    break;
                case FireMechanism.Grenade:
                    value.MechanismLabel = "抛射榴弹";
                    value.FireCadenceExplanation = "每次触发发射一枚实体榴弹；基础间隔 " + interval + " 秒，爆炸与飞行时序另计。";
                    break;
                case FireMechanism.Charge:
                    value.MechanismLabel = "蓄能单发";
                    value.FireCadenceExplanation = "按住 " + Number(value.ChargeSeconds) + " 秒蓄能，射后另需 " + interval
                        + " 秒间隔；提前松开不出弹、不扣弹。";
                    break;
                case FireMechanism.PhaseMark:
                    value.MechanismLabel = "相位标记·单发";
                    value.FireCadenceExplanation = "逐次触发，基础间隔 " + interval + " 秒；存活目标命中后标记5秒，后续枪弹与普通近战增伤20%；爆炸、飞刀及区域伤害不享受标记增伤。";
                    break;
                default: throw new InvalidOperationException("Unrecognized fire mechanism: " + definition.mechanism);
            }
            return value;
        }
    }
}
