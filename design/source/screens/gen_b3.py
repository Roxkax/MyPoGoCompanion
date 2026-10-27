"""Generates B3 Moves list: phone (raids numbers, PvP numbers) and desktop."""
import pathlib
HERE = pathlib.Path(__file__).parent
# name, raid power, duration, energy, PvP power, PvP energy, effect (text, who)
MOVES = [
    ("Dynamax Cannon", 215, "1.5", 100, 80, 45, None),
    ("Roar of Time", 160, "2.0", 100, 150, 65, None),
    ("Spacial Rend", 160, "2.5", 100, 95, 55, None),
    ("Dragon Energy", 155, "3.5", 50, 90, 45, None),
    ("Draco Meteor", 150, "3.5", 100, 150, 65, ("Self Atk −2", "self")),
    ("Clanging Scales", 120, "3.5", 100, 120, 45, ("Self Def −1", "self")),
    ("Outrage", 110, "4.0", 50, 110, 60, None),
    ("Glaive Rush", 105, "2.0", 50, 90, 40, ("Self Def −1", "self")),
    ("Dragon Pulse", 90, "3.5", 50, 90, 55, None),
    ("Twister", 50, "3.0", 33, 70, 40, None),
    ("Breaking Swipe", 45, "1.0", 33, 50, 50, ("Foe Atk −1", "foe")),
    ("Dragon Claw", 45, "1.5", 33, 80, 50, None),
]
DOT = '<span class="tdot" style="background:var(--t-dragon)"></span>'
FIL = '<svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round"><path d="M4 6h16M7 12h10M10 18h4"/></svg>'
X = '<span style="margin-left:2px;font-size:15px;line-height:1">×</span>'
head = lambda t: f'<!doctype html><html lang="en"><head><meta charset="utf-8"><title>{t}</title><link rel="stylesheet" href="../base.css"><link rel="stylesheet" href="_b1.css"><link rel="stylesheet" href="_b3.css"></head><body>'
script = '<script src="../chrome.js"></script><script>document.body.innerHTML=document.body.innerHTML.replaceAll("SEARCHICON",icon("search",19));</script>'
phead = '''<header class="apphead"><div class="titlerow"><h1 class="bigtitle">Pokédex</h1><span data-ask></span><div class="avatar">A</div></div>
<div class="seg"><span>Pokémon</span><span class="on">Moves</span><span>Type chart</span></div>
<div class="searchbox">SEARCHICON Search moves</div></header>'''
def eff(e): return f'<span class="eff {e[1]}">{e[0]}</span>' if e else '<span class="faint">–</span>'
def phone(pvp):
    hdr = ('<span>Move</span><span class="sorted">Power ↓</span><span>Energy</span><span>Effect</span>' if pvp else
           '<span>Move</span><span class="sorted">Power ↓</span><span>Time</span><span>Energy</span>')
    def row(m):
        n, rp, d, re_, pp, pe, e = m
        nums = f'<span>{pp}</span><span>−{pe}</span>{eff(e)}' if pvp else f'<span>{rp}</span><span>{d} s</span><span>−{re_}</span>'
        return f'<div class="mr"><span class="nm">{DOT}<b>{n}</b></span>{nums}</div>'
    tools = f'''<div style="padding:12px 12px 8px;display:flex;flex-direction:column;gap:10px">
<div class="seg"><span>Fast<i>80</i></span><span class="on">Charged<i>235</i></span></div>
<div class="toolbar"><span class="tbtn on">Dragon{X}</span><span class="tbtn">{FIL}Type</span><span class="tbtn">Sort: {"PvP" if pvp else "Raid"} power ↓</span></div>
<div style="display:flex;justify-content:space-between;align-items:center"><span class="count">12 charged moves</span>
<div class="seg" style="width:150px;padding:2px"><span class="{"" if pvp else "on"}" style="height:28px">Raids</span><span class="{"on" if pvp else ""}" style="height:28px">PvP</span></div></div></div>'''
    return head("B3 Moves") + f'''<div class="stage"><div class="device"><div class="screen"><div data-statusbar></div>{phead}
<div class="scroll">{tools}<div class="card mlist{" pvp" if pvp else ""}" style="margin:0 10px 16px;overflow:hidden"><div class="mh">{hdr}</div>{''.join(row(m) for m in (sorted(MOVES, key=lambda m: -m[4]) if pvp else MOVES))}</div></div>
<div data-tabbar="pokedex"></div></div></div></div>{script}</body></html>'''
def drow(m):
    n, rp, d, re_, pp, pe, e = m
    return f'''<tr><td class="l"><span style="display:flex;align-items:center;gap:10px;font-weight:800">{DOT}{n}</span></td><td class="l"><span class="type dragon">Dragon</span></td>
<td class="sep" style="font-weight:800">{rp}</td><td>{d} s</td><td>−{re_}</td><td class="sep">{pp}</td><td>−{pe}</td><td class="l">{eff(e)}</td></tr>'''
desk = head("B3 Moves") + f'''<div class="desktop"><div data-sidebar="moves"></div><div class="main"><div data-topbar></div>
<div class="content" style="max-width:1180px;display:flex;flex-direction:column;gap:16px">
<div style="display:flex;align-items:baseline;gap:14px"><h1 class="bigtitle" style="flex:none">Moves</h1><span class="count">315 moves</span><span style="flex:1"></span><a class="seclink">Type chart ›</a></div>
<div style="display:flex;gap:10px;align-items:center;flex-wrap:wrap"><div class="searchbox" style="width:280px;height:40px;background:var(--surface);border:1px solid var(--line)">SEARCHICON Search moves</div>
<div class="seg" style="width:240px"><span>Fast<i>80</i></span><span class="on">Charged<i>235</i></span></div>
<span class="tbtn on">Type: Dragon{X}</span><span style="flex:1"></span><span class="count">12 charged moves</span></div>
<div class="card" style="overflow:hidden"><table class="mv">
<tr class="g"><th class="l"></th><th></th><th colspan="3" class="sep"><span>Raids</span></th><th colspan="3" class="sep"><span>PvP (trainer battles)</span></th></tr>
<tr class="h"><th class="l">Move</th><th class="l">Type</th><th class="sep sorted">Power ↓</th><th>Time</th><th>Energy</th><th class="sep">Power</th><th>Energy</th><th class="l">Effect</th></tr>
{''.join(drow(m) for m in MOVES)}</table></div>
<p class="fresh" style="margin:0">Fast moves show raid power · time · energy gained, and PvP power · energy · turns. Click any column to sort; click a move for who learns it.</p></div></div></div>{script}</body></html>'''
(HERE / 'B3-phone.html').write_text(phone(False))
(HERE / 'B3p-phone.html').write_text(phone(True))
(HERE / 'B3-desktop.html').write_text(desk)
