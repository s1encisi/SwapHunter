using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace SwapHunter
{
    public sealed class RelayDevice : MonoBehaviour
    {
        static readonly List<RelayDevice> active = new List<RelayDevice>();
        public const float MaximumHealth=48;
        public float Health { get; private set; } = MaximumHealth;
        // Carrier death is intentionally NOT part of Alive: later events in the same damage batch still hit this receiver.
        public bool Alive => Health>0&&!DeathResolved&&isActiveAndEnabled;
        public EnemyActor Carrier { get; private set; }
        public Vector3 AimPoint => transform.position;
        public bool DeathResolved { get; private set; }
        public RelayRecoveryPickup Recovery { get; private set; }
        public bool Recovered => Recovery&&Recovery.Recovered;
        Transform sceneRoot;Vector3 fallbackFeet,lastLegalFeet;bool hasLegal;
        Renderer shell;Collider hurtbox;
        void Awake(){active.Add(this);}
        void OnDestroy(){active.Remove(this);}
        public static RelayDevice[] ActiveSnapshot()=>active.ToArray();
        public static RelayDevice Attach(EnemyActor carrier,Transform sceneRoot,Vector3 fallbackFeet)
        {
            if(!carrier||!sceneRoot)throw new System.ArgumentNullException();
            var go=new GameObject("R / 可用火控设备 48HP");go.layer=Layers.Actor;go.transform.SetParent(carrier.transform,false);go.transform.localPosition=new Vector3(0,1.4f,-.4f);
            var d=go.AddComponent<RelayDevice>();d.Carrier=carrier;d.sceneRoot=sceneRoot;d.fallbackFeet=fallbackFeet;
            var box=go.AddComponent<BoxCollider>();box.size=new Vector3(.54f,.58f,.3f);box.isTrigger=true;d.hurtbox=box;
            d.shell=Shapes.Box("Relay casing",go.transform,Vector3.zero,new Vector3(.54f,.58f,.3f),3).GetComponent<Renderer>();
            Shapes.Box("Relay signal lamp",go.transform,new Vector3(0,.08f,-.16f),new Vector3(.38f,.1f,.045f),4);
            d.TrackLegalPosition();return d;
        }
        void Update(){if(Carrier&&Carrier.Alive&&DemoGame.I&&DemoGame.I.IsPlaying)TrackLegalPosition();}
        void TrackLegalPosition()
        {
            if(Carrier.motion&&Carrier.motion.Airborne)return;
            if(NavMesh.SamplePosition(Carrier.transform.position,out var hit,.7f,NavMesh.AllAreas))
            {lastLegalFeet=hit.position;hasLegal=true;}
        }
        public bool ApplyDamage(float amount,Vector3 source)
        {
            if(!Alive||amount<=0||float.IsNaN(amount)||float.IsInfinity(amount))return false;
            float before=Health;Health=Mathf.Max(0,Health-amount);
            var g=DemoGame.I;if(g){g.Record("relay_device_damage","effective="+(before-Health)+";remaining="+Health);g.sound.Play("impact",AimPoint);}
            if(Health<=0)
            {
                if(hurtbox)hurtbox.enabled=false;if(shell)shell.sharedMaterial=Shapes.Mat(1);
                if(g){g.Record("relay_device_destroyed","optional_recovery_lost");g.Toast("火控设备已毁坏 · 中继终止，可选回收失去资格",3);}
                FireControlRelay.RevalidateAll();
            }
            return true;
        }
        public void ResolveCarrierDeath()
        {
            if(DeathResolved||Carrier&&Carrier.Alive)return;
            DeathResolved=true;if(hurtbox)hurtbox.enabled=false;
            if(Health<=0||!sceneRoot)return;
            Vector3 candidate=hasLegal?lastLegalFeet:fallbackFeet;
            if(!NavMesh.SamplePosition(candidate,out var nav,1.5f,NavMesh.AllAreas)&&!NavMesh.SamplePosition(fallbackFeet,out nav,2,NavMesh.AllAreas))
            {
                if(DemoGame.I)DemoGame.I.Record("relay_recovery_invalid_fallback",fallbackFeet.ToString());return;
            }
            var go=new GameObject("可用火控设备 / 长按{key:8}回收");go.transform.SetParent(sceneRoot,false);go.transform.position=nav.position;
            Shapes.Box("Recovered equipment",go.transform,new Vector3(0,.28f,0),new Vector3(.65f,.45f,.45f),5);
            Shapes.Ring(go.transform,nav.position+Vector3.up*.04f,.65f,new Color(1,.7f,.2f),.045f);
            Recovery=go.AddComponent<RelayRecoveryPickup>();Recovery.Configure(Health);
            D2WorldText.Create(go.transform,"可用设备 · 按住 {key:8} 2秒回收\n成功撤离才结算 +40",nav.position+Vector3.up*1.3f,.3f);
            D2InteractionDriver.Ensure(sceneRoot);
            if(DemoGame.I)DemoGame.I.Record("relay_recovery_spawned","hp="+Health+";feet="+nav.position);
        }
    }
    public sealed class RelayRecoveryPickup : D2HoldInteraction
    {
        public bool Recovered { get; private set; }
        public float DeviceHealth { get; private set; }
        public void Configure(float health){DeviceHealth=health;}
        public override Vector3 Point => transform.position+Vector3.up*.5f;
        public override bool Available => !Recovered;
        public override string Prompt => "回收可用设备（"+Mathf.CeilToInt(DeviceHealth)+"/48） · 成功撤离 +40";
        public override float Duration=>2;
        public override bool Commit()
        {
            var g=DemoGame.I;if(Recovered||!g||!g.IsPlaying||!g.expeditionActive)return false;
            Recovered=true;g.expeditionBonus=true;g.Record("relay_recovery_collected","optional_only;no_immediate_currency");g.Toast("设备已回收 · 成功撤离后结算可选奖励 +40",3);g.sound.Play("confirm");gameObject.SetActive(false);return true;
        }
    }
}
