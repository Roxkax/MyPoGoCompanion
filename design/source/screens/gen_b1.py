"""Generates B1 Pokédex list (phone, phone filter panel, desktop)."""
import pathlib
HERE = pathlib.Path(__file__).parent
ROWS = [  # dex, sprite, name, types, atk, def, sta, maxcp, versions
    (1, "001", "Bulbasaur", ["grass", "poison"], 118, 111, 128, 1260, ["Shadow", "Dynamax"]),
    (2, "002", "Ivysaur", ["grass", "poison"], 151, 143, 155, 1921, ["Shadow", "Dynamax"]),
    (3, "003", "Venusaur", ["grass", "poison"], 198, 189, 190, 3075, ["Mega", "Shadow", "Dynamax", "G-Max"]),
    (4, "004", "Charmander", ["fire"], 116, 93, 118, 1108, ["Shadow", "Dynamax"]),
    (5, "005", "Charmeleon", ["fire"], 158, 126, 151, 1868, ["Shadow", "Dynamax"]),
    (6, "006", "Charizard", ["fire", "flying"], 223, 173, 186, 3266, ["Mega X", "Mega Y", "Shadow", "Dynamax", "G-Max"]),
    (7, "007", "Squirtle", ["water"], 94, 121, 127, 1069, ["Shadow", "Dynamax"]),
    (8, "008", "Wartortle", ["water"], 126, 155, 153, 1682, ["Shadow", "Dynamax"]),
    (9, "009", "Blastoise", ["water"], 171, 207, 188, 2788, ["Mega", "Shadow", "Dynamax", "G-Max"]),
]
HOVER = None
TYPES = ["normal", "fire", "water", "grass", "electric", "ice", "fighting", "poison", "ground", "flying", "psychic", "bug", "rock", "ghost", "dragon", "dark", "steel", "fairy"]
f = lambda n: f"{n:,}"
def tbadges(ts): return ''.join(f'<span class="type {t}">{t.capitalize()}</span>' for t in ts)
def vchips(vs, on=None): return '<span class="vers">' + ''.join(f'<span class="ver {v.split()[0].lower().replace("-", "")}{" hov" if v == on else ""}">{v}</span>' for v in vs) + '</span>'
def hv(r):
    if HOVER and HOVER[0] == r[0]:
        d, sp, n, ts, a, de, s_, cp, vs = r
        _, badge, sp, a, de, s_, cp, label = HOVER
        return (d, sp, n, ts, a, de, s_, cp, vs), badge, label
    return r, None, None
def prow(r):
    r, on, label = hv(r)
    d, sp, n, ts, a, de, s, cp, vs = r
    sub = f'<span class="showing">Showing {label}</span>' if label else ''
    return f'''<div class="drow{" hovrow" if on else ""}"><img class="sprite" src="../sprites/{sp}.png" width="46" height="46" alt=""><div class="nm"><span class="no">#{d:03d}</span><b>{n}</b><span class="types">{tbadges(ts)}</span>{vchips(vs, on)}{sub}</div>
<div class="stats"><b>{f(cp)}</b><span>{a} · {de} · {s}</span></div></div>'''
def drow(r):
    r, on, label = hv(r)
    d, sp, n, ts, a, de, s, cp, vs = r
    return f'''<tr class="{"hovrow" if on else ""}"><td class="n" style="color:var(--text-3)">#{d:03d}</td><td><span style="display:flex;align-items:center;gap:10px"><img class="sprite" src="../sprites/{sp}.png" width="36" height="36" alt="">{n}</span></td>
<td><span style="display:flex;gap:4px">{tbadges(ts)}</span></td><td>{vchips(vs, on)}{f'<div class="showing">Showing {label}</div>' if label else ''}</td><td class="n">{a}</td><td class="n">{de}</td><td class="n">{s}</td><td class="n" style="font-weight:800">{f(cp)}</td></tr>'''
FIL = '<svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round"><path d="M4 6h16M7 12h10M10 18h4"/></svg>'
toolbar = f'<div class="toolbar"><span class="tbtn">{FIL}Filters</span><span class="tbtn">Sort: Number ↑</span><span class="tbtn on">Released</span><span class="tbtn">Has Mega</span><span class="tbtn">Has Shadow</span></div>'
head = lambda t: f'<!doctype html><html lang="en"><head><meta charset="utf-8"><title>{t}</title><link rel="stylesheet" href="../base.css"><link rel="stylesheet" href="_b1.css"></head><body>'
phead = '''<header class="apphead"><div class="titlerow"><h1 class="bigtitle">Pokédex</h1><span data-ask></span><div class="avatar">A</div></div>
<div class="searchbox">SEARCHICON Name, number or nickname</div></header>'''
script = '<script src="../chrome.js"></script><script>document.body.innerHTML=document.body.innerHTML.replaceAll("SEARCHICON",icon("search",19));</script>'
def listbody_src(rows=ROWS): return f'''<div style="padding:12px 12px 8px;display:flex;flex-direction:column;gap:10px">{toolbar}<div style="display:flex;justify-content:space-between;align-items:baseline"><span class="count">1,025 Pokémon</span><span class="count" style="text-align:right;line-height:1.25">Max CP (L50)<br>Atk · Def · Sta</span></div></div>
<div class="card" style="margin:0 10px 16px;overflow:hidden">{''.join(prow(r) for r in rows)}</div>'''
listbody = listbody_src()
def phone_src(rows=ROWS): return head("B1 Pokédex") + f'''<div class="stage"><div class="device"><div class="screen"><div data-statusbar></div>{phead}
<div class="scroll">{listbody_src(rows)}</div><div data-tabbar="pokedex"></div></div></div></div>{script}</body></html>'''
phone = phone_src()

def tsel(t): return f'<span class="type {t}{" sel" if t == "ground" else ""}">{t.capitalize()}</span>'
def rng(label, lo, hi, a, b):
    return f'<div><div class="slabel">{label}</div><div class="range"><span style="width:46px">{lo}</span><div class="track"><i style="left:{a}%;right:{100-b}%"></i><b style="left:calc({a}% - 11px)"></b><b style="left:calc({b}% - 11px)"></b></div><span style="width:52px;text-align:right">{hi}</span></div></div>'
sheet = f'''<div class="sheetbg"></div><div class="sheet"><div class="grab"></div>
<div class="sh"><b style="font-size:19px">Filters</b><a style="font-weight:800">Clear all</a></div>
<div class="sbody">
<div><div class="slabel">Type</div><div class="tgrid">{''.join(tsel(t) for t in TYPES)}</div></div>
{rng("Attack", "230", "414", 51, 100)}
{rng("Max CP", "10", "5,037", 0, 100)}
<div><div class="slabel">Generation</div><div class="toolbar" style="flex-wrap:wrap"><span class="tbtn on">All</span><span class="tbtn">Kanto</span><span class="tbtn">Johto</span><span class="tbtn">Hoenn</span><span class="tbtn">Sinnoh</span><span class="tbtn">…</span></div></div>
<div><div class="slabel">Show</div><div class="toolbar" style="flex-wrap:wrap"><span class="tbtn on">Released only</span><span class="tbtn">Has Mega</span><span class="tbtn">Has Shadow</span><span class="tbtn">Can Dynamax</span><span class="tbtn">Can Gigantamax</span><span class="tbtn">Regional forms</span><span class="tbtn">Legendary &amp; Mythical</span></div></div>
</div>
<div class="sfoot"><button class="btn sec">Reset</button><button class="btn pri">Show 7 Pokémon</button></div></div>'''
phonef = head("B1 Pokédex — filters") + f'''<div class="stage"><div class="device"><div class="screen"><div data-statusbar></div>{phead}
<div class="scroll">{listbody}</div><div data-tabbar="pokedex"></div>{sheet}</div></div></div>{script}</body></html>'''

def desk_src(): return head("B1 Pokédex") + f'''<div class="desktop"><div data-sidebar="pokedex"></div><div class="main"><div data-topbar></div>
<div class="content" style="max-width:1180px;display:flex;flex-direction:column;gap:16px">
<div style="display:flex;align-items:baseline;gap:14px"><h1 class="bigtitle" style="flex:none">Pokédex</h1><span class="count">1,025 Pokémon</span></div>
<div style="display:flex;gap:10px;align-items:center;flex-wrap:wrap"><div class="searchbox" style="width:300px;height:40px;background:var(--surface);border:1px solid var(--line)">SEARCHICON Name, number or nickname</div>
<span class="tbtn">Type: Any ▾</span><span class="tbtn">Generation: All ▾</span><span class="tbtn">{FIL}More filters</span><span style="flex:1"></span><span class="tbtn on">Released</span><span class="tbtn">Has Mega</span><span class="tbtn">Has Shadow</span></div>
<div class="card" style="overflow:hidden"><table class="dex"><tr><th class="n"># ↑</th><th>Pokémon</th><th>Types</th><th>Versions</th><th class="n">Attack</th><th class="n">Defense</th><th class="n">Stamina</th><th class="n">Max CP (L50)</th></tr>
{''.join(drow(r) for r in ROWS)}</table></div></div></div></div>{script}</body></html>'''
desk = desk_src()
(HERE / 'B1-phone.html').write_text(phone)
(HERE / 'B1f-phone.html').write_text(phonef)
(HERE / 'B1-desktop.html').write_text(desk)

# Hover / tap variant: Charizard showing Mega Y
HOVER = (6, "Mega Y", "006_52", 319, 212, 186, 5037, "Mega Charizard Y")
deskh = desk_src()
(HERE / 'B1h-desktop.html').write_text(deskh)
(HERE / 'B1h-phone.html').write_text(phone_src(ROWS[3:]))
