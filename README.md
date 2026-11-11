# Watcher 适配器（Combat Solver）

让《战斗路线求解器 / Combat Solver》看懂 [观者（Watcher）](https://steamcommunity.com/sharedfiles/filedetails/?id=3747526116) 这个角色 mod 的卡牌与架势。

观者不是原版角色，求解器默认无法推演她的卡，遇到会直接停摆。完整方案由两部分组成：

1. **兼容层**（`CombatSolverCompat`，本仓库 `CombatSolverCompat/`）：把"检测到未适配内容就拒绝启动"降级为"尽力推演 + 明确标风险"，让求解器在装了角色 mod 的情况下也能跑。
2. **适配层**（本仓库）：把观者的卡牌出牌效果、架势机制、部分能力的持续效果，按 mod 源码逐个镜像给求解器。

> 两个 mod 必须同时安装：只装适配层时，求解器仍会因"未适配内容"拒绝启动。

> 覆盖范围是**有限的、并且会明确标注**：能精确推演的卡按源码实现来，做不到的地方在路线上打风险标记，不会假装正确。当前覆盖率见 [COVERAGE.md](COVERAGE.md)（自动生成，勿手改）。

## 依赖

| 项目 | 版本 |
|---|---|
| 游戏 | 0.111.0 |
| [Combat Solver](https://steamcommunity.com/sharedfiles/filedetails/?id=3790899961) | ≥ 0.50.1（当前按 0.50.1 构建与验证） |
| [Watcher](https://steamcommunity.com/sharedfiles/filedetails/?id=3747526116) | ≥ 0.10.8 |
| RitsuLib | ≥ 0.6.0（Combat Solver 自己的要求） |

## 安装

1. 订阅 Combat Solver 与 Watcher，确认两者都启用
2. 把 `WatcherAdapter.dll` 与 `WatcherAdapter.json` 放进 `<游戏目录>/mods/WatcherAdapter/`
3. 把 `CombatSolverCompat.dll` 与 `CombatSolverCompat.json` 放进 `<游戏目录>/mods/CombatSolverCompat/`
4. 启动游戏，确认 mod 列表里两个都在

启动后 `%APPDATA%/SlayTheSpire2/logs/WatcherAdapter.log` 会写出登记结果，包括 `self-check: 86 Watcher card mirror(s) present`。

## 发布

Steam 创意工坊：**战斗求解器：观者兼容** —— https://steamcommunity.com/sharedfiles/filedetails/?id=3837745863
（物品 ID `3837745863`，上传工作区 `D:\work\workshop\watcher-adapter`，带 `mod_id.txt` 后再次 upload 即更新同一条目）

## 当前状态

- 卡牌镜像 79 / 83（观者牌池：玩家在奖励和商店里能看到的全部卡），逐条对照 mod 反编译源码写成
- 未镜像 4 张：嘲讽、规劝（多人专属，单人局不会出现）、他山之石（选项由游戏 RNG 现场抽取，无法对齐实机页面）、画符（效果是给牌附魔，求解器没有附魔概念）
- 架势机：平静 / 愤怒 / 神性 / 预见，含移除平静返能量、进神性返 3 能量、紫莲花回能
- 伤害倍率：愤怒 ×2（含受伤）、神性 ×3（仅出伤）
- Power 持续效果：静水、沸腾之怒、神性形态、虔诚（含真言满 10 转神性）
- 隐藏状态进指纹：本场累计真言 / 本回合真言（光辉、福音的数值来源）
- 抽牌时点：无明（抽到即消耗并生成奇迹）
- 选牌三选一：冥想、通晓万物、许愿
- 额外回合：腾跃
- 卡名对照表见 [COVERAGE.md](COVERAGE.md)（中文名与游戏内一致，自动生成）

## 已知限制（请先读）

- **能力侧不完整**：只有上面 4 个 Power 的持续效果被推演；其余能力牌"打出去"是对的，但之后的每回合效果求解器看不到，会低估这些牌的价值
- **研习 / 欧米茄**：回合末效果写在常规 pass 上，Combat Solver 0.50.1 只开放 Late pass，因此这两张的回合末效果**未建模**
- **Scry**：求解器没有"看牌堆顶弃牌"的概念，弃哪张由适配器按固定规则决定，不进入搜索分支
- **带风险标记的近似**：Rushdown 抽牌时机、连击回手、预见的启明命运、神性回合末退出、发脾气洗回抽牌堆
- **完全未镜像**：Scry 之外的选牌类（Wish / Omniscience / ForeignInfluence）、额外回合（Vault / Relinquish）、强制结束回合、斩杀后升级牌库等

## 实现要点

- 通过 `IgnoresAccessChecksTo` + 公开化引用，调用 Combat Solver 的第三方适配入口；不复制它的代码，也不分发它的二进制
- 所有登记在 mod 初始化阶段一次完成（注册表的派发缓存不会失效，晚登记会静默无效）
- 第三方 Power 若可能在搜索中首次被施加，必须在 `PowerDynamicVarWarmup.RegisterAdaptedCanonicalPower` 声明，否则整条路线会因后台懒创建被守卫中断
- 启动时校验 Combat Solver 版本：不是 0.50.1.x 就整层拒绝注册并说明原因——宁可明确停摆，也不给"看起来可信"的错误路线

## 构建

```sh
# 需要 .NET 9 SDK；编译期引用公开化后的 CombatSolver.dll 与游戏程序集
dotnet build -c Release
```

## 许可与致谢

- 本 mod 自己的代码：MIT
- 不包含、也不重新分发 Combat Solver 与 Random Foreseer 的任何代码或二进制
- 适配入口、文档与镜像约定来自 [Torch1230/CombatSolver](https://github.com/Torch1230/CombatSolver)（MIT，作者 Torch，核心源自 [hotwords123/Random Foreseer](https://github.com/hotwords123/StS2.RandomForeseer)）
- 观者角色 mod 由 Boninall 制作；本适配器与其作者无隶属关系

遇到问题请在 GitHub Issue 里附上 `WatcherAdapter.log` 与 Combat Solver 导出的问题包。
