using System;
using System.Collections.Generic;
using UnityEngine;

namespace SwapHunter
{
    // Fixed spherical field. Endpoint feet positions determine Q eligibility, never the ray segment.
    public sealed class PhaseInterferenceField : MonoBehaviour
    {
        static readonly List<PhaseInterferenceField> fields = new List<PhaseInterferenceField>();
        public string Id { get; private set; }
        public float Radius { get; private set; }
        public bool Active => configured && switchedOn && isActiveAndEnabled;
        bool configured, switchedOn;
        PhaseInterferencePresentation presentation;

        public void Configure(string id, float radius)
        {
            if (string.IsNullOrWhiteSpace(id) || radius <= 0 || float.IsNaN(radius) || float.IsInfinity(radius))
                throw new ArgumentException("Interference field requires an id and finite positive radius.");
            Id = id; Radius = radius; configured = switchedOn = true;
            if (!presentation) { presentation = gameObject.AddComponent<PhaseInterferencePresentation>(); presentation.Initialize(this); }
            else presentation.Refresh();
        }
        public void SetActive(bool active) { switchedOn = active; if (presentation) presentation.Refresh(); }
        void OnEnable() { if (!fields.Contains(this)) fields.Add(this); if (presentation) presentation.Refresh(); }
        void OnDisable() { fields.Remove(this); if (presentation) presentation.Refresh(); }
        public bool ContainsPoint(Vector3 position) => Active && (position - transform.position).sqrMagnitude <= Radius * Radius;
        public static bool Contains(Vector3 position) => TryGetBlockingField(position, out _);
        public static bool TryGetBlockingField(Vector3 position, out PhaseInterferenceField field)
        {
            foreach (var candidate in fields)
                if (candidate && candidate.ContainsPoint(position)) { field = candidate; return true; }
            field = null; return false;
        }
        void OnDrawGizmosSelected() { Gizmos.color = new Color(1, .55f, .1f); Gizmos.DrawWireSphere(transform.position, Radius); }
    }
}
