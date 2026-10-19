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

## D2/N1 本次实际使用的音乐与音效

核验日期：2026-10-19。以下六组均由作者原始发布页或 Kenney 官网明确标注 CC0 1.0，允许随游戏与源码再分发。原发布页、CC0 法律文本、Kenney 包内许可和逐文件处理/哈希台账保存在 `Game/Assets/StreamingAssets/ThirdParty/AudioN1/`。

| 实际采用项 | 原始来源与作者 | 当前用途 |
|---|---|---|
| Singularity calm / action | [vitalezzz 原发布页](https://opengameart.org/content/singularity-0) | 同曲两版同步切换；菜单/整备/探索使用 calm，交火与 Boss 使用 action |
| Vaporware | [The Cynic Project 原发布页](https://opengameart.org/content/calm-piano-1-vaporware) | 归航结局配乐；保留作者署名 The Cynic Project / cynicmusic.com / pixelsphere.org |
| Sea and river wave sounds | [RandomMind 原发布页](https://opengameart.org/content/sea-and-river-wave-sounds) | 原作者维斯瓦河实录选段，经首尾交叠得到58秒水岸环境循环 |
| Sci-fi Sounds | [Kenney 官网](https://kenney.nl/assets/sci-fi-sounds) | 相位/护盾、枪响、爆炸、机械状态和无线电提示音 |
| Interface Sounds | [Kenney 官网](https://kenney.nl/assets/interface-sounds) | 确认、错误、点击、返回 |
| Impact Sounds | [Kenney 官网](https://kenney.nl/assets/impact-sounds) | 金属命中与混凝土脚步 |

共使用3首配乐、17个精选音效和1段环境循环，导入目录为 `Game/Assets/_SwapHunter/Resources/Audio/N1/`。三首曲目映射五种音乐状态，未另行声称有五首独立曲目。音乐保留立体声，短音效转单声道并按用途剪裁和降幅；爆炸由同一CC0素材包两段混合。无线电采用字幕与提示音，没有中文人物配音。游戏还保留适合特定动作的程序合成音，不将全部音效归为外部素材。

这些音频的 CC0 许可不改变项目原创代码、剧情与配置的许可；保留作者署名不代表作者为项目背书。
