using System.Collections.Generic;
using UnityEngine;
namespace SwapHunter
{
    // Exact v2 authored budgets. The prepared MissionConfiguration asset remains the runtime source.
    public static class MapV2MissionDefinitions
    {
        static EncounterRole[] C(EnemyKind kind,int count,bool required=false,bool fixedPost=false,bool firstGrenade=false)
        {var result=new EncounterRole[count];for(int i=0;i<count;i++)result[i]=new EncounterRole(kind,required,fixedPost,firstGrenade);return result;}
        static EncounterRole[] R(params EncounterRole[][] batches)
        {var roles=new List<EncounterRole>();foreach(var batch in batches)roles.AddRange(batch);return roles.ToArray();}
        public static MissionDefinition[] Create()=>new[]
        {
            // Chapter 1
            new MissionDefinition
            {
                level=0, requiredObjectives=0, minimumSwaps=1, maximumEscorts=0, liveLimit=8, miniEscortLimit=0,
                instruction="安全区完成一次双向换位，再解决侧台实战守卫；击败本章指定小Boss后撤离",
                extraction="前方校准出口",
                bonusRisk="入门章无额外数据",
                objectives=new ObjectiveDefinition[0],
                groups=new[]
                {
                    new EncounterGroup
                    {
                        id="calibration", label="五处独立校准站", trigger=EncounterTrigger.Start, warningSeconds=2.6f,
                        roles=R(C(EnemyKind.Training, 5))
                    },
                    new EncounterGroup
                    {
                        id="combat", label="侧台实战守卫", trigger=EncounterTrigger.Region, warningSeconds=2.6f,
                        region="combat",
                        roles=R(C(EnemyKind.Assault, 1 ,required:true), C(EnemyKind.Assault, 1))
                    },
                    new EncounterGroup
                    {
                        id="hall", label="监理前厅巡查", trigger=EncounterTrigger.Region, warningSeconds=2.6f,
                        region="hall",
                        roles=R(C(EnemyKind.Assault, 3))
                    },
                    new EncounterGroup
                    {
                        id="mini-boss", label="B01 指定威胁", trigger=EncounterTrigger.MiniBoss, warningSeconds=2.6f,
                        region="miniboss", miniBossId="B01",
                        roles=R(C(EnemyKind.Elite, 1 ,required:true))
                    },
                }
            },
            // Chapter 2
            new MissionDefinition
            {
                level=1, requiredObjectives=0, minimumSwaps=0, maximumEscorts=0, liveLimit=8, miniEscortLimit=2,
                instruction="穿过两段盾线，解决两名指定盾卫；侧向突击兵可留在场内；击败本章指定小Boss后撤离",
                extraction="后方货检闸口",
                bonusRisk="引导章无额外数据",
                objectives=new ObjectiveDefinition[0],
                groups=new[]
                {
                    new EncounterGroup
                    {
                        id="first", label="第一检查街区", trigger=EncounterTrigger.Region, warningSeconds=2.6f,
                        region="first",
                        roles=R(C(EnemyKind.Shield, 1 ,required:true), C(EnemyKind.Shield, 1), C(EnemyKind.Assault, 3))
                    },
                    new EncounterGroup
                    {
                        id="second", label="第二检查街区", trigger=EncounterTrigger.Region, warningSeconds=2.6f,
                        region="second",
                        roles=R(C(EnemyKind.Shield, 1 ,required:true), C(EnemyKind.Shield, 1), C(EnemyKind.Assault, 4))
                    },
                    new EncounterGroup
                    {
                        id="rear", label="执法官后场", trigger=EncounterTrigger.Region, warningSeconds=2.6f,
                        region="miniboss", miniEscort=true,
                        roles=R(C(EnemyKind.Shield, 2), C(EnemyKind.Assault, 2))
                    },
                    new EncounterGroup
                    {
                        id="mini-boss", label="B02 指定威胁", trigger=EncounterTrigger.MiniBoss, warningSeconds=2.6f,
                        region="miniboss", miniBossId="B02",
                        roles=R(C(EnemyKind.Elite, 1 ,required:true))
                    },
                }
            },
            // Chapter 3
            new MissionDefinition
            {
                level=2, requiredObjectives=1, minimumSwaps=0, maximumEscorts=0, liveLimit=8, miniEscortLimit=2,
                instruction="取得上层通行件，再从上层通道离开；无需清空各层敌人；击败本章指定小Boss后撤离",
                extraction="上层通行出口",
                bonusRisk="引导章无额外数据",
                objectives=new[] { new ObjectiveDefinition("a", "上层通行件") },
                groups=new[]
                {
                    new EncounterGroup
                    {
                        id="lower", label="井底冷却区", trigger=EncounterTrigger.Start, warningSeconds=2.6f,
                        roles=R(C(EnemyKind.Assault, 3), C(EnemyKind.Leaper, 1))
                    },
                    new EncounterGroup
                    {
                        id="middle", label="中层检修环", trigger=EncounterTrigger.Region, warningSeconds=2.6f,
                        region="middle",
                        roles=R(C(EnemyKind.Assault, 2), C(EnemyKind.Leaper, 2), C(EnemyKind.Sniper, 2, fixedPost:true))
                    },
                    new EncounterGroup
                    {
                        id="upper", label="上层通行区", trigger=EncounterTrigger.Region, warningSeconds=2.6f,
                        region="upper", miniEscort=true,
                        roles=R(C(EnemyKind.Assault, 1), C(EnemyKind.Leaper, 3), C(EnemyKind.Sniper, 1, fixedPost:true))
                    },
                    new EncounterGroup
                    {
                        id="mini-boss", label="B03 指定威胁", trigger=EncounterTrigger.MiniBoss, warningSeconds=2.6f,
                        region="miniboss", miniBossId="B03",
                        roles=R(C(EnemyKind.Elite, 1 ,required:true))
                    },
                }
            },
            // Chapter 4
            new MissionDefinition
            {
                level=3, requiredObjectives=1, minimumSwaps=0, maximumEscorts=0, liveLimit=12, miniEscortLimit=2,
                instruction="取得核心凭据，中央捷径开放后前往对侧撤离；无需全清；击败本章指定小Boss后撤离",
                extraction="对侧撤离口",
                bonusRisk="暴露侧台：绕路与截击射线",
                objectives=new[] { new ObjectiveDefinition("a", "核心凭据") },
                groups=new[]
                {
                    new EncounterGroup
                    {
                        id="initial", label="前站台", trigger=EncounterTrigger.Start, warningSeconds=2.6f,
                        roles=R(C(EnemyKind.Assault, 3), C(EnemyKind.Shield, 2), C(EnemyKind.BarrierGunner, 1))
                    },
                    new EncounterGroup
                    {
                        id="credential", label="凭据作业区", trigger=EncounterTrigger.Region, warningSeconds=2.6f,
                        region="credential",
                        roles=R(C(EnemyKind.Assault, 3), C(EnemyKind.BarrierGunner, 3))
                    },
                    new EncounterGroup
                    {
                        id="intercept", label="撤离侧真实入口", trigger=EncounterTrigger.Objective, warningSeconds=2.6f,
                        objective="a", miniEscort=true,
                        roles=R(C(EnemyKind.Assault, 2), C(EnemyKind.Shield, 2), C(EnemyKind.BarrierGunner, 2), C(EnemyKind.Engineer, 2))
                    },
                    new EncounterGroup
                    {
                        id="mini-boss", label="B04 指定威胁", trigger=EncounterTrigger.MiniBoss, warningSeconds=2.6f,
                        region="miniboss", miniBossId="B04",
                        roles=R(C(EnemyKind.Elite, 1 ,required:true))
                    },
                }
            },
            // Chapter 5
            new MissionDefinition
            {
                level=4, requiredObjectives=2, minimumSwaps=0, maximumEscorts=0, liveLimit=12, miniEscortLimit=3,
                instruction="夺取左右火控节点，分别打开对应遮蔽通路；击败本章指定小Boss后撤离",
                extraction="中央货运出口",
                bonusRisk="塔侧观察位：另一座塔的暴露射线",
                objectives=new[] { new ObjectiveDefinition("a", "左塔火控"), new ObjectiveDefinition("b", "右塔火控") },
                groups=new[]
                {
                    new EncounterGroup
                    {
                        id="left", label="左雷达塔与塔根", trigger=EncounterTrigger.Region, warningSeconds=2.6f,
                        region="left",
                        roles=R(C(EnemyKind.Sniper, 1, fixedPost:true), C(EnemyKind.Assault, 1), C(EnemyKind.Sniper, 2, fixedPost:true), C(EnemyKind.Assault, 2), C(EnemyKind.Engineer, 1), C(EnemyKind.BarrierGunner, 1))
                    },
                    new EncounterGroup
                    {
                        id="right", label="右阵列塔与塔根", trigger=EncounterTrigger.Region, warningSeconds=2.6f,
                        region="right",
                        roles=R(C(EnemyKind.Sniper, 3, fixedPost:true), C(EnemyKind.Assault, 3), C(EnemyKind.BarrierGunner, 2))
                    },
                    new EncounterGroup
                    {
                        id="guard", label="中央调度场", trigger=EncounterTrigger.Region, warningSeconds=2.6f,
                        region="guard", miniEscort=true,
                        roles=R(C(EnemyKind.Shield, 4), C(EnemyKind.BarrierGunner, 3), C(EnemyKind.Engineer, 1))
                    },
                    new EncounterGroup
                    {
                        id="reinforce", label="首节点响应", trigger=EncounterTrigger.ObjectiveCount, warningSeconds=2.6f,
                        objectiveCount=1,
                        roles=R(C(EnemyKind.Assault, 2), C(EnemyKind.Engineer, 1), C(EnemyKind.ScoutDrone, 3))
                    },
                    new EncounterGroup
                    {
                        id="mini-boss", label="B05 指定威胁", trigger=EncounterTrigger.MiniBoss, warningSeconds=2.6f,
                        region="miniboss", miniBossId="B05",
                        roles=R(C(EnemyKind.Elite, 1 ,required:true))
                    },
                }
            },
            // Chapter 6
            new MissionDefinition
            {
                level=5, requiredObjectives=2, minimumSwaps=0, maximumEscorts=0, liveLimit=12, miniEscortLimit=3,
                instruction="用枪械、真实爆炸或近战摧毁两处封锁装置；击败本章指定小Boss后撤离",
                extraction="破开的货舱出口",
                bonusRisk="侧仓：额外交战和弹药支出",
                objectives=new[] { new ObjectiveDefinition("a", "封锁装置 A"), new ObjectiveDefinition("b", "封锁装置 B") },
                groups=new[]
                {
                    new EncounterGroup
                    {
                        id="first", label="投雷观察区", trigger=EncounterTrigger.Start, warningSeconds=2.6f,
                        roles=R(C(EnemyKind.Assault, 4), C(EnemyKind.Mortar, 2), C(EnemyKind.Engineer, 2))
                    },
                    new EncounterGroup
                    {
                        id="second", label="A装置后管廊", trigger=EncounterTrigger.Objective, warningSeconds=2.6f,
                        objective="a",
                        roles=R(C(EnemyKind.Assault, 4), C(EnemyKind.Breacher, 3), C(EnemyKind.Mortar, 1), C(EnemyKind.Shield, 2))
                    },
                    new EncounterGroup
                    {
                        id="third", label="B装置与炉区", trigger=EncounterTrigger.Objective, warningSeconds=2.6f,
                        objective="b", miniEscort=true,
                        roles=R(C(EnemyKind.Breacher, 3), C(EnemyKind.Mortar, 3), C(EnemyKind.Shield, 2))
                    },
                    new EncounterGroup
                    {
                        id="side", label="侧仓支路", trigger=EncounterTrigger.Region, warningSeconds=2.6f,
                        region="side",
                        roles=R(C(EnemyKind.Engineer, 2), C(EnemyKind.BarrierGunner, 2))
                    },
                    new EncounterGroup
                    {
                        id="mini-boss", label="B06 指定威胁", trigger=EncounterTrigger.MiniBoss, warningSeconds=2.6f,
                        region="miniboss", miniBossId="B06",
                        roles=R(C(EnemyKind.Elite, 1 ,required:true))
                    },
                }
            },
            // Chapter 7
            new MissionDefinition
            {
                level=6, requiredObjectives=3, minimumSwaps=0, maximumEscorts=0, liveLimit=14, miniEscortLimit=3,
                instruction="把任务锚点送入三处接入座；接入锁存，可使用维护复位；击败本章指定小Boss后撤离",
                extraction="C 岛出口",
                bonusRisk="C 岛侧支线：更长返程与暴露",
                objectives=new[] { new ObjectiveDefinition("a", "A 岛接入"), new ObjectiveDefinition("b", "B 岛接入"), new ObjectiveDefinition("c", "C 岛接入") },
                groups=new[]
                {
                    new EncounterGroup
                    {
                        id="island-a", label="A岛起始桥口", trigger=EncounterTrigger.Region, warningSeconds=2.6f,
                        region="island-a",
                        roles=R(C(EnemyKind.Assault, 3), C(EnemyKind.BarrierGunner, 1), C(EnemyKind.Leaper, 1), C(EnemyKind.Tether, 1), C(EnemyKind.Engineer, 1))
                    },
                    new EncounterGroup
                    {
                        id="island-b", label="B岛接入区", trigger=EncounterTrigger.Region, warningSeconds=2.6f,
                        region="island-b",
                        roles=R(C(EnemyKind.Assault, 2), C(EnemyKind.Sniper, 2, fixedPost:true), C(EnemyKind.BarrierGunner, 2), C(EnemyKind.Tether, 2))
                    },
                    new EncounterGroup
                    {
                        id="island-c", label="C岛接入区", trigger=EncounterTrigger.Region, warningSeconds=2.6f,
                        region="island-c",
                        roles=R(C(EnemyKind.Assault, 2), C(EnemyKind.Sniper, 2, fixedPost:true), C(EnemyKind.Leaper, 1), C(EnemyKind.Tether, 1))
                    },
                    new EncounterGroup
                    {
                        id="reinforce", label="双接入响应", trigger=EncounterTrigger.ObjectiveCount, warningSeconds=2.6f,
                        objectiveCount=2, miniEscort=true,
                        roles=R(C(EnemyKind.BarrierGunner, 1), C(EnemyKind.Leaper, 2), C(EnemyKind.Engineer, 1))
                    },
                    new EncounterGroup
                    {
                        id="mini-boss", label="B07 指定威胁", trigger=EncounterTrigger.MiniBoss, warningSeconds=2.6f,
                        region="miniboss", miniBossId="B07",
                        roles=R(C(EnemyKind.Elite, 1 ,required:true))
                    },
                }
            },
            // Chapter 8
            new MissionDefinition
            {
                level=7, requiredObjectives=2, minimumSwaps=0, maximumEscorts=0, liveLimit=14, miniEscortLimit=3,
                instruction="A 窄廊、B 长线、C 折返：取任意两匣即可撤离，第三匣 +40；击败本章指定小Boss后撤离",
                extraction="仓区汇合出口",
                bonusRisk="第三分支：额外路线与独立交战",
                objectives=new[] { new ObjectiveDefinition("a", "A 仓数据"), new ObjectiveDefinition("b", "B 仓数据"), new ObjectiveDefinition("c", "C 仓数据") },
                groups=new[]
                {
                    new EncounterGroup
                    {
                        id="warehouse-a", label="A仓入库缓冲", trigger=EncounterTrigger.Region, warningSeconds=2.6f,
                        region="a",
                        roles=R(C(EnemyKind.Assault, 3), C(EnemyKind.Shield, 5), C(EnemyKind.Breacher, 2))
                    },
                    new EncounterGroup
                    {
                        id="warehouse-b", label="B仓恒温长线", trigger=EncounterTrigger.Region, warningSeconds=2.6f,
                        region="b",
                        roles=R(C(EnemyKind.Assault, 3), C(EnemyKind.Sniper, 3, fixedPost:true), C(EnemyKind.BarrierGunner, 3), C(EnemyKind.Engineer, 1))
                    },
                    new EncounterGroup
                    {
                        id="warehouse-c", label="C仓高架分拣", trigger=EncounterTrigger.Region, warningSeconds=2.6f,
                        region="c",
                        roles=R(C(EnemyKind.Assault, 2), C(EnemyKind.Leaper, 4), C(EnemyKind.Breacher, 1), C(EnemyKind.Engineer, 1), C(EnemyKind.ScoutDrone, 2))
                    },
                    new EncounterGroup
                    {
                        id="mini-boss", label="B08 指定威胁", trigger=EncounterTrigger.MiniBoss, warningSeconds=2.6f,
                        region="miniboss", miniBossId="B08",
                        roles=R(C(EnemyKind.Elite, 1 ,required:true))
                    },
                }
            },
            // Chapter 9
            new MissionDefinition
            {
                level=8, requiredObjectives=1, minimumSwaps=0, maximumEscorts=0, liveLimit=16, miniEscortLimit=3,
                instruction="取得顶层核心，沿永久楼梯或换位路线下撤；上层敌人可遗留；击败本章指定小Boss后撤离",
                extraction="地面货运出口",
                bonusRisk="顶层侧路：额外暴露和下撤距离",
                objectives=new[] { new ObjectiveDefinition("a", "顶层核心") },
                groups=new[]
                {
                    new EncounterGroup
                    {
                        id="top", label="塔顶取件区", trigger=EncounterTrigger.Start, warningSeconds=2.6f,
                        roles=R(C(EnemyKind.Assault, 3), C(EnemyKind.Sniper, 3, fixedPost:true), C(EnemyKind.BarrierGunner, 2), C(EnemyKind.ScoutDrone, 1), C(EnemyKind.Leaper, 1))
                    },
                    new EncounterGroup
                    {
                        id="middle", label="上中层下撤", trigger=EncounterTrigger.Region, warningSeconds=2.6f,
                        region="middle", objective="a",
                        roles=R(C(EnemyKind.Assault, 3), C(EnemyKind.Sniper, 2, fixedPost:true), C(EnemyKind.Leaper, 4), C(EnemyKind.ScoutDrone, 2), C(EnemyKind.Suppressor, 1))
                    },
                    new EncounterGroup
                    {
                        id="lower-middle", label="下中层下撤", trigger=EncounterTrigger.Region, warningSeconds=2.6f,
                        region="lower-middle", objective="a",
                        roles=R(C(EnemyKind.Assault, 4), C(EnemyKind.Sniper, 2, fixedPost:true), C(EnemyKind.Leaper, 2), C(EnemyKind.ScoutDrone, 2), C(EnemyKind.Suppressor, 2))
                    },
                    new EncounterGroup
                    {
                        id="bottom", label="地面回收区", trigger=EncounterTrigger.Region, warningSeconds=2.6f,
                        region="bottom", objective="a", miniEscort=true,
                        roles=R(C(EnemyKind.Sniper, 1, fixedPost:true), C(EnemyKind.Leaper, 1), C(EnemyKind.ScoutDrone, 1), C(EnemyKind.Suppressor, 1), C(EnemyKind.BarrierGunner, 2))
                    },
                    new EncounterGroup
                    {
                        id="mini-boss", label="B09 指定威胁", trigger=EncounterTrigger.MiniBoss, warningSeconds=2.6f,
                        region="miniboss", miniBossId="B09",
                        roles=R(C(EnemyKind.Elite, 1 ,required:true))
                    },
                }
            },
            // Chapter 10
            new MissionDefinition
            {
                level=9, requiredObjectives=3, minimumSwaps=0, maximumEscorts=0, liveLimit=18, miniEscortLimit=3,
                instruction="关闭三个控制站，取消对应入口尚未落地的预算；活敌不会消失；击败本章指定小Boss后撤离",
                extraction="控制站货运出口",
                bonusRisk="已关闭站点后方仓库：折返与资源消耗",
                objectives=new[] { new ObjectiveDefinition("a", "左控制站"), new ObjectiveDefinition("b", "右控制站"), new ObjectiveDefinition("c", "后控制站") },
                groups=new[]
                {
                    new EncounterGroup
                    {
                        id="initial", label="运输场驻守", trigger=EncounterTrigger.Start, warningSeconds=2.6f,
                        roles=R(C(EnemyKind.Assault, 4), C(EnemyKind.Shield, 2), C(EnemyKind.BarrierGunner, 2), C(EnemyKind.Breacher, 1), C(EnemyKind.Mortar, 1), C(EnemyKind.Engineer, 2), C(EnemyKind.ScoutDrone, 1), C(EnemyKind.Elite, 2))
                    },
                    new EncounterGroup
                    {
                        id="left", label="左入口有限增援", trigger=EncounterTrigger.RegionElapsed, warningSeconds=4f,
                        region="left", cancelOnObjective="a", triggerSeconds=16,
                        roles=R(C(EnemyKind.Assault, 3), C(EnemyKind.BarrierGunner, 1), C(EnemyKind.Breacher, 2), C(EnemyKind.Mortar, 2), C(EnemyKind.ScoutDrone, 1), C(EnemyKind.Suppressor, 1))
                    },
                    new EncounterGroup
                    {
                        id="right", label="右入口有限增援", trigger=EncounterTrigger.RegionElapsed, warningSeconds=4f,
                        region="right", cancelOnObjective="b", triggerSeconds=16,
                        roles=R(C(EnemyKind.Assault, 2), C(EnemyKind.Shield, 2), C(EnemyKind.BarrierGunner, 2), C(EnemyKind.Mortar, 1), C(EnemyKind.Engineer, 2), C(EnemyKind.Suppressor, 1))
                    },
                    new EncounterGroup
                    {
                        id="rear", label="后入口有限增援", trigger=EncounterTrigger.RegionElapsed, warningSeconds=4f,
                        region="rear", cancelOnObjective="c", triggerSeconds=16,
                        roles=R(C(EnemyKind.Assault, 1), C(EnemyKind.Shield, 1), C(EnemyKind.BarrierGunner, 1), C(EnemyKind.Breacher, 1), C(EnemyKind.Mortar, 2), C(EnemyKind.ScoutDrone, 2), C(EnemyKind.Suppressor, 2))
                    },
                    new EncounterGroup
                    {
                        id="mini-boss", label="B10 指定威胁", trigger=EncounterTrigger.MiniBoss, warningSeconds=2.6f,
                        region="miniboss", miniBossId="B10",
                        roles=R(C(EnemyKind.Elite, 1 ,required:true))
                    },
                }
            },
            // Chapter 11
            new MissionDefinition
            {
                level=10, requiredObjectives=1, minimumSwaps=0, maximumEscorts=0, liveLimit=18, miniEscortLimit=3,
                instruction="从外环取得核心权限并撤离；两处局部干扰器可分别关闭，无需全部关闭；击败本章指定小Boss后撤离",
                extraction="安全外环出口",
                bonusRisk="非必经干扰区：额外渗透风险",
                objectives=new[] { new ObjectiveDefinition("a", "核心权限") },
                groups=new[]
                {
                    new EncounterGroup
                    {
                        id="outer", label="安全外环", trigger=EncounterTrigger.Start, warningSeconds=2.6f,
                        roles=R(C(EnemyKind.Assault, 4), C(EnemyKind.Shield, 2), C(EnemyKind.BarrierGunner, 2), C(EnemyKind.Sniper, 1, fixedPost:true), C(EnemyKind.Tether, 1))
                    },
                    new EncounterGroup
                    {
                        id="left", label="左实验区", trigger=EncounterTrigger.Region, warningSeconds=2.6f,
                        region="left",
                        roles=R(C(EnemyKind.Assault, 2), C(EnemyKind.Shield, 2), C(EnemyKind.Jammer, 2), C(EnemyKind.Leaper, 2), C(EnemyKind.Engineer, 2))
                    },
                    new EncounterGroup
                    {
                        id="right", label="右实验区", trigger=EncounterTrigger.Region, warningSeconds=2.6f,
                        region="right",
                        roles=R(C(EnemyKind.Assault, 2), C(EnemyKind.Sniper, 3, fixedPost:true), C(EnemyKind.Jammer, 2), C(EnemyKind.Tether, 2), C(EnemyKind.Engineer, 1))
                    },
                    new EncounterGroup
                    {
                        id="core", label="权限后段", trigger=EncounterTrigger.Region, warningSeconds=2.6f,
                        region="core", objective="a", miniEscort=true,
                        roles=R(C(EnemyKind.Leaper, 2), C(EnemyKind.Jammer, 2), C(EnemyKind.Tether, 1))
                    },
                    new EncounterGroup
                    {
                        id="mini-boss", label="B11 指定威胁", trigger=EncounterTrigger.MiniBoss, warningSeconds=2.6f,
                        region="miniboss", miniBossId="B11",
                        roles=R(C(EnemyKind.Elite, 1 ,required:true))
                    },
                }
            },
            // Chapter 12
            new MissionDefinition
            {
                level=11, requiredObjectives=3, minimumSwaps=0, maximumEscorts=4, liveLimit=18, miniEscortLimit=2,
                instruction="取得双高台授权和地面核心，击败指挥官后从对侧离开；随从无需全清；击败本章指定小Boss后撤离",
                extraction="对侧货运出口",
                bonusRisk="侧台观察点：战斗中额外停留与暴露",
                objectives=new[] { new ObjectiveDefinition("a", "左高台授权"), new ObjectiveDefinition("b", "右高台授权"), new ObjectiveDefinition("c", "地面核心") },
                groups=new[]
                {
                    new EncounterGroup
                    {
                        id="antechamber", label="前置小Boss控制区", trigger=EncounterTrigger.Region, warningSeconds=2.6f,
                        region="miniboss", miniEscort=true,
                        roles=R(C(EnemyKind.Assault, 2), C(EnemyKind.Shield, 2), C(EnemyKind.BarrierGunner, 1), C(EnemyKind.Breacher, 1), C(EnemyKind.Jammer, 2), C(EnemyKind.Engineer, 1), C(EnemyKind.Elite, 1))
                    },
                    new EncounterGroup
                    {
                        id="phase-one", label="最终Boss就绪段", trigger=EncounterTrigger.BossReady, warningSeconds=2.6f,
                        escort=true,
                        roles=R(C(EnemyKind.Assault, 2), C(EnemyKind.Sniper, 1, fixedPost:true), C(EnemyKind.BarrierGunner, 2), C(EnemyKind.Tether, 1))
                    },
                    new EncounterGroup
                    {
                        id="phase-two", label="最终Boss步枪段", trigger=EncounterTrigger.BossRifle, warningSeconds=2.6f,
                        escort=true,
                        roles=R(C(EnemyKind.Assault, 1), C(EnemyKind.Sniper, 1, fixedPost:true), C(EnemyKind.Breacher, 1), C(EnemyKind.Jammer, 1), C(EnemyKind.Tether, 1), C(EnemyKind.Engineer, 1), C(EnemyKind.Suppressor, 1))
                    },
                    new EncounterGroup
                    {
                        id="phase-three", label="最终Boss组合段", trigger=EncounterTrigger.BossCombined, warningSeconds=2.6f,
                        escort=true,
                        roles=R(C(EnemyKind.Assault, 1), C(EnemyKind.Shield, 1), C(EnemyKind.Sniper, 1, fixedPost:true), C(EnemyKind.BarrierGunner, 1), C(EnemyKind.Breacher, 1), C(EnemyKind.Elite, 1), C(EnemyKind.Suppressor, 1))
                    },
                    new EncounterGroup
                    {
                        id="mini-boss", label="B12 指定威胁", trigger=EncounterTrigger.MiniBoss, warningSeconds=2.6f,
                        region="miniboss", miniBossId="B12",
                        roles=R(C(EnemyKind.Elite, 1 ,required:true))
                    },
                    new EncounterGroup
                    {
                        id="boss", label="封锁指挥官", trigger=EncounterTrigger.Region, warningSeconds=2.6f,
                        region="final", afterDefeated="mini-boss",
                        roles=R(C(EnemyKind.Elite, 1 ,required:true))
                    },
                }
            },
        };
    }
}
