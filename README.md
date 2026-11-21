# Watcher 适配器（Combat Solver）

让《战斗路线求解器 / Combat Solver》看懂 [观者（Watcher）](https://steamcommunity.com/sharedfiles/filedetails/?id=3747526116) 这个角色 mod 的卡牌、架势，以及能力牌和遗物的每回合效果。

创意工坊：**战斗求解器：观者兼容** —— https://steamcommunity.com/sharedfiles/filedetails/?id=3837745863

我自己想用求解器打观者，可是它不认识这个角色，一遇到就停下不动，所以和 AI 一起做了这个。先说清楚能力范围：能精确推演的按 mod 源码逐条写，做不到的地方在路线上打风险标记，**不会假装正确**。覆盖率、缺口原因、中文卡名对照都在 [COVERAGE.md](COVERAGE.md)（自动生成，勿手改）。

观者不是原版角色，求解器默认无法推演她的卡。完整方案由两部分组成：

1. **兼容层**（`CombatSolverCompat`，本仓库 `CombatSolverCompat/`）：把"检测到未适配内容就拒绝启动"降级为"尽力推演 + 明确标风险"，让求解器在装了角色 mod 的情况下也能跑。
2. **适配层**（本仓库）：把观者的卡牌出牌效果、架势机制、能力牌与遗物的每回合效果，按 mod 源码逐个镜像给求解器。

> 两个 mod 必须同时安装：只装适配层时，求解器仍会因"未适配内容"拒绝启动。

## 依赖

| 项目 | 版本 |
|---|---|
| 游戏 | 0.113.0 |
| [Combat Solver](https://steamcommunity.com/sharedfiles/filedetails/?id=3790899961) | ≥ 0.51.9（当前按 0.51.9 构建与验证） |
| [Watcher](https://steamcommunity.com/sharedfiles/filedetails/?id=3747526116) | ≥ 0.10.20 |
| RitsuLib | ≥ 0.6.11（Combat Solver 自己的要求） |

## 安装

**工坊版（推荐）**：订阅上面的工坊条目即可，两个包都在里面，依赖也挂好了。

**手动版**：

1. 订阅 Combat Solver 与 Watcher，确认两者都启用
2. 把 `WatcherAdapter.dll` 与 `WatcherAdapter.json` 放进 `<游戏目录>/mods/WatcherAdapter/`
3. 把 `CombatSolverCompat.dll` 与 `CombatSolverCompat.json` 放进 `<游戏目录>/mods/CombatSolverCompat/`
4. 启动游戏，确认 mod 列表里两个都在

启动后 `%APPDATA%/SlayTheSpire2/logs/WatcherAdapter.log` 会写出登记结果，包括 `self-check: 86 Watcher card mirror(s) present`。这个数字是当前注册成功的卡牌镜像数，出现 `FAILED` 行说明某项没登记上。

## 当前状态

- 卡牌镜像 **79 / 83**（观者牌池：玩家在奖励和商店里能看到的全部卡），逐条对照 mod 反编译源码写成
- 未镜像 4 张：嘲讽、规劝（多人专属，单人局不会出现）、他山之石（选项由游戏 RNG 现场抽取，无法对齐实机页面）、画符（效果是给牌附魔，求解器没有附魔概念）
- 架势机：平静 / 愤怒 / 神性 / 预见，含移除平静返能量、进神性返 3 能量、紫莲花回能
- 伤害倍率：愤怒 ×2（含受伤）、神性 ×3（仅出伤）
- **能力牌 / 遗物每回合效果 15 项**：静水、沸腾之怒、神性形态、虔诚、战歌、收集、手摇鼓、宁静的减伤、斋戒扣能量、许愿的甲胄、斗篷扣、格挡回击、福音、冷眼旁观、亵渎的死亡印记
- 隐藏状态进指纹：本场累计真言 / 本回合真言（光辉的数值来源）
- 抽牌时点：无明（抽到即消耗并生成奇迹）
- 选牌三选一：冥想、通晓万物、许愿
- 额外回合：腾跃

## 已知限制（请先读）

- **等上游 PR**：研习、欧米伽、挥手、混乱的回合末效果，以及神格的回合末自动退出。Combat Solver 0.51.9 只开放回合末"晚阶段"入口，常规 pass 的第三方登记是我提交的 [#245](https://github.com/Torch1230/CombatSolver/pull/245)，合并后就能接上
- **Scry**：求解器没有"看牌堆顶弃牌"的概念，弃哪张由适配器按固定规则决定，不进入搜索分支，所以 scry 类牌会被低估
- **带风险标记的近似**：Rushdown 抽牌时机、连击回手、先见之明的启明命运、冥想给的单回合保留、发脾气洗回抽牌堆
- **做不了的**：沉思的触发点是自己写的方法而不是可登记的钩子；预见只施加知命，知命由梵音等卡消费，消费方还没做；天罚只在多人局出现
- **没有系统测试**：没有测试矩阵，验证方式是逐条对照源码 + 实机打几场，会有 bug

## 版本门

适配层只认构建时对应的 Combat Solver 版本（本版是 **0.51.9**）。求解器一更新，适配层会先自己停用并在日志里写一行 `DISABLED`，而不是拿一套可能已经变味的镜像去算错数。所以要等本仓库跟进发新版，中间会有一段不能用的窗口。

## 解除限制：能运行 ≠ 算得准

Combat Solver 原本检测到其他影响战斗的 mod 就拒绝推演。本 mod 的 `CombatSolverCompat` 把这条保护降级为
"尽力推演 + 明确标风险"，所以同时还开着别的 mod 时求解器也会继续运行。但其他 mod 的卡牌、遗物、能力都没有镜像，
只有只装观者相关 mod 时观者部分的数值才接近可信；多 mod 环境下请以面板上的风险标记为准。

本次发布的实测环境：除观者 + Combat Solver + 本 mod 外，后台一直开着 LexNinja2（另一角色 mod，约 7.8 万订阅），
全程求解器未拒绝运行。

## 实现要点

- 通过 `IgnoresAccessChecksTo` + 公开化引用，调用 Combat Solver 的第三方适配入口；不复制它的代码，也不分发它的二进制
- 所有登记在 mod 初始化阶段一次完成（注册表的派发缓存不会失效，晚登记会静默无效）
- 第三方 Power 若可能在搜索中首次被施加，必须在 `PowerDynamicVarWarmup.RegisterAdaptedCanonicalPower` 声明，否则整条路线会因后台懒创建被守卫中断
- 有些钩子家族（`AfterDamageGiven`、`AfterDamageReceived`、`ModifyDamage` 加法表）只有私有 `Registry` 字段、没有公开 Register，这类走 `MirrorBridge` 反射字段登记；桥接失败会大声抛进日志，而不是退回"把 mod 代码放到搜索线程跑"的老路
- 能力牌的私有计数（冷眼旁观的"这张牌算过了"）用分支状态对象承载：随分支 fork，存的卡牌引用在 fork 时重映射
- 启动时校验 Combat Solver 版本：不匹配就整层拒绝注册并说明原因——宁可明确停摆，也不给"看起来可信"的错误路线

## 构建

```sh
# 需要 .NET 9 SDK；编译期引用公开化后的 CombatSolver.dll 与游戏程序集
dotnet build -c Release
python tools/coverage_report.py   # 重新生成 COVERAGE.md
bash tools/package.sh             # 出 dist/ 下的两个 zip
```

## 许可与致谢

- 本 mod 自己的代码：MIT
- 不包含、也不重新分发 Combat Solver 与 Random Foreseer 的任何代码或二进制
- 适配入口、文档与镜像约定来自 [Torch1230/CombatSolver](https://github.com/Torch1230/CombatSolver)（MIT，作者 Torch，核心源自 [hotwords123/Random Foreseer](https://github.com/hotwords123/StS2.RandomForeseer)）
- 观者角色 mod 由 Boninall 制作；本适配器与其作者无隶属关系
- 代码基本是 AI 写的，我负责提需求、看日志和验收

遇到问题请在 GitHub Issue 里附上 `WatcherAdapter.log` 与 Combat Solver 导出的问题包。
