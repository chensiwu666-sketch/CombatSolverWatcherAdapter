"""Generate the adapter coverage report straight from the source, so it cannot drift.

Rewritten 2026-11-11. The old version read a card list from the agent's scratch directory (pruned
after 24h) and from the pre-migration working copy, so it silently kept reporting stale numbers,
and its hand-written "cannot be done" table had 11 cards whose stated reason was wrong (verified
against both source trees). This version reads:

  * the Watcher mod's decompiled source -> the authoritative card list and card pool
  * this repository's *.cs files       -> what is actually registered

Run:  python tools/coverage_report.py
"""

import os
import re
import sys

ADAPTER = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
WATCHER_SRC = os.environ.get("WATCHER_SRC", r"D:/work/watcher_src/src/WatcherMod")
WATCHER_LOC = os.environ.get("WATCHER_LOC", r"D:/work/watcher_loc_zh.json")
OUT = os.path.join(ADAPTER, "COVERAGE.md")

# Card display names: the mod ships its own Chinese localisation, and the user reads card names in
# Chinese. Screenshots of the in-game UI must match this table, so it is read from the extracted
# localisation rather than translated here.
ZH = {}
if os.path.exists(WATCHER_LOC):
    import json
    ZH = json.load(open(WATCHER_LOC, encoding="utf-8"))


def card_label(type_name):
    """WatcherFearNoEvil -> WATCHER_FEAR_NO_EVIL -> 不惧妖邪 (falls back to the short English name)."""
    short = type_name.replace("Watcher", "")
    key = re.sub(r"(?<!^)(?=(?<=_)[A-Z])", "_", re.sub(r"(?<!^)(?=[A-Z])", "_", short)).upper()
    key = key.replace("__", "_")
    value = ZH.get("WATCHER_" + key) or ZH.get(key)
    return f"{value}（{short}）" if value else short

if not os.path.isdir(WATCHER_SRC):
    sys.exit(f"Watcher 反编译源码目录不存在：{WATCHER_SRC}\n用 WATCHER_SRC=... 指向正确位置。")

# ---------------------------------------------------------------- mod side
card_types = set()
for name in os.listdir(WATCHER_SRC):
    if not name.endswith(".cs"):
        continue
    text = open(os.path.join(WATCHER_SRC, name), encoding="utf-8", errors="replace").read()
    for m in re.finditer(r"class\s+(\w+)\s*:\s*WatcherCard\b", text):
        card_types.add(m.group(1))

pool_text = open(os.path.join(WATCHER_SRC, "WatcherCardPool.cs"), encoding="utf-8").read()
pool = []
for m in re.finditer(r"ModelDb\.Card<(Watcher\w+)>\(\)", pool_text):
    if m.group(1) not in pool:
        pool.append(m.group(1))
declared = re.search(r"CardModel\[(\d+)\]", pool_text)
declared_count = declared.group(1) if declared else str(len(pool))

# ---------------------------------------------------------------- adapter side
registered = set()
for name in os.listdir(ADAPTER):
    if not name.endswith(".cs"):
        continue
    text = open(os.path.join(ADAPTER, name), encoding="utf-8").read()
    for m in re.finditer(r"Register<(?:\w+\.)?(\w+)>", text):
        if m.group(1) in card_types:
            registered.add(m.group(1))

prewarmed = set()
pw = os.path.join(ADAPTER, "Prewarm.cs")
if os.path.exists(pw):
    prewarmed = set(re.findall(r"typeof\(WatcherMod\.(\w+)\)", open(pw, encoding="utf-8").read()))

hook_mirrors, power_mirrors, draw_mirrors = [], [], []
hm = os.path.join(ADAPTER, "HookMirrors.cs")
if os.path.exists(hm):
    hook_mirrors = re.findall(r"RegisterStance<WatcherMod\.(\w+)>\((\d+)m", open(hm, encoding="utf-8").read())
pm = os.path.join(ADAPTER, "PowerMirrors.cs")
if os.path.exists(pm):
    t = open(pm, encoding="utf-8").read()
    power_mirrors = re.findall(r'Guard\("([^"]+)"', t)
    power_mirrors += ["不可用：" + n for n in re.findall(r'Entry\.Log\("power mirror unavailable: ([^"]+)"', t)]
dm = os.path.join(ADAPTER, "DrawMirrors.cs")
if os.path.exists(dm):
    draw_mirrors = re.findall(r"Registry\.Register<WatcherMod\.(\w+)>", open(dm, encoding="utf-8").read())

# ---------------------------------------------------------------- gap reasons (verified 2026-11-11)
# Every entry was checked against the decompiled mod source AND the installed solver's 0.50.1 API
# list. "可做" = the solver already exposes the entry point; only the mirror is missing.
GAP = {
    # --- solver entry point already exists, mirror not written yet ---
    "WatcherAlpha": ("可做", "打出后生成 Beta 进抽牌堆随机位（旧记录误标为「OnPlay 为空」）；"
                             "CreateAndAddGeneratedCardsToCombat(Draw, Random) 已具备"),
    "WatcherBeta": ("可做", "同 Alpha：打出后生成 Omega 进抽牌堆"),
    "WatcherBrilliance": ("可做", "伤害 + 本场累计真言；需把 WatcherStatePower.TotalMantraGainedThisCombat "
                                  "登记成 Power 隐藏状态，并在 MantraOps 里同步计数"),
    "WatcherConclude": ("可做", "伤害 + IsLastInSeries 时结束回合；SimulatedCombatState.RequestPlayerTurnEnd() "
                                "是 public，CardOnPlayMirrorContext.CardPlay 里有 IsLastInSeries"),
    "WatcherConjureBlade": ("可做", "X 费生成 Expunger 进抽牌堆并把 Repeat 设为 X（升级 +1）；"
                                    "生成结果 SimCardPileAddResult 带 PredictedCard，可写 DynamicVars"),
    "WatcherFearNoEvil": ("可做", "伤害 + 目标在攻击则进平静；SimulatedCombatState.IsEnemyIntendingToAttack() 是 public"),
    "WatcherForeignInfluence": ("做不了", "三张选项由 mod 用游戏 RNG 现场抽取，预测侧无法得知实机页面上是哪三张，"
                                            "部署时按 CardId 定位必然错位；选项固定的许愿则可以精确登记"),
    "WatcherLessonLearned": ("可做（部分）", "伤害部分可精确镜像；斩杀后升级牌库属局外状态，战斗内无数值影响，需打风险标记"),
    "WatcherMeditate": ("可做", "弃牌堆选 1 张回手 + 进平静 + 若为本张系列最后一张则结束回合；"
                                "CardChoiceMirrors + RequestPlayerTurnEnd"),
    "WatcherOmniscience": ("可做", "从抽牌堆选 1 张自动打出两次；CardChoiceMirrors + simulator.AutoPlay"),
    "WatcherVault": ("可做", "额外回合；ExtraTurnMirrors.RegisterShouldTakeExtraTurn<WatcherExtraTurnPower> + 移除该 Power"),
    "WatcherWish_P": ("可做", "三选一（力量/多层护甲/金币）；CardChoiceMirrors——求解器手册 §2.5 正是拿「观者的许愿」当示例"),

    # --- cannot appear in single player / not real cards ---
    "WatcherMockery": ("不适用", "MultiplayerConstraint.MultiplayerOnly：单人局奖励与商店里不会出现"),
    "WatcherPersuasion": ("不适用", "同上"),
    "WatcherRelinquish": ("不适用", "MultiplayerOnly：给队友额外回合，单人局不出现"),
    "WatcherIntentProxy": ("不适用", "不是卡牌，是意图代理"),
    "WatcherV2ChoiceTokenBase": ("不适用", "抽象基类，不是可打出的卡"),
    "WatcherWishAlmighty": ("不适用", "许愿的选项令牌，随 Wish_P 一起完成"),
    "WatcherWishFameAndFortune": ("不适用", "许愿的选项令牌，随 Wish_P 一起完成"),
    "WatcherWishLiveForever": ("不适用", "许愿的选项令牌，随 Wish_P 一起完成"),
    "WatcherDrawTalismanDirectedHand": ("不适用", "DrawTalisman 的选项令牌，随 DrawTalisman"),
    "WatcherDrawTalismanRandomDeck": ("不适用", "DrawTalisman 的选项令牌，随 DrawTalisman"),
    "WatcherHymnV2": ("不适用", "Unplayable，且全 mod 无生成引用、不在牌池：玩家遇不到"),
    "WatcherRevelation_P": ("不适用", "Unplayable，且全 mod 无生成引用、不在牌池：玩家遇不到"),

    # --- genuinely out of reach ---
    "WatcherDrawTalisman": ("做不了", "全部效果是「给牌附魔（Enchantment）」，求解器没有附魔概念；"
                                      "不做比近似成白打更诚实"),
}

missing_pool = [c for c in pool if c not in registered]
outside = sorted(card_types - set(pool) - registered)

lines = ["# 覆盖报告（自动生成）\n",
         "本文件由 `tools/coverage_report.py` 从**两棵源码树**生成，请勿手改。\n",
         f"- 观者 mod 的卡牌类：{len(card_types)} 个",
         f"- 牌池（玩家在奖励/商店能看到）：**{len(pool)} 张**（mod 声明 `CardModel[{declared_count}]`）",
         f"- 牌池已镜像：**{len(pool) - len(missing_pool)} / {len(pool)}**",
         f"- 已注册卡牌类型合计：{len(registered)}",
         f"- 抽牌时点镜像：{len(draw_mirrors)}（{', '.join(card_label(n) for n in draw_mirrors) or '—'}）",
         f"- Power 持续效果镜像：{len([p for p in power_mirrors if not p.startswith('不可用') and not p.startswith('hidden state')])}",
         f"- Power 隐藏状态进指纹：{len([p for p in power_mirrors if p.startswith('hidden state')])} 项",
         f"- 姿态伤害倍率：{', '.join('%s ×%s' % (card_label(n), v) for n, v in hook_mirrors)}",
         f"- Power 预热：{len(prewarmed)} 个\n"]

lines.append("## 牌池里还未镜像的卡\n")
lines.append("| 卡牌 | 判定 | 实测原因 / 缺什么 |\n|---|---|---|")
for name in missing_pool:
    verdict, why = GAP.get(name, ("未分类", "⚠ 原因表缺这一项，请补"))
    lines.append(f"| {card_label(name)} | {verdict} | {why} |")

lines.append("\n## 牌池之外的未镜像类型（生成牌 / 令牌 / 基类）\n")
lines.append("| 类型 | 判定 | 说明 |\n|---|---|---|")
for name in outside:
    verdict, why = GAP.get(name, ("未分类", "⚠ 原因表缺这一项，请补"))
    lines.append(f"| {card_label(name)} | {verdict} | {why} |")

lines.append("\n## 已镜像的牌池卡\n")
lines.append("| 卡牌 |\n|---|")
for name in sorted(c for c in pool if c in registered):
    lines.append(f"| {card_label(name)} |")

lines.append("\n## 带风险标记的近似（面板会显示提示）\n")
lines.append("- Scry 的弃牌选择由适配器按固定规则决定，不进入搜索分支")
lines.append("- Rushdown 抽牌时机早于 mod（数量正确）")
lines.append("- 连击从弃牌堆回手、预见的启明命运未建模")
lines.append("- 神性的回合末自动退出未建模")
lines.append("- 发脾气洗回抽牌堆未建模")
lines.append("- Study / Omega 的常规回合末效果不可用（求解器只开放 Late pass，PR #245 未合并）")
lines.append("- 两张以上的神性形态按单个计数器近似")

open(OUT, "w", encoding="utf-8").write("\n".join(lines) + "\n")

unclassified = [c for c in missing_pool + outside if c not in GAP]
print(f"wrote {OUT}")
print(f"card types {len(card_types)} | pool {len(pool)} | mirrored pool {len(pool) - len(missing_pool)} | registered {len(registered)}")
if unclassified:
    print("WARNING unclassified:", ", ".join(unclassified))
