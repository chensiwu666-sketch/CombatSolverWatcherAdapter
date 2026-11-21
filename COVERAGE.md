# 覆盖报告（自动生成）

本文件由 `tools/coverage_report.py` 从**两棵源码树**生成，请勿手改。

- 观者 mod 的卡牌类：101 个
- 牌池（玩家在奖励/商店能看到）：**83 张**（mod 声明 `CardModel[83]`）
- 牌池已镜像：**79 / 83**
- 已注册卡牌类型合计：87
- 抽牌时点镜像：1（机械降神（DeusExMachina））
- Power 持续效果镜像：14
- Power 隐藏状态进指纹：1 项
- 姿态伤害倍率：愤怒（Wrath） ×2, 神格（Divinity） ×3
- Power 预热：29 个

## 牌池里还未镜像的卡

| 卡牌 | 判定 | 实测原因 / 缺什么 |
|---|---|---|
| 嘲讽（Mockery） | 不适用 | MultiplayerConstraint.MultiplayerOnly：单人局奖励与商店里不会出现 |
| 规劝（Persuasion） | 不适用 | 同上 |
| 他山之石（ForeignInfluence） | 做不了 | 三张选项由 mod 用游戏 RNG 现场抽取，预测侧无法得知实机页面上是哪三张，部署时按 CardId 定位必然错位；选项固定的许愿则可以精确登记 |
| 画符（DrawTalisman） | 做不了 | 全部效果是「给牌附魔（Enchantment）」，求解器没有附魔概念；不做比近似成白打更诚实 |

## 牌池之外的未镜像类型（生成牌 / 令牌 / 基类）

| 类型 | 判定 | 说明 |
|---|---|---|
| 手牌·指向附魔（DrawTalismanDirectedHand） | 不适用 | DrawTalisman 的选项令牌，随 DrawTalisman |
| 牌组·随机附魔（DrawTalismanRandomDeck） | 不适用 | DrawTalisman 的选项令牌，随 DrawTalisman |
| 梵音（HymnV2） | 不适用 | Unplayable，且全 mod 无生成引用、不在牌池：玩家遇不到 |
| IntentProxy | 不适用 | 不是卡牌，是意图代理 |
| 让渡（Relinquish） | 不适用 | MultiplayerOnly：给队友额外回合，单人局不出现 |
| Revelation_P | 不适用 | Unplayable，且全 mod 无生成引用、不在牌池：玩家遇不到 |
| V2ChoiceTokenBase | 不适用 | 抽象基类，不是可打出的卡 |
| 全能全知（WishAlmighty） | 不适用 | 许愿的选项令牌，随 Wish_P 一起完成 |
| 名利双收（WishFameAndFortune） | 不适用 | 许愿的选项令牌，随 Wish_P 一起完成 |
| 长生不老（WishLiveForever） | 不适用 | 许愿的选项令牌，随 Wish_P 一起完成 |

## 能力牌 / 遗物的回合钩子

卡面镜像不等于数值正确：能力牌自己的钩子不登记，那张牌在搜索里就等于白打。本表按 mod 的覆写逐个核对。

| 类型 | 覆写的阶段 | 状态 | 说明 |
|---|---|---|---|
| BattleHymnPower | AfterPlayerTurnStart | 已镜像 | |
| BlessProphecyDamagePower | AfterSideTurnEnd | 未建模 | 伤害加成在 ModifyDamageAdditive，且要靠 AfterCardPlayed 和常规回合末移除；两处都没有开放或未桥接 |
| BlockReturnPower | AfterDamageGiven | 已镜像 | |
| CloakClasp_P | BeforeSideTurnEnd | 已镜像 | |
| ColdObservationPower | AfterDamageGiven | 未建模 | 伤害转格挡本身可做，但它按「每张牌只递减一次」记私有字段，多段攻击在分支里没有等价状态，硬做会少算层数 |
| CollectPower | AfterPlayerTurnStart | 已镜像 | |
| ConfusionPower | AfterSideTurnEnd、ModifyDamageMultiplicative | 未建模 | 受到的预言牌伤害倍率 + 敌方回合末递减；只做倍率不减时间会永久生效，等回合末入口 |
| Damaru | AfterPlayerTurnStart | 已镜像 | |
| DeepThoughtSleepPower | AfterDamageReceived | 未建模 | 触发点是自己写的 AfterSideTurnStartCompat，不是可登记的原生虚方法 |
| DevaPower | AfterEnergyReset | 已镜像 | |
| DevotionPower | AfterPlayerTurnStart | 已镜像 | |
| DivineDoomPower | AfterDamageGiven、AfterPlayerTurnStartEarly | 不需要 | 布道只在多人局施加，单人局不会出现 |
| Divinity | AfterSideTurnEnd、ModifyDamageMultiplicative | 部分 | 伤害倍率已镜像；自动退出写在同一 Power 的常规 AfterSideTurnEnd，未建模（PR #245） |
| EndTurnDeathPower | AfterPlayerTurnStart | 已镜像 | |
| EnergyDownPower | AfterEnergyReset | 已镜像 | |
| EnlightenFatePower | AfterPlayerTurnStartEarly、AfterSideTurnEnd | 未建模 | 常规 AfterSideTurnEnd 决定挂起真言，且依赖「知命」消费方，暂不做 |
| Foreseen | AfterPlayerTurnStart | 未建模 | 自身只施加知命；知命由梵音等卡消费，消费方未建模前单独做它没有意义 |
| GospelPower | AfterDamageGiven | 已镜像 | |
| LikeWaterPower | BeforeSideTurnEnd | 已镜像 | |
| OmegaPower | AfterSideTurnEnd | 未建模 | 同上：常规 AfterSideTurnEnd 无第三方入口 |
| RestfulPower | AfterPlayerTurnStartEarly、ModifyDamageMultiplicative | 已镜像 | |
| SimmeringFuryPower | AfterPlayerTurnStart | 已镜像 | |
| StudyPower | AfterSideTurnEnd | 未建模 | 效果写在常规 AfterSideTurnEnd；求解器只有 Late 入口，PR #245 合并后即可接 |
| WatcherDeusExMachina | AfterCardDrawn | 已镜像 | |
| WatcherExtraTurnPower | AfterTakingExtraTurn、ShouldTakeExtraTurn | 已镜像 | |
| WatcherStatePower | AfterPlayerTurnStartEarly | 已镜像 | |
| WaveOfTheHandPower | AfterSideTurnEnd | 未建模 | 主效果在 AfterBlockGained（每次获得格挡给全体上虚弱），而移除它的回合末入口还没开放；只做前半段会让这个能力整局不消失、把后续每回合都算强，等 PR #245 一起接 |
| WishPlatedArmorPower | AfterDamageReceived、BeforeSideTurnEnd | 已镜像 | |
| Wrath | ModifyDamageMultiplicative | 已镜像 | |

## 已镜像的牌池卡

| 卡牌 |
|---|
| 阿尔法（Alpha） |
| 战歌（BattleHymn） |
| 渎神（Blasphemy） |
| 碰撞连击（BowlingBash） |
| 光辉（Brilliance） |
| 改造现实（CarveReality） |
| 浩劫（Cataclysm） |
| 冷眼旁观（ColdObservation） |
| 收集（Collect） |
| 结末（Conclude） |
| 聚能成刃（ConjureBlade） |
| 供奉（Consecrate） |
| 渐强（Crescendo） |
| 粉碎关节（CrushJoints） |
| 斩破命运（CutThroughFate） |
| 欺瞒现实（DeceiveReality） |
| Defend_P |
| 机械降神（DeusExMachina） |
| 天人形态（DevaForm） |
| 虔信（Devotion） |
| 化体为空（EmptyBody） |
| 化拳为空（EmptyFist） |
| 化智为空（EmptyMind） |
| Eruption_P |
| 确立基础（Establishment） |
| 评估（Evaluate） |
| 斋戒（Fasting2） |
| 不惧妖邪（FearNoEvil） |
| 疾风连击（FlurryOfBlows） |
| 流云飞袖（FlyingSleeves） |
| 追击（FollowUp） |
| 先见之明（Foresight） |
| 停顿（Halt） |
| 义愤填膺（Indignation） |
| 内心宁静（InnerPeace） |
| 审判（Judgment） |
| 幸运一击（JustLucky） |
| 勤学精进（LessonLearned） |
| 如水（LikeWater） |
| 操控现实（MasterReality） |
| 冥想（Meditate） |
| 心灵堡垒（MentalFortress） |
| 涅槃（Nirvana） |
| 通晓万物（Omniscience） |
| 坚韧（Perseverance） |
| 祈祷（Pray） |
| 布道（Preach） |
| 点穴（PressurePoints） |
| 五体投地（Prostrate） |
| 护身（Protect） |
| 诸神之黄昏（Ragnarok） |
| 立地升天（ReachHeaven） |
| 猛虎下山（Rushdown） |
| 成圣（Sanctification） |
| 圣洁（Sanctity） |
| 时之沙（SandsOfTime） |
| 腰带抽打（SashWhip） |
| Scrawl_P |
| 宁静（Serenity） |
| 招牌技（SignatureMove） |
| 怒火中烧（SimmeringFury） |
| 精神护盾（SpiritShield） |
| Strike_P |
| 研习（Study） |
| 旋身（Swivel） |
| 以手拒之（TalkToTheHand） |
| 发泄（Tantrum） |
| 天眼（ThirdEye） |
| 安宁（Tranquility） |
| 腾跃（Vault） |
| 警惕（Vigilance） |
| 当头棒喝（Wallop） |
| 摆手（WaveOfTheHand） |
| 迂回（Weave） |
| 回环踢（WheelKick） |
| 旋转打击（WindmillStrike） |
| Wish_P |
| 敬拜（Worship） |
| 火焰纹（WreathOfFlame） |

## 带风险标记的近似（面板会显示提示）

- Scry 的弃牌选择由适配器按固定规则决定，不进入搜索分支
- Rushdown 抽牌时机早于 mod（数量正确）
- 连击从弃牌堆回手、预见的启明命运未建模
- 神性的回合末自动退出未建模
- 发脾气洗回抽牌堆未建模
- Study / Omega 的常规回合末效果不可用（求解器只开放 Late pass，PR #245 未合并）
- 两张以上的神性形态按单个计数器近似
