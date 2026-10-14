# SSU-01 系统协同升级：实现与验收

日期：2026年10月14日。分支：`design/system-synergy-upgrade`。基础游戏版本 `0.6.0`，本次行为规则版本 **3**，交付标识 **SSU-01**。

本期方案已实施并构建为 Windows 独立玩家；同一最终候选 **133 / 133 项专项检查通过**，退出码 0。4 张新增界面截图已查看，中文说明、等级、费用与按钮没有重叠。保持现行主要战斗数值、价格和奖励。

| 方案 | 已完成内容 | 主要实现 | 验收 |
|---|---|---|---|
| U0/U1 | 规则版本写入部署、回执和记录；战斗、图鉴与弹药包共享有效基础参数 | `TacticalPrototypeRules`、`EffectiveWeaponStats`、`PlayerMotor`、`WeaponDisplayStats`、`ExpeditionRun` | P1 临时弹匣30，实际/图鉴30，扩容备弹180但弹药包仍实际补60 |
| U2 | Q输入优先、未发连发取消、装填挂起与失败恢复、跨准备帧锁定、暂停保留、世界对象命令分流 | `PlayerMotor`、`PlayerCombatTools`、`PlayerClassSkill`、`PlayerTactics`、`DemoGame` | A1–A4，InputSystem 同帧输入、实际计时和世界对象 |
| U3 | 短硬直不冻空间技能；沉默只打断预告，执行承诺保留；跨阶段保留本次施放参数 | `EnemyActor`、`BossEncounter`、`BossSpatialSkills`、`BossReadabilityVisual` | B1–B3、D1；含400有效伤害→能量0/生命680、不同伤害入口及独立盾判断 |
| U4 | 采购、尝试、结算分开；实回/实补/实吸与累计时间；记录失败明确提示 | `AttemptMetrics`、`ExpeditionRun`、`ExpeditionData` | E1 失败后重试成功，仅最终使用永久扣除；记录I/O失败不改变已保存资产 |
| U5 | 关外商品定向试用、采购结果等级/等阶比较、独立窗口、返回原商品并读取当前账户 | `TrialSession`、`DemonstrationMode`、`RunStorage`、`DemoUI`、商品UI | T1–T3；6个真实子进程覆盖返回、死亡、短样例完成、终止、期间新采购、账号读取失败重试 |
| U6 | 八条合法路线、用途与取舍、拥有/上架/成本、替换差异、已拥有配置单事务应用 | `LoadoutRoutes`、`ExpeditionUI`、`ExpeditionData.ApplyTacticalRoute` | L1 全路线合法；缺项/跨职业/行动中拒绝；保存失败不发生部分替换 |
| U7 | 只运行本期相关专项；保留代表任务参数约束 | `SynergyQA`及4个专项检查类 | M1 第8章任选二、第10章增援时点/预警、第12章随从上限 |
| U8 | 新本地候选、中文游玩说明、同构建证据与源码打包入口 | `Build-Demo-v06.ps1`、`Validate-Demo-v06.ps1 -Suite Synergy`、`package_tactical.py --profile synergy` | 构建与专项成功；实际包信息见交付目录 `package-report.json` |

## 证据位置与复现

最终玩家：`Builds/SwapHunter-SSU01-20261014-02/SwapHunter.exe`。  
专项：`Logs/SSU01/acceptance-02/summary.json`、`synergy/synergy-report.json`、`synergy/player.log`。  
界面：`Logs/SSU01/acceptance-02/ui-review/review.json` 及4张PNG。

- 构建输入 SHA-256：`b7ace0c8c6f2268e9655a65441b75cbfbb308623f8ded1b83503125413411770`
- 程序集 SHA-256：`3b47c6d3b47e2f5b8264287b509b0b735f29481a8aa4d7370a6dd59ed20c8c01`

```powershell
.\Tools\Build-Demo-v06.ps1 -EditorPath 'E:\Unity\6000.3.25f1\Editor\Unity.exe' -OutputDirectory 'Builds/新的空目录'
.\Tools\Validate-Demo-v06.ps1 -Suite Synergy -DisplayMode Foreground -ExecutablePath 'Builds/新的空目录/SwapHunter.exe' -OutputDirectory 'Logs/SSU01/新的证据目录'
```

专项使用隔离账号、受控位置/目标/计时和真实运行入口。试用的 `sample_complete` 是自动结束的短样例，不是伪造整章通关。关卡几何未改，M1只检查相关规则，不替代真人平衡测试或全12章重新通关；本轮没有性能提升数据。

## 保留的交付边界

- 旧存档迁移仍只提供原有预览与工具，没有正式导入旧档。
- 已发布的 v0.6.0 保留；本候选没有自动上传远端或发布Release。
- 投递版设计文档与本内部升级/验收文档分开。后续数值试调仍应根据实际玩家记录选一个问题进行，不把自动化用时当作真实收益效率。

玩家操作见 [SSU-01中文游玩说明](system-synergy-playguide.zh-CN.md)。
