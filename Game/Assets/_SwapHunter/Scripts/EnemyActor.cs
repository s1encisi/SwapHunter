using UnityEngine;
using UnityEngine.AI;

namespace SwapHunter
{
    public enum EnemyHitRegion { Torso, Head, Limb, Shield }
    public sealed class EnemyHitPart : MonoBehaviour { public EnemyActor owner; }
    public sealed partial class EnemyActor : SwapTarget
    {
        public EnemyKind kind;
        public NavMeshAgent agent;
        public EnemyMotion motion;
        public BossEncounter boss;
        public EnemyTacticalAI tactical;
        public float health, maximumHealth, stunLeft, warningLeft, silenceLeft;
        public bool Silenced=>silenceLeft>0;
        public void ApplySilence(float seconds)
        {
            if(!Alive||seconds<=0)return;
            if(boss&&!boss.TryReceiveSilence(seconds))return;
            if(silenceLeft<=0)DemoGame.I.sound.Play("silence",AimPoint);
            silenceLeft=Mathf.Max(silenceLeft,seconds);DemoGame.I.Record("enemy_silenced",kind+":"+seconds);
        }
        public int arena;
        public bool expeditionOptional;
        public int shotsFired;
        public int grenadesThrown;
        public bool suppressAI;
        public override bool Alive => health > 0 && gameObject.activeInHierarchy;
        public override float Radius => kind == EnemyKind.Elite ? .45f : .36f;
        public override float Height => kind == EnemyKind.Elite ? 2.1f : 1.8f;
        public override Vector3 AimPoint => transform.position + Vector3.up * (Height * .62f);
        public string Label => (Marked ? BaseLabel + " · 已标记" : BaseLabel)+(Silenced?" · 沉默":"");
        string BaseLabel => kind == EnemyKind.Training ? "训练机" : kind == EnemyKind.Sniper ? "哨戒狙击手" : kind == EnemyKind.Shield ? "盾卫" : kind == EnemyKind.Elite ? "封锁指挥官" : "突击掷弹兵";
        Transform model, leftLeg, rightLeg, weaponPivot, shotMuzzle;
        Renderer[] bodyRenderers;
        Transform torso;
        Quaternion torsoRest;
        MaterialPropertyBlock impactBlock;
        bool wasFlashing;
        Collider body;
        readonly System.Collections.Generic.Dictionary<Collider,EnemyHitRegion> hurtboxes=new System.Collections.Generic.Dictionary<Collider,EnemyHitRegion>();
        float markedUntil;
        public bool Marked => Alive && Time.time<markedUntil;
        public EnemyHitRegion LastHitRegion { get; private set; }
        public string LastHitRegionLabel => LastHitRegion==EnemyHitRegion.Head?"头部":LastHitRegion==EnemyHitRegion.Limb?"四肢":LastHitRegion==EnemyHitRegion.Shield?"盾牌":"躯干";
        public void MarkPhase(float seconds){if(Alive){markedUntil=Mathf.Max(markedUntil,Time.time+Mathf.Clamp(seconds,0,10));stunLeft=Mathf.Max(stunLeft,.2f);}}
        public EnemyHitRegion RegionOf(Collider c)=>IsShieldCollider(c)?EnemyHitRegion.Shield:c&&hurtboxes.TryGetValue(c,out var region)?region:EnemyHitRegion.Torso;
        BoxCollider shieldCollider;
        public bool HasPhysicalShield => shieldCollider && shieldCollider.enabled;
        public bool IsShieldCollider(Collider candidate) => HasPhysicalShield && candidate && candidate == shieldCollider;
        float fireAt, grenadeAt, nextPath, flashLeft, age, deathAge; bool dying; Vector3 hitDirection, deathPosition; Quaternion deathRotation;
        int burstLeft;
        Vector3 shotLock, home;
        Vector3 lastKnownPlayer, gaitPrevious;
        float gaitPhase, gaitWeight;
        public float GaitPhase=>gaitPhase;
        public float GaitWeight=>gaitWeight;
        float lastSawPlayer = -100;
        LineRenderer laser;
        DemoGame G => DemoGame.I;

        public static EnemyActor Spawn(EnemyKind kind, Vector3 feet, int arena)
        {
            if (!EnemyMotion.FindSpawn(kind,feet,out var valid)) { DemoGame.I.Record("spawn_rejected",kind+":"+feet); return null; }
            feet=valid;
            GameObject go = new GameObject(kind.ToString()); go.layer = Layers.Actor; go.transform.position = feet;
            EnemyActor enemy = go.AddComponent<EnemyActor>(); enemy.kind = kind; enemy.arena = arena;
            enemy.Initialize(); DemoGame.I.enemies.Add(enemy);
            if(kind==EnemyKind.Elite&&DemoGame.I.expeditionActive&&DemoGame.I.expeditionLevel==11)enemy.EnableBoss();
            return enemy;
        }
        public BossEncounter EnableBoss()
        {
            if(boss)return boss;
            DisableShieldMeleePresentation();
            if(shieldCollider)shieldCollider.enabled=false;
            var shieldVisual=ImportedModels.Find(model,"shield_visual");
            if(shieldVisual)shieldVisual.gameObject.SetActive(false);
            boss=gameObject.AddComponent<BossEncounter>();boss.Initialize(this);return boss;
        }
        void Initialize()
        {
            maximumHealth = kind == EnemyKind.Sniper ? G.config.sniperHealth : kind == EnemyKind.Shield ? G.config.shieldHealth : kind == EnemyKind.Elite ? G.config.eliteHealth : G.config.assaultHealth;
            health = maximumHealth; home = transform.position;
            CharacterController capsule = gameObject.AddComponent<CharacterController>(); capsule.radius = Radius; capsule.height = Height; capsule.center = Vector3.up * Height * .5f; capsule.skinWidth=.02f;capsule.stepOffset=.32f;capsule.slopeLimit=48;capsule.minMoveDistance=0; body = capsule;
            agent = gameObject.AddComponent<NavMeshAgent>(); agent.height = Height; agent.radius = Radius; agent.baseOffset = 0;
            agent.speed=kind==EnemyKind.Elite?2.05f:kind==EnemyKind.Shield?2.3f:kind==EnemyKind.Sniper?2.9f:3.4f;
            agent.acceleration=kind==EnemyKind.Elite?7:kind==EnemyKind.Shield?8:kind==EnemyKind.Sniper?11:12;
            agent.autoBraking=true;agent.angularSpeed=150;agent.updateRotation=false;
            agent.stoppingDistance = kind == EnemyKind.Shield || kind == EnemyKind.Elite ? 3 : 9;
            agent.obstacleAvoidanceType = ObstacleAvoidanceType.MedQualityObstacleAvoidance;
            motion=gameObject.AddComponent<EnemyMotion>();motion.Initialize(this,capsule);
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
                Physics.IgnoreCollision(capsule,box,true);
            }
            BuildHurtboxes(modelScale);
            weaponPivot = ImportedModels.Find(model, "weapon_pivot"); shotMuzzle = ImportedModels.Find(model, "EnemyMuzzle");
            bodyRenderers = model.GetComponentsInChildren<Renderer>();
            torso = ImportedModels.Find(model, "torso"); if (torso) torsoRest = torso.localRotation;
            impactBlock = new MaterialPropertyBlock();
            fireAt = Time.time + 1.7f + Random.value; grenadeAt = Time.time + 5 + Random.value * 2;
            transform.rotation = Quaternion.Euler(0, 180, 0); gaitPrevious=transform.position;
            if(kind==EnemyKind.Assault||kind==EnemyKind.Shield){tactical=gameObject.AddComponent<EnemyTacticalAI>();tactical.Initialize(this);}
            if(kind==EnemyKind.Shield)InitializeShieldMeleePresentation();
        }
        void Update()
        {
            if (dying) { AnimateDeath(); return; }
            if (!Alive || !G.IsPlaying) return;
            float dt = Time.deltaTime; age += dt; silenceLeft=Mathf.Max(0,silenceLeft-dt); stunLeft = Mathf.Max(0, stunLeft - dt); flashLeft = Mathf.Max(0, flashLeft - dt);
            if (agent.enabled && agent.isOnNavMesh) agent.isStopped = stunLeft > 0 || suppressAI || G.qaSuppressAI || kind == EnemyKind.Training;
            Vector3 displacement=Vector3.ProjectOnPlane(transform.position-gaitPrevious,Vector3.up);
            gaitPrevious=transform.position;float strideDistance=displacement.magnitude;
            // Locomotion phase follows actual distance; teleport/warp is not a stride.
            float actualSpeed=strideDistance<1&&dt>.0001f?strideDistance/dt:0;
            if(strideDistance<1)gaitPhase+=strideDistance*(Mathf.PI*2/.95f);
            float targetGait=Mathf.Clamp01(actualSpeed/3.4f);
            gaitWeight=Mathf.MoveTowards(gaitWeight,targetGait,dt*(targetGait>gaitWeight?4:7));
            float heavy=kind==EnemyKind.Shield||kind==EnemyKind.Elite?.76f:1;
            float swing=Mathf.Sin(gaitPhase)*24*gaitWeight*heavy;
            leftLeg.localRotation=Quaternion.Slerp(leftLeg.localRotation,Quaternion.Euler(swing,0,0),1-Mathf.Exp(-18*dt));
            rightLeg.localRotation=Quaternion.Slerp(rightLeg.localRotation,Quaternion.Euler(-swing,0,0),1-Mathf.Exp(-18*dt));
            model.localPosition=Vector3.Lerp(model.localPosition,Vector3.up*(Mathf.Abs(Mathf.Sin(gaitPhase))*.028f*gaitWeight),1-Mathf.Exp(-15*dt));
            Vector3 localMotion=transform.InverseTransformDirection(displacement/Mathf.Max(.0001f,dt));
            float lean=Mathf.Clamp(-localMotion.x*2.3f,-7,7)*gaitWeight;
            model.localRotation=Quaternion.Slerp(model.localRotation,Quaternion.Euler(2.2f*gaitWeight,0,lean),1-Mathf.Exp(-7*dt));
            UpdateHitVisual();
            if (stunLeft > 0 || suppressAI || G.qaSuppressAI || kind == EnemyKind.Training) { ClearLaser(); return; }
            if(boss){boss.Tick(dt);return;}
            Vector3 playerAim = G.player.transform.position + Vector3.up * 1.1f;
            if (weaponPivot)
            {
                Vector3 localAim = transform.InverseTransformDirection(playerAim - AimPoint);
                weaponPivot.localRotation = Quaternion.Euler(-Mathf.Atan2(localAim.y, Mathf.Max(.1f, new Vector2(localAim.x, localAim.z).magnitude)) * Mathf.Rad2Deg, 0, 0);
            }
            Vector3 toPlayer = playerAim - AimPoint;
            Vector3 flat = Vector3.ProjectOnPlane(toPlayer, Vector3.up);
            if (flat.sqrMagnitude > .01f) FaceSafely(Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(flat), dt * (kind == EnemyKind.Shield || kind == EnemyKind.Elite ? 70 : 145)));
            if(tactical){tactical.Tick(dt);return;}
            bool visible = !Physics.Linecast(AimPoint, playerAim, Layers.WorldMask);
            if (visible) { lastKnownPlayer = G.player.transform.position; lastSawPlayer = Time.time; }
            float distance = toPlayer.magnitude;
            if(agent.enabled)agent.stoppingDistance = visible ? (kind == EnemyKind.Shield || kind == EnemyKind.Elite ? 3 : 9) : 1;
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
            if (!Silenced && kind == EnemyKind.Assault && (G.stage >= 2 || G.expeditionActive && G.expeditionLevel>=3) && distance > 5 && distance < 20 && Time.time > grenadeAt && Time.time - lastSawPlayer < 6)
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
        public void ShootAt(Vector3 point,float damage,float speed){Shoot(point,damage,speed);}
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
            LastHitRegion=RegionOf(collider);
            if (IsShieldCollider(collider))
            {
                Shapes.Impact(point, (point - source).normalized * -1, true); return false;
            }
            hitDirection = transform.InverseTransformDirection((point - source).normalized);
            float regionMultiplier=LastHitRegion==EnemyHitRegion.Head?1.5f:LastHitRegion==EnemyHitRegion.Limb?.8f:1;
            Damage(damage*regionMultiplier*(Marked?1.2f:1)); return true;
        }
        void BuildHurtboxes(float scale)
        {
            // Triggers are for precise weapon queries only. They never alter the actor capsule,
            // physical navigation, ground detection, or the swap landing-volume transaction.
            Part("Head hurtbox",EnemyHitRegion.Head,new Vector3(0,1.59f,0),new Vector3(.34f,.36f,.34f),scale,true);
            Part("Torso hurtbox",EnemyHitRegion.Torso,new Vector3(0,1.05f,0),new Vector3(.57f,.70f,.40f),scale);
            Part("Left arm hurtbox",EnemyHitRegion.Limb,new Vector3(-.38f,1.08f,.03f),new Vector3(.21f,.64f,.26f),scale);
            Part("Right arm hurtbox",EnemyHitRegion.Limb,new Vector3(.38f,1.08f,.03f),new Vector3(.21f,.64f,.26f),scale);
            Part("Left leg hurtbox",EnemyHitRegion.Limb,new Vector3(-.15f,.37f,0),new Vector3(.23f,.68f,.27f),scale);
            Part("Right leg hurtbox",EnemyHitRegion.Limb,new Vector3(.15f,.37f,0),new Vector3(.23f,.68f,.27f),scale);
        }
        void Part(string label,EnemyHitRegion region,Vector3 position,Vector3 size,float scale,bool sphere=false)
        {
            var go=new GameObject(label);go.layer=Layers.Actor;go.transform.SetParent(transform,false);go.transform.localPosition=position*scale;
            Collider collider;
            if(sphere){var c=go.AddComponent<SphereCollider>();c.radius=size.y*.5f*scale;collider=c;}
            else{var c=go.AddComponent<BoxCollider>();c.size=size*scale;collider=c;}
            collider.isTrigger=true;hurtboxes.Add(collider,region);go.AddComponent<EnemyHitPart>().owner=this;
        }
        public static bool CastWeaponRay(Vector3 origin,Vector3 direction,float distance,Transform ignore,out RaycastHit hit)
        {
            bool found=CombatRay.Cast(origin,direction,distance,ignore,out hit);
            float nearest=found?hit.distance:distance;
            // Enables hits on arms just outside the locomotion capsule without changing physics.
            foreach(var h in Physics.RaycastAll(origin,direction,distance,1<<Layers.Actor,QueryTriggerInteraction.Collide))
            {
                var part=h.collider.GetComponent<EnemyHitPart>();
                if(!part||!part.owner||!part.owner.Alive||h.distance>=nearest)continue;
                if(ignore&&(h.collider.transform==ignore||h.collider.transform.IsChildOf(ignore)))continue;
                hit=h;nearest=h.distance;found=true;
            }
            return found;
        }
        public Collider ResolveHitCollider(Ray ray,Collider fallback,ref Vector3 point)
        {
            if(IsShieldCollider(fallback)||hurtboxes.ContainsKey(fallback))return fallback;
            float nearest=90;Collider selected=null;
            if(Physics.Raycast(ray,out var wall,nearest,Layers.WorldMask,QueryTriggerInteraction.Ignore))nearest=wall.distance;
            foreach(var pair in hurtboxes)
                if(pair.Key&&pair.Key.enabled&&pair.Key.Raycast(ray,out var h,nearest)){nearest=h.distance;point=h.point;selected=pair.Key;}
            return selected?selected:fallback;
        }
        public void Damage(float damage)
        {
            if (!Alive) return;
            if(boss)damage=boss.Absorb(damage);
            health = Mathf.Max(0, health - damage); flashLeft = .12f;
            if(boss)boss.RefreshPhase();
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
            if(!motion||!motion.Relocate(feet))return false;
            gaitPrevious=transform.position;Physics.SyncTransforms();return true;
        }
        public void FaceSafely(Quaternion rotation)
        {
            if(HasPhysicalShield)
            {
                Quaternion delta=rotation*Quaternion.Inverse(transform.rotation);
                Vector3 center=transform.position+delta*(shieldCollider.transform.TransformPoint(shieldCollider.center)-transform.position);
                Vector3 half=Vector3.Scale(shieldCollider.size,shieldCollider.transform.lossyScale)*.5f;
                if(Physics.CheckBox(center,half,delta*shieldCollider.transform.rotation,Layers.WorldMask,QueryTriggerInteraction.Ignore))return;
            }
            transform.rotation=rotation;
        }
        public Vector3 ConstrainShieldMotion(Vector3 delta)
        {
            if(!HasPhysicalShield||delta.sqrMagnitude<.000001f)return delta;
            Vector3 half=Vector3.Scale(shieldCollider.size,shieldCollider.transform.lossyScale)*.5f;
            Vector3 center=shieldCollider.transform.TransformPoint(shieldCollider.center);
            if(Physics.BoxCast(center,half,delta.normalized,out var hit,shieldCollider.transform.rotation,delta.magnitude+.02f,Layers.WorldMask,QueryTriggerInteraction.Ignore))
            {
                Vector3 first=delta.normalized*Mathf.Min(delta.magnitude,Mathf.Max(0,hit.distance-.025f));
                Vector3 slide=Vector3.ProjectOnPlane(delta-first,hit.normal);
                if(slide.sqrMagnitude<.000001f)return first;
                if(Physics.BoxCast(center+first,half,slide.normalized,out var sideHit,shieldCollider.transform.rotation,slide.magnitude+.025f,Layers.WorldMask,QueryTriggerInteraction.Ignore))
                    slide=slide.normalized*Mathf.Min(slide.magnitude,Mathf.Max(0,sideHit.distance-.025f));
                return first+slide;
            }
            return delta;
        }
        public override void AfterSwap()
        {
            stunLeft = G.config.swapStun; warningLeft = 0; ClearLaser();if(tactical)tactical.Invalidate();
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

