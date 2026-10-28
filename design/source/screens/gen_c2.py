"""Generates C2 Raid boss page (Kyogre): phone tabs + desktop."""
import pathlib
HERE = pathlib.Path(__file__).parent
T = lambda ts: ''.join(f'<span class="type {t}">{t.capitalize()}</span>' for t in ts)
TOP = 57.3
# sprite, name, tag, fast, charged, elite charged, score, dps, lasts
BEST = [("254_51", "Sceptile", "Mega", "Bullet Seed", "Frenzy Plant", True, 57.3, 25.4, 26),
        ("644", "Zekrom", "Shadow", "Charge Beam", "Fusion Bolt", True, 44.6, 19.3, 29),
        ("807", "Zeraora", "", "Volt Switch", "Plasma Fists", False, 41.5, 17.9, 29),
        ("243", "Raikou", "Shadow", "Thunder Shock", "Wild Charge", False, 41.2, 18.1, 27),
        ("466", "Electivire", "Shadow", "Thunder Shock", "Wild Charge", False, 39.8, 18.8, 20),
        ("652", "Chesnaught", "Shadow", "Vine Whip", "Frenzy Plant", True, 39.3, 17.1, 28),
        ("893", "Zarude", "", "Vine Whip", "Power Whip", False, 39.2, 15.7, 39),
        ("894", "Regieleki", "", "Thunder Shock", "Thunder Cage", True, 39.2, 18.5, 20),
        ("796", "Xurkitree", "", "Thunder Shock", "Discharge", False, 39.1, 17.8, 23)]
# sprite, name, tag, fast, charged, score, dps, lasts, level, ivs, cp
MINE = [("254_51", "Sceptile", "Mega ready", "Bullet Seed", "Frenzy Plant", 57.2, 25.4, 26, "40", "14/15/13", "4,022"),
        ("644", "Zekrom", "Shadow", "Charge Beam", "Fusion Bolt", 42.2, 18.6, 26, "35", "13/15/14", "3,716"),
        ("807", "Zeraora", "", "Spark", "Plasma Fists", 40.1, 17.3, 29, "40", "15/15/14", "3,411"),
        ("462", "Magnezone", "", "Spark", "Wild Charge", 31.5, 13.7, 28, "40", "15/13/15", "3,190"),
        ("389", "Torterra", "", "Razor Leaf", "Frenzy Plant", 30.8, 12.9, 32, "40", "11/15/15", "2,880"),
        ("003", "Venusaur", "", "Vine Whip", "Frenzy Plant", 30.5, 13.4, 27, "35", "12/14/15", "2,484")]
def tag(t):
    if not t: return ''
    c = {"Mega": "mega", "Shadow": "shadow", "Mega ready": "ready"}[t]
    return f'<span class="tag {c}">{t}</span>'
def sc(score):
    pc = round(score / TOP * 100)
    return f'<div class="sc"><b>{score}</b><div class="sbar"><i style="width:{pc}%"></i></div><span class="pc">{pc}% of best</span></div>'
def brow(i, r):
    sp, n, tg, f, c, el, s, dps, life = r
    return f'''<div class="crow"><span class="rk">{i}</span><img class="sprite" src="../sprites/{sp}.png" width="46" height="46" alt=""><div class="nm"><b>{n}{tag(tg)}</b>
<span class="mv">{f} + {c}{" <span class=etm>Elite TM</span>" if el else ""}</span><span class="st">{dps} damage/s · lasts ~{life} s</span></div>{sc(s)}</div>'''
def mrow(i, r):
    sp, n, tg, f, c, s, dps, life, lv, iv, cp = r
    return f'''<div class="crow"><span class="rk">{i}</span><img class="sprite" src="../sprites/{sp}.png" width="46" height="46" alt=""><div class="nm"><b>{n}{tag(tg)}</b>
<span class="mv">{f} + {c}</span><span class="st">L{lv} · {iv} · CP {cp}</span></div>{sc(s)}</div>'''
MOVES = '''<div class="pad"><span class="lbl">KYOGRE'S MOVES — pick them when you see them in the lobby</span><div class="mpick"><span class="on">Not sure (average)</span><span><i class="d" style="background:var(--t-water)"></i>Origin Pulse</span><span><i class="d" style="background:var(--t-water)"></i>Surf</span><span><i class="d" style="background:var(--t-ice)"></i>Blizzard</span><span><i class="d" style="background:var(--t-electric)"></i>Thunder</span></div></div>'''
HOW = '''<div class="how"><b>How the raid score works</b>
<span>It combines two things you can see on each line: <b>damage per second</b> and <b>how long it lasts</b> before fainting. Speed counts most: the score is (damage/s³ × total damage)<sup>¼</sup>, the formula raid guides use.</span>
<span>Read it as a comparison: a counter with <b>80% of best</b> takes roughly a quarter longer to do the same work as the top one.</span>
<span>Best counters assume level 40 with perfect IVs; <b>Your counters</b> use your real levels, IVs and moves.</span></div>'''
def counters(phone):
    best = ''.join(brow(i + 1, r) for i, r in enumerate(BEST))
    how = HOW if not phone else ''
    link = '<div class="howlink">How the raid score works<span style="font-size:18px">›</span></div>' if phone else ''
    return f'''<section class="sec"><h2>Best counters</h2><div class="card" style="overflow:hidden">{MOVES}<div style="border-top:1px solid var(--line)"></div>{best}{link}</div>{f'<div class="card">{how}</div>' if how else ''}</section>'''
def yours():
    return f'''<section class="sec"><h2>Your counters</h2><div class="card" style="overflow:hidden"><div class="pad" style="padding-bottom:4px"><span class="sub">Your best 6 from 2,148 Pokémon (one Mega)</span></div>{''.join(mrow(i + 1, r) for i, r in enumerate(MINE))}
<div class="howlink">See all 38 that can help<span style="font-size:18px">›</span></div></div></section>'''
def boss():
    mv = [("water", "Waterfall", "Fast"), ("water", "Origin Pulse", "Charged"), ("water", "Surf", "Charged"), ("ice", "Blizzard", "Charged · hits Grass counters hard"), ("electric", "Thunder", "Charged · Electric counters resist it")]
    rows = ''.join(f'<div class="mrow2"><span class="dot" style="background:var(--t-{t});width:9px;height:9px"></span>{n}<span class="r">{k}</span></div>' for t, n, k in mv)
    return f'''<section class="sec"><h2>Boss</h2><div class="card" style="overflow:hidden"><div class="pad">
<div><div class="lbl" style="margin-bottom:6px">WEAK TO</div><div class="mm">{T(["electric", "grass"]).replace('class="type', 'style="height:24px;font-size:12px;padding:0 9px" class="type')} <span class="sub">×1.6</span></div></div>
<div><div class="lbl" style="margin-bottom:6px">RESISTS</div><div class="mm">{T(["fire", "ice", "steel", "water"]).replace('class="type', 'style="height:24px;font-size:12px;padding:0 9px" class="type')} <span class="sub">×0.63</span></div></div></div>
<div class="pad" style="padding-bottom:6px;border-top:1px solid var(--line)"><span class="lbl">POSSIBLE MOVES</span></div>{rows}</div></section>'''
def catch():
    return f'''<section class="sec"><h2>Catch</h2><div class="card" style="overflow:hidden"><div class="pad">
<div class="kv"><div><span>100% CP</span><b>2,351</b></div><div><span>Boosted (Rainy)</span><b>2,939</b></div><div><span>Lowest (10/10/10)</span><b>2,260</b></div></div></div>
<div class="pad" style="border-top:1px solid var(--line)"><b style="font-size:15px">Check the CP on the catch screen</b>
<div class="cpin"><input value="2,320"><span class="chk"><i></i>Boosted</span></div>
<div class="ivres"><span>13 / 12 / 15</span><span>89%</span><span>13 / 13 / 14</span><span>89%</span><span>15 / 13 / 11</span><span>87%</span><span>15 / 14 / 10</span><span>87%</span></div>
<span class="sub">4 possible IV combinations · not a hundo</span></div></div></section>'''
hero = lambda size: f'''<div class="hero"><img class="sprite" src="../sprites/382.png" width="{size}" height="{size}" alt="Kyogre"><div style="display:flex;flex-direction:column;gap:6px"><span class="no">5★ RAID · until Tue 3 Nov</span><h1>Kyogre</h1><span style="display:flex;gap:5px;align-items:center">{T(["water"])}<span class="hour" style="font-size:11px;font-weight:800;color:var(--accent);background:var(--accent-soft);padding:2px 7px;border-radius:7px">Raid Hour Wed 18:00</span></span></div></div>'''
TABS = [("c", "Counters"), ("y", "Yours"), ("b", "Boss"), ("k", "Catch")]
BODY = {"c": lambda: counters(True), "y": yours, "b": boss, "k": catch}
tabs = lambda on: '<div class="ptabs">' + ''.join(f'<span class="{"on" if k == on else ""}">{n}</span>' for k, n in TABS) + '</div>'
head = '<!doctype html><html lang="en"><head><meta charset="utf-8"><title>C2 Raid boss</title><link rel="stylesheet" href="../base.css"><link rel="stylesheet" href="_b2.css"><link rel="stylesheet" href="_c2.css"></head><body>'
def phone(tab):
    return head + f'''<div class="stage tint"><div class="device"><div class="screen">
<div data-statusbar></div><div class="band"><div data-dethead="Raids|"></div>{hero(84)}{tabs(tab)}</div>
<div class="scroll"><div style="padding:14px 10px 20px;display:flex;flex-direction:column;gap:22px">{BODY[tab]()}</div></div>
<div data-tabbar="battle"></div></div></div></div><script src="../chrome.js"></script>
<script>document.querySelector('.statusbar').style.background='color-mix(in srgb, var(--t-water) 13%, var(--surface))';</script></body></html>'''
# phone "how it works" sheet over the counters tab
sheet = f'''<div style="position:absolute;inset:0;background:rgba(10,20,18,.35)"></div><div style="position:absolute;left:0;right:0;bottom:0;background:var(--surface);border-radius:24px 24px 0 0;padding:8px 6px 40px">
<div style="width:40px;height:5px;border-radius:3px;background:var(--line);margin:0 auto 8px"></div>{HOW.replace('<b>How the raid score works</b>', '<b style="font-size:18px">How the raid score works</b>')}
<div class="how" style="padding-top:0"><span>Example — <b>Mega Sceptile</b>: 25.4 damage/s, lasts ~26 s → score <b>57.3</b>. <b>Zeraora</b>: 17.9 damage/s, lasts ~29 s → score <b>41.5</b> (72% of best).</span></div></div>'''
desk = head + f'''<div class="desktop tint"><div data-sidebar="battle"></div><div class="main"><div data-topbar></div>
<div class="band" style="padding:18px 32px 8px"><a class="backlink">‹ Raids</a>{hero(140)}</div>
<div class="content" style="max-width:1200px;display:grid;grid-template-columns:minmax(0,1.1fr) minmax(0,1fr);gap:24px;align-items:start">
<div style="display:flex;flex-direction:column;gap:22px">{counters(False)}</div>
<div style="display:flex;flex-direction:column;gap:22px">{yours()}{boss()}{catch()}</div></div></div></div><script src="../chrome.js"></script></body></html>'''
for k, _ in TABS:
    (HERE / f'C2{"" if k == "c" else k}-phone.html').write_text(phone(k))
(HERE / 'C2s-phone.html').write_text(phone("c").replace('<div data-tabbar="battle"></div></div>', f'<div data-tabbar="battle"></div>{sheet}</div>'))
(HERE / 'C2-desktop.html').write_text(desk)
