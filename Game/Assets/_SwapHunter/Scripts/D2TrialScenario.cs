using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using Unity.AI.Navigation;

namespace SwapHunter
{
    public static class D2TrialCatalog
    {
        public const int MarkComparison=2, RelayBreak=3, DenialReroute=4;
        public static bool Valid(int id)=>id>=0&&id<=4;
        public static int LevelFor(int id){if(!Valid(id))throw new ArgumentOutOfRangeException(nameof(id));return id==0||id==MarkComparison?0:4;}
        public static string Name(int id)=>id==0?"近距与转场":id==1?"长视线与火力间歇":id==MarkComparison?"标记伤害对照":id==RelayBreak?"换位断线":"封锁改道";
        public static bool Compatible(string kind,int product,int classId,int scenario,out string reason)
        {
            reason="";
            if(!Valid(scenario)){reason="情境编号不存在";return false;}
            if(scenario==MarkComparison&&classId!=3){reason="标记对照需远程兵合法双枪，不替换当前商品的职业";return false;}
            if(scenario==DenialReroute&&(kind!="class"||product!=TacticalPrototypeRules.DenialSkillIndex))
            {reason="封锁改道仅试用区域封锁，不把当前商品替换为另一技能";return false;}
            return true;
        }
        public static string Purpose(int scenario)=>scenario==MarkComparison?
            "先对未标记靶开火，再用相位标记器命中另一靶并接本次商品。比较首弹、标记后实际伤害与准备时间。训练靶不攻击，伤害规则正常。":scenario==RelayBreak?
            "观察蓝色 R→S 连线。用 Q 将 R 换到起点，离开狙击预告；再换回观察 0.8 秒重连。已锁定的预告仍会执行。":scenario==DenialReroute?
            "用本次等级的区域封锁覆盖 R 所在通路；观察敌人改走另一侧、连线变化和区域实际伤害。两侧通路都可通行。":"在原章节中体验本次商品，按 Esc 返回原整备页。";
        public static string Cost(int scenario)=>scenario==MarkComparison?"代价：占用一个武器槽，先打标记并完成切枪；设备不吃标记增伤。":scenario==RelayBreak?
            "代价：交出当前位置并消耗 Q 冷却；只断协同，不自动取消狙击预告。":scenario==DenialReroute?
            "代价：占用唯一职业技能槽、放置与冷却时间；改道次数不等于伤害收益。":"固定其它配装；当前商品保持采购结果等级或明确的同阶三级。";
    }

    public sealed class D2TrialScenario : MonoBehaviour
    {
        readonly List<EnemyActor> actors=new List<EnemyActor>();
        readonly List<PhaseAnchor> ownAnchors=new List<PhaseAnchor>();
        readonly Dictionary<int,float> markStarted=new Dictionary<int,float>();
        DemoGame game; DirectedTrialRequest request; FireControlRelay relay; RelayDevice device; RelayLinkState previousLink;
        float beganAt,lastConnect=-1,preparation,occupiedSeconds;
        int disconnects,connects,firstHits,markedHits,normalHits;
        float firstDamage,markedDamage,normalDamage,deviceDamage,denialDamage;
        public int ScenarioId=>request==null?-1:request.scenario;
        public FireControlRelay Relay=>relay;
        public IReadOnlyList<EnemyActor> Actors=>actors;
        public string Objective=>D2TrialCatalog.Name(ScenarioId)+" · "+(ScenarioId==2?"先标记，再衔接伤害":ScenarioId==3?"换位 R 并观察断线/重连":"在一侧部署封锁，观察真实改道")+" · Esc 返回";
        public static D2TrialScenario Create(DemoGame game,DirectedTrialRequest request)
        {
            if(!RunStorage.IsDirectedTrial&&!RunStorage.IsValidation)throw new InvalidOperationException("D2 trial scenes require isolated storage.");
            DirectedTrialBootstrap.Validate(request);
            if(request.scenario<2)throw new ArgumentException("Dedicated D2 scene needs scenario 2..4.");
            var go=new GameObject("D2 isolated trial scenario "+request.scenario);go.transform.SetParent(game.world,false);
            var scene=go.AddComponent<D2TrialScenario>();scene.Build(game,request);return scene;
        }
        void Build(DemoGame g,DirectedTrialRequest r)
        {
            game=g;request=r;beganAt=Time.time;
            if(game.mission!=null)game.mission.Stop("isolated_trial_scenario");
            foreach(var enemy in game.enemies)if(enemy)enemy.gameObject.SetActive(false);
            foreach(var anchor in game.anchors)if(anchor)anchor.gameObject.SetActive(false);
            if(game.expeditionMap!=null)foreach(var node in game.expeditionMap.nodes)if(node)node.gameObject.SetActive(false);
            Vector3 origin=new Vector3(0,0,-110);
            Shapes.Box("Trial deck",transform,origin+Vector3.down*.5f,new Vector3(36,1,44),1,true,Layers.World);
            Shapes.Box("Left boundary",transform,origin+new Vector3(-18,1.2f,0),new Vector3(.5f,2.4f,44),3,true,Layers.World);
            Shapes.Box("Right boundary",transform,origin+new Vector3(18,1.2f,0),new Vector3(.5f,2.4f,44),3,true,Layers.World);
            Shapes.Box("Far boundary",transform,origin+new Vector3(0,1.2f,22),new Vector3(36,2.4f,.5f),3,true,Layers.World);
            Shapes.Box("Near boundary",transform,origin+new Vector3(0,1.2f,-22),new Vector3(36,2.4f,.5f),3,true,Layers.World);
            if(r.scenario!=D2TrialCatalog.MarkComparison)
            {
                foreach(int side in new[]{-1,1})
                {
                    var cover=Shapes.Box("Side approach cover "+side,transform,origin+new Vector3(side*8,1.1f,-4),new Vector3(3,2.2f,5),3,true,Layers.World);
                    cover.AddComponent<CoverSite>();
                    Shapes.Box("Approach stripe",transform,origin+new Vector3(side*4,.015f,-4),new Vector3(.25f,.03f,12),5);
                }
            }
            var navigation=gameObject.AddComponent<NavMeshSurface>();navigation.collectObjects=CollectObjects.Children;
            navigation.layerMask=Layers.WorldMask;navigation.useGeometry=UnityEngine.AI.NavMeshCollectGeometry.PhysicsColliders;
            RuntimeNavigationOwner.BuildFresh(navigation);
            var layout=gameObject.AddComponent<MissionLayout>();
            layout.BindRegion("trial",new Bounds(origin+Vector3.up*2,new Vector3(34,8,42)));
            game.player.SetFeet(origin+new Vector3(0,.035f,-14));game.player.SetLook(0,0);game.player.verticalSpeed=0;
            if(r.scenario==D2TrialCatalog.MarkComparison)
            {
                var plain=Spawn(EnemyKind.Training,origin+new Vector3(-5,.03f,3),layout,"plain");
                var marked=Spawn(EnemyKind.Training,origin+new Vector3(5,.03f,3),layout,"marked");
                D2WorldText.Create(transform,"A 基准靶 / 不标记",plain.AimPoint+Vector3.up*1.1f,.2f);
                D2WorldText.Create(transform,"B 先标记 / 再衔接",marked.AimPoint+Vector3.up*1.1f,.2f);
                device=RelayDevice.Attach(marked,transform,origin+new Vector3(5,.03f,3));
                // The marker is actually equipped, not a synthetic MarkPhase call.
                game.player.SwitchWeapon(11);
            }
            else
            {
                var sender=Spawn(EnemyKind.Assault,origin+new Vector3(4,.03f,4),layout,"sender");
                var receiver=Spawn(EnemyKind.Sniper,origin+new Vector3(-4,.03f,4),layout,"receiver");
                device=RelayDevice.Attach(sender,transform,origin+new Vector3(4,.03f,4));
                relay=gameObject.AddComponent<FireControlRelay>();relay.Configure(sender,receiver,device);previousLink=relay.State;
                D2WorldText.Create(sender.transform,"R 火控中继",sender.AimPoint+Vector3.up*1.1f,.22f);
                D2WorldText.Create(receiver.transform,"S 狙击接收端",receiver.AimPoint+Vector3.up*1.1f,.22f);
            }
            var anchorObject=new GameObject("Trial return anchor");anchorObject.layer=Layers.Actor;anchorObject.transform.SetParent(transform,false);
            anchorObject.transform.position=origin+new Vector3(-10,.03f,-14);var a=anchorObject.AddComponent<PhaseAnchor>();
            var capsule=anchorObject.AddComponent<CapsuleCollider>();capsule.height=1.4f;capsule.radius=.32f;capsule.center=Vector3.up*.7f;
            Shapes.Make("Anchor body",PrimitiveType.Cylinder,anchorObject.transform,Vector3.up*.7f,new Vector3(.4f,.7f,.4f),5);
            game.anchors.Add(a);ownAnchors.Add(a);
            D2WorldText.Create(transform,"回程锚点",a.AimPoint+Vector3.up*.7f,.2f);
            Physics.SyncTransforms();game.Record("d2_trial_scene","scenario="+r.scenario+";product="+r.kind+":"+r.productId+";level="+r.level);
        }
        EnemyActor Spawn(EnemyKind kind,Vector3 position,MissionLayout layout,string id)
        {
            var actor=EnemyActor.Spawn(kind,position,0);if(!actor)throw new InvalidOperationException("D2 trial actor spawn failed: "+id);
            actor.transform.SetParent(transform,true);actor.transform.rotation=Quaternion.Euler(0,180,0);
            actor.gameObject.AddComponent<MissionEncounterAgent>().Configure(layout,"trial_"+id,false);actors.Add(actor);return actor;
        }
        void Update()
        {
            if(!game||!game.IsPlaying)return;
            if(relay)
            {
                var current=relay.State;
                if(current!=previousLink)
                {
                    if(current==RelayLinkState.Connected){connects++;lastConnect=Time.time-beganAt;}
                    if(previousLink==RelayLinkState.Connected)disconnects++;
                    previousLink=current;
                }
            }
            foreach(var actor in actors)if(actor&&actor.Alive)
                foreach(var field in ClassDenialField.Active)if(field&&field.Live&&ClassSkillArea.Contains(field.transform.position,actor.transform.position,actor.AimPoint))
                {occupiedSeconds+=Time.deltaTime;break;}
        }
        public void Observe(string evt,string detail)
        {
            if(evt!="d2_damage")return;
            var values=new Dictionary<string,string>();foreach(string pair in detail.Split(';')){int at=pair.IndexOf('=');if(at>0)values[pair.Substring(0,at)]=pair.Substring(at+1);}
            if(!values.TryGetValue("kind",out string kind)||!values.TryGetValue("source",out string source))return;
            float Read(string key)=>values.TryGetValue(key,out string text)&&float.TryParse(text,NumberStyles.Float,CultureInfo.InvariantCulture,out float v)?v:0;
            float actual=Read("healthLoss")+Read("energyLoss");if(actual<=0)return;
            if(kind=="device"){deviceDamage+=actual;return;}
            if(kind!="enemy"||source.StartsWith("Enemy")||source=="Environment")return;
            bool marked=Read("mark")>1;
            int receiver=values.TryGetValue("receiver",out string key)&&int.TryParse(key,out int id)?id:0;
            if(source=="Denial")denialDamage+=actual;
            if(source=="PlayerBullet"&&game.player.weapon==11&&!marked)
            {firstHits++;firstDamage+=actual;markStarted[receiver]=Time.time;}
            else if(marked)
            {markedHits++;markedDamage+=actual;if(markStarted.TryGetValue(receiver,out float started))preparation=Time.time-started;}
            else {normalHits++;normalDamage+=actual;}
        }
        public string Summary
        {
            get
            {
                if(ScenarioId==D2TrialCatalog.MarkComparison)return "未标记首弹 "+firstHits+" 次 / 实伤 "+firstDamage.ToString("0.0")+"；标记后 "+markedHits+" 次 / 实伤 "+markedDamage.ToString("0.0")+
                    "；未标记对照 "+normalHits+" 次 / "+normalDamage.ToString("0.0")+"\n最近衔接间隔 "+preparation.ToString("0.00")+" 秒；设备实伤 "+deviceDamage.ToString("0.0")+"；区域占用 "+occupiedSeconds.ToString("0.0")+" 人秒。按部位/来源区分观察，不把合计当成单发增伤。";
                int choices=0;Vector3 destination=Vector3.zero;foreach(var actor in actors)if(actor){var awareness=actor.GetComponent<EnemyHazardAwareness>();if(awareness&&awareness.Decisions>0){choices+=awareness.Decisions;destination=awareness.ChosenDestination;}}
                string link=relay?relay.Reason:"无关系";
                return "连线："+link+"；断开 "+disconnects+" 次 / 接入 "+connects+" 次；最近接入 "+(lastConnect<0?"尚无":lastConnect.ToString("0.0")+" 秒")+
                    "；设备 HP "+(device?device.Health.ToString("0.0"):"0")+"\n真实选位 "+choices+" 次"+(choices>0?" / 驻点 "+destination.ToString("F1"):"")+"；区域实伤 "+denialDamage.ToString("0.0")+" / 停留 "+occupiedSeconds.ToString("0.0")+" 人秒。";
            }
        }
        void OnDestroy(){if(game){foreach(var a in ownAnchors)game.anchors.Remove(a);foreach(var actor in actors)game.enemies.Remove(actor);}}
    }
}
