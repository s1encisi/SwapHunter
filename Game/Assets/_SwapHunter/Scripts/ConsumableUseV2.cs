using UnityEngine;
namespace SwapHunter
{
    [DefaultExecutionOrder(30)]
    public sealed class ConsumableUseV2:MonoBehaviour
    {
        PlayerMotor owner;DemoGame G=>DemoGame.I;int current,startedType,shots,weapon,sequence;float elapsed,duration;bool committed;Transform display;
        public bool ItemsSelected{get;private set;}
        public bool Using{get;private set;}
        public float Progress=>Using?Mathf.Clamp01(elapsed/Mathf.Max(.01f,duration)):0;
        public int CurrentType=>current;
        public string CurrentName=>TacticalPrototypeRules.ConsumableNames[Mathf.Clamp(current,0,2)];
        public int CurrentCount=>Count(current);
        void Awake(){owner=GetComponent<PlayerMotor>();}
        int Count(int type)=>!G?0:type==0?G.expeditionMedkits:type==1?G.expeditionAmmoPacks:G.expeditionArmorPacks;
        public bool CanUse(int type,out string reason)
        {
            reason="";if(!G||!G.IsPlaying||!G.expeditionActive||G.expeditionPending||!owner||owner.health<=0){reason="当前不能使用补给";return false;}
            if(type<0||type>2||Count(type)<=0){reason="没有携带该道具";return false;}
            if(type==0&&owner.health>=G.config.playerHealth){reason="生命已满";return false;}
            if(type==1){bool useful=false;foreach(int id in owner.EquippedWeapons)if(owner.reserve[id]<G.ExpeditionReserveLimit(id))useful=true;if(!useful){reason="双枪备用弹药已满";return false;}}
            if(type==2&&owner.TemporaryArmor>0){reason="现有护甲尚未耗尽";return false;}return true;
        }
        public void Select(){owner.SuspendReloadV2();ItemsSelected=true;if(Count(current)<=0)Cycle(1);owner.UnequipV2Handheld();ShowItem();}
        public void Cycle(int direction)
        {
            if(Using)return;int next=current;for(int i=0;i<3;i++){next=(next+(direction<0?2:1))%3;if(Count(next)>0){current=next;break;}}if(ItemsSelected)ShowItem();
        }
        public bool Begin(int type)
        {
            if(Using)return false;if(!CanUse(type,out var reason)){G.Toast(reason,2);return false;}
            if(owner.SwapActionsLocked||owner.IsVaulting||owner.ChargeProgress>0||owner.combatTools&&owner.combatTools.PoseActive){G.Toast("先完成当前动作",1.5f);return false;}
            owner.UnequipV2Handheld();owner.SuspendReloadV2();current=startedType=type;elapsed=0;duration=type==1?1:1.2f;committed=false;Using=ItemsSelected=true;shots=owner.shots;weapon=owner.weapon;sequence=owner.ActionSequence;D2HoldInteraction.CancelForAction();if(InteractionV2.Instance)InteractionV2.Instance.Cancel();ShowItem();G.Record("consumable_begin",type.ToString());return true;
        }
        public void Cancel(bool deselect=true)
        {
            if(Using&&G)G.Record("consumable_cancel",startedType+":"+elapsed.ToString("F2"));Using=false;elapsed=0;
            if(deselect){ItemsSelected=false;if(display)display.gameObject.SetActive(false);if(owner){owner.SetV2EquipmentVisibility(true);owner.RestoreFirearmHands();}}
        }
        void Update()
        {
            if(!G||!owner)return;if(!G.IsPlaying){if(G.state!=RunState.Paused)Cancel();return;}
            if(G.Pressed(26))Select();if(ItemsSelected){if(G.Pressed(14))Cycle(1);if(G.Pressed(15))Cycle(-1);}
            if(G.Pressed(27))Begin(current);if(G.Pressed(19))Begin(0);
            if(!Using)return;
            if(owner.health<=0||owner.weapon!=weapon||owner.ActionSequence!=sequence||owner.shots!=shots||owner.SwapActionsLocked||G.Held(12)||G.Pressed(6)||G.Pressed(10)||G.Pressed(11)||G.Pressed(25)||G.Pressed(7)){Cancel();return;}
            elapsed+=Time.deltaTime;
            if(display){float p=Progress;display.localPosition=new Vector3(-.12f,-.33f+Mathf.Sin(p*Mathf.PI)*.07f,.42f);owner.PoseKnifeHands(display,1);}
            if(elapsed<duration||committed)return;committed=true;
            bool success=startedType==0?G.UseExpeditionMedkit():startedType==1?G.UseExpeditionAmmo():G.UseExpeditionArmor();
            Using=false;elapsed=0;if(success)G.Record("consumable_commit",startedType.ToString());else G.Toast("状态已变化，道具未消耗",2);
            ItemsSelected=false;if(display)display.gameObject.SetActive(false);owner.SetV2EquipmentVisibility(true);owner.RestoreFirearmHands();
        }
        void ShowItem()
        {
            if(display)Destroy(display.gameObject);var go=new GameObject("Carried supply / "+CurrentName);go.transform.SetParent(owner.Eye,false);display=go.transform;display.localPosition=new Vector3(-.12f,-.33f,.42f);
            var body=GameObject.CreatePrimitive(PrimitiveType.Cube);body.name=CurrentName;body.transform.SetParent(display,false);body.transform.localScale=new Vector3(.13f,.10f,.075f);body.GetComponent<Collider>().enabled=false;Destroy(body.GetComponent<Collider>());body.GetComponent<Renderer>().sharedMaterial=Shapes.Mat(current==0?3:current==1?4:6);
            if(current==0)foreach(var scale in new[]{new Vector3(.065f,.016f,.004f),new Vector3(.016f,.065f,.004f)}){var cross=GameObject.CreatePrimitive(PrimitiveType.Cube);cross.transform.SetParent(display,false);cross.transform.localPosition=new Vector3(0,0,-.04f);cross.transform.localScale=scale;cross.GetComponent<Collider>().enabled=false;Destroy(cross.GetComponent<Collider>());cross.GetComponent<Renderer>().sharedMaterial=Shapes.Mat(7);}
            ImportedModels.SetLayer(display,Layers.ViewModel);owner.SetV2EquipmentVisibility(false);
        }
        void LateUpdate()
        {
            if(!ItemsSelected||!display||!owner||!G||!G.IsPlaying)return;
            owner.SetV2EquipmentVisibility(false);owner.PoseKnifeHands(display,1);
        }
        void OnDisable(){Cancel();}
    }
    public sealed partial class PlayerMotor
    {
        public void CancelConsumableV2(){var item=GetComponent<ConsumableUseV2>();if(item)item.Cancel();}
        public bool V2ItemsSelected=>GetComponent<ConsumableUseV2>()&&GetComponent<ConsumableUseV2>().ItemsSelected;
        public bool V2UsingConsumable=>GetComponent<ConsumableUseV2>()&&GetComponent<ConsumableUseV2>().Using;
    }
}
