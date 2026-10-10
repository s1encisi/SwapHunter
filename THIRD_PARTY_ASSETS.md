# 实际采用的第三方资源

精确文件、取得日期、来源链接与 SHA-256 见 THIRD_PARTY_ASSETS.json；转换细节见 Tools/VisualAuthoring。原始模型保留，不将外部作者作品表述为原创。

| 实际采用项 | 许可依据 | 当前用途 |
|---|---|---|
| Quaternius Animated Characters Pack | 作者 OpenGameArt 页面声明 CC0；归档没有单独许可文件，保存页面来源说明及 CC0 法律文本 | 四职业身体、普通敌人及派生双臂；人物保留17段真实动作 |
| Quaternius Animated Mech / George | 作者目录 CC0 许可及实际样本 | 已接入 Boss；20段源动作 |
| Quaternius Ultimate Gun Pack | 作者 OpenGameArt 页面声明 CC0；归档没有单独许可文件，保存来源说明及 CC0 文本 | 新十二枪的主要枪体与 Bayonet 刀体 |
| Kenney Blaster Kit 2.1 | 原包 License.txt，CC0 | 科幻枪组件及原纹理 |
| Quaternius Modular Sci-Fi Environments | 原作者目录许可和 OpenGameArt 原包许可，CC0 | 新增10种物件，继续使用已取得的5种新环境模块，覆盖全部章节及经典/教学区域 |
| Kenney Particle Pack | 原包 CC0；保留作者与附加贡献者 | Boss 施放提示的3张原始透明 PNG |
| CoplayDev Unity MCP v10.3.0 | Dependencies 中的上游 MIT 及 Tommy MIT | 固定提交的本地 UPM 包，用于源码复现 |

George 在上一版只是候选；当前已转为实际使用。旧候选记录保留在 JSON 的 evaluationHistory 中。本次角色身体、手臂和枪体读取 VisualV2，旧项目资产保留供追溯。

项目负责资源选择、米制及坐标转换、材质适配、骨骼与游戏状态绑定、持枪/装填触点、模型预览及物理代理绑定。护盾网格为项目新制的程序网格；游戏判定圈、弹道和灯条属于程序反馈几何。AI参与代码和转换工具，用户负责需求与设计决策。

导入、运行和画面的实际证据见 docs/models-final.zh-CN.md 及对应交付包。素材文件存在与许可清楚本身不代替运行验收。
