# 原创 Blender 资产

Unity 使用工程内已有 GLB，普通构建不需要安装 Blender。作者脚本、可编辑 `.blend` 与生成结果应一起保留。

- `generate_v04.py` 提供十把武器、角色手臂和四种机器人的作者函数。
- `generate_combat_v05.py` 复用同目录函数生成相位刀。
- `blend/` 保存可编辑原稿；`weapon-lineup.blend` 是历史展示场景，并不代表最新全部武器。
- 运行时模型位于 `Game/Assets/_SwapHunter/Art/BlenderModels/`；基础两枪由相邻 Three.js 作者工具生成。

## 选择性生成和保留引用

在独立工程副本中使用 Blender 后台模式。生成器会覆盖被选中的作者结果；例如 `generate_v04.py -- --only weapon_05 --render` 只选择该资产。只把已检查的候选同步回主工程，保留 Unity `.meta` 和 GUID，不要无理由重建全部模型。特殊枪的独立铰链、供弹盖、弹链和装填弹体属于动作合同，不能只检查外形。

几何单位为米，作者 X 右/Y 上/Z 前映射到 Blender `(x,-z,y)`。功能节点与瞄准/枪口合同须保留；Unity 的玩法碰撞体独立于视觉网格。

`python Tools/BlenderAuthoring/validate_exports.py` 读取真实 GLB 的头、轴向、功能节点、三角形数量及可编辑原稿。它会写本地 `asset-contract-report.json`；先保存旧报告。该检查覆盖 15 资产清单，额外相位刀由 `combat-v05-manifest.json` 描述并由 Unity 集成检查验证。

## 工程准备与构建

从仓库根目录运行 `python Tools/check_project_v06.py` 先检查依赖、配置、GUID 和基础节点。重新生成前后比较相关 `.meta` 哈希，保证引用身份不变。

需刷新模型库/材质/入口场景时显式运行 `Tools/Prepare-Demo-v06.ps1`。它会写入源资产与 ProjectSettings，应审阅差异；它不会启动 Blender，也不会生成新的作者模型。普通 `Tools/Build-Demo-v06.ps1` 使用现有场景、模型库与设置，不再自动准备资产，并拒绝覆盖已有玩家目录。

原创几何与作者脚本使用根 MIT 许可。移动原稿后应重新选择本地预览输出目录；预览只用于作者检查，不能代替原生画面或性能证据。
