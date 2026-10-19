using System;
namespace SwapHunter
{
    // Base expedition values. Attachments and temporary effects are applied by their owners.
    public readonly struct EffectiveWeaponStats
    {
        public readonly float Damage, Rate, Reload, Spread, Movement;
        public readonly int Magazine, Reserve, Pellets;
        EffectiveWeaponStats(int id, DemoConfig config)
        {
            var d = WeaponCatalog.Get(id);
            Damage = 3 * (id == 0 ? config.rifleDamage : id == 1 ? config.shotgunDamage : d.damage);
            Rate = id == 0 ? config.rifleRate : id == 1 ? config.shotgunRate : d.rate;
            Reload = id == 0 ? config.rifleReload : id == 1 ? config.shotgunReload : d.reload;
            Magazine = id == 0 ? config.rifleMagazine : id == 1 ? config.shotgunMagazine : d.magazine;
            Reserve = d.reserve; Pellets = id == 1 ? config.shotgunPellets : d.pellets;
            Spread = id == 0 ? config.stationarySpread : d.spread; Movement = d.movement;
        }
        public static EffectiveWeaponStats Read(int id, DemoConfig config)
        {
            if (!WeaponCatalog.Valid(id)) throw new ArgumentOutOfRangeException(nameof(id));
            if (!config) throw new ArgumentNullException(nameof(config));
            return new EffectiveWeaponStats(id, config);
        }
        public int AmmoPackRounds => Magazine * TacticalPrototypeRules.AmmoMagazineCount;
    }
}
