# v0.6.0 验证记录

本节记录原 RC09 基线 Windows Release，使用 Unity 6000.3.25f1。同一 RC09 构建的九套原生检查全部通过，Routes 覆盖完整 12 关；构建前后源输入相同。原始报告位于 `Logs/V06/rc09-final`，交付时应与对应 `build-identity.json`、`source-manifest.json` 一起保存。

- 源输入 SHA-256：`3b0fc4b066ae876a607f250c7728d21ea3cd0eb8c97789a752c0634f5090abda`
- `Assembly-CSharp.dll` SHA-256：`b270d968698286b3090ff2a395eae67b2e2d22d415c62670211cab34340c6329`

这是工作源码的哈希身份，不表示已经提交或推送到 Git 远端。

## 独立源码与作者复现

RC08 从不含 `.git` 和既有 `Library` 的独立 495 文件快照首次构建成功。RC09 保留该副本自行生成的 Library，仅更新 `ExpeditionWorld.cs`、`ExpeditionLayouts.cs` 两个文件后再构建；它是独立副本上的迭代，不是第二次无缓存冷构建。两次原始身份、快照清单及精确差异保留在交付的“工程实验与复现”资料中。

另一独立作者副本实际执行 Node/Blender 生成：31 个 GLB，保留 17 份 `.blend`（历史 lineup 未冒称重新生成）；节点/轴向合同 163 项通过，Blender 导出合同修正坐标系后 175 项通过，另有 5 个正反例。随后 Unity Prepare 导入 15 个 Three.js 与 16 个 Blender 模型，203 份 `.meta` 字节及原 GUID 不变，652 项静态检查通过。此副本有两份地形代码差异，只作为作者生成/导入证据。

普通 [Build](../Tools/Build-Demo-v06.ps1) 使用已有源资产，不调用 Prepare；[Prepare](../Tools/Prepare-Demo-v06.ps1) 是明确写入场景/材质/配置的单独步骤。源码 ZIP 的逐项回读与独立解压由 [Verify-Package](../Tools/Verify-Package-v06.ps1) 执行，解压成功本身不等于完成 Unity 构建。

## 最终二进制九套结果

| 套件 | 检查/尝试记录 | 证据范围 |
|---|---:|---|
| Core | 44 / 通过 | 换位原因码、双端局部场、预备终检、任务物伤害与锚点；受控规则夹具。 |
| Missions | 227 / 通过 | 真实任务/部署/取消/结算集成，包含控制目标状态与位置注入；不是实战通关。 |
| Routes | 297 / 通过 | 全 12 关；虚拟键鼠驱动真实 PlayerMotor，普通行走、不冲刺，耗尽跳板后的保底路。明确清敌前提；收集、换位和近战通过生产流程达成目标，10、11 关保留活敌，12 关夹具击败 Boss。 |
| Storage | 27 / 通过 | 全新隔离目录；原子写失败、主档/备份损坏与迁移，复用 CampaignDataQA；不触碰玩家档。 |
| Gunplay | 285 / 通过 | 原生换弹、机构与模型状态；本次后台报告跳过截图，未进行视觉审美判定。 |
| Visual | 80 / 通过 | URP 离屏相机截图 24 张；抑制 AI、明确设定视点，不覆盖完整前台 HUD/菜单交互。 |
| BossEscape | 9 / 通过 | 8 个起点 + 站立伤害对照；慢配装、固定等待 0.5 秒后由脚本输入冲刺，真实位移/生命判定；提前选路，不等于真人反应实验。 |
| Pool | 26 / 通过 | 生产 Bolt 复用、碰撞/过期、上限溢出与离章清理；不直接测量 FPS 或字节。 |
| Lifecycle | 124 / 通过 | 第 11 章 2 次预热 + 8 次重开；跟踪自有对象、材质、池和 NavMeshData；不强制 GC/UnloadUnusedAssets。 |

Routes 的 `allTwelveCovered=true`，逐章编号为 1–12；三关代表样本不能替代这一门槛。所有检查均在原生 Windows 玩家中后台执行，旧 Review/Flow 不替代 v0.6 任务规则。报告和截图不是初见真人体验结果。

## 独立优化与资源实验

固定夹具 `v06-bolt-spawn-allocation-v2-gcalloc-sync`：同一仪器源码、种子 60031、2 批预热及 8×64 次测量。下表来自两个 Development 构建，不与最终 Release 门禁混合。

| 实测指标 | Baseline05 | After07 |
|---|---:|---:|
| 成功 Spawn 调用 | 512 | 512 |
| 测量段新建 Bolt 根对象 | 512 | 0 |
| 同步 GC.Alloc 事件 | 6144 | 0 |
| 空循环对照事件 | 0 | 0 |
| 分配字节 | 不可用 | 不可用 |
| 程序集 SHA-256 前 12 位 | `62db0a1456ff` | `4194b4a5cd42` |

正分配探针检验了事件计数可用性；该环境字节计数不可用，不将 `-1` 写成零字节。After 每批最多观察到 64 个 inactive 根对象，累计 512 是多批求和。结论限定为同步 Spawn 范围的对象复用及分配事件，不声称整个游戏零分配或 FPS 提升。

同章重开实验最初观察到 NavMeshData 增量 **+8**；为运行时生成数据补上 [RuntimeNavigationOwner](../Game/Assets/_SwapHunter/Scripts/RuntimeNavigationOwner.cs) 后，独立 After08 实验为 **0**。释放发生在真正销毁时，先取消异步更新，经典世界临时禁用仍保留导航数据。最终 RC09 Lifecycle 也记录 NavMeshData 增量 **0**，Material/Mesh/Texture 增量分别为 **0/0/0**；全局计数属于观测，自有对象必须真实销毁，不设置任意“全局零增长即通过”规则。

## 已定位并修复的实际问题

- **原生初始化**：后台不能无条件等待 `SplashScreen.isFinished`；Unity API 不在报告字段初始化器调用，改在 `Awake` 读取版本。见 [CoreV06QA](../Game/Assets/_SwapHunter/Scripts/CoreV06QA.cs)、[MissionV06QA](../Game/Assets/_SwapHunter/Scripts/MissionV06QA.cs)。
- **路线几何**：实际 PlayerMotor 测试暴露第 3、9 关的路点/地形问题；修正路径与几何后，全 12 关再验通过。见 [RouteV06QA](../Game/Assets/_SwapHunter/Scripts/RouteV06QA.cs)、[ExpeditionLayouts](../Game/Assets/_SwapHunter/Scripts/ExpeditionLayouts.cs)。
- **作者坐标**：前后瞄点分属不同父节点，局部坐标不可直接比较。验证器改为组合父层级变换后比较同一坐标系，保留 `1e-5` 阈值和真实错位反例；不是放宽模型合同。见 [validate_exports.py](../Tools/BlenderAuthoring/validate_exports.py)。
- **设置恢复**：补齐 `InvalidDataException` 恢复分支；临时文件 flush 后原子替换，验证备份、保留损坏原件，写失败不伪报成功。长路径 QA 夹具改用新建短隔离根，生产保证未放宽。见 [GameOptions](../Game/Assets/_SwapHunter/Scripts/GameOptions.cs)、[StorageV06QA](../Game/Assets/_SwapHunter/Scripts/StorageV06QA.cs)。

## 重跑

在项目根执行；需要 Unity 6000.3.25f1、Windows 构建模块与 Python 3.10+。将 Unity 路径换成本机安装位置。输出路径每次使用新目录，源码及证据路径宜短。

```powershell
.\Tools\Check-Project-v06.ps1 -PythonPath python -ReportPath 'Logs/V06/static-new.json'
.\Tools\Build-Demo-v06.ps1 -EditorPath 'C:\Program Files\Unity\Hub\Editor\6000.3.25f1\Editor\Unity.exe' -OutputDirectory 'Builds/SwapHunter-v0.6.0-recheck'
.\Tools\Validate-Demo-v06.ps1 -ExecutablePath 'Builds/SwapHunter-v0.6.0-recheck/SwapHunter.exe' -Suite Core,Missions,Routes,Storage,Gunplay,Visual,BossEscape,Pool,Lifecycle -OutputDirectory 'Logs/V06/recheck-new' -CaseTimeoutSeconds 1800
```

完整路线不传 `-Levels`；开发时 `-Levels 2,6,7` 只代表三关。作者再生成流程见 [Three.js 作者说明](../Tools/ArtAuthoring/README.md)与 [Blender 作者说明](../Tools/BlenderAuthoring/README.md)，需要重建入口场景时才显式运行 Prepare。包体核验使用上方 Verify-Package，目标目录须不存在且与原源码/输出分离。

## 尚未完成的体验与诊断

前台完整 UI/输入流程、前台 FPS/帧时间与跨机器性能、6–8 名初见真人形成性测试仍未完成，不能由后台逻辑或离屏截图替代。运行结束时的部分 GraphicsBuffer/ComputeBuffer/JobTempAlloc 警告尚未逐一归因；NavMeshData 所有权修复与释放观测不等于这些退出警告已全部解决。玩法与工程边界另见[试玩指南](playguide.zh-CN.md)和[架构说明](architecture.zh-CN.md)。

## 2026-10-07 武器展示增量

当前源码新增武器图标、模型预览及数字属性条；原 RC09 结果只对应上文固定身份，不当作本增量的完整九套结果。新的 Development 候选已实际构建，WeaponLibraryQA 在 `Logs/WeaponUI/native01` 通过105项检查并生成12张图标、6张大模型图。覆盖真实资源对应、基础参数/配置覆盖、旋转复位、重复选择缓存、浏览不改配装/弹药，以及离页和关闭背包的自有资源释放。已查看上述实际渲染图；完整IMGUI排版与鼠标操作另需前台检查。

本次没有改武器平衡值；保留任务开始时已有的URP与质量设置。`-swapHunterWeaponLibraryQA` 使用隔离档，后台不伪报完整GUI截图。

最终武器展示增量的独立Release位于 `Builds/SwapHunter-v0.6.0-weapon-library`，`Logs/WeaponUI/final` 的105项针对性检查再次通过（12图标+6大图），源SHA为 `ed17c06547daa74f237aedf00026f0bdc6071802f11c0fe6b7113619f4ecd6ff`，程序集SHA为 `5b3aadb6aea56c862cf827ede5066b585ae4cbd6559fac7b510c33bb7a00c00a`。它不是旧RC09程序集，旧ZIP继续保留。完整界面前台检查仍未执行。
