using UnityEngine;
namespace SwapHunter
{
    // A bounded, swept ballistic projectile. Damage cannot cross world cover.
    public sealed class PlayerOrdnance : MonoBehaviour
    {
        PlayerMotor owner; Vector3 velocity,launchPoint; float damage,fuse,gravity,maximumRange; bool exploded;
        public const float BlastRadius = 3.1f;
        public bool Live => !exploded && isActiveAndEnabled;
        public float FuseLeft => fuse;
        public float ConfiguredDamage=>damage;
        public float MaximumRange=>maximumRange;
        public static PlayerOrdnance Launch(PlayerMotor player,Vector3 origin,Vector3 direction,float power)
        {
            var go=Shapes.Make("Player ballistic grenade",PrimitiveType.Sphere,DemoGame.I.effects,origin,Vector3.one*.12f,6);
            var definition=WeaponCatalog.Get(9);
            var shot=go.AddComponent<PlayerOrdnance>();shot.owner=player;shot.velocity=direction.normalized*definition.projectileSpeed+Vector3.up*definition.projectileLift;shot.damage=power;shot.launchPoint=origin;shot.gravity=definition.projectileGravity;shot.fuse=definition.projectileFuse;shot.maximumRange=definition.range;
            return shot;
        }
        void Update()
        {
            var g=DemoGame.I;if(!g||!g.IsPlaying)return;
            float dt=Time.deltaTime;velocity+=Vector3.down*gravity*dt;Vector3 travel=velocity*dt;
            bool rangeEnd=(transform.position+travel-launchPoint).sqrMagnitude>maximumRange*maximumRange;
            if(rangeEnd&&travel.sqrMagnitude>.000001f)
            {
                Vector3 offset=transform.position-launchPoint,axis=travel.normalized;float b=Vector3.Dot(offset,axis),c=offset.sqrMagnitude-maximumRange*maximumRange;
                float remaining=-b+Mathf.Sqrt(Mathf.Max(0,b*b-c));travel=axis*Mathf.Clamp(remaining,0,travel.magnitude);
            }
            if(CombatRay.Cast(transform.position,travel.normalized,travel.magnitude,owner?owner.transform:null,out var hit))
            {transform.position=hit.point+hit.normal*.035f;Explode();return;}
            transform.position+=travel;fuse-=dt;if(fuse<=0||rangeEnd)Explode();
        }
        public void Explode()
        {
            if(exploded)return;exploded=true;var g=DemoGame.I;if(!g){Destroy(gameObject);return;}
            Vector3 point=transform.position;const float radius=BlastRadius;bool any=false,killed=false;
            var batch=new DamageBatch(DamageSource.PlayerGrenade,owner?owner.transform:null,point);long damageEvent=batch.NewEvent();
            foreach(var enemy in g.enemies.ToArray())
            {
                if(!enemy||!enemy.Alive)continue;float distance=Vector3.Distance(point,enemy.AimPoint);
                if(distance>=radius||Physics.Linecast(point,enemy.AimPoint,Layers.WorldMask))continue;
                var sample=batch.Enemy(enemy,damage*Mathf.Lerp(1,.2f,distance/radius)*DirectionalBarrier.ExplosionMultiplier(point,enemy.AimPoint),damageEvent);
                if(sample!=null)sample.after=result=>{any|=result.applied;killed|=result.killed;};
            }
            foreach(var objective in DamageableObjective.ActiveSnapshot())
            {
                if(!objective||!objective.Alive)continue;float distance=Vector3.Distance(point,objective.AimPoint);
                if(distance>=radius||Physics.Linecast(point,objective.AimPoint,Layers.WorldMask))continue;
                var sample=batch.Objective(objective,damage*Mathf.Lerp(1,.2f,distance/radius),damageEvent);
                if(sample!=null)sample.after=result=>any|=result.applied;
            }
            foreach(var device in RelayDevice.ActiveSnapshot())
            {
                if(!device||!device.Alive)continue;float distance=Vector3.Distance(point,device.AimPoint);
                if(distance>=radius||Physics.Linecast(point,device.AimPoint,Layers.WorldMask))continue;
                var sample=batch.Device(device,damage*Mathf.Lerp(1,.2f,distance/radius),damageEvent);
                if(sample!=null)sample.after=result=>any|=result.applied;
            }
            batch.Apply();
            if(owner&&Vector3.Distance(point,owner.Eye.position)<radius&&!Physics.Linecast(point,owner.Eye.position,Layers.WorldMask))
                owner.Hurt((damage/3)*DirectionalBarrier.ExplosionMultiplier(point,owner.Eye.position)*.65f*Mathf.Clamp01(1-Vector3.Distance(point,owner.Eye.position)/radius),point,false);
            if(owner&&any){owner.ReportWeaponHit(killed,"爆炸");g.totalHits++;}
            Shapes.Burst(point,new Color(1,.55f,.12f),1.4f);g.sound.Play("explosion",point);g.Record("player_grenade",point.ToString());
            Destroy(gameObject);
        }
    }
}
