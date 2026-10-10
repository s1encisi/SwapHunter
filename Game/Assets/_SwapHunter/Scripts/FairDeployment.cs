using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace SwapHunter
{
    public sealed partial class DemoGame
    {
        sealed class Deployment
        {
            public EnemyKind kind;
            public Vector3 requested, point;
            public int arena;
            public float remaining, yaw, warningSeconds;
            public bool located, optional, mission, fixedPost;
            public string key, label;
            public Action<EnemyActor> onDeployed;
            public LineRenderer ring, beam;
        }
        readonly List<Deployment> deployments = new List<Deployment>();
        public int PendingDeployments => deployments.Count;
        public Vector3 NextDeploymentPoint => deployments.Count > 0 ? deployments[0].point : Vector3.zero;
        public int DeploymentWarnings { get; private set; }
        public int CompletedDeployments { get; private set; }

        public void QueueDeployment(EnemyKind kind, Vector3 requested, int arena, bool optional = false)
        {
            var entry = new Deployment { kind = kind, requested = requested, arena = arena, optional = optional };
            deployments.Add(entry); LocateDeployment(entry);
        }
        public void QueueMissionDeployment(string key, EnemyKind kind, Vector3 requested, float yaw,
            bool fixedPost, float warningSeconds, string label, bool optional, Action<EnemyActor> onDeployed)
        {
            foreach (var pending in deployments) if (pending.mission && pending.key == key) throw new InvalidOperationException("Duplicate encounter budget: " + key);
            var entry = new Deployment { key = key, kind = kind, requested = requested, arena = stage, optional = optional,
                mission = true, fixedPost = fixedPost, yaw = yaw, warningSeconds = warningSeconds, label = label, onDeployed = onDeployed };
            deployments.Add(entry); LocateDeployment(entry);
        }
        public bool CancelMissionDeployment(string key)
        {
            for (int i = deployments.Count - 1; i >= 0; i--)
                if (deployments[i].mission && deployments[i].key == key)
                { RemoveWarning(deployments[i]); deployments.RemoveAt(i); return true; }
            return false;
        }

        bool TryMissionSpawn(Deployment entry, out Vector3 chosen)
        {
            chosen = entry.requested;
            float radius = entry.kind == EnemyKind.Elite ? .45f : .36f;
            float height = entry.kind == EnemyKind.Elite ? 2.1f : 1.8f;
            int rings = entry.fixedPost ? 2 : 4;
            for (int ring = 0; ring <= rings; ring++)
                for (int k = 0; k < (ring == 0 ? 1 : 8); k++)
                {
                    Vector3 sample = entry.requested + new Vector3(Mathf.Cos(k * Mathf.PI / 4), 0, Mathf.Sin(k * Mathf.PI / 4)) * ring * .6f;
                    if (!NavMesh.SamplePosition(sample, out var nav, .35f, NavMesh.AllAreas)) continue;
                    // A tower role may shift within its platform, never to a ground
                    // entrance. Occupied or unbaked platforms retain their budget.
                    if (Mathf.Abs(nav.position.y - entry.requested.y) > (entry.fixedPost ? .25f : .55f)) continue;
                    if (!EnemyMotion.GroundAt(nav.position, out var point) || !EnemyMotion.ClearVolume(point, radius, height)) continue;
                    if (entry.kind == EnemyKind.Shield || entry.kind == EnemyKind.Elite)
                        if (Physics.CheckSphere(point + Vector3.up, entry.kind == EnemyKind.Elite ? .94f : .82f, Layers.WorldMask, QueryTriggerInteraction.Ignore)) continue;
                    if (Vector3.Distance(point, player.transform.position) < 7) continue;
                    bool occupied = false;
                    foreach (var other in deployments)
                        if (other != entry && other.located && Vector3.Distance(other.point, point) < 1.8f) { occupied = true; break; }
                    if (occupied) continue;
                    chosen = point; return true;
                }
            return false;
        }
        bool LocateDeployment(Deployment entry)
        {
            bool found = false;
            Vector3 chosen = entry.requested;
            if (entry.mission) found = TryMissionSpawn(entry, out chosen);
            else
            {
                float best = float.NegativeInfinity;
                var entrances = new List<Vector3> { entry.requested };
                if (expeditionActive && expeditionMap != null) entrances.AddRange(expeditionMap.enemySpawns);
                bool groundBoss = expeditionActive && expeditionLevel == 11 && entry.kind == EnemyKind.Elite;
                if (groundBoss) entrances.RemoveAll(p => Mathf.Abs(p.y) > .2f);
                foreach (var entrance in entrances)
                    for (int ring = 0; ring < 4; ring++)
                        for (int k = 0; k < (ring == 0 ? 1 : 8); k++)
                        {
                            float angle = k * Mathf.PI / 4;
                            Vector3 requested = entrance + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * ring * 3;
                            if (!EnemyMotion.FindSpawn(entry.kind, requested, out var p)) continue;
                            if (groundBoss && Mathf.Abs(p.y) > .2f) continue;
                            Vector3 delta = p - player.transform.position;
                            if (delta.magnitude < 7) continue;
                            bool duplicate = false;
                            foreach (var other in deployments)
                                if (other != entry && other.located && Vector3.Distance(other.point, p) < 2) { duplicate = true; break; }
                            if (duplicate) continue;
                            float front = Vector3.Dot(Vector3.ProjectOnPlane(delta, Vector3.up).normalized, player.transform.forward);
                            float score = front * 3 - Vector3.Distance(p, entry.requested) * .45f;
                            if (score > best) { best = score; chosen = p; found = true; }
                        }
            }
            if (!found)
            {
                entry.remaining = .75f; entry.located = false;
                if (entry.mission) Record("spawn_deferred", entry.key + " requested=" + entry.requested + " fixed=" + entry.fixedPost);
                return false;
            }
            RemoveWarning(entry); entry.point = chosen; entry.located = true;
            bool behind = Vector3.Dot((chosen - player.transform.position).normalized, player.transform.forward) < -.1f;
            entry.remaining = entry.mission ? Mathf.Max(entry.warningSeconds, behind ? 3.2f : 2.2f) : behind ? 3.2f : 2.2f;
            entry.ring = Shapes.Ring(effects, chosen + Vector3.up * .08f, 1.15f, new Color(1, .58f, .16f), .08f);
            entry.beam = Shapes.Line(effects, chosen + Vector3.up * .1f, chosen + Vector3.up * 4, new Color(1, .58f, .16f), .06f);
            DeploymentWarnings++;
            sound.Play("warning", chosen);
            Toast(entry.mission ? entry.label + " · 橙色入口预警" : (behind ? "侧后方" : "前方") + (entry.optional ? "数据追兵" : "增援") + "部署预警 · 观察橙色光柱", 2.1f);
            Record("spawn_warning", (entry.mission ? entry.key : entry.kind.ToString()) + " at=" + chosen + " yaw=" + entry.yaw + " seconds=" + entry.remaining);
            return true;
        }
        void UpdateDeployments()
        {
            if (!IsPlaying) return;
            // This runs before DemoGame.UpdateExpedition: cancel dead-Boss budgets
            // first so an expiring warning cannot spawn one frame after death.
            if (mission != null) mission.RefreshPendingCancellations();
            for (int i = deployments.Count - 1; i >= 0; i--)
            {
                var entry = deployments[i];
                entry.remaining -= Time.deltaTime;
                if (entry.remaining > 0) continue;
                if (!entry.located) { LocateDeployment(entry); continue; }
                float radius = entry.kind == EnemyKind.Elite ? .45f : .36f, height = entry.kind == EnemyKind.Elite ? 2.1f : 1.8f;
                if (Vector3.Distance(entry.point, player.transform.position) < 4.5f || !EnemyMotion.ClearVolume(entry.point, radius, height))
                { entry.located = false; RemoveWarning(entry); LocateDeployment(entry); continue; }
                var actor = EnemyActor.Spawn(entry.kind, entry.point, entry.arena);
                if (!actor) { entry.located = false; RemoveWarning(entry); entry.remaining = .75f; continue; }
                if (entry.mission)
                {
                    actor.FaceSafely(Quaternion.Euler(0, entry.yaw, 0));
                    if (Mathf.Abs(actor.transform.position.y - entry.requested.y) > (entry.fixedPost ? .25f : .55f))
                    {
                        enemies.Remove(actor); actor.gameObject.SetActive(false); Destroy(actor.gameObject);
                        entry.located = false; RemoveWarning(entry); entry.remaining = .75f; continue;
                    }
                }
                actor.expeditionOptional = entry.optional;
                actor.stunLeft = entry.mission ? .85f : .5f;
                CompletedDeployments++;
                RemoveWarning(entry); deployments.RemoveAt(i);
                entry.onDeployed?.Invoke(actor);
                Record("spawn_deployed", (entry.mission ? entry.key : entry.kind.ToString()) + " at=" + actor.transform.position + " yaw=" + actor.transform.eulerAngles.y + " optional=" + entry.optional);
            }
        }
        static void RemoveWarning(Deployment entry)
        {
            if (entry.ring) { entry.ring.gameObject.SetActive(false); UnityEngine.Object.Destroy(entry.ring.gameObject); }
            if (entry.beam) { entry.beam.gameObject.SetActive(false); UnityEngine.Object.Destroy(entry.beam.gameObject); }
            entry.ring = entry.beam = null;
        }
        public void ClearDeployments() { foreach (var entry in deployments) RemoveWarning(entry); deployments.Clear(); }
        void DrawDeploymentHud()
        {
            if (deployments.Count == 0) return;
            var entry = deployments[0];
            Vector3 local = player.transform.InverseTransformPoint(entry.point);
            string direction = (local.z < 0 ? "后方" : "前方") + (Mathf.Abs(local.x) > 3 ? (local.x < 0 ? "偏左" : "偏右") : "");
            bool bossActive = false;
            foreach (var e in enemies) if (e && e.Alive && e.boss) { bossActive = true; break; }
            string heading = entry.mission ? entry.label : entry.optional ? "数据追兵" : "增援";
            Text(heading + " · " + (entry.located ? direction + " / " + Mathf.Max(0, entry.remaining).ToString("F1") + "秒" : "等待合法入口"),
                new Rect(viewWidth - 460, bossActive ? 324 : 168, 410, 30), 17, Amber, TextAnchor.MiddleRight);
        }
    }
}
