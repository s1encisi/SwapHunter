# 换位猎手 D1.1 本地交付

游戏版本：{build_version}；D1.1 是本轮系统方案标识。本交付没有上传或发布。

- 同构建完整战术专项：{tactical_status}。
- 第 5、7、12 章代表路线：{routes_status}。这是清敌、耗尽跳板后的真实移动保底路线夹具，非真人或真实战斗通关；不宣称全十二章本轮重验。
- 原生离屏图像检查：{visual_status}；所提供的界面评审：{ui_status}。离屏截图不等于完整前台交互验收。
- 性能证据：{performance_status}。未运行不能写成通过，已有采样不代表跨设备表现或性能提升。
- 干净源码重建：打包工具未执行；具体结果由后续独立重建记录说明。ZIP完整性不等于Unity重建成功。

完整解压 Windows 包，双击 `{executable}` 进入正常成长流程。手动运行 `开始演示.ps1 -Level 5 -Class 0 -Skill 0` 可启动独立四职业演示；Class 为0突击/1重装/2侦查/3远程，Skill 为该职业0/1两技能选择。加 -Menu 可直接进入职业整备页。演示有明确预置和标识，不写普通成长，不代表真实通关成绩。

源码包包含当前工作树中公开的源码、作者原稿、Unity `.meta`、Packages锁定引用和必要本地依赖。不包含Git仓库、Library、运行Logs/Builds、私人备份或玩家档。打开 `Game` 工程后按锁定Packages恢复依赖；Unity Editor及包缓存不在源码包内。许可见 `THIRD_PARTY_ASSETS.md/json` 与原始许可文件；试玩包另集中附于 `Licenses/`。第三方材料和Unity组件不被项目根MIT许可重新授权。

构建输入 SHA-256：`{source_sha256}`  
程序集 SHA-256：`{assembly_sha256}`  
源码基准提交：`{source_commit}`；包含未提交工作树改动：{dirty}。这不是远端发布状态声明。

`package-manifest.json` 描述每个包的文件字节数和哈希；打包器执行一次ZIP内容核验和一次全新目录独立解压。原始所选QA报告、图像、经济路径及已提供的CSV位于交付目录 `evidence/`，不把原始测试存档混入公开源码ZIP。`validation-status.json` 分项记录缺测、失败与实际范围；详细打包/解压结果见交付目录 `package-report.json`。以前的包与失败记录继续保留。