using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace SwapHunter
{
    // Only these new short holds share a coordinator. Existing instant main-objective E is unchanged.
    public abstract class D2HoldInteraction : MonoBehaviour
    {
        static readonly List<D2HoldInteraction> active=new List<D2HoldInteraction>();
        static D2HoldInteraction holding;static float elapsed;static int weapon,sequence,shots;
        public abstract Vector3 Point { get; }
        public abstract string Prompt { get; }
        public abstract float Duration { get; }
        public abstract bool Available { get; }
        public abstract bool Commit();
        public virtual bool Ready(out string reason){reason="";return true;}
        protected virtual void OnEnable(){active.Add(this);}
        protected virtual void OnDisable(){active.Remove(this);if(holding==this)CancelForAction();}
        public static IEnumerable<D2HoldInteraction> CandidatesForV2=>active;
        public static bool HasCandidate=>!InteractionV2.Active&&FindCandidate();
        public static string Hint { get { var candidate=FindCandidate();return candidate?candidate.Prompt+" · 按住 {key:8} "+candidate.Duration.ToString("0.#")+"秒":""; } }
        public static float Progress=>holding?Mathf.Clamp01(elapsed/holding.Duration):0;
        public static bool Holding=>holding;
        public static void CancelForAction(){holding=null;elapsed=0;}
        static D2HoldInteraction FindCandidate()
        {
            var g=DemoGame.I;if(!g||!g.IsPlaying||!g.player||g.player.health<=0)return null;
            var eye=g.player.Eye;D2HoldInteraction nearest=null;float best=2.8f;
            foreach(var item in active)
            {
                if(!item||!item.isActiveAndEnabled||!item.Available)continue;
                var delta=item.Point-eye.position;float distance=delta.magnitude;
                if(distance>best||Vector3.Dot(eye.forward,delta.normalized)<.25f)continue;
                if(Physics.Linecast(eye.position,item.Point,Layers.WorldMask,QueryTriggerInteraction.Ignore))continue;
                nearest=item;best=distance;
            }
            return nearest;
        }
        static bool Busy(PlayerMotor p) => p.SwapActionsLocked||p.reloadLeft>0||p.ChargeProgress>0||p.IsVaulting||p.combatTools&&p.combatTools.PoseActive;
        internal static void Tick()
        {
            if(InteractionV2.Active){CancelForAction();return;}
            var g=DemoGame.I;if(!g||!g.player)return;
            if(!g.IsPlaying){if(g.state!=RunState.Paused)CancelForAction();return;}
            var p=g.player;var candidate=FindCandidate();
            bool attack=g.Held(12)||g.Pressed(21)||g.Pressed(22)||g.Pressed(23);
            bool switchOrReload=g.Pressed(7)||g.Pressed(10)||g.Pressed(11)||g.Pressed(14)||g.Pressed(15);
            if(holding)
            {
                if(!g.Held(8)||candidate!=holding||p.health<=0||Busy(p)||attack||switchOrReload||weapon!=p.weapon||sequence!=p.ActionSequence||shots!=p.shots)
                {CancelForAction();return;}
                elapsed+=Time.deltaTime;
                if(elapsed>=holding.Duration)
                {
                    var completed=holding;CancelForAction();
                    if(completed.Ready(out var reason))completed.Commit();else if(!string.IsNullOrEmpty(reason))g.Toast(reason,2);
                }
                return;
            }
            // Holding E during a different action never queues a delayed interaction.
            if(!g.Pressed(8)||!candidate||Busy(p)||attack||switchOrReload)return;
            if(!candidate.Ready(out var why)){if(!string.IsNullOrEmpty(why))g.Toast(why,2);return;}
            holding=candidate;elapsed=0;weapon=p.weapon;sequence=p.ActionSequence;shots=p.shots;
        }
    }
    [DefaultExecutionOrder(1000)]
    public sealed class D2InteractionDriver:MonoBehaviour
    {
        public static void Ensure(Transform root){if(!root.GetComponent<D2InteractionDriver>())root.gameObject.AddComponent<D2InteractionDriver>();}
        void Update(){D2HoldInteraction.Tick();}
        void OnDisable(){D2HoldInteraction.CancelForAction();}
    }
    public static class D2WorldText
    {
        public static TextMesh Create(Transform parent,string value,Vector3 worldPosition,float size)
        {
            var g=DemoGame.I;var go=new GameObject("D2 world instruction");go.transform.SetParent(parent,false);go.transform.position=worldPosition;
            var text=go.AddComponent<TextMesh>();text.text=value;text.font=g.font;text.fontSize=64;text.characterSize=size/8;text.anchor=TextAnchor.MiddleCenter;text.color=new Color(.3f,.9f,1);
            text.font.RequestCharactersInTexture(value,64);
            if(g.config.worldTextMaterial)
            {var material=new Material(g.config.worldTextMaterial);material.mainTexture=text.font.material.mainTexture;text.GetComponent<Renderer>().sharedMaterial=material;go.AddComponent<WorldFontAtlas>().Initialize(text.font,material);}
            go.AddComponent<ExpeditionWorldLabel>();return text;
        }
    }
    public enum D2FacilityChoice { Unselected, Cover, Supply }
    public sealed class D2MaintenanceFacility:MonoBehaviour
    {
        public static bool FeatureEnabled=true;
        public D2FacilityChoice Choice { get; private set; }
        public int MedicalRemaining { get; private set; }=1;
        public int AmmoRemaining { get; private set; }=1;
        public PhaseAnchor Anchor { get; private set; }
        public Vector3 CoverSocket { get; private set; }
        public Vector3 SupplySocket { get; private set; }
        public GameObject CoverOutput { get; private set; }
        readonly Vector3 coverSize=new Vector3(4,1.9f,.6f);
        Vector3 coverCenter;GameObject supplyVisual;TextMesh choiceLabel;
        public static D2MaintenanceFacility Create(Transform root,PhaseAnchor anchor,Vector3 coverSocket,Vector3 supplySocket,Vector3 coverFeet)
        {
            var go=new GameObject("D2 可选维护设施 / 一次二选一");go.transform.SetParent(root,false);
            var facility=go.AddComponent<D2MaintenanceFacility>();facility.Anchor=anchor;facility.CoverSocket=coverSocket;facility.SupplySocket=supplySocket;facility.coverCenter=coverFeet+Vector3.up;
            facility.CoverOutput=Shapes.Box("展开的固定转场掩体",go.transform,facility.coverCenter,facility.coverSize,3,true,Layers.World);
            var obstacle=facility.CoverOutput.AddComponent<NavMeshObstacle>();obstacle.shape=NavMeshObstacleShape.Box;obstacle.size=Vector3.one;obstacle.carving=true;obstacle.carveOnlyStationary=true;facility.CoverOutput.SetActive(false);
            facility.MakeSocket(D2FacilityChoice.Cover,coverSocket,"北向掩护 / 增加转场遮蔽");facility.MakeSocket(D2FacilityChoice.Supply,supplySocket,"南侧补给 / 30生命 + 双枪各一匣");
            facility.choiceLabel=D2WorldText.Create(go.transform,"搬运中央锚点后长按 {key:8} 通电\n本次仅选一项 · 搬走锚点仍保留",(coverSocket+supplySocket)/2+Vector3.up*2.5f,.3f);
            facility.supplyVisual=new GameObject("已解锁有限补给");facility.supplyVisual.transform.SetParent(go.transform,false);facility.supplyVisual.transform.position=supplySocket;
            FinalEnvironmentModels.Supply(facility.supplyVisual.transform,Vector3.zero);
            facility.MakeService(facility.supplyVisual.transform,false,new Vector3(-1.05f,0,-.7f));facility.MakeService(facility.supplyVisual.transform,true,new Vector3(1.05f,0,-.7f));
            facility.supplyVisual.SetActive(false);D2InteractionDriver.Ensure(root);return facility;
        }
        void MakeSocket(D2FacilityChoice output,Vector3 position,string title)
        {
            var go=new GameObject(title);go.transform.SetParent(transform,false);go.transform.position=position;
            Shapes.Ring(go.transform,position+Vector3.up*.05f,1.1f,new Color(.2f,.8f,1),.06f);
            var socket=go.AddComponent<D2FacilitySocket>();socket.owner=this;socket.output=output;
            D2WorldText.Create(go.transform,title+"\n锚点接入后 {key:8} 1.5秒",position+Vector3.up*1.55f,.3f);
        }
        void MakeService(Transform parent,bool ammo,Vector3 offset)
        {
            var go=new GameObject(ammo?"弹药服务":"医疗服务");go.transform.SetParent(parent,false);go.transform.localPosition=offset;
            Shapes.Box("Service color",go.transform,new Vector3(0,.4f,0),new Vector3(.45f,.7f,.4f),ammo?5:4);
            var service=go.AddComponent<D2FacilityService>();service.owner=this;service.ammo=ammo;
            D2WorldText.Create(go.transform,ammo?"弹药 / 1份":"医疗 / 1份",go.transform.position+Vector3.up*1.1f,.25f);
        }
        public bool AnchorReady(D2FacilityChoice choice)
        {
            if(!Anchor||!Anchor.Alive)return false;var point=choice==D2FacilityChoice.Cover?CoverSocket:SupplySocket;var delta=Anchor.transform.position-point;
            return Mathf.Abs(delta.y)<=.25f&&Vector3.ProjectOnPlane(delta,Vector3.up).magnitude<=1.1f;
        }
        public bool OutputClear()
        {
            return Physics.OverlapBox(coverCenter,coverSize*.5f,Quaternion.identity,Layers.WorldMask|(1<<Layers.Actor),QueryTriggerInteraction.Ignore).Length==0;
        }
        public bool TryChoose(D2FacilityChoice choice)
        {
            var g=DemoGame.I;
            if(!FeatureEnabled||Choice!=D2FacilityChoice.Unselected||choice==D2FacilityChoice.Unselected||!g||!g.IsPlaying||!AnchorReady(choice))return false;
            Physics.SyncTransforms();
            if(choice==D2FacilityChoice.Cover&&!OutputClear()){g.Toast("展开位置被占用 · 选择尚未提交",2);return false;}
            Choice=choice;
            if(choice==D2FacilityChoice.Cover)CoverOutput.SetActive(true);else supplyVisual.SetActive(true);
            string value=choice==D2FacilityChoice.Cover?"已选北向掩护 · 补给输出关闭":"已选南侧补给 · 医疗/弹药各1份";
            choiceLabel.text=value;choiceLabel.font.RequestCharactersInTexture(value,64);g.Record("facility_choice",choice.ToString());g.Toast(value+" · 锚点可继续搬运",3);g.sound.Play("objective");return true;
        }
        public float MedicalAmount()=>DemoGame.I&&DemoGame.I.player?Mathf.Min(30,Mathf.Max(0,DemoGame.I.config.playerHealth-DemoGame.I.player.health)):0;
        public int AmmoAmount()
        {
            var g=DemoGame.I;if(!g||!g.player)return 0;int amount=0;
            foreach(int id in g.player.EquippedWeapons)amount+=Mathf.Min(EffectiveWeaponStats.Read(id,g.config).Magazine,Mathf.Max(0,g.ExpeditionReserveLimit(id)-g.player.reserve[id]));
            return amount;
        }
        public bool TakeMedical()
        {
            var g=DemoGame.I;if(!FeatureEnabled||Choice!=D2FacilityChoice.Supply||MedicalRemaining<=0||!g||!g.IsPlaying||g.player.health<=0)return false;
            float amount=MedicalAmount();if(amount<=0){g.Toast("生命已满 · 医疗份数保留",2);return false;}
            MedicalRemaining=0;g.player.health+=amount;g.Record("facility_medical","healed="+amount);g.Toast("本次恢复 "+amount.ToString("0.#")+" 生命 · 医疗已用尽",2);g.sound.Play("confirm");return true;
        }
        public bool TakeAmmo()
        {
            var g=DemoGame.I;if(!FeatureEnabled||Choice!=D2FacilityChoice.Supply||AmmoRemaining<=0||!g||!g.IsPlaying||g.player.health<=0)return false;
            if(AmmoAmount()<=0){g.Toast("双枪备弹充足 · 弹药份数保留",2);return false;}
            AmmoRemaining=0;int given=0;
            foreach(int id in g.player.EquippedWeapons)
            {int before=g.player.reserve[id];int add=Mathf.Min(EffectiveWeaponStats.Read(id,g.config).Magazine,Mathf.Max(0,g.ExpeditionReserveLimit(id)-before));g.player.reserve[id]=before+add;given+=add;}
            g.Record("facility_ammo","rounds="+given);g.Toast("补充备弹 "+given+" 发 · 弹药已用尽",2);g.sound.Play("confirm");return true;
        }
    }
    public sealed class D2FacilitySocket:D2HoldInteraction
    {
        public D2MaintenanceFacility owner;public D2FacilityChoice output;
        public override Vector3 Point=>transform.position+Vector3.up*.8f;
        public override bool Available=>owner&&D2MaintenanceFacility.FeatureEnabled&&owner.Choice==D2FacilityChoice.Unselected;
        public override float Duration=>1.5f;
        public override string Prompt=>(output==D2FacilityChoice.Cover?"选择北向掩护":"选择南侧补给")+(owner&&owner.AnchorReady(output)?" · 可接入":" · 请搬来中央锚点");
        public override bool Ready(out string reason)
        {reason="请先把中央锚点放入此接入圈";if(!Available||!owner.AnchorReady(output))return false;if(output==D2FacilityChoice.Cover&&!owner.OutputClear()){reason="掩体展开范围被占用";return false;}reason="";return true;}
        public override bool Commit()=>owner&&owner.TryChoose(output);
    }
    public sealed class D2FacilityService:D2HoldInteraction
    {
        public D2MaintenanceFacility owner;public bool ammo;
        public override Vector3 Point=>transform.position+Vector3.up*.75f;
        public override bool Available=>owner&&D2MaintenanceFacility.FeatureEnabled&&owner.Choice==D2FacilityChoice.Supply&&(ammo?owner.AmmoRemaining:owner.MedicalRemaining)>0;
        public override float Duration=>.35f;
        public override string Prompt=>ammo?"领取弹药 · 实补 "+owner.AmmoAmount()+" 发（1份）":"领取医疗 · 实补 "+owner.MedicalAmount().ToString("0.#")+" 生命（1份）";
        public override bool Commit()=>owner&&(ammo?owner.TakeAmmo():owner.TakeMedical());
    }
}
