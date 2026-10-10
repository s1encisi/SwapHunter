# 视觉资源适配与复现

普通 Unity 构建读取 VisualV2 和保留的 VisualD11 资源，不需要 Blender。实际采用素材、原件哈希和许可见根目录 THIRD_PARTY_ASSETS.json。外部模型属于各自作者，本项目负责转换、绑定与游戏交互适配。

- convert_character_models.py：四种新角色身体、三种敌人、George机甲及新第一人称双臂。动作、骨骼和空间约定见 CHARACTERS.md、characters-source-index.json。
- export_weapons_v2.py：十二种新枪体与 Bayonet，来源和节点契约见 weapons-v2-sources.json、weapons-v2-export.json。
- convert_final_environment.py：本次新增十种场景物件，来源见 final-environment-source-index.json。
- convert_cc0_environment.py：已有五种 CC0 环境模型，继续实际使用。

在副本上用 Blender 5.2.2 LTS、--background --factory-startup --disable-autoexec --python-exit-code 1 执行自有转换脚本。参数以脚本 argparse 定义为准。原始 blend 不覆盖修改；重新导出派生 GLB 时保留 Unity meta 的 GUID。

运行时环境视觉拟合原物理代理；人物使用真实骨架动作且禁用根位移，由既有控制器决定世界坐标。枪械与手臂用明确的握点和骨链适配。老资产保留，当前角色与武器工厂从 VisualV2 读取新资产。

本版导入入口：SwapHunter.Editor.FinalModelsImport.Run -modelEvidence <新报告路径>。
原生验收入口：Tools/Validate-Demo-v06.ps1 -Suite Models -DisplayMode Foreground。
交付包与实际验证说明见 docs/models-final.zh-CN.md。
