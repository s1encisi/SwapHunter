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

## 2026-10-20 枪械音效与命中反馈

本次新增 88 段 48 kHz、16-bit、单声道音效，位于 `Game/Assets/_SwapHunter/Resources/Audio/Refinement/`：12 把武器各有 3 个射击变体、4 个装填／切枪事件，共 84 段；另有 4 段项目程序合成的躯干、肢体、头部命中及爆头击杀反馈。

| 采用内容 | 来源与许可 | 处理及使用 |
|---|---|---|
| cg1、pistol、rifle、shotgun 枪响 | [Michel Baradari / apollo-music.de 原发布页](https://opengameart.org/content/chaingun-pistol-rifle-shotgun-shots)，[CC BY 3.0](https://creativecommons.org/licenses/by/3.0/) | 切分、变调、均衡、分层、短反射和归一化，生成 36 段射击变体；部分能量武器另叠加项目合成声层 |
| gunreload1、assaultriflereload1、shotguncock | [SpringySpringo 原发布页](https://opengameart.org/content/gun-reload-sounds)，[CC0 1.0](https://creativecommons.org/publicdomain/zero/1.0/) | 气枪装填录音按移除、装入、上膛和切枪事件切分，生成 48 段动作音效 |

原始录音保存在 `Tools/AudioAuthoring/audio_sources/`，源文件与当前输出的 SHA-256 已纳入 JSON 台账。作者、原链接、许可及修改说明保存在 `Game/Assets/_SwapHunter/Resources/Audio/Refinement/CREDITS.txt`。发布构建时随包保留该说明；CC BY 3.0 枪响及其改编输出不改用项目 MIT 许可。

生成工具、锁定依赖与部署步骤见 [音频制作说明](Tools/AudioAuthoring/README.md)。2026-10-20 在独立工作目录重新生成 88 个 WAV，与当前游戏资源逐字节一致；此项验证覆盖生成闭包与输出一致性，不代表人工听感评审。

## 2026-10-20 模型细化与菜单背景

十二枪继续使用既有 Quaternius 与 Kenney 合法来源，本次细化硬边、枪口内壁和机械细节，并修正双管枪准星与折管部件的绑定。当前 12 个 GLB 产物的哈希与字节数已更新；原始第三方源文件哈希保持不变。制作过程对应 `Tools/VisualAuthoring/refine_weapon_assets.py` 与 `fix_break_action_details.py`。

`Game/Assets/_SwapHunter/Resources/UI/Homecoming/menu-harbor-v2.png` 为 2026-10-20 使用内置 imagegen 生成的港区概念背景，实际尺寸 **1672 × 941**，不是实机截图或下载的外部游戏素材。生成提示词请求了 2560 × 1440，但实际产物尺寸以上述 PNG 为准；完整提示词、实际尺寸与文件哈希保存在 JSON 台账的 `projectGeneratedAssets` 中。
