"""Generates C3 Tier lists: PvP (all, only mine) and Raids (Ground), phone + desktop."""
import pathlib
HERE = pathlib.Path(__file__).parent
# rank, sprite, name, shadow, fast, charged1, charged2, score
GL = [(1, "809", "Melmetal", False, "Thunder Shock", "Double Iron Bash", "Dynamic Punch", 93.9),
      (2, "334", "Altaria", False, "Dragon Breath", "Moonblast", "Flamethrower", 92.9),
      (3, "038", "Ninetales", True, "Ember", "Weather Ball", "Energy Ball", 92.7),
      (4, "845", "Cramorant", False, "Peck", "Dive", "Fly", 92.0),
      (5, "778_DISGUISED", "Mimikyu", False, "Shadow Claw", "Shadow Sneak", "Play Rough", 92.0),
      (6, "959", "Tinkaton", False, "Fairy Wind", "Gigaton Hammer", "Bulldoze", 91.7),
      (7, "334", "Altaria", True, "Dragon Breath", "Moonblast", "Flamethrower", 91.6),
      (8, "222_GALARIAN", "Galarian Corsola", False, "Astonish", "Night Shade", "Power Gem", 91.5),
      (9, "823", "Corviknight", False, "Sand Attack", "Air Cutter", "Iron Head", 91.4),
      (10, "671_RED", "Florges", False, "Fairy Wind", "Chilling Water", "Moonblast", 91.3)]
# rank, sprite, name, fast, c1, c2, score, your ivs, iv rank, cp
MINE = [(2, "334", "Altaria", "Dragon Breath", "Moonblast", "Flamethrower", 92.9, "0/14/15", "1", "1,497"),
        (9, "823", "Corviknight", "Sand Attack", "Air Cutter", "Iron Head", 91.4, "4/13/14", "1,396", "1,471"),
        (12, "195", "Quagsire", "Mud Shot", "Aqua Tail", "Stone Edge", 91.2, "2/15/12", "176", "1,486"),
        (16, "980", "Clodsire", "Poison Sting", "Earthquake", "Stone Edge", 90.7, "1/15/15", "236", "1,483"),
        (24, "197", "Umbreon", "Snarl", "Dark Pulse", "Last Resort", 90.2, "1/14/13", "616", "1,475"),
        (25, "979", "Annihilape", "Low Kick", "Rage Fist", "Ice Punch", 90.1, "3/12/15", "50", "1,494"),
        (29, "184", "Azumarill", "Bubble", "Ice Beam", "Play Rough", 90.0, "0/15/14", "6", "1,496"),
        (54, "308", "Medicham", "Counter", "Ice Punch", "Psychic", 89.3, "1/15/15", "333", "1,451")]
# rank, sprite, name, tag, fast, charged, score, dps, lasts
GROUND = [(1, "383", "Groudon", "Shadow", "Mud Shot", "Precipice Blades", 42.7, 18.1, 31),
          (2, "445", "Garchomp", "Shadow", "Mud Shot", "Earth Power", 40.8, 17.4, 30),
          (3, "645_12", "Landorus (Therian)", "", "Mud Shot", "Sandsear Storm", 40.6, 17.4, 29),
          (4, "383", "Groudon", "", "Mud Shot", "Precipice Blades", 39.9, 16.2, 37),
          (5, "530", "Excadrill", "Shadow", "Mud Slap", "Scorching Sands", 38.9, 18.1, 21),
          (6, "645_11", "Landorus", "Shadow", "Mud Shot", "Earth Power", 38.8, 17.4, 25),
          (7, "464", "Rhyperior", "Shadow", "Mud Slap", "Drill Run", 38.0, 16.0, 32),
          (8, "473", "Mamoswine", "Shadow", "Mud Slap", "High Horsepower", 36.7, 16.6, 24),
          (9, "445", "Garchomp", "", "Mud Shot", "Earth Power", 34.2, 13.9, 37),
          (10, "623", "Golurk", "Shadow", "Mud Slap", "Earth Power", 33.5, 15.6, 21)]
TOP = 67.3
tg = lambda t: f'<span class="tag {t.lower()}">{t}</span>' if t else ''
def prow(r):
    rk, sp, n, sh, f, c1, c2, s = r
    return f'<div class="pr"><span class="rk">{rk}</span><img class="sprite" src="../sprites/{sp}.png" width="42" height="42" alt=""><div class="nm"><b>{n}{tg("Shadow" if sh else "")}</b><span class="mv">{f} · {c1}, {c2}</span></div><span class="sc">{s}</span></div>'
def mrow(r):
    rk, sp, n, f, c1, c2, s, iv, ivr, cp = r
    return f'<div class="pr"><span class="rk">{rk}</span><img class="sprite" src="../sprites/{sp}.png" width="42" height="42" alt=""><div class="nm"><b>{n}</b><span class="mv">{f} · {c1}, {c2}</span><span class="mine">Yours: {iv} · IV rank {ivr} · CP {cp}</span></div><span class="sc">{s}</span></div>'
def rrow(r):
    rk, sp, n, t, f, c, s, dps, life = r
    pc = round(s / TOP * 100)
    return f'<div class="pr"><span class="rk">{rk}</span><img class="sprite" src="../sprites/{sp}.png" width="42" height="42" alt=""><div class="nm"><b>{n}{tg(t)}</b><span class="mv">{f} + {c}</span><span class="mv" style="color:var(--text-3)">{dps} damage/s · lasts ~{life} s</span></div><span class="sc">{s}<span class="sbar"><i style="width:{pc}%"></i></span><small>{pc}% of best</small></span></div>'
TIERS = [("S", "Score 90 and above · 33 Pokémon"), ("A", "Score 85–90 · 98 Pokémon"), ("B", "Score 80–85 · 166 Pokémon"), ("C", "Score 75–80 · 204 Pokémon")]
th = lambda t, d: f'<div class="tierh"><span class="tl {t}">{t}</span><span>{d}</span></div>'
head = '<!doctype html><html lang="en"><head><meta charset="utf-8"><title>C3 Tier lists</title><link rel="stylesheet" href="../base.css"><link rel="stylesheet" href="_b1.css"><link rel="stylesheet" href="_c3.css"></head><body>'
phead = '''<header class="apphead"><div class="titlerow"><h1 class="bigtitle">Battle</h1><span data-ask></span><div class="avatar">A</div></div>
<div class="seg"><span>Raids</span><span class="on">Tier lists</span><span>Teams</span></div></header>'''
def leagues(on="Great"): return '<div class="toolbar">' + ''.join(f'<span class="tbtn{" on" if l == on else ""}">{l}</span>' for l in ["Great", "Ultra", "Master"]) + '</div>'
def kind(on): return '<div class="seg">' + ''.join(f'<span class="{"on" if k == on else ""}">{k}</span>' for k in ["PvP", "Raids"]) + '</div>'
def own(on): return f'<div class="toggle"><span class="sw{" on" if on else ""}"></span>Only what I own</div>'
def phone(body):
    return head + f'''<div class="stage"><div class="device"><div class="screen"><div data-statusbar></div>{phead}
<div class="scroll"><div style="padding:12px 10px 20px;display:flex;flex-direction:column;gap:12px">{body}</div></div>
<div data-tabbar="battle"></div></div></div></div><script src="../chrome.js"></script></body></html>'''
pvp = phone(f'''{kind("PvP")}{leagues()}{own(False)}
<div class="card" style="overflow:hidden">{th(*TIERS[0])}{''.join(prow(r) for r in GL)}<div class="pr" style="display:block;text-align:center;font-weight:800;color:var(--accent);font-size:14px">Show all 33 in S ›</div>{th(*TIERS[1])}</div>''')
mine = phone(f'''{kind("PvP")}{leagues()}{own(True)}
<div class="sub" style="margin:0 4px">You own 112 of the 501 ranked Pokémon. Your best copy of each is shown.</div>
<div class="card" style="overflow:hidden">{th(*TIERS[0])}{''.join(mrow(r) for r in MINE[:7])}{th("A", "Score 85–90 · you own 21")}{mrow(MINE[7])}</div>''')
TYPES = ["Ground", "Electric", "Grass", "Fire", "Water", "Ice", "Dragon", "…"]
types = lambda: '<div class="toolbar">' + ''.join(f'<span class="tbtn{" on" if t == "Ground" else ""}">{t}</span>' for t in TYPES) + '</div>'
MEGA = '<div class="megaslot"><img class="sprite" src="../sprites/383.png" width="38" height="38" alt=""><div style="display:flex;flex-direction:column"><span class="sub" style="font-weight:800">YOUR MEGA SLOT</span><b>Primal Groudon · 67.3</b></div><a>Change</a></div>'
raids = phone(f'''{kind("Raids")}<div style="display:flex;justify-content:space-between;align-items:center;gap:8px"><span class="tbtn">All types</span>{types().replace('<div class="toolbar">', '<div class="toolbar" style="flex:1">')}</div>
<div class="sub" style="margin:0 4px">Ground attackers · level 40, perfect IVs · one Mega slot</div>
<div class="card" style="overflow:hidden">{MEGA}{''.join(rrow(r) for r in GROUND[:8])}</div>''')
def drowp(r):
    rk, sp, n, sh, f, c1, c2, s = r
    return f'<tr><td class="n" style="color:var(--text-3)">{rk}</td><td><span style="display:flex;align-items:center;gap:10px"><img class="sprite" src="../sprites/{sp}.png" width="34" height="34" alt="">{n}{tg("Shadow" if sh else "")}</span></td><td style="color:var(--text-2)">{f}</td><td style="color:var(--text-2)">{c1}, {c2}</td><td class="n" style="font-weight:800">{s}</td></tr>'
desk = head + f'''<div class="desktop"><div data-sidebar="chart"></div><div class="main"><div data-topbar></div>
<div class="content" style="max-width:1180px;display:flex;flex-direction:column;gap:16px">
<div style="display:flex;align-items:center;gap:20px"><h1 class="bigtitle" style="flex:none">Battle</h1><div class="seg" style="width:360px"><span>Raids</span><span class="on">Tier lists</span><span>Teams</span></div></div>
<div style="display:flex;align-items:center;gap:14px"><div class="seg" style="width:200px">{kind("PvP")[17:-6]}</div>{leagues()}<span style="flex:1"></span>{own(False)}</div>
<div class="card" style="overflow:hidden"><table class="tl2"><tr><th class="n">#</th><th>Pokémon</th><th>Fast move</th><th>Charged moves</th><th class="n">Score</th></tr>
<tr class="th"><td colspan="5">{th(*TIERS[0]).replace('class="tierh"', 'class="tierh" style="padding:0;background:none"')}</td></tr>{''.join(drowp(r) for r in GL)}
<tr><td colspan="5" style="text-align:center;color:var(--accent);font-weight:800">Show all 33 in S</td></tr>
<tr class="th"><td colspan="5">{th(*TIERS[1]).replace('class="tierh"', 'class="tierh" style="padding:0;background:none"')}</td></tr></table></div>
<p class="sub" style="margin:0">Rankings update every day. Tiers by score: S 90+, A 85–90, B 80–85, C 75–80. Tap a Pokémon for its page and best IVs.</p></div></div></div><script src="../chrome.js"></script></body></html>'''
(HERE / 'C3-phone.html').write_text(pvp); (HERE / 'C3o-phone.html').write_text(mine); (HERE / 'C3r-phone.html').write_text(raids); (HERE / 'C3-desktop.html').write_text(desk)
