# 覆盖报告（自动生成）

本文件由 `tools/coverage_report.py` 从**两棵源码树**生成，请勿手改。

- 观者 mod 的卡牌类：101 个
- 牌池（玩家在奖励/商店能看到）：**83 张**（mod 声明 `CardModel[83]`）
- 牌池已镜像：**69 / 83**
- 已注册卡牌类型合计：76
- 抽牌时点镜像：1（DeusExMachina）
- Power 持续效果镜像：4
- 姿态伤害倍率：Wrath ×2, Divinity ×3
- Power 预热：25 个

## 牌池里还未镜像的卡

| 卡牌 | 判定 | 实测原因 / 缺什么 |
|---|---|---|
| Mockery | 不适用 | MultiplayerConstraint.MultiplayerOnly：单人局奖励与商店里不会出现 |
| Persuasion | 不适用 | 同上 |
| Conclude | 可做 | 伤害 + IsLastInSeries 时结束回合；SimulatedCombatState.RequestPlayerTurnEnd() 是 public，CardOnPlayMirrorContext.CardPlay 里有 IsLastInSeries |
| FearNoEvil | 可做 | 伤害 + 目标在攻击则进平静；SimulatedCombatState.IsEnemyIntendingToAttack() 是 public |
| ForeignInfluence | 可做 | 三选一攻击牌入手；CardChoiceMirrors + PlanChoiceEffect.GenerateToHand |
| Meditate | 可做 | 弃牌堆选 1 张回手 + 进平静 + 若为本张系列最后一张则结束回合；CardChoiceMirrors + RequestPlayerTurnEnd |
| Alpha | 可做 | 打出后生成 Beta 进抽牌堆随机位（旧记录误标为「OnPlay 为空」）；CreateAndAddGeneratedCardsToCombat(Draw, Random) 已具备 |
| Brilliance | 可做 | 伤害 + 本场累计真言；需把 WatcherStatePower.TotalMantraGainedThisCombat 登记成 Power 隐藏状态，并在 MantraOps 里同步计数 |
| ConjureBlade | 可做 | X 费生成 Expunger 进抽牌堆并把 Repeat 设为 X（升级 +1）；生成结果 SimCardPileAddResult 带 PredictedCard，可写 DynamicVars |
| LessonLearned | 可做（部分） | 伤害部分可精确镜像；斩杀后升级牌库属局外状态，战斗内无数值影响，需打风险标记 |
| Omniscience | 可做 | 从抽牌堆选 1 张自动打出两次；CardChoiceMirrors + simulator.AutoPlay |
| Vault | 可做 | 额外回合；ExtraTurnMirrors.RegisterShouldTakeExtraTurn<WatcherExtraTurnPower> + 移除该 Power |
| Wish_P | 可做 | 三选一（力量/多层护甲/金币）；CardChoiceMirrors——求解器手册 §2.5 正是拿「观者的许愿」当示例 |
| DrawTalisman | 做不了 | 全部效果是「给牌附魔（Enchantment）」，求解器没有附魔概念；不做比近似成白打更诚实 |

## 牌池之外的未镜像类型（生成牌 / 令牌 / 基类）

| 类型 | 判定 | 说明 |
|---|---|---|
| Beta | 可做 | 同 Alpha：打出后生成 Omega 进抽牌堆 |
| DrawTalismanDirectedHand | 不适用 | DrawTalisman 的选项令牌，随 DrawTalisman |
| DrawTalismanRandomDeck | 不适用 | DrawTalisman 的选项令牌，随 DrawTalisman |
| HymnV2 | 不适用 | Unplayable，且全 mod 无生成引用、不在牌池：玩家遇不到 |
| IntentProxy | 不适用 | 不是卡牌，是意图代理 |
| Relinquish | 不适用 | MultiplayerOnly：给队友额外回合，单人局不出现 |
| Revelation_P | 不适用 | Unplayable，且全 mod 无生成引用、不在牌池：玩家遇不到 |
| V2ChoiceTokenBase | 不适用 | 抽象基类，不是可打出的卡 |
| WishAlmighty | 不适用 | 许愿的选项令牌，随 Wish_P 一起完成 |
| WishFameAndFortune | 不适用 | 许愿的选项令牌，随 Wish_P 一起完成 |
| WishLiveForever | 不适用 | 许愿的选项令牌，随 Wish_P 一起完成 |

## 已镜像的牌池卡

| 卡牌 |
|---|
| BattleHymn |
| Blasphemy |
| BowlingBash |
| CarveReality |
| Cataclysm |
| ColdObservation |
| Collect |
| Consecrate |
| Crescendo |
| CrushJoints |
| CutThroughFate |
| DeceiveReality |
| Defend_P |
| DeusExMachina |
| DevaForm |
| Devotion |
| EmptyBody |
| EmptyFist |
| EmptyMind |
| Eruption_P |
| Establishment |
| Evaluate |
| Fasting2 |
| FlurryOfBlows |
| FlyingSleeves |
| FollowUp |
| Foresight |
| Halt |
| Indignation |
| InnerPeace |
| Judgment |
| JustLucky |
| LikeWater |
| MasterReality |
| MentalFortress |
| Nirvana |
| Perseverance |
| Pray |
| Preach |
| PressurePoints |
| Prostrate |
| Protect |
| Ragnarok |
| ReachHeaven |
| Rushdown |
| Sanctification |
| Sanctity |
| SandsOfTime |
| SashWhip |
| Scrawl_P |
| Serenity |
| SignatureMove |
| SimmeringFury |
| SpiritShield |
| Strike_P |
| Study |
| Swivel |
| TalkToTheHand |
| Tantrum |
| ThirdEye |
| Tranquility |
| Vigilance |
| Wallop |
| WaveOfTheHand |
| Weave |
| WheelKick |
| WindmillStrike |
| Worship |
| WreathOfFlame |

## 带风险标记的近似（面板会显示提示）

- Scry 的弃牌选择由适配器按固定规则决定，不进入搜索分支
- Rushdown 抽牌时机早于 mod（数量正确）
- 连击从弃牌堆回手、预见的启明命运未建模
- 神性的回合末自动退出未建模
- 发脾气洗回抽牌堆未建模
- Study / Omega 的常规回合末效果不可用（求解器只开放 Late pass，PR #245 未合并）
- 两张以上的神性形态按单个计数器近似
