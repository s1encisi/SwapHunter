using UnityEngine;

namespace SwapHunter
{
    [CreateAssetMenu(menuName = "SwapHunter/Demo configuration")]
    public sealed class DemoConfig : ScriptableObject
    {
        public float playerHealth = 100, walkSpeed = 5.5f, sprintSpeed = 8, jumpHeight = 1.1f;
        public float swapRange = 22, swapCooldown = 6, swapWindup = .08f, swapStun = .65f;
        public float rifleDamage = 24, rifleRate = 7.5f, rifleReload = 1.6f;
        public int rifleMagazine = 24, rifleReserve = 144;
        public float shotgunDamage = 12, shotgunRate = 1.2f, shotgunReload = 1.8f;
        public int shotgunPellets = 8, shotgunMagazine = 6, shotgunReserve = 36;
        public float assaultHealth = 96, sniperHealth = 72, shieldHealth = 144, eliteHealth = 240;
        public float grenadeFuse = 2.4f, grenadeRadius = 3.2f, grenadeDamage = 80;
        public Material[] materials;
        public Font uiFont;
        public Material worldTextMaterial;
        public ModelLibrary models;
        public int tuningVersion;
        public float acceleration = 24, braking = 36, counterBraking = 52, airAcceleration = 4, slowSpeed = 2.2f;
        public float stationarySpread = .12f, movingSpread = 1.6f, airborneSpread = 5.2f;
        public float shotSpreadGrowth = .10f, shortRecovery = 7, longRecovery = 2.4f;
    }
    public enum RunState { Loading, Menu, Playing, Paused, Dead, Victory }
    public enum EnemyKind { Training, Assault, Sniper, Shield, Elite }
    public static class Layers
    {
        public const int World = 8, Actor = 9, Effects = 10, ViewModel = 11, Grenade = 12;
        public const int WorldMask = 1 << World, CombatMask = (1 << World) | (1 << Actor);
    }
    public abstract class SwapTarget : MonoBehaviour
    {
        public virtual Vector3 AimPoint => transform.position + Vector3.up;
        public virtual float Radius => .35f;
        public virtual float Height => 1.8f;
        public abstract bool Alive { get; }
        public virtual bool Grounded => Physics.Raycast(transform.position + Vector3.up * .18f, Vector3.down, .4f, Layers.WorldMask);
        public abstract bool MoveTo(Vector3 feet);
        public virtual void AfterSwap() { }
    }
    public sealed class PhaseAnchor : SwapTarget
    {
        public override bool Alive => enabled && gameObject.activeInHierarchy;
        public override float Height => 1.4f;
        public override Vector3 AimPoint => transform.position + Vector3.up * .95f;
        public override bool MoveTo(Vector3 feet)
        {
            Vector3 delta = feet - transform.position; transform.position = feet;
            foreach (LineRenderer line in GetComponentsInChildren<LineRenderer>())
                for (int i = 0; i < line.positionCount; i++) line.SetPosition(i, line.GetPosition(i) + delta);
            return true;
        }
    }
}
