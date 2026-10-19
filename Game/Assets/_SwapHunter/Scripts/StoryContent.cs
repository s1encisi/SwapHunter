using System;
using System.Collections.Generic;
namespace SwapHunter
{
    // Text only; runtime owns dispatch, deduplication and successful settlement.
    public sealed class StoryLine
    {
        public readonly string key, speaker, text;
        public StoryLine(string key, string speaker, string text)
        { this.key=key; this.speaker=speaker; this.text=text; }
    }
    public sealed class StoryChapter
    {
        public readonly string subtitle, brief, ending;
        public readonly StoryLine[] lines;
        public StoryChapter(string subtitle,string brief,string ending,params StoryLine[] lines)
        { this.subtitle=subtitle; this.brief=brief; this.ending=ending; this.lines=lines; }
    }
    public static class StoryContent
    {
        public const string Title="归航行动";
        public const string Premise="岬湾港宣布完成疏散，却仍有三百一十二人困在下层。前相位救援员林越穿过十二段封锁，找回三年前的真实记录，重新打开被关闭的归途。";
        public const string ArchiveTitle="归航行动 · 行动档案";
        public const string ReplayLabel="行动复盘";
        public const string FrameworkNote="四种职业是林越的四套作业框架配置；身份不变，共享装备库存。";
        public const string CreditNote="信用点是互助站的物资调拨额度；武器上架后仍需采购。";
        static readonly StoryLine[] None=Array.Empty<StoryLine>();
        static StoryLine L(string key,string speaker,string text)=>new StoryLine(key,speaker,text);
        // Repeated keys denote lines in one dialogue event. Stable chapter IDs: 0..11.
        public static readonly StoryChapter[] Chapters={
            new StoryChapter("被注销的救援员",
                "在外侧检修站完成装置校准，处理侧台守卫，从前方出口进入货检通道。",
                "相位装置正常。救援身份已注销，安保系统仍将你列为事故责任人。",
                L("start","沈乔","这里还是旧检修站。先完成一次换位，再走侧台通道。"),
                L("swap","沈乔","回头看，训练机到了你原来的位置。交换成功。"),
                L("swap","港区终端","身份：林越。救援授权已注销，限制进入。"),
                L("ready","沈乔","你的设备没问题。失效的是身份。"),
                L("ready","林越","设备还能用就够了。")),
            new StoryChapter("有效命令",
                "突破两道盾线，解除货检闸口封锁。交换前确认自己的源位置与侧向枪线。",
                "闸口已通过。封锁来自有效命令，周砺已经知道你回来了。",
                L("start","林越","盾上的救援涂装还在。现在却把路封住了。"),
                L("first-guard","沈乔","维护记录没有入侵痕迹。它们执行的是有效命令，签名是周砺。"),
                L("first-guard","周砺","林越，交出装置，退出封控区。我可以安排重新审查。"),
                L("ready","周砺","你知道擅自行动会害死多少人。"),
                L("ready","林越","三年前我等过命令。")),
            new StoryChapter("灯还亮着",
                "取得上层通行件，从检修高台前往档案站。楼梯与低通道仍可通行，无需清空守卫。",
                "上层线路接通。被宣布清空的下层仍有灯光与求救信号。",
                L("start","林越","“回程走外梯。”许宁写的。旁边还有新的维修标记。"),
                L("region-upper","林越","下面那些灯在反复亮灭。不是故障，是有人在打信号。"),
                L("objective-a","求救频道","七号下层……不要断电……"),
                L("objective-a","沈乔","正式目录里，那一层是空的。我接着听。")),
            new StoryChapter("三百一十二人",
                "带回调度凭据。中央捷径开放后从对侧撤离，不必消灭所有截击者。",
                "凭据确认：清港目录之外，仍有三百一十二人被困。你决定返回。",
                L("start","沈乔","旧档案站到了。拿到本地凭据，我们就能核对清港清单。"),
                L("objective-a","夜班班长","纸上点过三遍，三百一十二人。照明在掉，运输门打不开！"),
                L("objective-a","沈乔","这四个避难点，全被清港目录标成了无人区。"),
                L("ready","老韩","对侧有人接应。把凭据带出来，不用追着机器人打。")),
            new StoryChapter("给船让路",
                "取得左右通路授权，逐段开放船队与工人的撤离通路。中继支援狙击，可保全回收；独立维护站提供掩护或有限补给。",
                "货运通路开放。救援船开始接近，第一批人员正在撤离。",
                L("start","老韩","船在遮蔽区等着。每拿下一座塔，我们就多一段能走的路。"),
                L("objective-a","老韩","左塔这段能走了，左线船队开始接人。"),
                L("objective-b","老韩","右塔这段放行了，右线撤离队可以往前走。"),
                L("ready","林越","两段都通了。按现在看到的路走。"),
                L("ready","老韩","收到。第一批在登船。")),
            new StoryChapter("被焊死的退路",
                "用实际伤害破坏两处锁闭装置，打开被封存的应急卸载支路。射击、爆炸与近战都有效。",
                "应急线恢复接入条件。三年前未启用的路线，今天重新有了机会。",
                L("start","沈乔","应急卸载线还在。他们把控制口封住了。这两处外加锁闭可以拆。"),
                L("objective-a","沈乔","A 处锁闭已解除，旧支路的第一段重新接通。"),
                L("objective-b","沈乔","B 处锁闭已解除，检修端口恢复。"),
                L("ready","沈乔","两处都解除了。下一步把三个继电座接上，这条线才能工作。"),
                L("ready","林越","那就把它接回来。")),
            new StoryChapter("有人回答",
                "搬运锚点完成三处继电接入，恢复通风、照明与救援通路。顺序自由，锁存后可移走锚点。",
                "下层生命维持暂时稳定。无线电里有人回答：他们还在等待。",
                L("objective-a","沈乔","通风支路锁存。锚点可以带走，线路会自己维持。"),
                L("objective-b","夜班班长","应急灯亮了！我们看得见出口标记了。"),
                L("objective-c","沈乔","救援通路锁存，接入座开始自行供能。"),
                L("ready","夜班班长","泵响了……对，我们听见泵响了。"),
                L("ready","避难点内","门边别挤，孩子先到里面等。")),
            new StoryChapter("签字的人",
                "三个仓区的核心记录互为备份，任意两匣即可恢复关键报告。第三匣只补充班组附件。",
                "完整时间线恢复。撤离桥先被断电，林越的操作记录后被调换。",
                L("start","沈乔","任意两匣就够。关键时间线都有交叉备份，不用把三条路全走一遍。"),
                L("data-two","沈乔","恢复出来了。撤离桥先断电，你的相位操作在后。报告调换了日志顺序。"),
                L("data-two","沈乔","“未登记区域按无载荷处理”……审批链里有我的签字。"),
                L("data-three","沈乔","第三匣是班组交接附件。把它带出去，我们能多知道一点许宁当晚的事。"),
                L("ready","林越","先把完整记录带出去。下面的人还在等。")),
            new StoryChapter("从这里看下去",
                "从顶层带回原始控制核，沿永久楼梯或换位路线向地面撤离；上层活敌不必清空。",
                "安全停机方案得到验证。先断援，再取维护授权，最后分离两侧支路。",
                L("start","老韩","往下看。双塔间的是我们的船，继电岛下亮着应急灯。前面的路没有白走。"),
                L("objective-a","沈乔","控制核验证通过：先断持续部署，再取维护授权，最后分离城侧和港侧。"),
                L("objective-a","周砺","停主港的损失，手动切换失败的风险，你算过吗？"),
                L("ready","林越","算过。所以我们先把退路接上了。"),
                L("ready","老韩","带核心往地面来。别为了高台耽误撤离。")),
            new StoryChapter("不让下一批进场",
                "关闭三个部署控制站，取消尚未落地的增援。接近入口后才启动部署倒计时；站内补给柜提供有限保障。",
                "后续部署已切断。救援队开始转移最后几个避难点。",
                L("start","老韩","前线投送受限，带来的道具还在。先断控制站，别跟有限的待投送清单耗下去。"),
                L("ready","老韩","三个控制站都停了，我们能过这条线。"),
                L("ready","林越","正在过去。别等我清场。")),
            new StoryChapter("留给检修工的门",
                "穿过局部干扰与外环通路，取得本地维护钥匙后撤离。两处干扰器独立，无需全部关闭。",
                "本地维护权限到手。周砺已进入核心区，最后的操作需要现场完成。",
                L("start","沈乔","检修路线还在。干扰器可以分别关，也可以绕行，目标是拿到本地维护钥匙。"),
                L("objective-a","沈乔","中央通道被切了。我已转到救援专线，城侧由我维持。"),
                L("ready","沈乔","检修口一直给现场的人留着。最后两处确认，得你自己到。"),
                L("ready","林越","这回留住了。")),
            new StoryChapter("把出口留出来",
                "先通过门卫控制厅，再取得双侧授权、接管地面核心，击败封港指挥官并撤离。授权顺序自由，战斗与交互可交错。",
                "清港停止，独立救援支路保全。最后一批已经上船，现在等你归航。",
                L("start","周砺","记录带走。你的处分，我可以撤。清港不能停。"),
                L("start","林越","那三百一十二个人呢？我不是来谈处分的。"),
                L("objective-a","沈乔","城侧独立确认已记录。"),
                L("objective-b","沈乔","港侧受控卸载确认已记录。"),
                L("objective-c","沈乔","本地停机控制核已保存，可用于受控停机。"),
                L("boss-down","沈乔","执令模块停止重发。已入场守卫还在，核对双侧授权和控制核。"),
                L("ready","老韩","受控停机已提交。门开了，最后一批已经上船。现在等你。"))
        };
        public static StoryLine[] Event(int level,string key)
        {
            if(level<0||level>=Chapters.Length||string.IsNullOrEmpty(key))return None;
            var matches=new List<StoryLine>();
            foreach(var line in Chapters[level].lines)if(line.key==key)matches.Add(line);
            return matches.Count==0?None:matches.ToArray();
        }
        public static readonly StoryLine[] Prologue={
            L("prologue","港口广播","岬湾港已完成全员疏散。请勿进入封控区域。"),
            L("prologue","归航行动","三年前，七号泊位撤离桥提前断电。林越用相位装置逃出，被教官周砺救走；许宁留在另一侧，没有回来。公开报告却把林越列为事故责任人。"),
            L("prologue","互助站补给船","今夜，环海工业准备再次执行清港协议。林越检查相位装置。柜门里的旧合照下，补写着许宁的名字。"),
            L("prologue","相位救援装置","相位技术交换两个已定位对象的位置。港区机器人沿用工业接口，仍可成为合法目标；林越的四种职业，是同一名救援员的四套作业配置。"),
            L("prologue","沈乔","找到旧调度凭据，从对侧服务口回来。我的代码也在事故系统里，我需要知道当时究竟执行了什么。"),
            L("prologue","老韩","这次先把东西带回来。"),
            L("prologue","林越","人呢？"),
            L("prologue","老韩","广播说没有人了。")
        };
        static readonly StoryLine[] AfterFour={
            L("interlude-04","互助站整备台","凭据已经带回，最初的委托完成了。返航方向仍在屏幕上，林越把那份写着三百一十二人的记录重新放回任务台。"),
            L("interlude-04","老韩","你已经回来了。"),
            L("interlude-04","林越","下面的人还没有。"),
            L("interlude-04","林越","你去接船。我去开路。"),
            L("interlude-04","行动更新","先解除双塔火控，让救援船接近；再打开应急支路，恢复下层的通风与照明。")
        };
        static readonly StoryLine[] AfterEight={
            L("interlude-08","原始时间线","撤离桥断电命令早于林越的相位操作。公开报告调换了两段日志，让一次逃生看起来像事故起因。"),
            L("interlude-08","沈乔","“未登记区域按无载荷处理”，这条默认规则是我签的。我没去下面核实。"),
            L("interlude-08","林越","把完整的记录留着。"),
            L("interlude-08","沈乔","我的名字也在里面。"),
            L("interlude-08","林越","那就一起留着。"),
            L("interlude-08","行动更新","下一步取回高塔原始控制核。证据已经足以说明旧案，但安全停机仍需要核对本地设备记录。")
        };
        // Complete outcome is available without optional collection.
        public static readonly StoryLine[] Epilogue={
            L("epilogue","指挥广场","重型抢修战甲被击毁，周砺死于战斗。他曾用这套救援技术带林越离开险境，如今却坚持封港命令。林越使用已经恢复的支路和维护权限完成停机。"),
            L("epilogue","清港停止","城侧基础供给转入独立支路，集中调度和外侧卸载命令被中止。主港进入人工停运。生活区、检修通道与工作船的灯仍亮着。"),
            L("epilogue","完整记录","沈乔将原始控制核、修改记录和签字链送往港外多个保管点。她把自己的说明附在最后，没有删除自己的名字。"),
            L("epilogue","补给船","老韩合上手写名单：三百一十二名下层滞留者全部完成转移。他又划掉外勤栏中，林越名字旁的“未归”。"),
            L("epilogue","老韩","到齐了。"),
            L("epilogue","七号泊位","船经过旧址，断桥仍在。许宁当晚留在桥边组织最后一组工人撤离，已经无法生还。林越把旧工牌夹进完整名单，没有把它留在海里。"),
            L("epilogue","数周之后","停港维修与重新安置仍在进行。环海工业公开事故资料，相关决策进入调查。林越的相位误操作指控被撤销；沈乔提交全部技术记录，接受责任调查。"),
            L("epilogue","重新打开的路","林越继续参与独立救援。短桥对侧的检修台被临时封住，他与合法锚点交换过去，打开机械通道。没有人需要再等一次许可。"),
            L("epilogue","林越","路通了。慢点走。"),
            L("epilogue","归航通道，请勿封闭","归航行动 · 完")
        };
        public static readonly StoryLine[] OptionalRecording={
            L("optional-xuning","班组附件 · 许宁","桥边还有最后一组。我留着，等他们到外梯再跟你交班。"),
            L("optional-xuning","班组附件 · 许宁","下次回程箭头画大点。新来的老是只顾着看对面。"),
            L("optional-xuning","附件说明","第八章第三数据匣的班组交接记录。它补充许宁的工作与选择，不改变主线真相或救援结局。")
        };
        public static StoryLine[] Interlude(int completedLevel)
        {
            switch(completedLevel)
            {
                case 3:return AfterFour;
                case 7:return AfterEight;
                case 11:return Epilogue;
                default:return None;
            }
        }
    }
}
