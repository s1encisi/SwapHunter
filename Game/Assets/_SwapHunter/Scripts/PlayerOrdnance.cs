using UnityEngine;
namespace SwapHunter
{
    // A bounded, swept ballistic projectile. Damage cannot cross world cover.
    public sealed class PlayerOrdnance : MonoBehaviour
    {
        PlayerMotor owner; Vector3 velocity; float damage, fuse=2.4f; bool exploded;
        public static PlayerOrdnance Launch(PlayerMotor player,Vector3 origin,Vector3 direction,float power)
        {
            var go=Shapes.Make("Player ballistic grenade",PrimitiveType.Sphere,DemoGame.I.effects,origin,Vector3.one*.12f,6);
            var shot=go.AddComponent<PlayerOrdnance>();shot.owner=player;shot.velocity=direction.normalized*19+Vector3.up*2.2f;shot.damage=power;
            return shot;
        }
        void Update()
        {
            var g=DemoGame.I;if(!g||!g.IsPlaying)return;
            float dt=Time.deltaTime;velocity+=Vector3.down*12*dt;Vector3 travel=velocity*dt;
            if(CombatRay.Cast(transform.position,travel.normalized,travel.magnitude,owner?owner.transform:null,out var hit))
            {transform.position=hit.point+hit.normal*.035f;Explode();return;}
            transform.position+=travel;fuse-=dt;if(fuse<=0)Explode();
        }
        public void Explode()
        {
            if(exploded)return;exploded=true;var g=DemoGame.I;if(!g){Destroy(gameObject);return;}
            Vector3 point=transform.position;const float radius=3.1f;bool any=false,killed=false;
            foreach(var enemy in g.enemies.ToArray())
            {
                if(!enemy||!enemy.Alive)continue;float distance=Vector3.Distance(point,enemy.AimPoint);
                if(distance>=radius||Physics.Linecast(point,enemy.AimPoint,Layers.WorldMask))continue;
                bool alive=enemy.Alive;enemy.Damage(damage*Mathf.Lerp(1,.2f,distance/radius));any=true;killed|=alive&&!enemy.Alive;
            }
            if(owner&&Vector3.Distance(point,owner.Eye.position)<radius&&!Physics.Linecast(point,owner.Eye.position,Layers.WorldMask))
                owner.Hurt(damage*.65f*Mathf.Clamp01(1-Vector3.Distance(point,owner.Eye.position)/radius),point);
            if(owner&&any){owner.ReportWeaponHit(killed,"爆炸");g.totalHits++;}
            Shapes.Burst(point,new Color(1,.55f,.12f),1.4f);g.sound.Play("explosion",point);g.Record("player_grenade",point.ToString());
            Destroy(gameObject);
        }
    }
}
