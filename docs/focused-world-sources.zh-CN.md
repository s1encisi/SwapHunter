# 第 1／12 关空间精修与环境来源

本次只替换第 1 关和第 12 关的建图入口，使用独立的 FocusedWorld／FocusEnvironment 实现。第 2—11 关地图、所有任务 ID、编组预算及任务配置保持原样。两关不沿用上一版“20 倍面积”目标，优先让战斗距离、观察位置和侧路作用清楚。

## 两关空间安排

- 第 1 关：出生处可见刀桌与第一条短射线；射击、持刀、Q 交换、高差校准后进入实战区。校准闸门沿用 `$calibrated`，监理及最终出口沿用原任务规则。设备库和巡查室墙体隔开连续遭遇，侧台、右侧通道与监理前机组提供实际接近路线。
- 第 12 关：保留前厅门卫、双授权 `a/b`、地面核心 `c` 与最终 Boss。前厅之后先到补给和防弹观察窗，再从两侧进入约 58 × 56 米的核心战区。左机房屋顶和墙体提供遮蔽，代价是绕行与较低射角；右管廊提供 5 米高位，代价是上楼段暴露。取得各自授权后打开本侧中门／后维护口。三座双入口检修台提供离开地面危险的实际高差，机组间保留横移和撤退路线。
- 两关都使用连续站场基础、围墙、道路与海岸周边；港区、山体、海面是远景，不能计入可玩路径。主要战斗位置、掩体、台阶、机组和任务平台有实体碰撞。相机远裁剪保持现有 400 米。

## 免费素材与许可

2026-10-19 下载并保存原始来源及逐文件 SHA-256；实际使用文件位于 `Game/Assets/_SwapHunter/Resources/FocusEnvironment/`，机器可读记录为同目录 `sources.json`。

| 内容 | 原始来源 | 使用方式 |
|---|---|---|
| 混凝土 | [ambientCG Concrete034](https://ambientcg.com/view?id=Concrete034) | 1K 色彩、OpenGL 法线、粗糙度；站场基础、墙体和楼梯 |
| 沥青 | [ambientCG Asphalt012](https://ambientcg.com/view?id=Asphalt012) | 1K 三贴图；站场道路和港区联络路 |
| 金属板 | [ambientCG MetalPlates006](https://ambientcg.com/view?id=MetalPlates006) | 1K 三贴图；平台、设备外壳、管廊和标记边缘 |
| 云天空 | [Poly Haven Kloofendal 48d Partly Cloudy Pure Sky](https://polyhaven.com/a/kloofendal_48d_partly_cloudy_puresky) | 2K HDR；Greg Zaal、Jarod Guest，实际天空全景 |

上述素材均为 CC0；许可原文：[ambientCG](https://docs.ambientcg.com/license/)、[Poly Haven](https://polyhaven.com/license)。空间几何、场站组合和 Shader 为本项目代码。地面／墙体用世界坐标三向投影按实际米数重复，避免把一张纹理拉满巨大平板。独立环境组件在退出两关时恢复全局天空和雾设置。

## 构建验收入口

`FocusedArenaDefinition` 暴露两关 `cameras`（每项含 `feet` 和 `lookAt`）、`observation`、`preparation` 与 `combatCore`，可供实际相机验收直接读取。第 1 关预设教学入口、刀具、相位高差、监理接近 4 个视角；第 12 关预设前厅、观察台、核心、左右高位、后侧退路 6 个视角。

运行验收应核实：两关实际材质与天空、刀具取得、校准闸门、任务节点及出口的永久可达路径、第 12 关三组跳板的起落点支撑与真实轨迹，以及前厅至最终战的原任务顺序。本说明记录设计和素材接入，不替代对应构建的实机结果。
本轮原生验收：Focused-02 的两关路线、跳板与32组一级无道具配置可达检查通过，原生视图保存在 Logs/Focused/polish-02。观察窗标牌已侧移缩小，真实材质、天空及场景视线已在游戏画面中查看。该记录不等同于真人通关。
