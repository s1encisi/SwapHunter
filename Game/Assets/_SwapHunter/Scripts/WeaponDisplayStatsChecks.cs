using System;
using UnityEngine;

namespace SwapHunter
{
    // Optional focused checks for the native UI/weapon QA harness.
    // The passed asset and WeaponCatalog are never modified; only a temporary clone is edited.
    public static class WeaponDisplayStatsChecks
    {
        static bool Near(float a, float b) => Mathf.Abs(a - b) < .0001f;
        public static void Run(DemoConfig config, Action<string, bool, string> check)
        {
            var all = WeaponDisplayStats.BuildAll(config);
            check("display_stats_twelve_entries", all.Length == 12, "Display-only values for each stable weapon ID.");
            check("display_shotgun_per_pellet", all[1].IsShotgun && Near(all[1].Rows[0].Value, config.shotgunDamage)
                && all[1].Pellets == config.shotgunPellets, all[1].DamageText);
            check("display_grenade_spread_not_applied", !all[9].UsesSpread && !all[9].Rows[4].IsApplicable
                && all[9].Rows[4].Normalized == 0, "Launch uses Eye.forward, not the catalog spread field.");
            check("display_burst_and_charge_timing", all[2].BurstRounds == 3 && Near(all[2].BurstPostRecoverySeconds, .28f)
                && Near(all[10].ChargeSeconds, .65f) && Near(all[10].BaseIntervalSeconds, 1),
                "Burst recovery and charge time are explicit; no sustained-DPS claim.");

            bool uniform = true, bounded = true;
            for (int row = 0; row < 6; row++)
                foreach (var entry in all)
                {
                    var value = entry.Rows[row];
                    uniform &= Near(value.Maximum, all[0].Rows[row].Maximum);
                    bounded &= !float.IsNaN(value.Normalized) && value.Normalized >= 0 && value.Normalized <= 1;
                    if (value.IsApplicable && value.Maximum > 0)
                        bounded &= Near(value.Normalized, value.Value / value.Maximum);
                }
            check("display_uniform_raw_value_axes", uniform && bounded, "Every row uses value / same twelve-weapon maximum, without inverted scores.");

            float originalDamage = config.rifleDamage, originalPellets = config.shotgunPellets;
            float catalogDamage = WeaponCatalog.Get(0).damage;
            var variant = UnityEngine.Object.Instantiate(config);
            try
            {
                variant.rifleDamage = 201; variant.rifleRate = 2; variant.rifleReload = 4;
                variant.rifleMagazine = 17; variant.stationarySpread = .42f; variant.rifleReserve = 999;
                variant.shotgunDamage = 9; variant.shotgunPellets = 5; variant.shotgunMagazine = 7;
                var changed = WeaponDisplayStats.BuildAll(variant);
                check("display_runtime_config_overrides", Near(changed[0].BaseDamage, 201)
                    && Near(changed[0].BaseIntervalSeconds, .5f) && Near(changed[0].ReloadSeconds, 4)
                    && changed[0].Magazine == 17 && Near(changed[0].HipSpreadDegrees, .42f)
                    && Near(changed[1].BaseDamage, 9) && changed[1].Pellets == 5 && changed[1].Magazine == 7,
                    "First two weapons follow actual DemoConfig overrides.");
                check("display_reserve_follows_setloadout", changed[0].BaseReserve == WeaponCatalog.Get(0).reserve,
                    "Ordinary expedition loadout uses catalog reserve, not legacy config.rifleReserve.");
                check("display_axis_tracks_config_maximum", Near(changed[0].Rows[0].Maximum, 201)
                    && Near(changed[0].Rows[0].Normalized, 1), "An explicit config override updates the shared axis.");
            }
            finally
            {
                if (Application.isPlaying) UnityEngine.Object.Destroy(variant);
                else UnityEngine.Object.DestroyImmediate(variant);
            }
            check("display_leaves_balance_inputs_unchanged", Near(config.rifleDamage, originalDamage)
                && Near(config.shotgunPellets, originalPellets) && Near(WeaponCatalog.Get(0).damage, catalogDamage),
                "No mutation of the caller's asset or the weapon catalog.");
        }
    }
}
