using System;
using UnityEngine;
namespace SwapHunter
{
    public sealed class TacticalLoadoutRoute
    {
        public readonly int id,classId,primary,secondary,classSkill,general;
        public readonly string name,purpose,tradeoff;
        public TacticalLoadoutRoute(int id,int c,string name,int p,int s,int skill,int g,string purpose,string tradeoff)
        {this.id=id;classId=c;this.name=name;primary=p;secondary=s;classSkill=skill;general=g;this.purpose=purpose;this.tradeoff=tradeoff;}
        public bool Owned(ExpeditionProfile p)=>p.weapons[primary]&&p.weapons[secondary]&&p.classSkillLevels[classSkill]>0&&p.generalLevels[general]>0;
        public int MissingListPrice(ExpeditionProfile p)=>(p.weapons[primary]?0:TacticalPrototypeRules.WeaponPrice(primary))+
            (p.weapons[secondary]?0:TacticalPrototypeRules.WeaponPrice(secondary))+(p.classSkillLevels[classSkill]>0?0:TacticalPrototypeRules.SkillBasePrice(classSkill))+(p.generalLevels[general]>0?0:TacticalPrototypeRules.SkillBasePrice(general,true));
        public bool Listed(ExpeditionProfile p)=>(p.weapons[primary]||TacticalPrototypeRules.WeaponListed(primary,p.cleared))&&(p.weapons[secondary]||TacticalPrototypeRules.WeaponListed(secondary,p.cleared));
    }
    public static class TacticalLoadoutRoutes
    {
        public static readonly TacticalLoadoutRoute[] All={
            new TacticalLoadoutRoute(0,0,"冲进去，再撤出",4,5,1,0,"先用快速机动靠近掩体，再用冲锋枪压住敌人；贴近后切双管。遇到正面火力时放盾，打完沿侧路撤离。","适合近距离突入。开火会结束快速机动；护盾留在部署位置，不会跟随换位。"),
            new TacticalLoadoutRoute(1,0,"近距离稳步推进",0,1,0,1,"先用卡宾处理前方目标，借掩体向前走；需要过开阔地时用战术推进，接近后用泵动霰弹枪。飞刀用来打断威胁。","适合短通道和房间战。推进有独立冷却；飞刀必须有可通行的飞行路线。"),
            new TacticalLoadoutRoute(2,1,"持续开火，及时补弹",8,0,3,1,"用机枪压制，换位置时切卡宾；开始换弹后再使用快速装填，缩短补弹空档。危险逼近时用飞刀争取时间。","适合连续交战。快速装填只能加速已经开始的换弹；换位准备期间无法释放。"),
            new TacticalLoadoutRoute(3,1,"守住一条通道",8,3,2,0,"先选能撤退的掩体，再开启阵地加固并部署正面护盾；用机枪持续射击，用重型突击步枪补充重火力。","适合防守固定区域。敌人会绕侧；离开阵地会失去加固，看到地面危险要及时撤离。"),
            new TacticalLoadoutRoute(4,2,"先侦查，再推进",2,6,4,0,"先扫描附近敌人的位置和朝向，选一条威胁较少的路；三连发步枪处理中距离目标，精确射手逐个清理远处火力点。","适合分岔路和陌生区域。扫描只提供信息，不会增加伤害，也不能让换位穿墙。"),
            new TacticalLoadoutRoute(5,2,"优先解决关键敌人",6,7,5,1,"从掩体观察，开启精确射击后瞄准关键敌人；精确射手负责连续点射，栓动狙击负责远处单点。需要打断时先投飞刀。","适合远距离优先击破。弹匣小、射击间隔长；每次开火后留时间重新找掩体。"),
            new TacticalLoadoutRoute(6,3,"标记后集中火力",10,11,6,0,"先用标记器标记关键目标，再切线圈枪蓄力射击；把遥控炸药放在敌人经过的位置，护盾保护准备过程。","适合重要目标和通道伏击。线圈枪蓄力时不能换位；固定护盾和炸药不会随你移动。"),
            new TacticalLoadoutRoute(7,3,"封住路线，再拆敌人",9,0,7,1,"先把区域封锁放在敌人的推进路线，用榴弹打击聚集目标；敌人散开后切卡宾点射，飞刀用来打断最危险的敌人。","适合多入口交战。爆炸和封锁会伤到自己，投放前先给自己留一条安全退路。")
        };
    }
    public sealed partial class DemoGame
    {
        int selectedRoute=-1;bool routeConfirm;
        internal void SelectRouteForReview(int id){selectedRoute=id;routeConfirm=false;}
        void DrawLoadoutRoutes()
        {
            var p=expeditionStore.Profile;float width=(viewWidth-112)/2;bool enabled=GUI.enabled;
            Text("先选一种打法 → 查看武器与技能 → 确认替换。应用路线后，还可以自行调整配件、刀具和道具。",new Rect(48,263,viewWidth-96,30),17,Muted);
            GUI.enabled=enabled&&selectedRoute<0&&!TrialSessionActive;
            foreach(var r in TacticalLoadoutRoutes.All)
            {
                float x=48+(r.id%2)*(width+16),y=308+(r.id/2)*125;
                DrawUiPanel(new Rect(x,y,width,113),r.classId==p.activeClass);
                Text(TacticalPrototypeRules.ClassNames[r.classId]+" / "+r.name,new Rect(x+14,y+10,width-150,28),20,Paper);
                Text(WeaponCatalog.Get(r.primary).name+" ＋ "+WeaponCatalog.Get(r.secondary).name+"\n"+
                    TacticalPrototypeRules.ClassSkillNames[r.classSkill]+" ＋ "+TacticalPrototypeRules.GeneralSkillNames[r.general],new Rect(x+14,y+42,width-145,47),16,Muted);
                Text(r.Owned(p)?"内容已拥有 · 可查看替换差异":(r.Listed(p)?"内容已上架":"部分武器尚未上架")+" · 缺项标价 "+r.MissingListPrice(p),new Rect(x+14,y+89,width-140,21),14,PhaseBlue);
                if(Button("查看怎么用",new Rect(x+width-126,y+43,112,39)))selectedRoute=r.id;
            }
            GUI.enabled=enabled;
            Text("每条路线是一套配装建议。尚未拥有的装备需要先解锁；应用路线不会自动购买，也不会改动道具携带。",new Rect(48,814,viewWidth-96,26),16,Muted);
            if(selectedRoute>=0)DrawRouteDetail(TacticalLoadoutRoutes.All[selectedRoute]);
        }
        string RouteItemState(TacticalLoadoutRoute r,int slot)
        {
            var p=expeditionStore.Profile;
            if(slot<2){int w=slot==0?r.primary:r.secondary;return p.weapons[w]?"已拥有 · 该职业可用":TacticalPrototypeRules.WeaponListed(w,p.cleared)?"已上架 / "+TacticalPrototypeRules.WeaponPrice(w)+" 信用点":"尚未上架";}
            int level=slot==2?p.classSkillLevels[r.classSkill]:p.generalLevels[r.general];return level>0?"已拥有 "+level+"级":"未拥有 / 基础1级 100信用点（保障可先领取）";
        }
        string RouteDifference(TacticalLoadoutRoute r)
        {
            var p=expeditionStore.Profile;var l=p.classLoadouts[r.classId];
            int oldC=TacticalPrototypeRules.SkillIndex(l.classSkillId),oldG=TacticalPrototypeRules.SkillIndex(l.tacticalSkillId,true);
            return "当前职业 "+TacticalPrototypeRules.ClassNames[p.activeClass]+" → "+TacticalPrototypeRules.ClassNames[r.classId]+"\n"+
                "主武器 "+WeaponCatalog.Get(l.primary).name+" → "+WeaponCatalog.Get(r.primary).name+"；副武器 "+WeaponCatalog.Get(l.secondary).name+" → "+WeaponCatalog.Get(r.secondary).name+"\n"+
                "职业技能 "+(oldC<0?"空":TacticalPrototypeRules.ClassSkillNames[oldC])+" → "+TacticalPrototypeRules.ClassSkillNames[r.classSkill]+"；通用技能 "+(oldG<0?"空":TacticalPrototypeRules.GeneralSkillNames[oldG])+" → "+TacticalPrototypeRules.GeneralSkillNames[r.general]+"\n保留当前刀具、该职业配件和道具。确认只替换上面列出的武器与技能。";
        }
        void DrawRouteDetail(TacticalLoadoutRoute r)
        {
            Fill(FullScreenGuiRect,new Color(0,0,0,.88f));float x=100,w=viewWidth-200;
            DrawUiPanel(new Rect(x,95,w,711),true);
            Text(TacticalPrototypeRules.ClassNames[r.classId]+" / "+r.name,new Rect(x+24,116,w-180,44),29,Paper);
            if(Button("关闭",new Rect(x+w-125,117,100,38))){selectedRoute=-1;routeConfirm=false;return;}
            Text(r.purpose+"\n"+r.tradeoff,new Rect(x+24,175,w-48,85),20,Paper);
            if(routeConfirm)
            {
                Text("确认应用以下替换",new Rect(x+24,280,w-48,36),25,PhaseBlue);
                Text(RouteDifference(r),new Rect(x+24,340,w-48,185),21,Paper);
                if(Button("确认应用已有配置",new Rect(x+24,638,300,48),true))
                {ExpeditionAction(()=>expeditionStore.ApplyTacticalRoute(r.classId,r.primary,r.secondary,r.classSkill,r.general));routeConfirm=false;selectedRoute=-1;}
                if(Button("返回查看",new Rect(x+344,638,200,48)))routeConfirm=false;
                return;
            }
            for(int i=0;i<4;i++)
            {
                float y=278+i*70;int item=i;string name=i<2?WeaponCatalog.Get(i==0?r.primary:r.secondary).name:i==2?TacticalPrototypeRules.ClassSkillNames[r.classSkill]:TacticalPrototypeRules.GeneralSkillNames[r.general];
                Text((i<2?"武器":i==2?"职业技能":"通用技能")+" / "+name+"\n"+RouteItemState(r,i),new Rect(x+24,y,w-480,60),18,Paper);
                if(Button(i<2?"查看商品":"切至该职业技能页",new Rect(x+w-290,y+5,265,42)))
                {
                    if(i<2){ExpeditionAction(()=>expeditionStore.SelectClass(r.classId));expeditionWeaponSlot=i;expeditionSelectedWeapon=i==0?r.primary:r.secondary;expeditionTab=1;}
                    else{ExpeditionAction(()=>expeditionStore.SelectClass(r.classId));expeditionTab=2;}
                    selectedRoute=-1;return;
                }

            }
            var p=expeditionStore.Profile;bool owned=r.Owned(p),enabled=GUI.enabled;
            Text("缺项按标价合计 "+r.MissingListPrice(p)+" 信用点。先领取该职业的免费入门装备；其余缺项可在装配或技能页查看。",new Rect(x+24,575,w-48,56),17,Muted);
            GUI.enabled=enabled&&owned&&string.IsNullOrEmpty(p.activeRun)&&!TrialSessionActive;
            if(Button(owned?"查看替换差异":"先查看缺项商品或领取保障",new Rect(x+24,653,w-48,50),owned))routeConfirm=true;
            GUI.enabled=enabled;
            Text("使用顺序：先确认两把枪，再选择职业与通用技能，最后补足两格道具；每格最多3个。",new Rect(x+24,726,w-48,48),17,Muted);
        }
    }
}
