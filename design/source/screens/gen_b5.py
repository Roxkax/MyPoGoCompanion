"""Generates B5 Type chart: phone (defending, attacking, full chart) and desktop."""
import pathlib
HERE = pathlib.Path(__file__).parent
TYPES = ["normal", "fire", "water", "electric", "grass", "ice", "fighting", "poison", "ground", "flying", "psychic", "bug", "rock", "ghost", "dragon", "dark", "steel", "fairy"]
AB = dict(zip(TYPES, ["Nor", "Fir", "Wat", "Ele", "Gra", "Ice", "Fig", "Poi", "Gro", "Fly", "Psy", "Bug", "Roc", "Gho", "Dra", "Dar", "Ste", "Fai"]))
SE, NVE, IM = {}, {}, {}
def add(d, a, ds):
    for x in ds.split(): d.setdefault(a, set()).add(x)
for a, se, nve, im in [
    ("normal", "", "rock steel", "ghost"), ("fire", "grass ice bug steel", "fire water rock dragon", ""),
    ("water", "fire ground rock", "water grass dragon", ""), ("electric", "water flying", "electric grass dragon", "ground"),
    ("grass", "water ground rock", "fire grass poison flying bug dragon steel", ""), ("ice", "grass ground flying dragon", "fire water ice steel", ""),
    ("fighting", "normal ice rock dark steel", "poison flying psychic bug fairy", "ghost"), ("poison", "grass fairy", "poison ground rock ghost", "steel"),
    ("ground", "fire electric poison rock steel", "grass bug", "flying"), ("flying", "grass fighting bug", "electric rock steel", ""),
    ("psychic", "fighting poison", "psychic steel", "dark"), ("bug", "grass psychic dark", "fire fighting poison flying ghost steel fairy", ""),
    ("rock", "fire ice flying bug", "fighting ground steel", ""), ("ghost", "psychic ghost", "dark", "normal"),
    ("dragon", "dragon", "steel", "fairy"), ("dark", "psychic ghost", "fighting dark fairy", ""),
    ("steel", "ice rock fairy", "fire water electric steel", ""), ("fairy", "fighting dragon dark", "fire poison steel", "")]:
    add(SE, a, se); add(NVE, a, nve); add(IM, a, im)
def eff(a, d):
    if d in SE.get(a, ()): return 1.6
    if d in NVE.get(a, ()): return 0.625
    if d in IM.get(a, ()): return 0.390625
    return 1.0
def fmt(m): return {2.56: "2.56", 1.6: "1.6", 1.0: "1", 0.625: "0.63", 0.390625: "0.39", 0.244140625: "0.24", 1.0 * 1.6 * 0.625: "1"}.get(round(m, 9), f"{m:.2f}")
DEF = ["water", "ground"]
def combined(a): 
    m = 1.0
    for d in DEF: m *= eff(a, d)
    return round(m, 9)
T = lambda t, x="": f'<span class="type {t}">{t.capitalize()}{f"<i>×{x}</i>" if x else ""}</span>'
def defending():
    ms = {a: combined(a) for a in TYPES}
    weak = sorted([a for a in TYPES if ms[a] > 1], key=lambda a: -ms[a])
    res = sorted([a for a in TYPES if ms[a] < 1], key=lambda a: ms[a])
    neutral = [a for a in TYPES if ms[a] == 1]
    who = ''.join(f'<img class="sprite" src="../sprites/{s}.png" width="34" height="34" alt="">' for s in ["260", "195", "340", "423_11", "537"])
    return f'''<div class="card" style="overflow:hidden">
<div class="res"><div class="lab"><b>Weak to</b><span class="m-se">takes more damage</span></div><div class="chips">{''.join(T(a, fmt(ms[a])) for a in weak)}</div></div>
<div class="res"><div class="lab"><b>Resists</b><span class="m-nve">takes less damage</span></div><div class="chips">{''.join(T(a, fmt(ms[a])) for a in res)}</div></div>
<div class="res"><div class="lab"><b>Neutral</b><span class="faint">×1</span></div><div class="chips">{''.join(T(a) for a in neutral)}</div></div>
<div class="who" style="border-top:1px solid var(--line)"><span style="display:flex">{who}</span><span style="flex:1;font-size:13.5px;font-weight:700">Swampert, Quagsire, Whiscash and 6 more</span><span class="chev" style="color:var(--text-3);font-size:18px;font-weight:700">›</span></div></div>'''
def attacking(a="ground"):
    se = [d for d in TYPES if eff(a, d) > 1]; nve = [d for d in TYPES if eff(a, d) == 0.625]; im = [d for d in TYPES if eff(a, d) < 0.5]
    return f'''<div class="card" style="overflow:hidden">
<div class="res"><div class="lab"><b>Super effective against</b><span class="m-se">×1.6</span></div><div class="chips">{''.join(T(d) for d in se)}</div></div>
<div class="res"><div class="lab"><b>Not very effective against</b><span class="m-nve">×0.63</span></div><div class="chips">{''.join(T(d) for d in nve)}</div></div>
<div class="res"><div class="lab"><b>Barely works against</b><span class="m-dbl">×0.39</span></div><div class="chips">{''.join(T(d) for d in im)}</div></div>
<div class="res"><div class="lab"><b>Moves of this type</b><span class="faint">15</span></div><div style="font-size:13.5px;font-weight:700;display:flex;justify-content:space-between">Earthquake, Earth Power, Mud Shot and 12 more<span style="color:var(--text-3);font-size:18px">›</span></div></div></div>'''
def chart(hl_def=(), hl_atk=None):
    th = ''.join(f'<th class="ab type {d}{" hl" if d in hl_def else ""}" style="display:table-cell">{AB[d]}</th>' for d in TYPES)
    rows = ''
    for a in TYPES:
        cells = ''
        for d in TYPES:
            m = eff(a, d); cls = "se" if m > 1 else "nve" if m == 0.625 else "dbl" if m < .5 else "n"
            txt = {1.6: "1.6", 0.625: ".63", 0.390625: ".39"}.get(m, "")
            cells += f'<td class="{cls}{" hlc" if d in hl_def else ""}">{txt}</td>'
        rows += f'<tr><th class="ab type {a}{" hl" if a == hl_atk else ""}" style="display:table-cell">{AB[a]}</th>{cells}</tr>'
    return f'<table class="tc"><tr><th class="corner">ATK ↓<br>DEF →</th>{th}</tr>{rows}</table>'
LEG = '<div class="legend"><span><i style="background:#CDEBD6"></i>×1.6 super effective</span><span><i style="background:#F8DCC8"></i>×0.63 not very effective</span><span><i style="background:#F2B8B8"></i>×0.39 barely works</span><span><i style="background:var(--surface-2)"></i>×1</span></div>'
def picker(sel, two=True):
    cells = ''.join('<span class="type %s%s">%s</span>' % (t, " sel" if t in sel else "", t.capitalize()) for t in TYPES)
    return f'<div class="tpick">{cells}</div>'
head = '<!doctype html><html lang="en"><head><meta charset="utf-8"><title>B5 Type chart</title><link rel="stylesheet" href="../base.css"><link rel="stylesheet" href="_b5.css"></head><body>'
phead = '''<header class="apphead"><div class="titlerow"><h1 class="bigtitle">Pokédex</h1><span data-ask></span><div class="avatar">A</div></div>
<div class="seg"><span>Pokémon</span><span>Moves</span><span class="on">Type chart</span></div></header>'''
def mode(on, ks=("Defending", "Attacking", "Full chart")): return '<div class="seg">' + ''.join('<span class="%s">%s</span>' % ("on" if k == on else "", k) for k in ks) + '</div>'
def phone(body):
    return head + f'''<div class="stage"><div class="device"><div class="screen"><div data-statusbar></div>{phead}
<div class="scroll"><div style="padding:12px 12px 20px;display:flex;flex-direction:column;gap:12px">{body}</div></div>
<div data-tabbar="pokedex"></div></div></div></div><script src="../chrome.js"></script></body></html>'''
pd = phone(f'''{mode("Defending")}<div class="card" style="padding:12px 14px;display:flex;flex-direction:column;gap:10px"><div style="display:flex;justify-content:space-between;align-items:center"><span class="picked">{T("water")}<span class="faint">+</span>{T("ground")}</span><a style="font-weight:800;font-size:14px">Clear</a></div>
<div class="sub">Pick one or two types</div>{picker(DEF)}</div>{defending()}''')
pa = phone(f'''{mode("Attacking")}<div class="card" style="padding:12px 14px;display:flex;flex-direction:column;gap:10px"><div class="sub">Pick the move's type</div>{picker(["ground"])}</div>{attacking()}''')
pc = phone(f'''{mode("Full chart")}{LEG.replace('class="legend"', 'class="legend" style="gap:8px 12px;font-size:12px"')}<div class="card" style="padding:8px;overflow:hidden">{chart().replace("width: 30px", "")}</div>
<div class="sub" style="text-align:center">Scroll sideways · tap a cell for details · turn your phone for the whole chart</div>''')
desk = head + f'''<div class="desktop"><div data-sidebar="pokedex"></div><div class="main"><div data-topbar></div>
<div class="content" style="max-width:1180px;display:flex;flex-direction:column;gap:16px">
<div style="display:flex;align-items:baseline;gap:14px"><h1 class="bigtitle" style="flex:none">Type chart</h1><span style="flex:1"></span><a class="seclink">Moves ›</a></div>
<div style="display:grid;grid-template-columns:360px 1fr;gap:24px;align-items:start">
<div style="display:flex;flex-direction:column;gap:12px">{mode("Defending", ("Defending", "Attacking"))}
<div class="card" style="padding:12px 14px;display:flex;flex-direction:column;gap:10px"><div style="display:flex;justify-content:space-between;align-items:center"><span class="picked">{T("water")}<span class="faint">+</span>{T("ground")}</span><a style="font-weight:800;font-size:14px">Clear</a></div>{picker(DEF)}</div>{defending()}</div>
<div class="card" style="padding:18px;display:flex;flex-direction:column;gap:14px;align-items:flex-start">{LEG}{chart(DEF)}<div class="sub">Rows: the attacking move's type · columns: the defending Pokémon's type. Highlighted: Water and Ground.</div></div></div></div></div></div><script src="../chrome.js"></script></body></html>'''
(HERE / 'B5-phone.html').write_text(pd); (HERE / 'B5a-phone.html').write_text(pa); (HERE / 'B5c-phone.html').write_text(pc); (HERE / 'B5-desktop.html').write_text(desk)
