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
            new TacticalLoadoutRoute(0,0,"突入撤离",4,5,1,0,"冲锋枪持续压制，双管处理近距窗口；快速机动先抢位，A盾保护固定正面。","实际攻击结束机动；换位不会带走A盾，远处目标仍需接近。"),
            new TacticalLoadoutRoute(1,0,"近身控场",0,1,0,1,"卡宾负责接近中的火力，泵动利用近距窗口；震荡和飞刀分别争取出手机会。","控制仍受范围、盾面、视线与冷却限制，不能持续锁死Boss空间技能。"),
            new TacticalLoadoutRoute(2,1,"机动补弹",8,0,3,1,"机枪维持长火力，卡宾负责轻装转场；快速装填缩短当前装填，飞刀提供主动控制。","装填开始后才可加速；Q准备时装填挂起，加速不能在此期间释放。"),
            new TacticalLoadoutRoute(3,1,"定点守线",8,3,2,0,"机枪与重突承担持续和较重单发；阵地加固与定向A盾保护预先选择的位置。","离开固定阵地失去加固；空间伤害与侧背威胁仍需走位、掩体或Q。"),
            new TacticalLoadoutRoute(4,2,"信息择线",2,6,4,0,"扫描先看分支敌人位置和朝向；三连发处理中距，射手逐点拆解长线威胁。","扫描不允许穿墙Q；本路线不能同时带精确射击，信息收益不等于直接增伤。"),
            new TacticalLoadoutRoute(5,2,"关键单点",6,7,5,1,"精确准备强化下一次真实射击；射手与栓狙切换节奏，飞刀提供控制机会。","保留小弹匣、射后等待和瞄准成本；无扫描信息，不能盲目持续开火。"),
            new TacticalLoadoutRoute(6,3,"直击集火",10,11,6,0,"标记后用线圈处理关键目标；遥控爆破处理独立区域，A盾保障准备位置。","线圈蓄能时不能Q；爆破不吃标记增伤，固定盾和炸药都不随换位移动。"),
            new TacticalLoadoutRoute(7,3,"封锁拆解",9,0,7,1,"榴弹与封锁改变区域压力，卡宾补充直射；飞刀干扰空间技能，为拆解提供时机。","爆炸与封锁保留自伤，受遮挡约束；标记不会放大范围伤害。")
        };
    }
    public sealed partial class DemoGame
    {
        int selectedRoute=-1;bool routeConfirm;
        internal void SelectRouteForReview(int id){selectedRoute=id;routeConfirm=false;}
        void DrawLoadoutRoutes()
        {
            var p=expeditionStore.Profile;float width=(viewWidth-112)/2;bool enabled=GUI.enabled;
            Text("路线说明分工与代价，不代表强度排行。第1—3章先用保障配置；4—6章比较预算；7—12章尝试横向解法。",new Rect(48,263,viewWidth-96,30),17,Muted);
            GUI.enabled=enabled&&selectedRoute<0&&!TrialSessionActive;
            foreach(var r in TacticalLoadoutRoutes.All)
            {
                float x=48+(r.id%2)*(width+16),y=308+(r.id/2)*125;
                DrawUiPanel(new Rect(x,y,width,113),r.classId==p.activeClass);
                Text(TacticalPrototypeRules.ClassNames[r.classId]+" / "+r.name,new Rect(x+14,y+10,width-150,28),20,Paper);
                Text(WeaponCatalog.Get(r.primary).name+" ＋ "+WeaponCatalog.Get(r.secondary).name+"\n"+
                    TacticalPrototypeRules.ClassSkillNames[r.classSkill]+" ＋ "+TacticalPrototypeRules.GeneralSkillNames[r.general],new Rect(x+14,y+42,width-145,47),16,Muted);
                Text(r.Owned(p)?"内容已拥有 · 可查看替换差异":(r.Listed(p)?"内容已上架":"部分武器尚未上架")+" · 缺项标价 "+r.MissingListPrice(p),new Rect(x+14,y+89,width-140,21),14,PhaseBlue);
                if(Button("查看配合",new Rect(x+width-126,y+43,112,39)))selectedRoute=r.id;
            }
            GUI.enabled=enabled;
            Text("重装后期继续使用重突与机枪，两条路线体现技能取舍；配件与携带由你保留调整。缺项标价未扣除可领取的保障。",new Rect(48,814,viewWidth-96,26),16,Muted);
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
                "职业技能 "+(oldC<0?"空":TacticalPrototypeRules.ClassSkillNames[oldC])+" → "+TacticalPrototypeRules.ClassSkillNames[r.classSkill]+"；通用技能 "+(oldG<0?"空":TacticalPrototypeRules.GeneralSkillNames[oldG])+" → "+TacticalPrototypeRules.GeneralSkillNames[r.general]+"\n保留该职业原有配件与道具携带；不花钱、不赠送、不强化。";
        }
        void DrawRouteDetail(TacticalLoadoutRoute r)
        {
            Fill(FullScreenGuiRect,new Color(0,0,0,.88f));float x=100,w=viewWidth-200;
            DrawUiPanel(new Rect(x,95,w,711),true);
            Text(TacticalPrototypeRules.ClassNames[r.classId]+" / "+r.name,new Rect(x+24,116,w-180,44),29,Paper);
            if(Button("关闭",new Rect(x+w-125,117,100,38))){selectedRoute=-1;routeConfirm=false;return;}
            Text(r.purpose+"\n取舍："+r.tradeoff,new Rect(x+24,175,w-48,85),20,Paper);
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
                if(Button(i<2?"查看商品":"切至该职业技能页",new Rect(x+w-445,y+5,235,42)))
                {
                    if(i<2){expeditionSelectedWeapon=i==0?r.primary:r.secondary;expeditionTab=1;}
                    else{ExpeditionAction(()=>expeditionStore.SelectClass(r.classId));expeditionTab=2;}
                    selectedRoute=-1;return;
                }
                if(Button("定向试用",new Rect(x+w-195,y+5,170,42)))
                {selectedRoute=-1;if(item<2)OpenWeaponTrial(item==0?r.primary:r.secondary);else OpenSkillTrial(item==2?r.classSkill:r.general,item==3);return;}
            }
            var p=expeditionStore.Profile;bool owned=r.Owned(p),enabled=GUI.enabled;
            Text("缺项按标价合计 "+r.MissingListPrice(p)+" 信用点；可先领取对应保障。查看技能页会明确切换整备职业，试用不更改正式配置。",new Rect(x+24,575,w-48,56),17,Muted);
            GUI.enabled=enabled&&owned&&string.IsNullOrEmpty(p.activeRun)&&!TrialSessionActive;
            if(Button(owned?"查看替换差异":"先查看缺项商品或领取保障",new Rect(x+24,653,w-48,50),owned))routeConfirm=true;
            GUI.enabled=enabled;
            Text("适用：第5章枪线、第8章分支选择、第10章断援与第12章威胁配合；配装不替代第7章搬锚规划。",new Rect(x+24,726,w-48,48),17,Muted);
        }
    }
}
