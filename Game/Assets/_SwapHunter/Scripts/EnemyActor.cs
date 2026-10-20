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
        public ActorVisualRig VisualRig {get;private set;}
        public BossEncounter boss;
        public MiniBossController miniBoss;
        public EnemyV2Ability ability;
        public bool IsBoss => boss || miniBoss;
        public bool KnifeExecutable => !IsBoss && kind != EnemyKind.Elite;
        public int ProjectileHits {get;private set;}
        public int MuzzleBlockedShots {get;private set;}
        public float FirstShotTime {get;private set;}=-1;
        public Vector3 MuzzlePosition=>shotMuzzle?shotMuzzle.position:AimPoint+transform.forward*.85f;
        public void ReportProjectileHit(bool playerHit){if(playerHit)ProjectileHits++;}
        public EnemyTacticalAI tactical;
        public EncounterGrenadeCue grenadeCue;
        public float health, maximumHealth, stunLeft, warningLeft, silenceLeft;
        public bool Silenced=>silenceLeft>0;
        public void ApplySilence(float seconds)
        {
            if(!Alive||seconds<=0)return;
            if(boss&&!boss.TryReceiveSilence(seconds))return;
            if(miniBoss){seconds=Mathf.Min(1.5f,seconds);if(!miniBoss.TryReceiveSilence(seconds))return;}
            if(ability)ability.Interrupt("silence");
            if(silenceLeft<=0)DemoGame.I.sound.Play("silence",AimPoint);
            silenceLeft=Mathf.Max(silenceLeft,seconds);DemoGame.I.Record("enemy_silenced",kind+":"+seconds);
        }
        public bool ApplyOrdinaryStagger(float seconds)
        {
            if(!Alive||IsBoss||seconds<=0)return false;
            stunLeft=Mathf.Max(stunLeft,seconds);DemoGame.I.Record("enemy_staggered",kind+":"+seconds);return true;
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
        string BaseLabel => miniBoss?miniBoss.DisplayName:EnemyV2Definition.Name(kind);
        Transform shotMuzzle;
        Renderer[] bodyRenderers;
        MaterialPropertyBlock impactBlock;
        bool wasFlashing;
        Collider body,headHurtbox,torsoHurtbox;
        public Collider HeadHitbox=>headHurtbox;
        public Collider TorsoHitbox=>torsoHurtbox;
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
        float fireAt, grenadeAt, nextPath, flashLeft, age; bool dying;
        float sniperRearmWork;
        public float SniperRearmWork => sniperRearmWork;
        internal void BeginSniperRearm(){sniperRearmWork=2.4f;}
        internal void AdvanceSniperRearm(float dt){sniperRearmWork=Mathf.Max(0,sniperRearmWork-Mathf.Max(0,dt)*FireControlRelay.RearmRate(this));}
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

            return enemy;
        }
        public BossEncounter EnableBoss()
        {
            if(boss)return boss;
            DisableShieldMeleePresentation();
            if(shieldCollider)shieldCollider.enabled=false;

            if(ability){Destroy(ability);ability=null;}
            boss=gameObject.AddComponent<BossEncounter>();boss.Initialize(this);return boss;
        }
        public MiniBossController EnableMiniBoss(int chapter)
        {
            if(miniBoss)return miniBoss;
            DisableShieldMeleePresentation();
            if(shieldCollider)shieldCollider.enabled=chapter==1;
            if(ability){Destroy(ability);ability=null;}
            miniBoss=gameObject.AddComponent<MiniBossController>();miniBoss.Initialize(this,chapter);return miniBoss;
        }
        void Initialize()
        {
            maximumHealth = EnemyV2Definition.Health(kind,G.config);
            health = maximumHealth; home = transform.position;
            CharacterController capsule = gameObject.AddComponent<CharacterController>(); capsule.radius = Radius; capsule.height = Height; capsule.center = Vector3.up * Height * .5f; capsule.skinWidth=.02f;capsule.stepOffset=.32f;capsule.slopeLimit=48;capsule.minMoveDistance=0; body = capsule;
            agent = gameObject.AddComponent<NavMeshAgent>(); agent.height = Height; agent.radius = Radius; agent.baseOffset = 0;
            agent.speed=kind==EnemyKind.Elite?2.05f:kind==EnemyKind.Shield?2.3f:kind==EnemyKind.Sniper?2.9f:3.4f;
            agent.acceleration=kind==EnemyKind.Elite?7:kind==EnemyKind.Shield?8:kind==EnemyKind.Sniper?11:12;
            agent.autoBraking=true;agent.angularSpeed=150;agent.updateRotation=false;
            agent.stoppingDistance = kind == EnemyKind.Shield || kind == EnemyKind.Elite ? 3 : 9;
            agent.obstacleAvoidanceType = ObstacleAvoidanceType.MedQualityObstacleAvoidance;
            motion=gameObject.AddComponent<EnemyMotion>();motion.Initialize(this,capsule);
            float modelScale = kind == EnemyKind.Elite ? 1.16f : 1;
            if (kind == EnemyKind.Shield)
            {
                var shield = new GameObject("Shield"); shield.layer = Layers.Actor; shield.transform.SetParent(transform, false);
                shield.transform.localPosition = new Vector3(-.12f, .99f, .57f) * modelScale;
                var box = shield.AddComponent<BoxCollider>(); shieldCollider = box; box.size = new Vector3(1, 1.42f, .16f) * modelScale;
                Physics.IgnoreCollision(capsule,box,true);
            }
            BuildHurtboxes(modelScale);
            VisualRig=ActorVisualRig.CreateEnemy(this,shieldCollider);RefreshVisualReferences();
            impactBlock = new MaterialPropertyBlock();
            fireAt = Time.time + 1.7f + Random.value; grenadeAt = Time.time + 5 + Random.value * 2;
            transform.rotation = Quaternion.Euler(0, 180, 0); gaitPrevious=transform.position;
            if(kind!=EnemyKind.Training){tactical=gameObject.AddComponent<EnemyTacticalAI>();tactical.Initialize(this);}
            if(kind==EnemyKind.Elite||(int)kind>=5){ability=gameObject.AddComponent<EnemyV2Ability>();ability.Initialize(this);}
            if(kind==EnemyKind.Shield)InitializeShieldMeleePresentation();
        }
        void Update()
        {
            if (dying) { AnimateDeath(); return; }
            if (!Alive || !G.IsPlaying) return;
            float dt = Time.deltaTime; if(kind==EnemyKind.Sniper)AdvanceSniperRearm(dt); age += dt; silenceLeft=Mathf.Max(0,silenceLeft-dt); stunLeft = Mathf.Max(0, stunLeft - dt); flashLeft = Mathf.Max(0, flashLeft - dt);
            if (agent.enabled && agent.isOnNavMesh) agent.isStopped = stunLeft > 0 || suppressAI || G.qaSuppressAI || kind == EnemyKind.Training || grenadeCue && grenadeCue.Preparing;
            Vector3 displacement=Vector3.ProjectOnPlane(transform.position-gaitPrevious,Vector3.up);
            gaitPrevious=transform.position;float strideDistance=displacement.magnitude;
            // Locomotion phase follows actual distance; teleport/warp is not a stride.
            float actualSpeed=strideDistance<1&&dt>.0001f?strideDistance/dt:0;
            if(strideDistance<1)gaitPhase+=strideDistance*(Mathf.PI*2/.95f);
            float targetGait=Mathf.Clamp01(actualSpeed/3.4f);
            gaitWeight=Mathf.MoveTowards(gaitWeight,targetGait,dt*(targetGait>gaitWeight?4:7));
            VisualRig.SetLocomotion(actualSpeed,gaitWeight,stunLeft>0);
            if(tactical)tactical.ObservePlayer();
            VisualRig.SetAim(tactical?tactical.ObservedPlayerAim:G.player.Eye.position);
            VisualRig.SetGrenadePreparing(grenadeCue&&grenadeCue.Preparing);
            UpdateHitVisual();
            if (suppressAI || G.qaSuppressAI || kind == EnemyKind.Training) { ClearLaser(); return; }
            if(Vector3.Distance(transform.position,G.player.transform.position)>80){if(agent.enabled&&agent.isOnNavMesh){agent.isStopped=true;agent.ResetPath();}ClearLaser();return;}
            // Ordinary stagger pauses basic actions; the Boss owns an independent spatial clock.
            if(boss){boss.Tick(dt);return;}
            if(miniBoss){miniBoss.Tick(dt);return;}
            if (stunLeft > 0) { ClearLaser(); return; }
            Vector3 playerAim = tactical?tactical.ObservedPlayerAim:G.player.transform.position + Vector3.up * 1.1f;
            Vector3 toPlayer = playerAim - AimPoint;
            Vector3 flat = Vector3.ProjectOnPlane(toPlayer, Vector3.up);
            if ((!ability||!ability.LockFacing)&&(!tactical||!tactical.LocksFacing&&tactical.HasPlayerMemory)&&flat.sqrMagnitude > .01f) FaceSafely(Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(flat), dt * (kind == EnemyKind.Shield || kind == EnemyKind.Elite ? 70 : 145)));
            if(grenadeCue && grenadeCue.Preparing) return;
            if(ability&&ability.Tick(dt))return;
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
                    if (NavMesh.SamplePosition(destination, out var nav, 2, NavMesh.AllAreas))
                    {
                        var territory=GetComponent<MissionEncounterAgent>();
                        if(!territory||territory.Allows(nav.position))agent.SetDestination(nav.position);
                        else agent.ResetPath();
                    }
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
                    if (warningLeft <= 0) { Shoot(shotLock, 30, 90); BeginSniperRearm(); ClearLaser(); }
                }
                else if (Time.time > fireAt && sniperRearmWork<=0) { warningLeft = 1.2f; shotLock = playerAim; G.sound.Play("warning", transform.position); }
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
            if (!Alive || !G.IsPlaying) return;
            if(VisualRig){VisualRig.SetAim(point);VisualRig.NotifyShot();}
            Vector3 origin = shotMuzzle ? shotMuzzle.position : AimPoint + transform.forward * .85f + transform.right * .24f;
            if(Physics.Linecast(AimPoint,origin,out var muzzleObstacle,Layers.WorldMask,QueryTriggerInteraction.Ignore))
            {MuzzleBlockedShots++;if(tactical)tactical.NotifyBlockedMuzzle();G.Record("enemy_muzzle_blocked",kind+":"+muzzleObstacle.collider.name);return;}
            if(CombatRay.Cast(origin,(point-origin).normalized,Vector3.Distance(origin,point),transform,out var obstruction))
            {var ally=obstruction.collider.GetComponentInParent<EnemyActor>();if(ally&&ally!=this){MuzzleBlockedShots++;if(tactical)tactical.NotifyBlockedMuzzle();return;}}
            shotsFired++;if(FirstShotTime<0)FirstShotTime=Time.time;
            Bolt.Spawn(this, origin, point - origin, speed, damage); G.sound.PlayWeapon(kind==EnemyKind.Sniper?7:kind==EnemyKind.Elite?8:0,"fire",origin);
            Shapes.Burst(origin, new Color(1, .64f, .25f), .12f);
        }
        void ClearLaser() { if (laser) Destroy(laser.gameObject); laser = null; }
        public bool Hit(float damage, Vector3 point, Collider collider, Vector3 source)
        {
            var contextSource=DamageSource.PlayerBullet;
            return DamageResolution.Single(this,damage,contextSource,G.player?G.player.transform:null,source,RegionOf(collider)).applied;
        }
        internal DamageResult ReceiveDamage(DamageContext context,float damage,EnemyHitRegion region)
        {
            var result=new DamageResult{receiverKind="enemy",region=region,markMultiplier=1};
            if(!Alive||damage<=0||float.IsNaN(damage)||float.IsInfinity(damage))return result;
            LastHitRegion=region;
            if(region==EnemyHitRegion.Shield)
            {
                result.blocked=true;DamageResolution.Record(context,GetInstanceID(),damage,result);return result;
            }
            bool bullet=context.source==DamageSource.PlayerBullet||context.source==DamageSource.EnemyBullet;
            float regionMultiplier=bullet?(region==EnemyHitRegion.Head?(IsBoss?1.5f:2f):region==EnemyHitRegion.Limb?.8f:1):1;
            float beforeMark=damage*regionMultiplier;
            result.markMultiplier=DamageResolution.MarkMultiplier(context.source,Marked);
            float hpBefore=health,energyBefore=boss?boss.Energy:0;
            ApplyResolvedDamage(beforeMark*result.markMultiplier);
            result.applied=true;result.killed=!Alive;
            result.healthLoss=Mathf.Max(0,hpBefore-health);result.energyLoss=Mathf.Max(0,energyBefore-(boss?boss.Energy:0));
            DamageResolution.Record(context,GetInstanceID(),beforeMark,result);return result;
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
            if(region==EnemyHitRegion.Head)headHurtbox=collider;else if(region==EnemyHitRegion.Torso)torsoHurtbox=collider;
        }
        public static bool CastWeaponRay(Vector3 origin,Vector3 direction,float distance,Transform ignore,out RaycastHit hit)
        {
            bool found=CombatRay.Cast(origin,direction,distance,ignore,out hit);
            float nearest=found?hit.distance:distance;
            // Enables hits on arms just outside the locomotion capsule without changing physics.
            foreach(var h in Physics.RaycastAll(origin,direction,distance,1<<Layers.Actor,QueryTriggerInteraction.Collide))
            {
                var part=h.collider.GetComponent<EnemyHitPart>();
                var device=h.collider.GetComponentInParent<RelayDevice>();
                if((!device||!device.Alive)&&(!part||!part.owner||!part.owner.Alive)||h.distance>=nearest)continue;
                if(ignore&&(h.collider.transform==ignore||h.collider.transform.IsChildOf(ignore)))continue;
                hit=h;nearest=h.distance;found=true;
            }
            return found;
        }
        public Collider ResolveHitCollider(Ray ray,Collider fallback,ref Vector3 point)
        {
            if(IsShieldCollider(fallback)||hurtboxes.ContainsKey(fallback))return fallback;
            // Resolve this actor's body parts at the actual hit distance. The old
            // 90 m constant incorrectly turned long-range head hits into torso hits.
            float nearest=Vector3.Distance(ray.origin,point)+Height;Collider selected=null;
            if(Physics.Raycast(ray,out var wall,nearest,Layers.WorldMask,QueryTriggerInteraction.Ignore))nearest=wall.distance;
            foreach(var pair in hurtboxes)
                if(pair.Key&&pair.Key.enabled&&pair.Key.Raycast(ray,out var h,nearest)){nearest=h.distance;point=h.point;selected=pair.Key;}
            return selected?selected:fallback;
        }
        public void Damage(float damage) => DamageResolution.Single(this,damage,DamageSource.Environment,null,transform.position);
        void ApplyResolvedDamage(float damage)
        {
            if (!Alive) return;
            float resourcesBefore=health+(boss?boss.Energy:0);
            if(boss)damage=boss.Absorb(damage);
            health = Mathf.Max(0, health - damage); flashLeft = LastHitRegion==EnemyHitRegion.Head?.20f:.10f;if(VisualRig)VisualRig.NotifyHit(LastHitRegion==EnemyHitRegion.Head);
            if(boss)boss.RefreshPhase();
            float effectiveDamage=Mathf.Max(0,resourcesBefore-health-(boss?boss.Energy:0));
            if(effectiveDamage>0&&ability)ability.NotifyHurt(effectiveDamage);
            if(effectiveDamage>0&&miniBoss)miniBoss.NotifyHurt(effectiveDamage);
            if(effectiveDamage>0&&tactical)tactical.NotifyHurt();
            if(effectiveDamage>0)G.Record("enemy_effective_damage",effectiveDamage.ToString("R",System.Globalization.CultureInfo.InvariantCulture));
            if (health > 0) return;
            if(ability)ability.Interrupt("death");if(miniBoss)miniBoss.OnKilled();
            ClearLaser(); flashLeft = 0; UpdateHitVisual(); agent.enabled = false;
            foreach (Collider collider in GetComponentsInChildren<Collider>()) collider.enabled = false;
            dying = true;if(VisualRig)VisualRig.BeginDeath();
            Shapes.Burst(AimPoint, new Color(1, .55f, .15f), .8f); G.sound.Play("death", AimPoint);
            gameObject.AddComponent<TimedEffect>().life = 2.5f;
            DamageResolution.CarrierDied(this);
            G.EnemyKilled(this); G.Record("enemy_killed", kind.ToString());
        }
        void RefreshVisualReferences()
        {
            shotMuzzle=VisualRig.Muzzle;bodyRenderers=VisualRig.GetComponentsInChildren<Renderer>(true);AlignVisualHitRegions(VisualRig);
        }
        internal void AlignVisualHitRegions(ActorVisualRig rig)
        {
            if(headHurtbox)headHurtbox.transform.position=rig.HeadAimMarker;
            if(torsoHurtbox)torsoHurtbox.transform.position=rig.TorsoAimMarker;
        }
        void AnimateDeath()
        {
            if(VisualRig&&G.IsPlaying)VisualRig.BeginDeath();
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
            var territory=GetComponent<MissionEncounterAgent>();
            if(territory)territory.RebaseAfterSwap();
            if(ability)ability.Interrupt("swap");if(miniBoss)miniBoss.AfterSwap();
            stunLeft = Mathf.Max(stunLeft,G.config.swapStun); warningLeft = 0; ClearLaser();if(tactical)tactical.Invalidate();
            fireAt = Mathf.Max(fireAt, Time.time + stunLeft + .25f);
            if (agent.enabled && agent.isOnNavMesh) { agent.ResetPath(); agent.isStopped = true; }
        }
        void OnDestroy() { ClearLaser(); }
        internal void DisablePhysicalShield(){if(shieldCollider)shieldCollider.enabled=false;}
        internal void AddMiniBossPhysicalShield()
        {
            if(shieldCollider){shieldCollider.enabled=true;return;}
            var go=new GameObject("执法官实体盾碰撞");go.layer=Layers.Actor;go.transform.SetParent(transform,false);go.transform.localPosition=new Vector3(-.12f,1.05f,.64f);
            shieldCollider=go.AddComponent<BoxCollider>();shieldCollider.size=new Vector3(1.15f,1.7f,.16f);
            foreach(var c in GetComponentsInChildren<Collider>())if(c!=shieldCollider)Physics.IgnoreCollision(c,shieldCollider,true);
        }
        void UpdateHitVisual()
        {
            bool flashing = flashLeft > 0;
            if (!flashing && !wasFlashing) return;
            foreach (var renderer in bodyRenderers)
            {
                if (!renderer) continue;
                Material[] materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    if (!flashing) { renderer.SetPropertyBlock(null, i); continue; }
                    Material material = materials[i]; string property = material.HasProperty("baseColorFactor") ? "baseColorFactor" : material.HasProperty("_BaseColorFactor") ? "_BaseColorFactor" : material.HasProperty("_BaseColor") ? "_BaseColor" : "_Color";
                    if (!material.HasProperty(property)) continue;
                    impactBlock.Clear(); impactBlock.SetColor(property, Color.Lerp(material.GetColor(property), Color.white, .32f * flashLeft / .12f)); renderer.SetPropertyBlock(impactBlock, i);
                }
            }
            wasFlashing = flashing;
        }
    }
}

