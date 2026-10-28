"""Generates C4 Best team: by type (best possible, from mine) and against a boss; phone + desktop."""
import pathlib
HERE = pathlib.Path(__file__).parent
LOCK = '<svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.6" stroke-linecap="round"><rect x="5" y="11" width="14" height="10" rx="2"/><path d="M8 11V7a4 4 0 0 1 8 0v4"/></svg>'
# sprite, name, tag, fast, charged, score, level/ivs (None = best possible), % base
BEST_G = [("383", "Groudon", "Primal", "Mud Shot", "Precipice Blades", 67.3, None),
          ("383", "Groudon", "Shadow", "Mud Shot", "Precipice Blades", 42.7, None),
          ("445", "Garchomp", "Shadow", "Mud Shot", "Earth Power", 40.8, None),
          ("645_12", "Landorus (Therian)", "", "Mud Shot", "Sandsear Storm", 40.6, None),
          ("383", "Groudon", "", "Mud Shot", "Precipice Blades", 39.9, None),
          ("530", "Excadrill", "Shadow", "Mud Slap", "Scorching Sands", 38.9, None)]
MINE_G = [("260", "Swampert", "Mega ready", "Mud Shot", "Earthquake", 47.9, "L40 · 14/13/15"),
          ("383", "Groudon", "", "Mud Shot", "Precipice Blades", 39.8, "L40 · 15/15/13"),
          ("464", "Rhyperior", "", "Mud Slap", "Earthquake", 32.2, "L40 · 13/15/15"),
          ("530", "Excadrill", "", "Mud Slap", "Drill Run", 32.1, "L40 · 15/14/15"),
          ("473", "Mamoswine", "", "Mud Slap", "High Horsepower", 30.2, "L35 · 12/15/14"),
          ("445", "Garchomp", "", "Dragon Tail", "Earth Power", 26.0, "L23 · 2/15/13")]
MINE_K = [("254_51", "Sceptile", "Mega ready", "Bullet Seed", "Frenzy Plant", 57.2, "L40 · 14/15/13"),
          ("644", "Zekrom", "Shadow", "Charge Beam", "Fusion Bolt", 42.2, "L35 · 13/15/14"),
          ("807", "Zeraora", "", "Spark", "Plasma Fists", 40.1, "L40 · 15/15/14"),
          ("462", "Magnezone", "", "Spark", "Wild Charge", 31.5, "L40 · 15/13/15"),
          ("389", "Torterra", "", "Razor Leaf", "Frenzy Plant", 30.8, "L40 · 11/15/15"),
          ("003", "Venusaur", "", "Vine Whip", "Frenzy Plant", 30.5, "L35 · 12/14/15")]
def slot(r, top, i, weak=False):
    sp, n, t, f, c, s, lv = r
    pc = round(s / top * 100)
    tg = f'<span class="tag {t.split()[0].lower()}">{t}</span>' if t else ''
    hd = f'<span class="lock">{LOCK}MEGA SLOT</span><a>Change</a>' if i == 0 else f'<span>{i + 1}</span>'
    note = '<div class="warnnote">Weak slot: under 40% of the best, the best of your Ground attackers left</div>' if weak else ''
    return f'''<div class="slot{" mega" if i == 0 else ""}{" weakslot" if weak else ""}"><div class="hd">{hd}</div><div class="mid"><img class="sprite" src="../sprites/{sp}.png" width="48" height="48" alt=""><div style="min-width:0"><b class="n">{n}</b><br>{tg}</div></div>
<span class="mv">{f} + {c}</span>{f'<span class="lv">{lv}</span>' if lv else ''}<div class="ft"><b>{s}</b><div class="sbar"><i style="width:{pc}%"></i></div><span>{pc}%</span></div>{note}</div>'''
def team(rows, top, weak_last=False):
    return '<div class="team">' + ''.join(slot(r, top, i, weak_last and i == 5) for i, r in enumerate(rows)) + '</div>'
TYPES = ["Ground", "Electric", "Grass", "Fire", "Water", "Ice", "Dragon"]
types = lambda on="Ground": '<div class="toolbar">' + ''.join(f'<span class="tbtn{" on" if t == on else ""}">{t}</span>' for t in TYPES) + '</div>'
seg = lambda ks, on, style="": f'<div class="seg" style="{style}">' + ''.join(f'<span class="{"on" if k == on else ""}">{k}</span>' for k in ks) + '</div>'
head = '<!doctype html><html lang="en"><head><meta charset="utf-8"><title>C4 Best team</title><link rel="stylesheet" href="../base.css"><link rel="stylesheet" href="_b1.css"><link rel="stylesheet" href="_c4.css"></head><body>'
phead = '''<header class="apphead"><div class="titlerow"><h1 class="bigtitle">Battle</h1><span data-ask></span><div class="avatar">A</div></div>
<div class="seg"><span>Raids</span><span>Tier lists</span><span class="on">Teams</span></div></header>'''
def phone(body):
    return head + f'''<div class="stage"><div class="device"><div class="screen"><div data-statusbar></div>{phead}
<div class="scroll"><div style="padding:12px 10px 20px;display:flex;flex-direction:column;gap:10px">{body}</div></div>
<div data-tabbar="battle"></div></div></div></div><script src="../chrome.js"></script></body></html>'''
src = lambda on: seg(["Best possible", "From my Pokémon"], on)
SUB_BEST = '<div class="sub" style="margin:0 4px">Best 6 Ground attackers · level 40, perfect IVs · % of the best one</div>'
SUB_MINE = '<div class="sub" style="margin:0 4px">Your best 6 Ground attackers from 2,148 Pokémon, at their real levels and IVs</div>'
p1 = phone(f'{seg(["By type", "Against a boss"], "By type")}{types()}{src("Best possible")}{SUB_BEST}{team(BEST_G, 67.3)}')
p2 = phone(f'{seg(["By type", "Against a boss"], "By type")}{types()}{src("From my Pokémon")}{SUB_MINE}{team(MINE_G, 67.3, True)}')
BOSS = '<div class="bossbar"><img class="sprite" src="../sprites/382.png" width="40" height="40" alt=""><div style="display:flex;flex-direction:column"><span class="sub" style="font-weight:800">AGAINST</span><b>Kyogre · 5★</b></div><span style="margin-left:auto;font-size:12.5px;font-weight:800;color:var(--text-2)">Moves: not sure</span><a style="font-weight:800;font-size:13.5px;margin-left:10px">Change</a></div>'
p3 = phone(f'{seg(["By type", "Against a boss"], "Against a boss")}{BOSS}{src("From my Pokémon")}<div class="sub" style="margin:0 4px">Your best team for Kyogre · same numbers as its raid page</div>{team(MINE_K, 57.3)}')
desk = head + f'''<div class="desktop"><div data-sidebar="battle"></div><div class="main"><div data-topbar></div>
<div class="content" style="max-width:1180px;display:flex;flex-direction:column;gap:16px">
<div style="display:flex;align-items:center;gap:20px"><h1 class="bigtitle" style="flex:none">Battle</h1>{seg(["Raids", "Tier lists", "Teams"], "Teams", "width:360px")}</div>
<div style="display:flex;align-items:center;gap:14px">{seg(["By type", "Against a boss"], "By type", "width:280px")}{types()}</div>
<div style="display:grid;grid-template-columns:1fr 1fr;gap:24px;align-items:start">
<div style="display:flex;flex-direction:column;gap:10px"><h2 style="margin:0;font-size:19px">Best possible</h2>{SUB_BEST}{team(BEST_G, 67.3).replace('class="team"', 'class="team" style="grid-template-columns:repeat(3,minmax(0,1fr))"')}</div>
<div style="display:flex;flex-direction:column;gap:10px"><h2 style="margin:0;font-size:19px">From my Pokémon</h2>{SUB_MINE}{team(MINE_G, 67.3, True).replace('class="team"', 'class="team" style="grid-template-columns:repeat(3,minmax(0,1fr))"')}</div></div>
<p class="sub" style="margin:0">Only one Mega fits in a raid team. Lock the Mega you'll use with "Change"; the other five are rebuilt around it. Same scores as the raid pages: damage/s and how long each lasts, combined.</p></div></div></div><script src="../chrome.js"></script></body></html>'''
(HERE / 'C4-phone.html').write_text(p1); (HERE / 'C4m-phone.html').write_text(p2); (HERE / 'C4b-phone.html').write_text(p3); (HERE / 'C4-desktop.html').write_text(desk)
