"""Generate the adapter coverage report straight from the source, so it cannot drift."""
import json, re, glob, os, collections

ADAPTER = r"D:/work/WatcherAdapter"
SCAN = r"C:/Users/wb007/AppData/Local/hermes/cache/scratch/watcher_scan2.json"
OUT = os.path.join(ADAPTER, "COVERAGE.md")

scan = {c["name"]: c for c in json.load(open(SCAN, encoding="utf-8"))}
all_cards = sorted(scan)

mirrored = {}
for path in glob.glob(os.path.join(ADAPTER, "Cards*.cs")):
    txt = open(path, encoding="utf-8").read()
    for m in re.finditer(r'Register<WatcherMod\.(\w+)>\("(\w+)",', txt):
        mirrored[m.group(1)] = os.path.basename(path)

draw_mirrors = []
p = os.path.join(ADAPTER, "DrawMirrors.cs")
if os.path.exists(p):
    txt = open(p, encoding="utf-8").read()
    draw_mirrors = re.findall(r"Register<WatcherMod\.(\w+)>", txt)

prewarmed = set()
p = os.path.join(ADAPTER, "Prewarm.cs")
if os.path.exists(p):
    prewarmed = set(re.findall(r"typeof\(WatcherMod\.(\w+)\)", open(p, encoding="utf-8").read()))

power_mirrors = []
p = os.path.join(ADAPTER, "PowerMirrors.cs")
if os.path.exists(p):
    txt = open(p, encoding="utf-8").read()
    for m in re.finditer(r'Guard\("([^"]+)"', txt):
        power_mirrors.append(m.group(1))
    for m in re.finditer(r'Entry\.Log\("power mirror unavailable: ([^"]+)"', txt):
        power_mirrors.append("不可用：" + m.group(1))

hook_mirrors = []
p = os.path.join(ADAPTER, "HookMirrors.cs")
if os.path.exists(p):
    txt = open(p, encoding="utf-8").read()
    hook_mirrors = re.findall(r'RegisterStance<WatcherMod\.(\w+)>\((\d+)m', txt)

# Reasons for the cards we deliberately did not mirror.

BLOCKED = {
    "Alpha": "效果在抽到时/生成链上，OnPlay 为空",
    "Beta": "同上",
    "Brilliance": "需要维护「本场累计真言」的隐藏计数（分支无此状态）",
    "Conclude": "强制结束回合，属于回合层，镜像碰不到",
    "ConjureBlade": "生成的 Expunger 需要写入自定义字段（HitCount），生成入口不支持",
    "DrawTalisman": "需要二选一的选牌决策",
    "FearNoEvil": "需要读取怪物意图（分支无 intent）",
    "ForeignInfluence": "需要三选一的选牌决策",
    "LessonLearned": "斩杀后升级牌库，属于战斗外状态",
    "Meditate": "需要从弃牌堆选牌的决策",
    "Mockery": "生成 0 费复制牌，成本改写入口不可达",
    "Omniscience": "需要「选一张牌并再次打出」的决策",
    "Persuasion": "同 Mockery",
    "Relinquish": "额外回合，属于回合层",
    "Vault": "额外回合，属于回合层",
    "Wish_P": "需要三选一的选牌决策",
}

lines = []
lines.append("# 覆盖报告（自动生成）\n")
lines.append("本文件由 `tools/coverage_report.py` 从源码生成，请勿手改。\n")
lines.append(f"- 卡牌镜像：**{len(mirrored)} / {len(all_cards)}**（有 OnPlay 的卡）")
lines.append(f"- 抽牌时点镜像：{len(draw_mirrors)}")
lines.append(f"- Power 持续效果镜像：{len([p for p in power_mirrors if not p.startswith('不可用')])}")
lines.append(f"- 姿态伤害倍率：{', '.join('%s ×%s' % (n.replace('Watcher', ''), v) for n, v in hook_mirrors)}")
lines.append(f"- Power 预热（首次施加前必须在主线程物化）：{len(prewarmed)} 个\n")

lines.append("## 已镜像的卡牌\n")
lines.append("| 卡牌 | 来源批次 |\n|---|---|")
for name in sorted(mirrored):
    lines.append(f"| {name.replace('Watcher', '')} | {mirrored[name]} |")

lines.append("\n## 抽牌时点\n")
for name in draw_mirrors:
    lines.append(f"- {name}（抽到即消耗并生成奇迹）")

lines.append("\n## Power 持续效果\n")
for entry in power_mirrors:
    lines.append(f"- {entry}")

lines.append("\n## 明确未镜像的卡牌与原因\n")
lines.append("| 卡牌 | 原因 |\n|---|---|")
for name in all_cards:
    if name in mirrored:
        continue
    short = name.replace("Watcher", "")
    lines.append(f"| {short} | {BLOCKED.get(short, '需要求解器当前未开放的能力')} |")

lines.append("\n## 带风险标记的近似（面板会显示提示）\n")
lines.append("- Scry 的弃牌选择由适配器按固定规则决定，不进入搜索分支")
lines.append("- Rushdown 抽牌时机早于 mod（数量正确）")
lines.append("- 连击从弃牌堆回手、预见的启明命运未建模")
lines.append("- 神性的回合末自动退出未建模")
lines.append("- 发脾气洗回抽牌堆未建模")
lines.append("- Study / Omega 的回合末效果不可用（求解器只开放 Late pass）")
lines.append("- 两张以上的神性形态按单个计数器近似")

open(OUT, "w", encoding="utf-8").write("\n".join(lines) + "\n")
print("wrote", OUT)
print("mirrored:", len(mirrored), "of", len(all_cards))
