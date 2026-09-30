# 换位猎手 / SwapHunter

**以“交换双方位置”为核心机制的离线单人 FPS 原型。** 玩家保留各自朝向、生命与装备，通过换位改变掩体、高低差和交叉火力关系，再配合射击、定向护盾与可召回飞刀完成行动。

项目将空间战斗机制扩展为可游玩的教学、章节与成长循环，同时实现碰撞校验、敌人行为、存档结算、程序化资产导入和原生玩家构建验证。当前源码版本为 **v0.5.2**。

## 可以体验什么

- **空间换位战斗**：支持地面和空中换位；执行前检查视线、双方占用空间及目标附属碰撞体，导航移动失败时回滚双方位置。
- **12 关相位行动与 12 把武器**：双武器装备、不同开火机制、配件取舍、专精成长、章节间补给，以及主目标、可选数据与撤离结算。
- **战术工具与敌人应对**：定向 A 盾、近战、投刀与召回；敌人包含推进、探角和预警部署逻辑，终局 Boss 使用分阶段空间技能。
- **可操作的教学与设置**：分步实战教学、键盘/鼠标菜单导航、按键重绑、显示及音量设置；旧战役、合约与试招入口继续保留。
- **武器表现与原创资产**：按供弹结构区分装填动作，取消装填会恢复部件状态；Three.js / Blender 生成模型，经 GLB 导入 Unity，游戏碰撞体与装饰网格分开。

![SwapHunter 实机画面](docs/media/gameplay.png)

*仓库已有的 v0.3 实机截图，展示换位战斗场景；v0.5.2 的武器模型与界面已有更新。*

## 值得阅读的实现

| 设计 | 实现入口 | 解决的问题 |
|---|---|---|
| 换位先校验、执行前复核、失败回滚 | [PlayerMotor.cs](Game/Assets/_SwapHunter/Scripts/PlayerMotor.cs) | 避免换位准备期间目标变化、双方穿入障碍或导航失败造成位置不一致 |
| 章节状态与幂等结算 | [ExpeditionData.cs](Game/Assets/_SwapHunter/Scripts/ExpeditionData.cs)、[CampaignData.cs](Game/Assets/_SwapHunter/Scripts/CampaignData.cs) | 使用 runId 与结算收据阻止重复发奖；先落盘后更新内存，保留损坏存档和可恢复备份 |
| 数据与表现分离 | [WeaponCatalog.cs](Game/Assets/_SwapHunter/Scripts/WeaponCatalog.cs)、[PlayerWeaponPresentation.cs](Game/Assets/_SwapHunter/Scripts/PlayerWeaponPresentation.cs) | 武器数值/开火机制与供弹部件动画分别管理，弹药在装填完成时结算 |
| 原生运行验证与存档隔离 | [RunStorage.cs](Game/Assets/_SwapHunter/Scripts/RunStorage.cs)、[验证脚本](Tools/Validate-Demo-v052.ps1) | QA 使用独立目录和检查点，不写入玩家进度；结果输出为 JSON 和日志 |
| 可再生成的美术资产 | [Three.js 工具](Tools/ArtAuthoring/README.md)、[Blender 工具](Tools/BlenderAuthoring/README.md) | 保留生成器、原始模型、GLB 和 Unity 引用，检查轴向、几何及功能节点 |

运行入口是 `DemoGame`，主要流程分布在 `ExpeditionRun`、`DemoCampaign`、`TutorialCourse`；场景和敌人由 `WorldBuilder`、`ExpeditionWorld` 及对应行为组件组织。

## 从源码运行

需要 **Unity 6000.3.25f1** 与 Windows 构建支持。项目使用 URP 17.3.0、Input System 1.14.2、AI Navigation 2.0.9 和 glTFast 6.15.0，依赖锁定在 `Game/Packages/`。首次打开需要获取 Unity 包。

1. 用 Unity Hub 添加仓库中的 `Game` 项目。
2. 打开 `Assets/_SwapHunter/Scenes/SwapHunter.unity` 并进入 Play Mode，或在仓库根目录构建 Windows 玩家：

```powershell
$env:SWAPHUNTER_UNITY_EDITOR = 'C:\Program Files\Unity\Hub\Editor\6000.3.25f1\Editor\Unity.exe'
.\Tools\Build-Demo-v052.ps1
```

输出为 `Builds/SwapHunter-v0.5.2/SwapHunter.exe`。运行时保留同目录的 `SwapHunter_Data`、`UnityPlayer.dll` 与 `MonoBleedingEdge`。已导入模型随源码提供，普通构建不需要安装 Node.js 或 Blender；重新生成资产时再使用对应工具说明。

首次游玩建议选择“逐步实战教学”，再进入“相位行动”。

| 默认按键 | 功能 |
|---|---|
| WASD / 鼠标 | 移动 / 观察 |
| 左键 / 右键 / R | 开火 / 瞄准 / 装填 |
| Q / C | 相位换位 / 定向 A 盾 |
| V / G / X | 近战 / 投刀 / 召回（需对应研究） |
| Space / E / Esc | 跳跃或翻越 / 交互 / 暂停与设置 |
| 1、2 / 滚轮 | 切换已装备武器 |

## 验证

构建后可运行后台检查：

```powershell
.\Tools\Validate-Demo-v052.ps1 -Suite Gunplay
.\Tools\Validate-Demo-v052.ps1 -Suite Review
.\Tools\Validate-Demo-v052.ps1 -Suite Flow
```

Gunplay 覆盖装填部件、取消恢复、弹药结算及弹丸呈现；Review 覆盖核心机制、系统、关卡与存储；Flow 覆盖教学/行动流程和追兵撤离。报告默认保存在 `Logs/V052/`。脚本默认使用 Background 模式；路线画面和性能套件需要另选 Foreground 模式。

本次源码整理后已完成 Windows 原生构建及后台检查（2026-09-30）：Gunplay: 285 checks; Review: 100 checks; Flow: 15 checks，退出码均为 0。

自动检查用于验证程序行为。主观枪感、首次游玩体验与跨硬件性能仍需实际试玩；后台结果不作为画面或帧率测量。

## 许可证与资源

原创代码、程序化几何与合成音效使用 [MIT License](LICENSE)。字体、Unity 包和 Three.js 等组件遵循各自许可证，见 [第三方声明](THIRD_PARTY_NOTICES.md)。项目包含 AI 辅助编写的代码及原创生成素材；菜单概念图属于插画，本文展示的图片为游戏实机截图。
