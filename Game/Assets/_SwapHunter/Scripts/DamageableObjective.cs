using System;
using System.Collections.Generic;
using UnityEngine;

namespace SwapHunter
{
    // A physical mission prop, deliberately not a SwapTarget and not an enemy kill.
    public sealed class DamageableObjective : MonoBehaviour
    {
        static readonly List<DamageableObjective> active = new List<DamageableObjective>();
        public string Id { get; private set; }
        public float Health { get; private set; }
        public float MaximumHealth { get; private set; }
        public bool Destroyed { get; private set; }
        public bool Alive => configured && !Destroyed && isActiveAndEnabled;
        public Vector3 AimPoint => Destroyed ? transform.TransformPoint(localAim) : body ? body.bounds.center : transform.position;
        Vector3 localAim;
        ObjectiveDamagePresentation presentation;
        bool configured;
        Collider body;
        Collider[] colliders;
        Action<DamageableObjective> onDestroyed;

        public void Configure(string id, float health, Action<DamageableObjective> destroyed)
        {
            if (configured) throw new InvalidOperationException("A mission objective cannot be configured twice.");
            if (string.IsNullOrWhiteSpace(id) || health <= 0 || float.IsNaN(health) || float.IsInfinity(health))
                throw new ArgumentException("Objective requires an id and finite positive health.");
            colliders = GetComponentsInChildren<Collider>();
            foreach (var shape in colliders)
            {
                if (shape.isTrigger) continue;
                shape.gameObject.layer = Layers.Actor;
                if (!body) body = shape;
            }
            if (!body) throw new InvalidOperationException("Damageable objective needs a non-trigger collider.");
            Id = id; Health = MaximumHealth = health; onDestroyed = destroyed; configured = true;
            localAim = transform.InverseTransformPoint(body.bounds.center);
            presentation = gameObject.AddComponent<ObjectiveDamagePresentation>(); presentation.Initialize(this);
        }
        void OnEnable() { if (!active.Contains(this)) active.Add(this); }
        void OnDisable() { active.Remove(this); }
        public static DamageableObjective[] ActiveSnapshot() => active.ToArray();

        public bool ApplyDamage(float damage, Vector3 source)
        {
            if (!Alive || damage <= 0 || float.IsNaN(damage) || float.IsInfinity(damage)) return false;
            var game = DemoGame.I;
            if (game && !game.IsPlaying) return false;
            Health = Mathf.Max(0, Health - damage);
            if (presentation) presentation.NotifyHit();
            if (game) game.Record("objective_damage", Id + ";damage=" + damage.ToString("F2") + ";health=" + Health.ToString("F2") + ";source=" + source);
            if (Health > 0) return true;
            // Latch before notifying: callbacks may disable/destroy this object or cause further damage.
            Destroyed = true;
            if (presentation) presentation.Refresh();
            foreach (var shape in colliders) if (shape) shape.enabled = false;
            if (game) game.Record("objective_destroyed", Id);
            var callback = onDestroyed; onDestroyed = null;
            callback?.Invoke(this);
            return true;
        }
    }
}
