# VisualV2 新角色与动作资源

完整数据以 characters-source-index.json 为准，包括资源键、原件、SHA、固定GUID、真实骨路径、归一后的Unity坐标、头/躯干/脚顶点权重区域及每段动画时长。角色/敌人为新CC0人体，Boss为George机甲，FPS双臂由新人体蒙皮抽取；没有复用旧身体或旧双臂网格。

- 四职业：VisualV2/Actors/class_00..03（BlueSoldier_Male、Soldier_Male、Ninja_Male、Worker_Male）。
- 普通敌人：VisualV2/Actors/enemy_assault、enemy_sniper、enemy_shield。
- Boss：VisualV2/Actors/boss_george。
- 第一人称：VisualV2/Characters/fps_arms。

普通身体参考Idle高1.8米，Boss高2.1米；实例外层scale为1，保留内部VisualV2Root的缩放/偏移。直接实例化，不再套旧ModelLibrary的-X修正。GLB节点坐标已在索引转换为Unity空间；骨原点不是头部几何中心，应结合区域bounds。身体显示网格重命名为VisualMesh_0，避免与Body骨重名。

glTFast 6.15.0实际枚举Mecanim=2，所有新meta明确设为2。全身人体17段、George20段原动作均保留；用 Resources.LoadAll<AnimationClip>(resourceKey)读取，导入后必须实查clip.legacy=false。适配层控制Animator/Playables、关闭root motion与事件；不能让动作产生第二次伤害。源动作里不存在精确双手持枪/装填或本项目完整技能，应在新骨架上补姿势并明确来源。

FPS资源含真实skinned双臂与完整辅助骨架，没有头/躯干/腿网格；真骨hand_L/R、forearm_L/R以及wrist/elbow/PalmContact marker可查询。肩根约(±.178,-.08,-.30)，臂长约.294+.237米。手是前臂子骨，不能套旧siblings对象IK。PalmContact按新Fist骨中点导出，禁止沿用旧手掌常量。

可复现命令（全新输出与报告路径）：

    blender --background --factory-startup --disable-autoexec --python Tools/VisualAuthoring/convert_character_models.py -- --project . --output new-model-candidate --report new-model-report.json

原始.blend/.gltf永不保存覆盖；转换器只导出选定模型、骨架和动作，不导出发布者控制器、碰撞或相机灯光。动画骨权重按最大4项归一，与当前Unity预算一致。人物原动作包含少量不存在Palm.L/R的冗余曲线，实际骨为Fist；未把这些冗余引用当作成功的手部动作。

Blender结果不是Unity实机验收。最终采用/许可由根台账合并；包内未附单独人物许可证的事实和作者发布页CC0证据保留于Sources/AnimatedCharacters2019/License-source.md。
