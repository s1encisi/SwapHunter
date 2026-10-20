# 归航行动当前版本说明

本页记录 2026 年 10 月 20 日已实现的系统状态。当前本地验证构建为 `SwapHunter-Refined-20261020-04`；应用基础版本号沿用 `0.6.0`。GitHub 的历史 `v0.6.0` Release 与当前源码属于不同阶段。

## 系统范围

- **行动结构**：12 章，第 1 章为剧情教学，第 12 章为最终 Boss 战；每章配置对应小 Boss。撤离由主目标、指定威胁和出口交互共同决定，不要求清空全部普通敌人。
- **换位**：50 米三维范围、0.08 秒准备、3.5 秒成功冷却。双端检查视线、落点体积和干扰；保留双方生命、装备及朝向。
- **职业与装备**：4 职业、8 项职业技能、2 种通用技能、12 枪、6 刀；主武器、副武器、近战按 1—3 槽统一装配。技能与通用工具支持三级成长。
- **动作衔接**：换位准备暂停装填，成功后保留阶段进度；已转移弹药保留。泵动霰弹逐发装填，单次补给动作完成时才提交消耗与效果。
- **补给与成长**：医疗、弹药、护甲三类道具，两槽各最多 3 件，可同类叠加；成功按实际消耗扣库存，失败重试保留出发快照，采购记录不回滚。
- **交互与剧情**：三项主菜单、下拉式离散设置、统一预览和 HUD。31 项动作可改绑，包含剧情推进和通讯记录；归航档案提供完整故事文本。
- **敌人行为**：14 种原型及 B01—B12。根据视线、枪声和有限位置共享建立记忆，执行搜索、追击、掩体与侧翼调整；护盾和已感知危险参与射击与路径决策。部署检查可达性、净空、单位间距及预警。

## 首尾章节

第 1 章将移动、射击、装填、换位、职业技能、道具、侧台实战和小 Boss 撤离串成八段学习流程。第 12 章形成前厅、补给准备、观察窗和交战核心；两侧授权各可一次削减最多 90 点 Boss 当前能量，后续未出手的普通攻势保留缓冲，已承诺的空间攻击继续执行。

最终 Boss 当前为 3600 生命、360 能量，生命降至 1800 时进入混合阶段。第 2—11 章保留既有 MapV2 地图结构，承接剧情及全局交互、敌人行为更新。

## 独立全内容预览

在由当前源码构建的玩家目录运行：

```powershell
.\SwapHunter.exe -swapHunterTacticalDemo -demoAllUnlocked -demoLevel 1 -demoMenu
```

该模式创建独立临时进度，开放章节、故事档案、武器、技能和六刀。预置通关标记用于内容浏览，不生成最佳时间或真实交易收据；键位设置单独保存，普通成长进度不被覆盖。

## 验证证据

检查随改动分组运行，结果保留其真实构建归属。表中数字是检查项数，不是玩家人数或真人通关次数。

| 构建 | 范围 | 结果 |
|---|---|---|
| Refined 02 | 刀具装配、真实输入、道具、教学及故事结构 | 69 项通过 |
| Refined 02 | 第 1、12 章路线、跳板及配装可达性 | 43 项通过 |
| Refined 02 | 十二枪切换、持枪与真实换弹 | 65 项通过；36 张动作截图 |
| Refined 03 | 最终界面与预览资源 | 14 项通过；界面截图已检查 |
| Refined 03 | 敌人感知、追击、弹丸伤害、护盾和避险 | 28 项通过 |
| Refined 03 | 双管枪折管部件跟随修复 | 10 项通过；关键图已检查 |
| Refined 04 | 通讯键位迁移及真实改绑输入 | 12 项通过；全内容启动自检通过 |

Refined 03 的受控 AI 场景使用实际第 12 章地图与 NavMesh。一次绕掩体调查产生约 10.5 米位移；一次交火中发射 5 发、3 发物理命中，造成 18 点玩家伤害；危险回避最终距危险中心约 6.42 米。夹具提高玩家生命并抑制其他演员，以分离观测对象，没有直接改敌方生命或伪造击杀。

Refined 04 仅在 03 基础上调整通讯键位、对应剧情输入及验收脚本，未将其他检查写作在 04 重跑。现有证据覆盖确定的规则与运行路径，不构成十二章真人完整通关、总体平衡或人工听感结论。下拉展开画面和键盘选择已验证，物理鼠标点击展开尚未完成实测。

### 复现入口

```powershell
.\SwapHunter.exe -swapHunterOctoberQA -octoberGroup all -qaOutput 'Logs/October/review'
.\SwapHunter.exe -swapHunterOctoberCombatQA -qaOutput 'Logs/October/combat'
.\SwapHunter.exe -swapHunterOctoberQA -octoberGroup maps -qaOutput 'Logs/October/maps'
.\SwapHunter.exe -swapHunterOctoberQA -octoberGroup firearms -qaOutput 'Logs/October/firearms'
.\SwapHunter.exe -swapHunterOctoberQA -octoberGroup storykeys -qaOutput 'Logs/October/storykeys'
```

截图检查需使用正常渲染窗口；隐藏窗口可能不产生有效的 IMGUI 截图。每次检查使用新的证据目录，验证档与普通成长档隔离。

核心脚本：[OctoberRefinementQA](../Game/Assets/_SwapHunter/Scripts/OctoberRefinementQA.cs)、[OctoberCombatQA](../Game/Assets/_SwapHunter/Scripts/OctoberCombatQA.cs)、[OctoberWeaponMotionChecks](../Game/Assets/_SwapHunter/Scripts/OctoberWeaponMotionChecks.cs)。音效复现方式及来源见 [AudioAuthoring](../Tools/AudioAuthoring/README.md)，全部资源归属见[素材台账](../THIRD_PARTY_ASSETS.md)。
