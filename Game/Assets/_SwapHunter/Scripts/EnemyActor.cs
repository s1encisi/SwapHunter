using UnityEngine;
using UnityEngine.AI;

namespace SwapHunter
{
    public sealed class EnemyActor : SwapTarget
    {
        public EnemyKind kind;
        public NavMeshAgent agent;
        public float health, maximumHealth, stunLeft, warningLeft;
        public int arena;
        public int shotsFired;
        public int grenadesThrown;
        public bool suppressAI;
        public override bool Alive => health > 0 && gameObject.activeInHierarchy;
        public override float Radius => kind == EnemyKind.Elite ? .45f : .36f;
        public override float Height => kind == EnemyKind.Elite ? 2.1f : 1.8f;
        public override Vector3 AimPoint => transform.position + Vector3.up * (Height * .62f);
        public string Label => kind == EnemyKind.Training ? "训练机" : kind == EnemyKind.Sniper ? "哨戒狙击手" : kind == EnemyKind.Shield ? "盾卫" : kind == EnemyKind.Elite ? "封锁指挥官" : "突击掷弹兵";
        Transform model, leftLeg, rightLeg, weaponPivot, shotMuzzle;
        Renderer[] bodyRenderers;
        Transform torso;
        Quaternion torsoRest;
        MaterialPropertyBlock impactBlock;
        bool wasFlashing;
        Collider body;
        BoxCollider shieldCollider;
        public bool IsShieldCollider(Collider candidate) => candidate && candidate == shieldCollider;
        float fireAt, grenadeAt, nextPath, flashLeft, age, deathAge; bool dying; Vector3 hitDirection, deathPosition; Quaternion deathRotation;
        int burstLeft;
        Vector3 shotLock, home;
        Vector3 lastKnownPlayer;
        float lastSawPlayer = -100;
        LineRenderer laser;
        DemoGame G => DemoGame.I;

        public static EnemyActor Spawn(EnemyKind kind, Vector3 feet, int arena)
        {
            if (NavMesh.SamplePosition(feet, out var hit, 1, NavMesh.AllAreas)) feet = hit.position;
            GameObject go = new GameObject(kind.ToString()); go.layer = Layers.Actor; go.transform.position = feet;
            EnemyActor enemy = go.AddComponent<EnemyActor>(); enemy.kind = kind; enemy.arena = arena;
            enemy.Initialize(); DemoGame.I.enemies.Add(enemy); return enemy;
        }
        void Initialize()
        {
            maximumHealth = kind == EnemyKind.Sniper ? G.config.sniperHealth : kind == EnemyKind.Shield ? G.config.shieldHealth : kind == EnemyKind.Elite ? G.config.eliteHealth : G.config.assaultHealth;
            health = maximumHealth; home = transform.position;
            CapsuleCollider capsule = gameObject.AddComponent<CapsuleCollider>(); capsule.radius = Radius; capsule.height = Height; capsule.center = Vector3.up * Height * .5f; body = capsule;
            agent = gameObject.AddComponent<NavMeshAgent>(); agent.height = Height; agent.radius = Radius; agent.baseOffset = 0;
            agent.speed = kind == EnemyKind.Shield || kind == EnemyKind.Elite ? 2.3f : 3.4f; agent.acceleration = 18; agent.angularSpeed = 150; agent.updateRotation = false;
            agent.stoppingDistance = kind == EnemyKind.Shield || kind == EnemyKind.Elite ? 3 : 9;
            agent.obstacleAvoidanceType = ObstacleAvoidanceType.MedQualityObstacleAvoidance;
            GameObject prefab = kind == EnemyKind.Training ? G.config.models.playerBody : kind == EnemyKind.Sniper ? G.config.models.sniper : kind == EnemyKind.Shield ? G.config.models.shield : kind == EnemyKind.Elite ? G.config.models.commander : G.config.models.assault;
            float modelScale = kind == EnemyKind.Elite ? 1.16f : 1;
            model = ImportedModels.Create(prefab, transform, Vector3.zero, Vector3.one * modelScale).transform;
            leftLeg = ImportedModels.Find(model, "leg_L"); rightLeg = ImportedModels.Find(model, "leg_R");
            if (!leftLeg || !rightLeg) throw new System.InvalidOperationException("Imported enemy rig has no leg pivots");
            if (kind == EnemyKind.Shield || kind == EnemyKind.Elite)
            {
                var shield = new GameObject("Shield"); shield.layer = Layers.Actor; shield.transform.SetParent(transform, false);
                shield.transform.localPosition = new Vector3(-.12f, .99f, .57f) * modelScale;
                var box = shield.AddComponent<BoxCollider>(); shieldCollider = box; box.size = new Vector3(1, 1.42f, .16f) * modelScale;
            }
            weaponPivot = ImportedModels.Find(model, "weapon_pivot"); shotMuzzle = ImportedModels.Find(model, "EnemyMuzzle");
            bodyRenderers = model.GetComponentsInChildren<Renderer>();
            torso = ImportedModels.Find(model, "torso"); if (torso) torsoRest = torso.localRotation;
            impactBlock = new MaterialPropertyBlock();
            fireAt = Time.time + 1.7f + Random.value; grenadeAt = Time.time + 5 + Random.value * 2;
            transform.rotation = Quaternion.Euler(0, 180, 0);
        }
        void Update()
        {
            if (dying) { AnimateDeath(); return; }
            if (!Alive || !G.IsPlaying) return;
            float dt = Time.deltaTime; age += dt; stunLeft = Mathf.Max(0, stunLeft - dt); flashLeft = Mathf.Max(0, flashLeft - dt);
            if (agent.enabled && agent.isOnNavMesh) agent.isStopped = stunLeft > 0 || suppressAI || G.qaSuppressAI || kind == EnemyKind.Training;
            float moving = agent.enabled && agent.isOnNavMesh ? Mathf.Min(agent.velocity.magnitude, 4) : 0;
            leftLeg.localRotation = Quaternion.Euler(Mathf.Sin(age * 9) * moving * 7, 0, 0);
            rightLeg.localRotation = Quaternion.Euler(-Mathf.Sin(age * 9) * moving * 7, 0, 0);
            model.localPosition = Vector3.up * (Mathf.Abs(Mathf.Sin(age * 9)) * moving * .008f);
            UpdateHitVisual();
            if (stunLeft > 0 || suppressAI || G.qaSuppressAI || kind == EnemyKind.Training) { ClearLaser(); return; }
            Vector3 playerAim = G.player.transform.position + Vector3.up * 1.1f;
            if (weaponPivot)
            {
                Vector3 localAim = transform.InverseTransformDirection(playerAim - AimPoint);
                weaponPivot.localRotation = Quaternion.Euler(-Mathf.Atan2(localAim.y, Mathf.Max(.1f, new Vector2(localAim.x, localAim.z).magnitude)) * Mathf.Rad2Deg, 0, 0);
            }
            Vector3 toPlayer = playerAim - AimPoint;
            Vector3 flat = Vector3.ProjectOnPlane(toPlayer, Vector3.up);
            if (flat.sqrMagnitude > .01f) transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(flat), dt * (kind == EnemyKind.Shield || kind == EnemyKind.Elite ? 70 : 145));
            bool visible = !Physics.Linecast(AimPoint, playerAim, Layers.WorldMask);
            if (visible) { lastKnownPlayer = G.player.transform.position; lastSawPlayer = Time.time; }
            float distance = toPlayer.magnitude;
            agent.stoppingDistance = visible ? (kind == EnemyKind.Shield || kind == EnemyKind.Elite ? 3 : 9) : 1;
            if (agent.enabled && agent.isOnNavMesh && Time.time >= nextPath)
            {
                nextPath = Time.time + .45f;
                if (kind == EnemyKind.Sniper && visible && distance > 5) agent.ResetPath();
                else if (distance > agent.stoppingDistance + 1 || !visible)
                {
                    Vector3 destination = G.player.transform.position;
                    if (kind == EnemyKind.Assault && visible) destination += transform.right * Mathf.Sin(age * .5f + GetInstanceID()) * 3;
                    if (NavMesh.SamplePosition(destination, out var nav, 2, NavMesh.AllAreas)) agent.SetDestination(nav.position);
                }
                else agent.ResetPath();
            }
            if (kind == EnemyKind.Assault && G.stage >= 2 && distance > 5 && distance < 20 && Time.time > grenadeAt && Time.time - lastSawPlayer < 6)
            {
                grenadeAt = Time.time + 10 + Random.value * 3; grenadesThrown++;
                GrenadeActor.Spawn(AimPoint + transform.forward * .8f + Vector3.up * .4f, lastKnownPlayer + Vector3.up * .15f);
                G.Record("grenade_throw", "last visible position");
                G.Toast("手雷落点将变危险 · 尝试把敌人换过去", 2.5f);
            }
            if (!visible || distance > 36) { warningLeft = 0; ClearLaser(); return; }
            float facing = Vector3.Dot(transform.forward, flat.normalized);
            if (facing < .8f) { warningLeft = 0; ClearLaser(); return; }
            if (kind == EnemyKind.Sniper)
            {
                if (warningLeft > 0)
                {
                    warningLeft -= dt;
                    if (warningLeft > .32f) shotLock = playerAim;
                    if (!laser) laser = Shapes.Line(G.effects, AimPoint, shotLock, new Color(1, .2f, .22f), .013f);
                    laser.SetPosition(0, AimPoint); laser.SetPosition(1, shotLock);
                    if (warningLeft <= 0) { Shoot(shotLock, 30, 90); fireAt = Time.time + 2.4f; ClearLaser(); }
                }
                else if (Time.time > fireAt) { warningLeft = 1.2f; shotLock = playerAim; G.sound.Play("warning", transform.position); }
            }
            else if (Time.time > fireAt)
            {
                Vector3 spread = Random.insideUnitSphere * Mathf.Clamp(distance * .04f, .1f, .9f);
                Shoot(playerAim + spread, kind == EnemyKind.Elite ? 9 : 6, 38);
                burstLeft++;
                if (burstLeft >= 3) { burstLeft = 0; fireAt = Time.time + 1.7f + Random.value * .4f; }
                else fireAt = Time.time + .23f;
            }
        }
        void Shoot(Vector3 point, float damage, float speed)
        {
            shotsFired++;
            Vector3 origin = shotMuzzle ? shotMuzzle.position : AimPoint + transform.forward * .85f + transform.right * .24f;
            Bolt.Spawn(this, origin, point - origin, speed, damage); G.sound.Play("rifle", origin);
            Shapes.Burst(origin, new Color(1, .64f, .25f), .12f);
        }
        void ClearLaser() { if (laser) Destroy(laser.gameObject); laser = null; }
        public bool Hit(float damage, Vector3 point, Collider collider, Vector3 source)
        {
            if (!Alive) return false;
            if (IsShieldCollider(collider))
            {
                Shapes.Impact(point, (point - source).normalized * -1, true); return false;
            }
            hitDirection = transform.InverseTransformDirection((point - source).normalized); Damage(damage); return true;
        }
        public void Damage(float damage)
        {
            if (!Alive) return;
            health = Mathf.Max(0, health - damage); flashLeft = .12f;
            if (health > 0) return;
            ClearLaser(); flashLeft = 0; UpdateHitVisual(); agent.enabled = false;
            foreach (Collider collider in GetComponentsInChildren<Collider>()) collider.enabled = false;
            dying = true; deathAge = 0; deathPosition = model.localPosition; deathRotation = model.localRotation;
            Shapes.Burst(AimPoint, new Color(1, .55f, .15f), .8f); G.sound.Play("death", AimPoint);
            gameObject.AddComponent<TimedEffect>().life = 2.5f;
            G.EnemyKilled(this); G.Record("enemy_killed", kind.ToString());
        }
        void AnimateDeath()
        {
            if (!model || !G.IsPlaying) return;
            deathAge += Time.deltaTime;
            float t = Mathf.Clamp01(deathAge / .48f);
            float eased = t * t * (3 - 2 * t);
            float side = hitDirection.x < -.1f ? -1 : 1;
            model.localRotation = deathRotation * Quaternion.Euler(8 * eased, 0, side * 82 * eased);
            model.localPosition = deathPosition + Vector3.up * (.3f * eased + Mathf.Sin(t * Mathf.PI) * .08f);
            if (leftLeg) leftLeg.localRotation = Quaternion.Euler(-16 * eased, 0, 0);
            if (rightLeg) rightLeg.localRotation = Quaternion.Euler(12 * eased, 0, 0);
            if (torso) torso.localRotation = torsoRest * Quaternion.Euler(14 * eased, 0, 0);
        }
        public override bool MoveTo(Vector3 feet)
        {
            if (!agent || !agent.enabled || !NavMesh.SamplePosition(feet, out var hit, .4f, NavMesh.AllAreas)) return false;
            if (!agent.Warp(hit.position)) return false;
            agent.ResetPath(); Physics.SyncTransforms(); return true;
        }
        public override void AfterSwap()
        {
            stunLeft = G.config.swapStun; warningLeft = 0; ClearLaser();
            fireAt = Mathf.Max(fireAt, Time.time + stunLeft + .25f);
            if (agent.enabled && agent.isOnNavMesh) { agent.ResetPath(); agent.isStopped = true; }
        }
        void OnDestroy() { ClearLaser(); }
        void UpdateHitVisual()
        {
            bool flashing = flashLeft > 0;
            if (torso) torso.localRotation = torsoRest * Quaternion.Euler(-7 * Mathf.Clamp01(flashLeft / .12f), hitDirection.x * 5 * Mathf.Clamp01(flashLeft / .12f), hitDirection.x * -3 * Mathf.Clamp01(flashLeft / .12f));
            if (!flashing && !wasFlashing) return;
            foreach (var renderer in bodyRenderers)
            {
                if (!renderer) continue;
                Material[] materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    if (!flashing) { renderer.SetPropertyBlock(null, i); continue; }
                    Material material = materials[i]; string property = material.HasProperty("_BaseColorFactor") ? "_BaseColorFactor" : material.HasProperty("_BaseColor") ? "_BaseColor" : "_Color";
                    if (!material.HasProperty(property)) continue;
                    impactBlock.Clear(); impactBlock.SetColor(property, Color.Lerp(material.GetColor(property), Color.white, .32f * flashLeft / .12f)); renderer.SetPropertyBlock(impactBlock, i);
                }
            }
            wasFlashing = flashing;
        }
    }
}

