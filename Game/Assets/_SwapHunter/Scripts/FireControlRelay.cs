using System.Collections.Generic;
using UnityEngine;

namespace SwapHunter
{
    public enum RelayLinkState { Waiting, Disconnected, Connecting, Connected, Suppressed, Terminated }
    [DefaultExecutionOrder(-80)]
    public sealed class FireControlRelay : MonoBehaviour
    {
        public static bool FeatureEnabled = true;
        public const float AcquireDistance = 13, RetainDistance = 14, ConnectSeconds = .8f, CheckInterval = .1f;
        static readonly List<FireControlRelay> active = new List<FireControlRelay>();
        public EnemyActor Sender { get; private set; }
        public EnemyActor Receiver { get; private set; }
        public RelayDevice Device { get; private set; }
        public RelayLinkState State { get; private set; } = RelayLinkState.Waiting;
        public string Reason { get; private set; } = "等待两端部署";
        public float Charge => State == RelayLinkState.Connecting ? Mathf.Clamp01((Time.time-connectStarted)/ConnectSeconds) : State==RelayLinkState.Connected?1:0;
        public Vector3 ReceivePoint => Receiver ? Receiver.transform.position + Vector3.up*1.4f : Vector3.zero;
        public bool Connected { get { Revalidate(); return State == RelayLinkState.Connected; } }
        float nextCheck, connectStarted=-1;
        bool senderAssigned, receiverAssigned;
        LineRenderer link; TextMesh status;
        void Awake(){active.Add(this);}
        void OnDestroy(){active.Remove(this);}
        public static FireControlRelay[] ActiveSnapshot()=>active.ToArray();
        public void Bind(string member, EnemyActor actor)
        {
            if(member=="left:1"&&!senderAssigned)
            {
                Sender=actor;senderAssigned=true;
                Device=RelayDevice.Attach(actor,transform,new Vector3(-3,0,20));
                status=D2WorldText.Create(transform,"R 火控中继",actor.transform.position+Vector3.up*2.2f,.26f);
            }
            if(member=="left:0"&&!receiverAssigned){Receiver=actor;receiverAssigned=true;}
            Revalidate();
        }
        public void Configure(EnemyActor sender,EnemyActor receiver,RelayDevice device)
        {Sender=sender;Receiver=receiver;Device=device;senderAssigned=receiverAssigned=true;Revalidate();}
        void SetState(RelayLinkState next,string why)
        {
            if(next!=State){if(DemoGame.I)DemoGame.I.Record("relay_state",State+" -> "+next+";reason="+why);State=next;}
            Reason=why;
        }
        public void Revalidate()
        {
            if(State==RelayLinkState.Terminated)return;
            if(!senderAssigned||!receiverAssigned){connectStarted=-1;SetState(RelayLinkState.Waiting,"等待两端部署");return;}
            if(!Sender||!Receiver||!Sender.Alive||!Receiver.Alive||!Device||!Device.Alive)
            {connectStarted=-1;SetState(RelayLinkState.Terminated,"设备或关系成员失效");return;}
            if(!FeatureEnabled){connectStarted=-1;SetState(RelayLinkState.Disconnected,"中继对照关闭");return;}
            if(Sender.stunLeft>0||Sender.Silenced)
            {connectStarted=-1;SetState(RelayLinkState.Suppressed,Sender.Silenced?"发送端沉默":"发送端硬直");return;}
            float distance=Vector3.Distance(Device.AimPoint,ReceivePoint);
            float limit=State==RelayLinkState.Connected?RetainDistance:AcquireDistance;
            bool blocked=Physics.Linecast(Device.AimPoint,ReceivePoint,Layers.WorldMask,QueryTriggerInteraction.Ignore);
            if(distance>limit||blocked)
            {connectStarted=-1;SetState(RelayLinkState.Disconnected,blocked?"实体遮挡":"超过连接距离");return;}
            if(State==RelayLinkState.Connected)return;
            if(connectStarted<0)connectStarted=Time.time;
            if(Time.time-connectStarted>=ConnectSeconds)SetState(RelayLinkState.Connected,"只加速射后整备");
            else SetState(RelayLinkState.Connecting,"连续接入 0.8 秒");
        }
        void Update()
        {
            var g=DemoGame.I;if(!g||!g.IsPlaying)return;
            if(Time.time>=nextCheck){nextCheck=Time.time+CheckInterval;Revalidate();}
            if(Device)Device.ResolveCarrierDeath();
            bool visible=(State==RelayLinkState.Connecting||State==RelayLinkState.Connected)&&Device&&Receiver;
            if(visible)
            {
                if(!link)link=Shapes.Line(transform,Device.AimPoint,ReceivePoint,new Color(.2f,.85f,1),.025f);
                link.enabled=true;link.SetPosition(0,Device.AimPoint);link.SetPosition(1,ReceivePoint);
                Color color=Color.Lerp(new Color(.2f,.4f,.5f),new Color(.2f,.9f,1),State==RelayLinkState.Connected?.65f+.35f*Mathf.Sin(Time.time*8):Charge);
                link.startColor=link.endColor=color;
            }
            else if(link)link.enabled=false;
            if(status&&Sender)
            {
                status.transform.position=Sender.transform.position+Vector3.up*2.25f;
                string value="R → S · "+(State==RelayLinkState.Connecting?"接入 "+Mathf.RoundToInt(Charge*100)+"%":State==RelayLinkState.Connected?"连接":Reason);
                if(status.text!=value){status.text=value;status.font.RequestCharactersInTexture(value,64);}
            }
        }
        public static void RevalidateAll(){Physics.SyncTransforms();foreach(var relay in active.ToArray())if(relay)relay.Revalidate();}
        public static float RearmRate(EnemyActor receiver)
        {
            foreach(var relay in active)if(relay&&relay.Receiver==receiver&&relay.Connected)return 1f/.75f;
            return 1;
        }
        public static float ConnectionValue(EnemyActor owner,Vector3 candidate)
        {
            if(!FeatureEnabled||!owner)return 0;
            foreach(var relay in active)
            {
                if(!relay||relay.State==RelayLinkState.Terminated||!relay.Device||!relay.Device.Alive||!relay.Sender||!relay.Receiver||!relay.Sender.Alive||!relay.Receiver.Alive)continue;
                Vector3 a=relay.Device.AimPoint,b=relay.ReceivePoint;
                if(owner==relay.Sender)a+=candidate-owner.transform.position;
                else if(owner==relay.Receiver)b+=candidate-owner.transform.position;else continue;
                if(Vector3.Distance(a,b)<=AcquireDistance&&!Physics.Linecast(a,b,Layers.WorldMask,QueryTriggerInteraction.Ignore))return 1;
            }
            return 0;
        }
        public string ScanSummary(Vector3 observer,float radius=20)
        {
            if(!Sender||!Receiver||Vector3.Distance(observer,Sender.AimPoint)>radius||Vector3.Distance(observer,Receiver.AimPoint)>radius)return "";
            return "R 中继 → S 狙击 · "+Reason+(Receiver.warningLeft>0?" · 狙击正在预告":"");
        }
    }
}
