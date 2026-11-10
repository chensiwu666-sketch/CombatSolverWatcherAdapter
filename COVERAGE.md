# 覆盖报告（自动生成）

本文件由 `tools/coverage_report.py` 从源码生成，请勿手改。

- 卡牌镜像：**75 / 92**（有 OnPlay 的卡）
- 抽牌时点镜像：1
- Power 持续效果镜像：4
- 姿态伤害倍率：Wrath ×2, Divinity ×3
- Power 预热（首次施加前必须在主线程物化）：25 个

## 已镜像的卡牌

| 卡牌 | 来源批次 |
|---|---|
| BattleHymn | CardsBatch6.cs |
| Blasphemy | CardsBatch7.cs |
| BowlingBash | Cards.cs |
| CarveReality | CardsBatch6.cs |
| Cataclysm | Cards.cs |
| ColdObservation | Cards.cs |
| Collect | Cards.cs |
| Consecrate | Cards.cs |
| Crescendo | Cards.cs |
| CrushJoints | CardsBatch4.cs |
| CutThroughFate | CardsBatch8.cs |
| DeceiveReality | CardsBatch5.cs |
| Defend_P | Cards.cs |
| DevaForm | Cards.cs |
| Devotion | CardsBatch6.cs |
| EmptyBody | CardsBatch6.cs |
| EmptyFist | CardsBatch6.cs |
| EmptyMind | CardsBatch5.cs |
| Eruption_P | Cards.cs |
| Establishment | Cards.cs |
| Evaluate | CardsBatch3.cs |
| Expunger | Cards.cs |
| Fasting2 | Cards.cs |
| FlurryOfBlows | Cards.cs |
| FlyingSleeves | Cards.cs |
| FollowUp | CardsBatch5.cs |
| Foresight | Cards.cs |
| Halt | CardsBatch3.cs |
| Indignation | CardsBatch7.cs |
| InnerPeace | CardsBatch3.cs |
| Insight | CardsBatch5.cs |
| Judgment | CardsBatch7.cs |
| JustLucky | CardsBatch8.cs |
| LikeWater | Cards.cs |
| MasterReality | Cards.cs |
| MentalFortress | Cards.cs |
| Miracle | CardsBatch5.cs |
| Nirvana | Cards.cs |
| Omega | Cards.cs |
| Perseverance | Cards.cs |
| Pray | CardsBatch7.cs |
| Preach | Cards.cs |
| PressurePoints | CardsBatch7.cs |
| Prostrate | CardsBatch3.cs |
| Protect | Cards.cs |
| Ragnarok | CardsBatch6.cs |
| ReachHeaven | CardsBatch7.cs |
| Rushdown | Cards.cs |
| Safety | Cards.cs |
| Sanctification | CardsBatch7.cs |
| Sanctity | CardsBatch5.cs |
| SandsOfTime | Cards.cs |
| SashWhip | CardsBatch4.cs |
| Scrawl_P | CardsBatch8.cs |
| Serenity | CardsBatch3.cs |
| SignatureMove | Cards.cs |
| SimmeringFury | Cards.cs |
| Smite | Cards.cs |
| SpiritShield | CardsBatch3.cs |
| Strike_P | Cards.cs |
| Study | Cards.cs |
| Swivel | CardsBatch7.cs |
| TalkToTheHand | Cards.cs |
| Tantrum | Cards.cs |
| ThirdEye | CardsBatch8.cs |
| ThroughViolence | Cards.cs |
| Tranquility | Cards.cs |
| Vigilance | CardsBatch3.cs |
| Wallop | CardsBatch3.cs |
| WaveOfTheHand | Cards.cs |
| Weave | Cards.cs |
| WheelKick | CardsBatch5.cs |
| WindmillStrike | Cards.cs |
| Worship | CardsBatch6.cs |
| WreathOfFlame | Cards.cs |

## 抽牌时点

- WatcherDeusExMachina（抽到即消耗并生成奇迹）

## Power 持续效果

- LikeWaterPower (block at side turn end while in Calm)
- SimmeringFuryPower (Wrath + draw + remove at turn start)
- DevaPower (energy after reset, growing counter)
- DevotionPower (mantra each turn + Divinity at 10)
- 不可用：StudyPower / OmegaPower hook the regular AfterSideTurnEnd pass, 

## 明确未镜像的卡牌与原因

| 卡牌 | 原因 |
|---|---|
| Alpha | 效果在抽到时/生成链上，OnPlay 为空 |
| Beta | 同上 |
| Brilliance | 需要维护「本场累计真言」的隐藏计数（分支无此状态） |
| Conclude | 强制结束回合，属于回合层，镜像碰不到 |
| ConjureBlade | 生成的 Expunger 需要写入自定义字段（HitCount），生成入口不支持 |
| DeusExMachina | 需要求解器当前未开放的能力 |
| DrawTalisman | 需要二选一的选牌决策 |
| FearNoEvil | 需要读取怪物意图（分支无 intent） |
| ForeignInfluence | 需要三选一的选牌决策 |
| LessonLearned | 斩杀后升级牌库，属于战斗外状态 |
| Meditate | 需要从弃牌堆选牌的决策 |
| Mockery | 生成 0 费复制牌，成本改写入口不可达 |
| Omniscience | 需要「选一张牌并再次打出」的决策 |
| Persuasion | 同 Mockery |
| Relinquish | 额外回合，属于回合层 |
| Vault | 额外回合，属于回合层 |
| Wish_P | 需要三选一的选牌决策 |

## 带风险标记的近似（面板会显示提示）

- Scry 的弃牌选择由适配器按固定规则决定，不进入搜索分支
- Rushdown 抽牌时机早于 mod（数量正确）
- 连击从弃牌堆回手、预见的启明命运未建模
- 神性的回合末自动退出未建模
- 发脾气洗回抽牌堆未建模
- Study / Omega 的回合末效果不可用（求解器只开放 Late pass）
- 两张以上的神性形态按单个计数器近似
