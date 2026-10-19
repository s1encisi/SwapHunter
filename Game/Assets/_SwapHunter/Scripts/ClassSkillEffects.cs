using UnityEngine;
using System.Collections.Generic;

namespace SwapHunter
{
    // Fixed areas use feet height plus a world ray, so floors are not conflated.
    public static class ClassSkillArea
    {
        public static bool Contains(Vector3 center, Vector3 feet, Vector3 aim)
        {
            return Mathf.Abs(feet.y - center.y) <= ClassSkillTuning.FloorTolerance
                && Vector3.ProjectOnPlane(feet - center, Vector3.up).sqrMagnitude <= ClassSkillTuning.AreaRadius * ClassSkillTuning.AreaRadius
                && !Physics.Linecast(center + Vector3.up * .2f, aim, Layers.WorldMask, QueryTriggerInteraction.Ignore);
        }
        public static void Blast(PlayerMotor owner, Vector3 center, float power)
        {
            var game = DemoGame.I; if (!game || !game.IsPlaying) return;
            var batch=new DamageBatch(DamageSource.RemoteCharge,owner?owner.transform:null,center);long damageEvent=batch.NewEvent();
            foreach (var enemy in game.enemies.ToArray())
            {
                if(!enemy||!enemy.Alive)continue;
                float factor=BlastFactor(center,enemy.AimPoint);
                if(factor>0)batch.Enemy(enemy,power*factor,damageEvent);
            }
            foreach (var objective in DamageableObjective.ActiveSnapshot())
            {
                if(!objective||!objective.Alive)continue;
                float factor=BlastFactor(center,objective.AimPoint);
                if(factor>0)batch.Objective(objective,power*factor,damageEvent);
            }
            foreach(var device in RelayDevice.ActiveSnapshot())
            {
                if(!device||!device.Alive)continue;
                float factor=BlastFactor(center,device.AimPoint);
                if(factor>0)batch.Device(device,power*factor,damageEvent);
            }
            batch.Apply();
            if (owner && owner.health > 0)
            {
                float factor = BlastFactor(center, owner.transform.position + Vector3.up * .9f);
                if (factor > 0) owner.Hurt(power * factor * ClassSkillTuning.RemoteSelfScale, center);
            }
        }
        static float BlastFactor(Vector3 center, Vector3 aim)
        {
            float distance = Vector3.Distance(center, aim);
            if (distance >= ClassSkillTuning.AreaRadius || Physics.Linecast(center, aim, Layers.WorldMask, QueryTriggerInteraction.Ignore)) return 0;
            return Mathf.Lerp(1, ClassSkillTuning.RemoteEdgeScale, distance / ClassSkillTuning.AreaRadius);
        }
    }

    // Render-only device: its swept flight uses the existing combat ray, never SwapTarget.
    public sealed class RemoteClassCharge : MonoBehaviour
    {
        PlayerClassSkill skill; PlayerMotor owner;
        Vector3 velocity; float age, power; bool settled, ended;
        LineRenderer ring;
        public bool Armed => !ended && age >= ClassSkillTuning.RemoteArmTime;
        public bool Live => !ended && isActiveAndEnabled;
        public float LifetimeLeft => Mathf.Max(0, ClassSkillTuning.RemoteLifetime - age);
        public static RemoteClassCharge Create(PlayerClassSkill skill, PlayerMotor owner, float power)
        {
            D2HoldInteraction.CancelForAction();
            var game = DemoGame.I;
            var go = Shapes.Make("Remote class charge", PrimitiveType.Sphere, game.effects, owner.Eye.position, Vector3.one * .18f, 6, false, Layers.Effects);
            var charge = go.AddComponent<RemoteClassCharge>(); charge.skill = skill; charge.owner = owner; charge.power = power;
            charge.velocity = owner.Eye.forward * ClassSkillTuning.RemoteSpeed + Vector3.up * ClassSkillTuning.RemoteLift;
            charge.ring = Shapes.Ring(go.transform, go.transform.position, .22f, new Color(1,.65f,.2f), .025f);
            return charge;
        }
        void Update()
        {
            var game = DemoGame.I; if (!game || !game.IsPlaying || ended) return;
            if (!owner || owner.health <= 0) { Cancel(); return; }
            float dt = Time.deltaTime; age += dt;
            if (age >= ClassSkillTuning.RemoteLifetime) { End(false); return; }
            if (!settled)
            {
                velocity += Vector3.down * ClassSkillTuning.RemoteGravity * dt;
                Vector3 travel = velocity * dt;
                if (CombatRay.Cast(transform.position, travel.normalized, travel.magnitude, owner.transform, out var hit))
                { transform.position = hit.point + hit.normal * .06f; settled = true; velocity = Vector3.zero; }
                else transform.position += travel;
            }
            if (ring)
            {
                float radius = Armed ? .3f : .2f;
                for (int i = 0; i < ring.positionCount; i++)
                { float angle = i * Mathf.PI * 2 / (ring.positionCount - 1); ring.SetPosition(i, transform.position + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * radius); }
            }
        }
        public bool TryDetonate()
        {
            var game = DemoGame.I;
            if (!Armed || !game || !game.IsPlaying) return false;
            // Consume first: self damage may end the run and clear this same device.
            ended = true; D2HoldInteraction.CancelForAction(); Vector3 center = transform.position;
            if (owner && owner.classSkill) owner.classSkill.EndForAttack("detonate");
            if (skill) skill.RemoteEnded(this, true);
            Shapes.Burst(center, new Color(1,.45f,.15f), ClassSkillTuning.AreaRadius);
            game.sound.Play("explosion", center);
            ClassSkillArea.Blast(owner, center, power);
            Destroy(gameObject); return true;
        }
        void End(bool detonated)
        {
            if (ended) return; ended = true;
            if (skill) skill.RemoteEnded(this, detonated);
            Destroy(gameObject);
        }
        public void Cancel() { if (ended) return; ended = true; Destroy(gameObject); }
        void OnDestroy() { if (!ended) { ended = true; if (skill) skill.RemoteEnded(this, false); } }
    }

    public sealed class ClassDenialField : MonoBehaviour
    {
        PlayerMotor owner; float power, age, untilTick; bool ended;
        static readonly List<ClassDenialField> active=new List<ClassDenialField>();
        public static IReadOnlyList<ClassDenialField> Active=>active;
        public float DamagePerSecond=>power;
        void OnEnable(){if(!active.Contains(this))active.Add(this);}
        void OnDisable(){active.Remove(this);}

        public float SecondsLeft => Mathf.Max(0, ClassSkillTuning.DenialDuration - age);
        public bool Live => !ended && isActiveAndEnabled;
        public int TicksApplied { get; private set; }
        public static ClassDenialField Create(PlayerMotor owner, Vector3 ground, float dps)
        {
            D2HoldInteraction.CancelForAction();
            var go = new GameObject("Class area denial"); go.transform.SetParent(DemoGame.I.effects, false); go.transform.position = ground;
            var field = go.AddComponent<ClassDenialField>(); field.owner = owner; field.power = dps; field.untilTick = ClassSkillTuning.DenialTick;
            Shapes.Ring(go.transform, ground + Vector3.up * .045f, ClassSkillTuning.AreaRadius, new Color(1,.32f,.12f), .08f);
            Shapes.Ring(go.transform, ground + Vector3.up * .06f, .3f, new Color(1,.6f,.2f), .045f);
            return field;
        }
        void Update()
        {
            var game = DemoGame.I; if (!game || !game.IsPlaying || ended) return;
            if (!owner || owner.health <= 0) { Cancel(); return; }
            float dt = Mathf.Min(Time.deltaTime, SecondsLeft); age += dt; untilTick -= dt;
            while (untilTick <= .00001f && !ended && game.IsPlaying)
            {
                untilTick += ClassSkillTuning.DenialTick; ApplyTick();
            }
            if (age >= ClassSkillTuning.DenialDuration) Cancel();
        }
        void ApplyTick()
        {
            var game = DemoGame.I; Vector3 center = transform.position; float damage = power * ClassSkillTuning.DenialTick; TicksApplied++;
            var batch=new DamageBatch(DamageSource.Denial,owner?owner.transform:null,center);long damageEvent=batch.NewEvent();
            foreach(var enemy in game.enemies.ToArray())
            {
                if(!enemy||!enemy.Alive||!ClassSkillArea.Contains(center,enemy.transform.position,enemy.AimPoint))continue;
                var target=enemy;var sample=batch.Enemy(target,damage,damageEvent);
                if(sample!=null)sample.after=result=>{if(result.healthLoss+result.energyLoss>0&&target)target.GetComponent<EnemyHazardAwareness>()?.NotifyHurt(this);};
            }
            foreach(var objective in DamageableObjective.ActiveSnapshot())
                if(objective&&objective.Alive&&ClassSkillArea.Contains(center,objective.transform.position,objective.AimPoint))batch.Objective(objective,damage,damageEvent);
            foreach(var device in RelayDevice.ActiveSnapshot())
                if(device&&device.Alive&&ClassSkillArea.Contains(center,device.Carrier?device.Carrier.transform.position:device.transform.position,device.AimPoint))batch.Device(device,damage,damageEvent);
            batch.Apply();
            // The same occupancy/LOS rule applies to the caster. It is an ordinary self-damage source.
            if (owner && owner.health > 0 && ClassSkillArea.Contains(center, owner.transform.position, owner.transform.position + Vector3.up * .9f)) owner.Hurt(damage, center + Vector3.up * .2f);
        }
        public void Cancel() { if (ended) return; ended = true; Destroy(gameObject); }
    }
}