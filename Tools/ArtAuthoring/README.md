# 原创 Three.js 资产

普通 Unity 构建使用仓库内已有的 GLB，不需要 Node.js。只有修改作者几何时才运行下述生成命令。

## 在副本中生成

本目录使用 Node.js 和锁定的 Three.js 0.186.1。先执行 `npm ci --ignore-scripts`；`npm run build` 调用 `generate-assets.mjs`，会改写工程中的 `Game/Assets/_SwapHunter/Art/ThreeModels/`，因此实验应在独立副本中进行。

v0.5.2 引入的 `tactical-weapons-v052.mjs` 是主生成器必需的本地模块。`generate-base-weapons-v052.mjs` 只生成卡宾和泵动枪，必须指定一个新的输出目录；`base-weapons-v052-manifest.json` 保留作者检查记录。这三个文件均属于源码交付。

```powershell
node generate-base-weapons-v052.mjs --output-dir ../../work/base-weapons-candidate01
```

专用生成器拒绝覆盖已有同名 GLB。检查候选后才将需要更新的 GLB 复制到工程，保留原 `.meta` 文件与 GUID。不要用批量再生成覆盖未修改的作者资产。

## 节点与导入

GLB 使用米制，保留枪口、瞄准、供弹机构与轴向标记。基础枪的 `Muzzle`、`AimRear`、`AimFront`、`magazine`、`charging_handle` 是运行时合同；装饰几何不参与换位和导航碰撞体。

从仓库根目录运行 `python Tools/check_project_v06.py`，可检查本地模块闭合、锁定包、模型基础节点、Unity `.meta` 和 GUID。它不执行生成器，也不替代 Unity 导入与枪械原生检查。重新生成前后另保存相关 `.meta` 的 SHA256；哈希应保持一致。

新 GLB 经审阅后，显式运行 `Tools/Prepare-Demo-v06.ps1` 以重建模型库、材质及入口场景，检查源文件差异，再运行 `Tools/Build-Demo-v06.ps1`。普通 Build 不再执行这些准备操作。Prepare 不运行 Three.js 或 Blender，且会重写源资产及场景；不要用它代替一般构建。

生成器只读取本项目的数值几何描述。代码和原创几何遵循根 MIT 许可，Three.js 使用其 MIT 许可；作者预览不是 Unity 实机画面。
