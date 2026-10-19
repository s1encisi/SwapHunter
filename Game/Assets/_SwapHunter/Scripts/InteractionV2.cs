using System;
using System.Collections.Generic;
using UnityEngine;
namespace SwapHunter
{
    [DefaultExecutionOrder(30)]
    public sealed class InteractionV2:MonoBehaviour
    {
        public static InteractionV2 Instance{get;private set;}
        public static bool Active=>Instance&&Instance.isActiveAndEnabled;
        PlayerMotor owner;DemoGame G=>DemoGame.I;
        UnityEngine.Object focus,holding;ExpeditionNode node;D2HoldInteraction device;KnifePickupV2 knife;
        float elapsed,duration;int shots,sequence,weapon;Vector3 origin;
        public string Hint{get;private set;}="";
        public float Progress=>holding&&duration>0?Mathf.Clamp01(elapsed/duration):0;
        public bool Holding=>holding;
        public UnityEngine.Object Focus=>focus;
        void Awake(){owner=GetComponent<PlayerMotor>();Instance=this;}
        void OnDestroy(){if(Instance==this)Instance=null;}
        public void Cancel(){holding=null;elapsed=duration=0;}
        bool Busy=>!owner||owner.SwapActionsLocked||owner.reloadLeft>0||owner.ChargeProgress>0||owner.IsVaulting||owner.combatTools&&owner.combatTools.PoseActive||owner.GetComponent<ConsumableUseV2>()&&owner.GetComponent<ConsumableUseV2>().Using;
        float Score(Vector3 p,float max)
        {
            Vector3 delta=p-owner.Eye.position;float distance=delta.magnitude;if(distance>max||distance<.01f)return float.PositiveInfinity;
            float facing=Vector3.Dot(owner.Eye.forward,delta/distance);if(facing<.70f)return float.PositiveInfinity;
            return (1-facing)*5+distance*.06f;
        }
        public void RefreshFocus()
        {
            focus=null;node=null;device=null;knife=null;Hint="";if(!G||!G.IsPlaying||!owner||owner.health<=0)return;
            float best=float.PositiveInfinity;
            void Take(UnityEngine.Object obj,Vector3 point,float max)
            {float score=Score(point,max);if(score<best){best=score;focus=obj;}}
            if(G.expeditionActive&&G.expeditionMap!=null)foreach(var n in G.expeditionMap.nodes)
            {
                if(!n||n.used&&!n.repeatable&&n.kind!=ExpeditionNodeKind.Extraction)continue;
                if(Physics.Linecast(owner.Eye.position,n.Point,out var hit,Layers.WorldMask)&&hit.collider.GetComponentInParent<ExpeditionNode>()!=n)continue;
                Take(n,n.Point,2f);
            }
            foreach(var d in D2HoldInteraction.CandidatesForV2)
            {if(!d||!d.isActiveAndEnabled||!d.Available||Physics.Linecast(owner.Eye.position,d.Point,Layers.WorldMask,QueryTriggerInteraction.Ignore))continue;Take(d,d.Point,2f);}
            var k=KnifePickupV2.Focus(owner);if(k&&k.Available)Take(k,k.Point,2);
            node=focus as ExpeditionNode;device=focus as D2HoldInteraction;knife=focus as KnifePickupV2;
            string key=G.KeyName(8);
            if(knife)Hint=key+" · "+knife.Hint;
            else if(device)Hint=device.Prompt+" · 按住 "+key+" "+device.Duration.ToString("0.#")+" 秒";
            else if(node)Hint=node.DisplayLabel+" · "+(Duration(node)>0?"按住 "+key+" "+Duration(node).ToString("0.#")+" 秒":key);
        }
        static float Duration(ExpeditionNode n)
        {
            if(!n)return 0;if(n.kind==ExpeditionNodeKind.Extraction)return 1.2f;
            var map=DemoGame.I?DemoGame.I.expeditionMap:null;var layout=map!=null&&map.root?map.root.GetComponent<MissionLayout>():null;
            return layout&&layout.TryObjective(n,out _)? .6f:0;
        }
        public bool HandlePress()
        {
            RefreshFocus();if(!focus||Busy)return false;
            if(knife)
            {
                Cancel();if(!knife.CanBegin(owner,out string reason)){G.Toast(reason,2);return false;}
                return owner.meleeEquipment&&owner.meleeEquipment.TryPickup(knife);
            }
            if(node&&node.kind==ExpeditionNodeKind.Extraction&&!G.ExpeditionMainComplete){G.Toast(MissingExtraction(),3);return false;}
            if(device&&!device.Ready(out var why)){G.Toast(why,2);return false;}
            duration=device?device.Duration:Duration(node);
            if(duration<=0)return node&&G.UseExpeditionNode(node);
            holding=focus;elapsed=0;shots=owner.shots;sequence=owner.ActionSequence;weapon=owner.weapon;origin=owner.transform.position;return true;
        }
        string MissingExtraction()
        {
            var missing=new List<string>();var m=G.mission;if(m==null)return "撤离条件尚未完成";
            if(m.CompletedObjectives<m.Definition.requiredObjectives){var labels=new List<string>();foreach(var o in m.Definition.objectives)if(!m.ObjectiveComplete(o.id))labels.Add(o.label);missing.Add("目标："+string.Join(" / ",labels));}
            if(m.RequiredRemaining>0)missing.Add("指定威胁剩余 "+m.RequiredRemaining);
            if(G.totalSwaps<m.Definition.minimumSwaps)missing.Add("还需完成一次实际换位");
            return "暂不能撤离 · "+string.Join("；",missing);
        }
        void Update()
        {
            if(!G||!owner)return;if(!G.IsPlaying){if(G.state!=RunState.Paused)Cancel();return;}
            RefreshFocus();if(!holding)return;
            if(focus!=holding||!G.Held(8)||Busy||owner.weapon!=weapon||owner.ActionSequence!=sequence||owner.shots!=shots||Vector3.Distance(origin,owner.transform.position)>.75f||G.Held(12)||G.Pressed(7)||G.Pressed(6)){Cancel();return;}
            elapsed+=Time.deltaTime;if(elapsed<duration)return;
            var complete=holding;Cancel();
            if(complete is D2HoldInteraction d){if(d.Ready(out var why))d.Commit();else G.Toast(why,2);}
            else if(complete is ExpeditionNode n)G.UseExpeditionNode(n);
        }
    }
    public sealed partial class DemoGame
    {
        void DrawV2InteractionContext()
        {
            if(!IsPlaying||!player)return;var interaction=player.GetComponent<InteractionV2>();
            if(interaction&&!string.IsNullOrEmpty(interaction.Hint))
            {
                float x=viewWidth*.5f-330;Text(interaction.Hint,new Rect(x,737,660,32),19,Cyan,TextAnchor.MiddleCenter);
                if(interaction.Holding){Fill(new Rect(x,771,660,4),new Color(.1f,.2f,.25f));Fill(new Rect(x,771,660*interaction.Progress,4),Cyan);}
            }
            var items=player.GetComponent<ConsumableUseV2>();if(!items)return;
            Text(KeyName(26)+" 道具："+items.CurrentName+" × "+items.CurrentCount+"   "+KeyName(27)+" 使用 / "+KeyName(19)+" 医疗",new Rect(32,550,560,30),19,Paper);
            if(items.Using){Text("正在使用 "+items.CurrentName,new Rect(32,520,420,27),18,Cyan);Fill(new Rect(32,547,340,5),Muted);Fill(new Rect(32,547,340*items.Progress,5),Cyan);}
        }
    }
}
