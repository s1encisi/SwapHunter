using System;
using System.Collections.Generic;
using UnityEngine;

namespace SwapHunter
{
    public static partial class ExpeditionWorld
    {
        static Vector3 V(float x,float y,float z)=>new Vector3(x,y,z);
        static Color[] V2Palette={new Color(.8f,.88f,.94f),new Color(.54f,.42f,.32f),new Color(.27f,.42f,.48f),new Color(.20f,.28f,.35f),new Color(.47f,.55f,.62f),new Color(.47f,.30f,.19f),new Color(.32f,.44f,.42f),new Color(.54f,.68f,.72f),new Color(.46f,.52f,.62f),new Color(.56f,.44f,.26f),new Color(.73f,.70f,.84f),new Color(.30f,.41f,.50f)};
        static void V2Tint(GameObject go,Color color)
        {var block=new MaterialPropertyBlock();block.SetColor("_BaseColor",color);block.SetColor("_Color",color);foreach(var r in go.GetComponentsInChildren<Renderer>())r.SetPropertyBlock(block);}
        static MapV2WorldFeature Feature(GameObject go,string id,string purpose,string role,string interaction,string nearby)
        {var f=go.AddComponent<MapV2WorldFeature>();f.Describe(id,purpose,role,interaction,nearby);return f;}
        static void Deck(string zone,float x,float z,float width,float depth,float y=0)
        {
            Rect? cut=visualLevel==5?new Rect(-22,-25,12,230):visualLevel==6?new Rect(-2.6f,-10,5.2f,9.05f):visualLevel==9?new Rect(-5,-1,10,170):visualLevel==10?new Rect(-5,-15,10,160):(Rect?)null;
            if(Mathf.Abs(y)<.01f && cut.HasValue && !zone.Contains("跨沟"))
            {
                var a=new Rect(x-width/2,z-depth/2,width,depth);var c=cut.Value;float l=Mathf.Max(a.xMin,c.xMin),r=Mathf.Min(a.xMax,c.xMax),f=Mathf.Max(a.yMin,c.yMin),back=Mathf.Min(a.yMax,c.yMax);
                if(l<r && f<back)
                {
                    void Part(float px,float pz,float w,float d){if(w>.02f&&d>.02f)DeckRect(zone,px+w/2,pz+d/2,w,d,y);}
                    Part(a.xMin,a.yMin,l-a.xMin,a.height);Part(r,a.yMin,a.xMax-r,a.height);Part(l,a.yMin,r-l,f-a.yMin);Part(l,back,r-l,a.yMax-back);return;
                }
            }
            DeckRect(zone,x,z,width,depth,y);
        }
        static void DeckRect(string zone,float x,float z,float width,float depth,float y=0)
        {
            var go=Shapes.Box("可达地面 / "+zone,root,V(x,y-.3f,z),V(width,.6f,depth),0,true,Layers.World);
            V2Tint(go,Color.Lerp(V2Palette[visualLevel],new Color(.16f,.22f,.27f),.65f));
            var footprint=go.AddComponent<MapV2Footprint>();footprint.zone=zone;footprint.center=new Vector2(x,z);footprint.size=new Vector2(width,depth);footprint.height=y;
            // Wide industrial slab joints are visual only; no seams in the collision surface.
            for(float k=-width/2+16;k<width/2;k+=16)Shapes.Box("地面施工缝",root,V(x+k,y+.006f,z),V(.035f,.008f,depth),3);
        }
        static void Link(string name,Vector3 a,Vector3 b,float width=8)
        {
            if(Mathf.Abs(a.y-b.y)>.05f)throw new InvalidOperationException("Use visible stairs for changes in height: "+name);
            if(Mathf.Abs(a.x-b.x)>.05f && Mathf.Abs(a.z-b.z)>.05f){var c=V(a.x,a.y,b.z);Link(name,a,c,width);Link(name,c,b,width);return;}
            Deck(name,(a.x+b.x)/2,(a.z+b.z)/2,Mathf.Max(width,Mathf.Abs(a.x-b.x)+width),Mathf.Max(width,Mathf.Abs(a.z-b.z)+width),a.y);
            Label(name,(a+b)/2+Vector3.up*2.4f,.42f);
        }
        static void Equipment(string key,Vector3 p,Vector3 size,string purpose,string role)
        {
            var proxy=Shapes.Box(purpose,root,p+Vector3.up*size.y/2,size,2,true,Layers.World);
            FinalEnvironmentModels.Replace(proxy,key);Feature(proxy,purpose,purpose,role,"实体遮挡；从两侧绕行","相邻作业线与维护通道");proxy.AddComponent<CoverSite>();
        }
        static void Workshop(string purpose,float x,float z,float y=0,float width=16,float depth=12)
        {
            // Two real entrances and a raised service foundation keep each room a readable route choice.
            Deck("设备间跨沟基础 / "+purpose,x,z,width,depth,y);
            var back=Solid(purpose+" rear wall",V(x,y+2.5f,z+depth/2),V(width,5,.45f),1);V2Tint(back,V2Palette[visualLevel]);
            Solid(purpose+" right wall",V(x+width/2,y+2.5f,z),V(.45f,5,depth),1);
            foreach(int side in new[]{-1,1})Solid(purpose+" side access wall",V(x-width/2,y+2.5f,z+side*(depth/4+1)),V(.45f,5,depth/2-2),1);
            foreach(int side in new[]{-1,1})Solid(purpose+" front wall",V(x+side*(width/4+2),y+2.5f,z-depth/2),V(width/2-4,5,.45f),1);
            Solid(purpose+" roof",V(x,y+5.15f,z),V(width+.5f,.3f,depth+.5f),3);
            FinalEnvironmentModels.Create("Props_ComputerSmall",root,V(x-width/2+2,y+.8f,z+depth/2-1.4f),V(1.4f,1.6f,.8f));
            Label(purpose,V(x,y+3.5f,z-depth/2-.5f),.45f);
            Feature(back,purpose,purpose,"提供短时脱离火线与装填位置","开放入口；无额外任务钥匙","作业主路、侧向维护通路");
        }
        static void District(string name,float x,float z,float width,float depth,float y=0,int industry=0)
        {
            var district=new GameObject("设施分区 / "+name);district.transform.SetParent(root,false);var tag=district.AddComponent<MapV2District>();tag.title=name;tag.center=V(x,y,z);tag.width=width;tag.depth=depth;tag.industry=industry;
            Deck(name,x,z,width,depth,y);
            // Boundary facilities occupy marked service bays; the central freight lane stays clear.
            Workshop(name+" / 值守间",x-width/2+11,z-depth/2+11,y);
            Workshop(name+" / "+(industry==1?"阀组巡检室":industry==2?"卸货调度室":industry==3?"设备维护室":"二次复核室"),x+width*.38f,z-depth*.18f,y,16,14);
            Equipment(industry==1?"Props_Vessel_Tall":industry==3?"Props_Computer":"Props_ContainerFull",V(x-width*.30f,y,z+depth*.04f),V(8,industry==1?5:2.3f,10),name+" / "+(industry==1?"支路换热机组":industry==2?"定向卸货泊位":industry==3?"分线检修单元":"设备核验工位"),"中段视线遮挡；靠主路和外侧维护带均能接近");
            FinalEnvironmentModels.Create("Column_Slim",root,V(x,y+4.2f,z-depth*.25f),V(18,.35f,.35f),Quaternion.Euler(0,0,90));
            foreach(int side in new[]{-1,1})Equipment("Column_Slim",V(x+side*9,y,z-depth*.25f),V(.45f,4.4f,.45f),name+" / 设备桥架立柱","标明人员通道与设备接口，不侵占移动带");
            Label("维护通路 ←   作业方向 →",V(x,y+3.1f,z+depth*.1f),.38f);
            float ex=x+width/2-12,ez=z+depth/2-13;
            string key=industry==1?"Props_Vessel_Tall":industry==2?"Props_ContainerFull":industry==3?"Props_Computer":"Props_Capsule";
            for(int i=0;i<3;i++)
            {
                var p=V(ex-i*7,y,ez);
                Equipment(key,p,V(4,industry==1?6:2.2f,4),name+" / "+(industry==1?"稳压罐":industry==2?"标线待检货柜":industry==3?"分线控制台":"检验舱")+" "+(i+1),"分割远端枪线，背后维护带可绕行");
                Shapes.Box("作业位黄线",root,p+V(0,.015f,-2.7f),V(5,.025f,.09f),5);
            }
            Equipment(industry==1?"Props_Vessel_Tall":"Props_ContainerFull",V(x-width/2+10,y,z+8),V(5,2.2f,10),name+" / 卸料停靠位","隔断侧向射线，保留内外两条路线");
            Label(name,V(x,y+4,z-depth/2+4),.65f);
            // Every district has at least two ground approaches; arrows describe the work flow.
            for(int i=-1;i<=1;i++)Shapes.Box("物流方向标线",root,V(x+i*3,y+.02f,z),V(.22f,.025f,5),4);
        }
        static void V2Supply(Vector3 p,string name="驻守弹药柜",bool medical=false)
        {
            var node=Node(p,ExpeditionNodeKind.Jammer,name);node.repeatable=false;FinalEnvironmentModels.ReplaceNodeBody(node.transform,"Props_Chest");
            node.useAction=()=>
            {
                bool changed=false;var player=g.player;
                foreach(int id in player.EquippedWeapons){int maximum=g.ExpeditionReserveLimit(id);if(player.reserve[id]<maximum){player.reserve[id]=maximum;changed=true;}}
                if(medical && player.health<g.config.playerHealth){player.health=Mathf.Min(g.config.playerHealth,player.health+30);changed=true;}
                if(!changed){g.Toast(medical?"生命与两枪备弹已满，补给柜保留":"两枪备弹已满，补给柜保留",2);return false;}
                node.Activate();g.sound.Play("confirm");g.Record("mapv2_supply",name+";magazines_unchanged");g.Toast("已补满两枪备弹"+(medical?"，恢复30生命":"")+" · 不自动装填",3);return true;
            };
            Feature(node.gameObject,name,"驻守人员的非随机保障库存","位于可短暂断视线的维护入口","每次行动只取一次；满备弹不消耗","对应战斗区与返程路线");
        }
        static void V2Group(string id,Vector3 origin,float yaw=180,int columns=4,float spacing=4)
        {
            var d=MissionCatalog.Get(visualLevel);EncounterGroup group=null;foreach(var q in d.groups)if(q.id==id)group=q;
            if(group==null)throw new InvalidOperationException("Unknown authored group "+id);
            for(int i=0;i<group.roles.Length;i++)Spawn(id,i,origin+V((i%columns-(columns-1)*.5f)*spacing,0,(i/columns)*spacing),yaw,group.roles[i].fixedPost);
        }
        static void V2Boss(Vector3 p,Vector3 left,Vector3 right,Vector3? regionCenter=null,Vector3? regionSize=null)
        {
            Spawn("mini-boss",0,p,180);layout.MiniBossPosts=new[]{p,left,right};
            Region("miniboss",regionCenter??p,regionSize??V(44,12,42));
            foreach(var point in layout.MiniBossPosts)Shapes.Ring(root,point+Vector3.up*.03f,1.8f,new Color(.9f,.55f,.1f),.04f);
            Label("指定威胁 / B"+(visualLevel+1).ToString("00"),p+V(0,4,-10),.45f,true);
        }
        static void V2Cart(Vector3 a,Vector3 b,Vector3 control)
        {
            var holder=new GameObject("轨道运货台车");holder.transform.SetParent(root,false);holder.transform.position=a;
            var body=Shapes.Box("台车装载货物",holder.transform,V(0,1.4f,0),V(5.4f,2.8f,3.4f),2,true,Layers.World);FinalEnvironmentModels.Replace(body,"Props_ContainerFull");
            var obstacle=body.AddComponent<UnityEngine.AI.NavMeshObstacle>();obstacle.shape=UnityEngine.AI.NavMeshObstacleShape.Box;obstacle.size=Vector3.one;obstacle.carving=true;obstacle.carveOnlyStationary=false;
            var cart=holder.AddComponent<MapV2RailCart>();cart.a=a;cart.b=b;
            var rail=Shapes.Box("固定运输轨道",root,(a+b)/2+V(0,.02f,0),V(Mathf.Max(1,Mathf.Abs(a.x-b.x)+7),.04f,Mathf.Max(1,Mathf.Abs(a.z-b.z)+1)),3);
            var n=Node(control,ExpeditionNodeKind.Jammer,"台车停靠控制 / 改变枪线");n.repeatable=true;n.useAction=()=>{bool moved=cart.Toggle();g.Toast(moved?"台车驶向另一停靠位；有人阻挡时停机":"台车正在运行",2);return moved;};
            Feature(holder,"rail-cart","沿实轨道转运货物","一个停靠位遮挡长枪线，另一个开放维护入口","控制器切换两个固定终点；不碾压角色","轨道、控制室、装卸位");
        }
        static void V2Gate(Vector3 p,float width,Vector3 control)
        {
            var h=new GameObject("货检闸口驱动");h.transform.SetParent(root,false);var gate=h.AddComponent<MapV2TimedGate>();
            gate.barrier=Solid("货检活动 barrier",p+V(0,1.4f,0),V(width,2.8f,.5f),3);gate.barrier.SetActive(false);
            var n=Node(control,ExpeditionNodeKind.Jammer,"手动升起本侧闸杆");n.repeatable=true;n.useAction=()=>{gate.Open();return true;};
            Feature(h,"inspection-gate","分隔检查车道","短时改变一条接近路线，另一侧巷永久开放","Boss可关闭4秒；手柄随时升起","岗亭与车道");
        }
        static void V2Valve(Vector3 control,Vector3 a,Vector3 b)
        {
            var n=Node(control,ExpeditionNodeKind.Jammer,"旁路泄压阀 / 转移热区");n.repeatable=true;var valve=n.gameObject.AddComponent<MapV2SteamValve>();valve.outletA=a;valve.outletB=b;valve.Initialize();n.useAction=()=>valve.Toggle();
            foreach(var p in new[]{a,b}){FinalEnvironmentModels.Create("Props_Vessel_Tall",root,p+V(0,2,0),V(1,4,1));Shapes.Box("警示喷口范围",root,p+V(0,.02f,0),V(6,.03f,6),5);}
            Feature(n.gameObject,"steam-valve","为相邻高温支路泄压","改变维护近路与敌阵地的危险位置","开启后持续喷流；玩家和敌人均受伤","两个喷口与控制管线");
        }
        static void V2Breakable(Vector3 p,float width,string name)
        {
            var panel=Solid(name,p+V(0,1.3f,0),V(width,2.6f,.4f),5);
            var hp=panel.AddComponent<DamageableObjective>();hp.Configure("breakable-"+name,288,_=>{panel.SetActive(false);layout.MarkNavigationDirty();g.Record("world_partition_broken",name);});panel.layer=Layers.World;
            Feature(panel,name,"隔离非承重危险设备区","挡弹但侧道永远可绕行","枪弹、爆炸、近战或冲撞破坏","维护沟与设备作业面");Label("可破防爆隔断",p+V(0,3.4f,0),.35f,true);
        }
        static void V2Shortcut(string objective,System.Action route)
        {
            Transform previous=root;var group=new GameObject("锁存捷径 / "+objective);group.transform.SetParent(root,false);root=group.transform;route();root=previous;group.SetActive(false);layout.OpenOn(objective,group);if(!map.returnBridge)map.returnBridge=group;
        }
        static void V2AnchorChain(params Vector3[] points){foreach(var p in points)Anchor(p+Vector3.up*.02f);}
        static void V2Tower(string name,float x,float z,float y,float width=22,float depth=22)
        {Tower(x,z,y,width,depth);Stairs(x+width*.25f,z-depth*.5f-(Mathf.CeilToInt(y/.2f)-1)*.5f+.05f,y,4);Label(name,V(x,y+4,z),.6f);Equipment("Props_Computer",V(x-width/2+4,y,z+depth/2-3),V(3,2.2f,2),name+"控制设备","限制射角，背面有绕行空间");}
        static void V2Steps(float x,float z,float rise,float baseY=0,float yaw=0){Stairs(x,z,rise,4,baseY,yaw);}
        static void V2Knife(int id,Vector3 p){KnifePickupV2.Create(root,id,p,Quaternion.identity,id==3);}
        static void MapV2Build(int level)
        {
            RenderSettings.fogStartDistance=100;RenderSettings.fogEndDistance=420;
            switch(level){case 0:V2Calibration();break;case 1:V2ShieldLine();break;case 2:V2CoolingWell();break;case 3:V2RailStation();break;case 4:V2DualTowers();break;case 5:V2Refinery();break;case 6:V2Islands();break;case 7:V2Warehouses();break;case 8:V2CliffTower();break;case 9:V2Mine();break;case 10:V2Laboratory();break;case 11:V2Reactor();break;}
            V2ExpandWorksites();V2AuthorArchitecture();
        }
    }
}
