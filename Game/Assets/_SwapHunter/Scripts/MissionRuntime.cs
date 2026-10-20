using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SwapHunter
{
    public sealed class MissionSpawnBinding
    {
        public Vector3 position;
        public float yaw;
        public bool fixedPost;
        public MissionSpawnBinding(Vector3 p, float yaw, bool fixedPost)
        { position = p; this.yaw = yaw; this.fixedPost = fixedPost; }
    }

    // Small explicit wiring surface used by ExpeditionWorld. It is intentionally
    // not a general mission graph or a second scene-authoring system.
    public sealed class MissionLayout : MonoBehaviour
    {
        readonly Dictionary<string, MissionSpawnBinding> spawns = new Dictionary<string, MissionSpawnBinding>();
        readonly Dictionary<string, Bounds> regions = new Dictionary<string, Bounds>();
        readonly Dictionary<ExpeditionNode, string> objectives = new Dictionary<ExpeditionNode, string>();
        sealed class Passage { public string objective; public GameObject target; public bool active; }
        readonly List<Passage> passages = new List<Passage>();
        bool navigationDirty, navigationRunning;
        public bool NavigationUpdating => navigationDirty || navigationRunning;
        public Vector3[] MiniBossPosts { get; set; } = new Vector3[0];
        public void MarkNavigationDirty() { navigationDirty = true; if (!navigationRunning) StartCoroutine(RefreshNavigation()); }

        static string SpawnKey(string group, int index) => group + ":" + index;
        public void BindObjective(ExpeditionNode node, string id)
        {
            if (!node || string.IsNullOrEmpty(id)) throw new ArgumentException("A mission objective requires a node and stable id.");
            objectives.Add(node, id);
        }
        public bool TryObjective(ExpeditionNode node, out string id) => objectives.TryGetValue(node, out id);
        public bool HasObjective(string id) => objectives.ContainsValue(id);
        public void BindSpawn(string groupId, int index, Vector3 feet, float yaw, bool fixedPost = false)
        { spawns.Add(SpawnKey(groupId, index), new MissionSpawnBinding(feet, yaw, fixedPost)); }
        public MissionSpawnBinding Spawn(string groupId, int index)
        {
            if (!spawns.TryGetValue(SpawnKey(groupId, index), out var result))
                throw new InvalidOperationException("Missing mission spawn " + SpawnKey(groupId, index));
            return result;
        }
        public void BindRegion(string id, Bounds bounds) { regions.Add(id, bounds); }
        public bool TryRegion(string id, out Bounds bounds)
        { if (id == null) { bounds = default; return false; } return regions.TryGetValue(id, out bounds); }
        public IEnumerable<KeyValuePair<string, Bounds>> Regions => regions;
        public Bounds RegionAt(Vector3 point, bool fixedPost)
        {
            if (fixedPost) return new Bounds(point + Vector3.up, new Vector3(7, 3, 7));
            Bounds chosen = new Bounds(point + Vector3.up * 2, new Vector3(18, 12, 18));
            float volume = float.PositiveInfinity;
            foreach (var r in regions.Values)
            {
                if (!r.Contains(point + Vector3.up * .2f)) continue;
                float size = r.size.x * r.size.y * r.size.z;
                if (size < volume) { chosen = r; volume = size; }
            }
            return chosen;
        }
        public void OpenOn(string objectiveId, GameObject passage, bool activeWhenComplete = true)
        {
            if (!passage) throw new ArgumentNullException(nameof(passage));
            passages.Add(new Passage { objective = objectiveId, target = passage, active = activeWhenComplete });
            passage.SetActive(!activeWhenComplete);
        }
        public void Complete(string id)
        {
            foreach (var pair in objectives)
                if (pair.Value == id && pair.Key && !pair.Key.used) pair.Key.Activate();
            foreach (var p in passages)
                if (p.objective == id && p.target && p.target.activeSelf != p.active)
                { p.target.SetActive(p.active); navigationDirty = true; }
            if (navigationDirty && !navigationRunning) StartCoroutine(RefreshNavigation());
        }
        IEnumerator RefreshNavigation()
        {
            navigationRunning = true;
            while (navigationDirty)
            {
                navigationDirty = false;
                Physics.SyncTransforms();
                var map = DemoGame.I.expeditionMap;
                if (map != null && map.root == transform && map.navigation && map.navigation.navMeshData)
                    yield return map.navigation.UpdateNavMesh(map.navigation.navMeshData);
            }
            navigationRunning = false;
        }
        public void Validate(MissionDefinition definition)
        {
            foreach (var objective in definition.objectives)
                if (!HasObjective(objective.id)) throw new InvalidOperationException("Unbound mission objective " + objective.id);
            foreach (var group in definition.groups)
            {
                for (int i = 0; i < group.roles.Length; i++) Spawn(group.id, i);
                if ((group.trigger == EncounterTrigger.Region || group.trigger == EncounterTrigger.RegionElapsed || group.trigger == EncounterTrigger.MiniBoss) && !regions.ContainsKey(group.region))
                    throw new InvalidOperationException("Unbound mission region " + group.region);
                if (!string.IsNullOrEmpty(group.alternativeRegion) && !regions.ContainsKey(group.alternativeRegion))
                    throw new InvalidOperationException("Unbound alternative mission region " + group.alternativeRegion);
            }
        }
        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(.1f, .8f, 1, .8f);
            foreach (var bounds in regions.Values) Gizmos.DrawWireCube(bounds.center, bounds.size);
            foreach (var spawn in spawns.Values)
            {
                Gizmos.color = spawn.fixedPost ? Color.magenta : Color.yellow;
                Gizmos.DrawWireSphere(spawn.position + Vector3.up, .45f);
                Gizmos.DrawLine(spawn.position + Vector3.up, spawn.position + Vector3.up + Quaternion.Euler(0, spawn.yaw, 0) * Vector3.forward * 2);
            }
        }
    }

    // AI queries this component for walk/cover destinations only. Q and gravity
    // remain unrestricted; a displaced enemy adopts its actual landing region.
    public sealed class MissionEncounterAgent : MonoBehaviour
    {
        MissionLayout layout;
        EnemyActor owner;
        Bounds territory;
        bool awaitingLanding,bossMovement,fixedStation;
        public string GroupId{get;private set;}
        public Vector3 CurrentCenter=>territory.center;
        public Vector3 Fallback=>territory.ClosestPoint(transform.position);
        public bool FixedPost=>fixedStation;
        public bool Allows(Vector3 destination)
        {
            if(bossMovement||awaitingLanding||territory.Contains(destination+Vector3.up*.15f))return true;
            if(fixedStation||!owner||!owner.tactical||!owner.tactical.HasPlayerMemory)return false;
            // Ordinary squads can pursue through connected doors/corridors. The
            // NavMesh path checks still own reachability; this is only a leash.
            Vector3 fromHome=Vector3.ProjectOnPlane(destination-territory.center,Vector3.up);
            return fromHome.magnitude<=Mathf.Max(48,Mathf.Max(territory.extents.x,territory.extents.z)+18);
        }
        public void Configure(MissionLayout layout,string groupId,bool fixedPost)
        {
            this.layout=layout;GroupId=groupId;owner=GetComponent<EnemyActor>();
            bossMovement=owner.kind==EnemyKind.Elite;fixedStation=fixedPost;
            territory=layout.RegionAt(transform.position,fixedPost);
        }
        public void RebaseAfterSwap()
        {
            awaitingLanding=owner&&owner.motion&&owner.motion.Airborne;fixedStation=false;
            territory=layout.RegionAt(transform.position,false);
            if(DemoGame.I)DemoGame.I.Record("encounter_relocated",GroupId+" center="+territory.center);
        }
        void LateUpdate()
        {
            if(!awaitingLanding||!owner||!owner.Alive||owner.motion&&owner.motion.Airborne)return;
            awaitingLanding=false;territory=layout.RegionAt(transform.position,false);
        }
    }

    public sealed class MissionMember
    {
        public EncounterGroup group;
        public EncounterRole role;
        public int index;
        public string Key => group.id + ":" + index;
        public EncounterBudgetState state;
        public EnemyActor actor;
        public bool Defeated => state == EncounterBudgetState.Deployed && (!actor || !actor.Alive);
    }

    public sealed class MissionRuntime
    {
        readonly DemoGame game;
        readonly MissionLayout layout;
        readonly HashSet<string> completed = new HashSet<string>(), visited = new HashSet<string>(), triggered = new HashSet<string>();
        readonly List<MissionMember> members = new List<MissionMember>();
        readonly Dictionary<string,float> regionTimes = new Dictionary<string,float>();
        bool miniCancellationRecorded, calibrationOpened;
        int lastShots;
        bool stopped, bossCancellationRecorded, mainReadyRecorded;
        public MissionDefinition Definition { get; }
        public IReadOnlyList<MissionMember> Members => members;
        public float Elapsed { get; private set; }
        public int CompletedObjectives { get { int n = 0; foreach (var o in Definition.objectives) if (completed.Contains(o.id)) n++; return n; } }
        public int UntriggeredBudget => Count(EncounterBudgetState.Untriggered);
        public int WarningBudget => Count(EncounterBudgetState.Warning);
        public int DeployedBudget => Count(EncounterBudgetState.Deployed);
        public int CancelledBudget => Count(EncounterBudgetState.Cancelled);
        public int DefeatedBudget { get { int n=0; foreach(var m in members) if(m.Defeated)n++; return n; } }
        public int LivingBudget { get { int n=0; foreach(var m in members) if(m.state==EncounterBudgetState.Deployed && m.actor && m.actor.Alive)n++; return n; } }
        public int LocalActiveBudget { get { int n=0; foreach(var m in members) { var p=m.actor?m.actor.transform.position:layout.Spawn(m.group.id,m.index).position; if (Vector3.Distance(p,game.player.transform.position)>80) continue; if(m.state==EncounterBudgetState.Warning || m.state==EncounterBudgetState.Deployed && m.actor && m.actor.Alive)n++; } return n; } }
        public int ActiveMiniEscorts { get { int n=0; foreach(var m in members) if(m.group.miniEscort && (m.state==EncounterBudgetState.Warning || m.state==EncounterBudgetState.Deployed && m.actor && m.actor.Alive))n++; return n; } }
        public int ActiveEscorts { get { int n = 0; foreach (var m in members) if (m.group.escort && (m.state == EncounterBudgetState.Warning || m.state == EncounterBudgetState.Deployed && m.actor && m.actor.Alive)) n++; return n; } }
        public int RequiredRemaining { get { int n = 0; foreach (var m in members) if (m.role.required && !m.Defeated) n++; return n; } }
        public int LivingNonRequired { get { int n = 0; foreach (var m in members) if (!m.role.required && m.actor && m.actor.Alive && m.role.kind != EnemyKind.Training) n++; return n; } }
        public bool CanExtract => !stopped && CompletedObjectives >= Definition.requiredObjectives && RequiredRemaining == 0 && game.totalSwaps >= Definition.minimumSwaps;
        public bool IsStopped => stopped;
        public bool ObjectiveComplete(string id) => completed.Contains(id);
        public bool RegionVisited(string id) => visited.Contains(id);
        public bool GroupTriggered(string id) => triggered.Contains(id);
        public string CountdownStatus { get { string text="";foreach(var q in Definition.groups)if(q.trigger==EncounterTrigger.RegionElapsed && !completed.Contains(q.cancelOnObjective) && regionTimes.TryGetValue(q.region,out float entered) && !triggered.Contains(q.id)) text+=" · "+q.label+" "+Mathf.CeilToInt(Mathf.Max(0,q.triggerSeconds+q.warningSeconds-(Elapsed-entered)))+"秒";return text; } }
        public string Status => CanExtract
            ? "任务完成 · 前往" + Definition.extraction + " · 可遗留敌人"
            : Definition.instruction + " · 目标 " + CompletedObjectives + "/" + Definition.requiredObjectives
                + (RequiredRemaining > 0 ? " · 指定威胁 " + RequiredRemaining : "") + CountdownStatus;
        int Count(EncounterBudgetState state) { int n = 0; foreach (var m in members) if (m.state == state) n++; return n; }

        public MissionRuntime(DemoGame game, MissionLayout layout, MissionDefinition definition)
        {
            this.game = game; this.layout = layout;
            // Freeze this deployment even when the asset is edited in a live
            // Editor session. New settings take effect on the next mission.
            Definition = JsonUtility.FromJson<MissionDefinition>(JsonUtility.ToJson(definition));
            layout.Validate(Definition); lastShots = game.totalShots;
            foreach (var group in Definition.groups)
                for (int i = 0; i < group.roles.Length; i++) members.Add(new MissionMember { group = group, role = group.roles[i], index = i });
        }

        public bool CompleteObjective(string id)
        {
            if (stopped || !game.IsPlaying || completed.Contains(id)) return false;
            bool known = Definition.level == 10 && (id == "field-left" || id == "field-right");
            foreach (var objective in Definition.objectives) if (objective.id == id) known = true;
            if (!known) { game.Record("mission_unknown_objective", id ?? "<null>"); return false; }
            completed.Add(id); layout.Complete(id); game.expeditionObjectives = CompletedObjectives;
            game.Record("mission_objective", Definition.level + ":" + id + " count=" + CompletedObjectives);
            foreach (var group in Definition.groups) if (group.cancelOnObjective == id) CancelGroup(group.id, "control:" + id);
            if (Definition.level == 7 && CompletedObjectives == 3)
            {
                game.expeditionBonus = true; game.Toast("第三匣已取得 · 成功撤离额外 +40", 4);
            }
            else game.Toast("目标已记录 · " + CompletedObjectives + "/" + Definition.requiredObjectives, 2.5f);
            EvaluateGroups(); CheckMainReady();
            return true;
        }

        void Visit(string id, string cause)
        {
            if (!visited.Add(id)) return;
            regionTimes[id] = Elapsed;
            foreach(var group in Definition.groups) if(group.trigger==EncounterTrigger.RegionElapsed && group.region==id && !completed.Contains(group.cancelOnObjective)) game.Toast(group.label + " · " + (group.triggerSeconds + group.warningSeconds).ToString("0") + "秒后开始部署，关闭对应控制站可断援", 5);
            game.Record("mission_region", Definition.level + ":" + id + " via=" + cause + " feet=" + game.player.transform.position);
        }
        void DetectRegions()
        {
            foreach (var region in layout.Regions)
                if (region.Value.Contains(game.player.transform.position + Vector3.up * .2f)) Visit(region.Key, "arrival");
            // A shot into an unvisited branch activates its budget even if the
            // player never crosses the doorway. Solid walls still block this cue.
            if (game.totalShots != lastShots)
            {
                var ray = new Ray(game.player.Eye.position, game.player.Eye.forward);
                float wall = Physics.Raycast(ray, out var hit, 100, Layers.WorldMask, QueryTriggerInteraction.Ignore) ? hit.distance : 100;
                foreach (var region in layout.Regions)
                    if (region.Value.IntersectRay(ray, out float distance) && distance <= wall + .05f) Visit(region.Key, "fire");
                lastShots = game.totalShots;
            }
        }
        BossEncounter Boss
        {
            get { foreach (var member in members) if (member.group.id == "boss" && member.actor) return member.actor.boss; return null; }
        }
        bool BossDefeated
        {
            get { foreach (var member in members) if (member.group.id == "boss" && member.Defeated) return true; return false; }
        }
        bool MiniDefeated { get { foreach(var m in members) if(!string.IsNullOrEmpty(m.group.miniBossId) && m.Defeated)return true; return false; } }
        bool OriginalRequiredDefeated { get { foreach(var m in members) if(m.role.required && string.IsNullOrEmpty(m.group.miniBossId) && m.group.id!="boss" && !m.Defeated)return false; return true; } }
        bool GroupDefeated(string id)
        {
            bool found = false;
            foreach (var member in members) if (member.group.id == id) { found = true; if (!member.Defeated) return false; }
            return found;
        }
        bool TriggerReady(EncounterGroup group)
        {
            if (!string.IsNullOrEmpty(group.afterDefeated) && !GroupDefeated(group.afterDefeated)) return false;
            if (Definition.level == 0 && group.id == "combat" && game.totalSwaps < 1) return false;
            if (Definition.level == 1 && group.id == "second") foreach(var m in members) if(m.group.id=="first" && m.role.required && !m.Defeated)return false;
            if (group.trigger == EncounterTrigger.Region && !string.IsNullOrEmpty(group.objective) && !completed.Contains(group.objective)) return false;
            if (!string.IsNullOrEmpty(group.alternativeRegion) && visited.Contains(group.alternativeRegion)) return true;
            switch (group.trigger)
            {
                case EncounterTrigger.MiniBoss: return layout.TryRegion(group.region,out var arenaBounds) && arenaBounds.Contains(game.player.transform.position+Vector3.up*.2f) && (Definition.level==11 || CompletedObjectives >= Definition.requiredObjectives && OriginalRequiredDefeated && game.totalSwaps>=Definition.minimumSwaps);
                case EncounterTrigger.RegionElapsed: return regionTimes.TryGetValue(group.region,out float entered) && Elapsed-entered>=group.triggerSeconds;
                case EncounterTrigger.Start: return Elapsed >= group.triggerSeconds;
                case EncounterTrigger.Region: return visited.Contains(group.region);
                case EncounterTrigger.Objective: return completed.Contains(group.objective);
                case EncounterTrigger.ObjectiveCount: return CompletedObjectives >= group.objectiveCount;
                case EncounterTrigger.Elapsed: return Elapsed >= group.triggerSeconds;
                case EncounterTrigger.BossReady: return Boss && !BossDefeated;
                case EncounterTrigger.BossRifle: return Boss && !BossDefeated && (int)Boss.Phase >= (int)BossPhase.Rifle;
                case EncounterTrigger.BossCombined: return Boss && !BossDefeated && (int)Boss.Phase >= (int)BossPhase.Combined;
                default: return false;
            }
        }
        public void RefreshPendingCancellations()
        {
            if (MiniDefeated && !miniCancellationRecorded) { miniCancellationRecorded=true; layout.Complete("$mini"); foreach(var group in Definition.groups) if(group.miniEscort) CancelGroup(group.id,"mini_boss_defeated"); game.Record("mini_boss_defeated",Definition.level.ToString()); }
            if (Definition.level == 11 && BossDefeated && !bossCancellationRecorded)
            {
                bossCancellationRecorded = true;
                foreach (var group in Definition.groups) if (group.escort) CancelGroup(group.id, "boss_defeated");
            }
        }
        public void Tick(float dt)
        {
            if (stopped || !game.IsPlaying) return;
            Elapsed += Mathf.Max(0, dt); DetectRegions();
            if(Definition.level==0 && game.totalSwaps>=1 && !calibrationOpened){calibrationOpened=true;layout.Complete("$calibrated");game.Record("world_calibration_passage","opened_after_real_q");}
            RefreshPendingCancellations();
            EvaluateGroups(); CheckMainReady();
        }
        void CheckMainReady()
        {
            if (!mainReadyRecorded && CanExtract)
            { mainReadyRecorded = true; layout.Complete("$main"); game.Record("mission_ready", Definition.level + ":" + Definition.extraction); game.Toast("任务完成 · 可前往" + Definition.extraction + "，无需全清", 4); }
        }
        void EvaluateGroups()
        {
            if (stopped) return;
            foreach (var group in Definition.groups)
            {
                if (!string.IsNullOrEmpty(group.cancelOnObjective) && completed.Contains(group.cancelOnObjective)) continue;
                if (group.escort && BossDefeated || group.miniEscort && MiniDefeated) continue;
                if (!triggered.Contains(group.id) && TriggerReady(group))
                {
                    triggered.Add(group.id); game.expeditionWave = triggered.Count;
                    game.Record("encounter_trigger", Definition.level + ":" + group.id);
                }
                if (!triggered.Contains(group.id)) continue;
                foreach (var member in members)
                {
                    if (member.group != group || member.state != EncounterBudgetState.Untriggered) continue;
                    if (group.escort && ActiveEscorts >= Definition.maximumEscorts) break;
                    if (group.miniEscort && ActiveMiniEscorts >= Definition.miniEscortLimit) break;
                    if (LocalActiveBudget >= Definition.liveLimit - 1 && !member.role.required) break;
                    if (LocalActiveBudget >= Definition.liveLimit && member.role.required) break;
                    var binding = layout.Spawn(group.id, member.index);
                    if (Vector3.Distance(binding.position, game.player.transform.position)>100) continue;
                    member.state = EncounterBudgetState.Warning;
                    game.QueueMissionDeployment(member.Key, member.role.kind, binding.position, binding.yaw,
                        member.role.fixedPost || binding.fixedPost, group.warningSeconds, group.label, !member.role.required,
                        actor => Deployed(member, actor));
                }
            }
        }
        void Deployed(MissionMember member, EnemyActor actor)
        {
            if (member.state != EncounterBudgetState.Warning || stopped) throw new InvalidOperationException("Deployment no longer belongs to this mission.");
            member.actor = actor; member.state = EncounterBudgetState.Deployed;
            if (!string.IsNullOrEmpty(member.group.miniBossId)) { actor.EnableMiniBoss(Definition.level); var controller=actor.GetComponent<MiniBossController>(); if(controller)controller.SetPosts(layout.MiniBossPosts); }
            else if (member.group.id=="boss") actor.EnableBoss();
            if(Definition.level==4 && member.group.id=="left" && member.index<2)
            {
                var relay=layout.GetComponent<FireControlRelay>();
                if(!relay)relay=layout.gameObject.AddComponent<FireControlRelay>();
                relay.Bind(member.Key,actor);
            }
            var binding = layout.Spawn(member.group.id, member.index);
            actor.gameObject.AddComponent<MissionEncounterAgent>().Configure(layout, member.group.id, member.role.fixedPost || binding.fixedPost);
            if (member.role.firstGrenade) actor.gameObject.AddComponent<EncounterGrenadeCue>();
            game.Record("encounter_budget_deployed", Definition.level + ":" + member.Key);
        }
        public void CancelGroup(string id, string reason)
        {
            foreach (var member in members)
            {
                if (member.group.id != id || member.state == EncounterBudgetState.Deployed || member.state == EncounterBudgetState.Cancelled) continue;
                game.CancelMissionDeployment(member.Key);
                member.state = EncounterBudgetState.Cancelled;
                game.Record("encounter_budget_cancelled", Definition.level + ":" + member.Key + " reason=" + reason);
            }
        }
        public void Stop(string reason)
        {
            if (stopped) return;
            foreach (var group in Definition.groups) CancelGroup(group.id, reason);
            stopped = true;
        }
    }

    public enum FirstGrenadeCueState { Waiting, Paused, AISuppressed, Silenced, Stunned, OutOfRange, Occluded, Preparing, Thrown, Ended }

    // EnemyActor yields ordinary tactical decisions while Preparing. This component
    // runs afterward to revalidate the visible throw opportunity and release/cancel it.
    [DefaultExecutionOrder(100)]
    public sealed class EncounterGrenadeCue : MonoBehaviour
    {
        EnemyActor owner;
        float visibleSeconds;
        public FirstGrenadeCueState State { get; private set; }
        public bool Preparing => State == FirstGrenadeCueState.Preparing && isActiveAndEnabled;
        public float PreparationSeconds => visibleSeconds;
        public GrenadeActor LastThrown { get; private set; }
        void Awake() { owner = GetComponent<EnemyActor>(); if (owner) owner.grenadeCue = this; }
        void SetState(FirstGrenadeCueState state)
        {
            if (State == state) return;
            State = state;
            var game = DemoGame.I;
            if (game && owner) game.Record("mission_first_grenade_state", state + ";feet=" + owner.transform.position + ";preparation=" + visibleSeconds.ToString("F3"));
        }
        void Interrupt(FirstGrenadeCueState state) { visibleSeconds = 0; SetState(state); }
        void Update()
        {
            var game = DemoGame.I;
            if (!owner || !owner.Alive) { SetState(FirstGrenadeCueState.Ended); enabled = false; return; }
            if (owner.grenadesThrown > 0) { SetState(FirstGrenadeCueState.Thrown); enabled = false; return; }
            if (!game || !game.IsPlaying) { SetState(FirstGrenadeCueState.Paused); return; }
            if (game.qaSuppressAI || owner.suppressAI) { SetState(FirstGrenadeCueState.AISuppressed); return; }
            if (owner.Silenced) { Interrupt(FirstGrenadeCueState.Silenced); return; }
            if (owner.stunLeft > 0) { Interrupt(FirstGrenadeCueState.Stunned); return; }
            Vector3 target = game.player.transform.position;
            float distance = Vector3.Distance(owner.transform.position, target);
            if (distance < 5 || distance > 20) { Interrupt(FirstGrenadeCueState.OutOfRange); return; }
            if (Physics.Linecast(owner.AimPoint, game.player.Eye.position, Layers.WorldMask)) { Interrupt(FirstGrenadeCueState.Occluded); return; }
            bool beginPreparation = !Preparing;
            SetState(FirstGrenadeCueState.Preparing);
            if (beginPreparation && owner.agent.enabled && owner.agent.isOnNavMesh)
            { owner.agent.isStopped = true; owner.agent.ResetPath(); }
            visibleSeconds += Time.deltaTime;
            if (visibleSeconds < 1.25f) return;
            // Release only through the real rigidbody/fuse/area-warning grenade implementation.
            LastThrown = GrenadeActor.Spawn(owner.AimPoint + Vector3.up * .4f, target + Vector3.up * .15f);
            if (!LastThrown) throw new InvalidOperationException("First grenade creation failed.");
            owner.grenadesThrown++;
            SetState(FirstGrenadeCueState.Thrown);
            game.Record("mission_first_grenade", "real grenade=" + LastThrown.GetInstanceID() + ";source=" + owner.AimPoint + ";target=" + target + ";visiblePreparation=" + visibleSeconds.ToString("F3"));
            game.Toast("真实手雷已落向你的原位置 · 跑离或确认双端后换位", 3);
            enabled = false;
        }
    }
}
