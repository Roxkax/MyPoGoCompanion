"""Generates E2c All changes (phone + desktop)."""
import pathlib
HERE = pathlib.Path(__file__).parent
UP = [("383", "Groudon", "15/15/13", "L35 → L40", "CP 3,804 → 4,096"), ("254", "Sceptile", "14/15/13", "L35 → L40", "CP 2,536 → 2,731"),
      ("807", "Zeraora", "15/15/14", "L35 → L40", "CP 3,168 → 3,411"), ("260", "Swampert", "14/13/15", "L38 → L40", "CP 2,861 → 2,945"),
      ("464", "Rhyperior", "13/15/15", "L38 → L40", "CP 3,598 → 3,704"), ("334", "Altaria", "0/14/15", "L27.5 → L29", "CP 1,420 → 1,497"),
      ("184", "Azumarill", "0/15/14", "L44 → L45.5", "CP 1,469 → 1,496")]
EVO = [("444", "Gabite", "12/12/13", "Gible → Gabite", "CP 1,296"), ("608", "Lampent", "15/13/14", "Litwick → Lampent", "CP 965"),
       ("356", "Dusclops", "10/14/12", "Duskull → Dusclops", "CP 951")]
MOV = [("308", "Medicham", "1/15/15", "New move: Psychic", "was Dynamic Punch"), ("464", "Rhyperior", "13/15/15", "New move: Earthquake", "was Stone Edge")]
NEW = [("607", "Litwick", 14), ("092", "Gastly", 12), ("355", "Duskull", 10), ("302", "Sableye", 6), ("361", "Snorunt", 5), ("443", "Gible", 3)]
GONE = [("016", "Pidgey", 11), ("019", "Rattata", 9), ("265", "Wurmple", 8), ("530", "Excadrill", 1)]
def one(sp, n, iv, a, b, kind):
    return f'<div class="chg"><img class="sprite" src="../sprites/{sp}.png" width="38" height="38" alt=""><div class="t"><b>{n} <span style="font-weight:700;color:var(--text-3);font-size:12.5px">{iv}</span></b><span>{a} · {b}</span></div></div>'
def grp(sp, n, c):
    return f'<div class="chg"><img class="sprite" src="../sprites/{sp}.png" width="38" height="38" alt=""><div class="t"><b>{n}</b></div><span class="cnt">×{c}</span><span class="chev">›</span></div>'
def section(title, kind, count, rows, more=""):
    return f'<div class="card" style="overflow:hidden"><div class="grh"><span class="kind {kind}">{title}</span><span>{count}</span></div>{rows}{more}</div>'
MORE = lambda t: f'<div class="ur" style="border-top:1px solid var(--line)"><span>{t}</span></div>'
CHIPS = '<div class="toolbar">' + ''.join(f'<span class="tbtn{" on" if i == 0 else ""}">{t}</span>' for i, t in enumerate(["All 115", "New 63", "Gone 40", "Powered up 7", "Evolved 3", "New moves 2"])) + '</div>'
BODY = (section("Powered up", "up", "7", ''.join(one(*r, "up") for r in UP))
        + section("Evolved", "", "3", ''.join(one(*r, "") for r in EVO))
        + section("New moves", "", "2", ''.join(one(*r, "") for r in MOV))
        + section("New", "new", "63 Pokémon · 14 species", ''.join(grp(*r) for r in NEW), MORE("And 8 more species (13 Pokémon)"))
        + section("Gone", "gone", "40 Pokémon · 11 species", ''.join(grp(*r) for r in GONE), MORE("And 7 more species (11 Pokémon) · transferred or traded")))
head = '<!doctype html><html lang="en"><head><meta charset="utf-8"><title>E2 All changes</title><link rel="stylesheet" href="../base.css"><link rel="stylesheet" href="_b1.css"><link rel="stylesheet" href="_e2.css"></head><body>'
phone = head + f'''<div class="stage"><div class="device"><div class="screen"><div data-statusbar></div><div data-dethead="Report|All changes"></div>
<div class="scroll"><div style="padding:12px 10px 20px;display:flex;flex-direction:column;gap:12px"><div class="sub" style="margin:0 4px">Upload Sun 25 Oct vs Sun 18 Oct</div>{CHIPS}{BODY}</div></div>
<div data-tabbar="collection"></div></div></div></div><script src="../chrome.js"></script></body></html>'''
def drow(sp, n, iv, before, after, kind, label):
    return f'<tr><td><span style="display:flex;align-items:center;gap:10px"><img class="sprite" src="../sprites/{sp}.png" width="32" height="32" alt="">{n}</span></td><td style="color:var(--text-2)">{iv}</td><td><span class="kind {kind}">{label}</span></td><td>{before}</td><td style="color:var(--text-2)">{after}</td></tr>'
rows = ('<tr class="g"><td colspan="5">Changed · 12</td></tr>'
        + ''.join(drow(sp, n, iv, a, b, "up", "Powered up") for sp, n, iv, a, b in UP)
        + ''.join(drow(sp, n, iv, a, b, "", "Evolved") for sp, n, iv, a, b in EVO)
        + ''.join(drow(sp, n, iv, a, b, "", "New move") for sp, n, iv, a, b in MOV)
        + '<tr class="g"><td colspan="5">New · 63</td></tr>'
        + ''.join(drow(sp, f"{n} ×{c}", "", "", "", "new", "New") for sp, n, c in NEW)
        + '<tr><td colspan="5" style="color:var(--text-2)">And 8 more species (13 Pokémon)</td></tr>'
        + '<tr class="g"><td colspan="5">Gone · 40</td></tr>'
        + ''.join(drow(sp, f"{n} ×{c}", "", "", "", "gone", "Gone") for sp, n, c in GONE)
        + '<tr><td colspan="5" style="color:var(--text-2)">And 7 more species (11 Pokémon)</td></tr>')
desk = head + f'''<div class="desktop"><div data-sidebar="collection"></div><div class="main"><div data-topbar></div>
<div class="content" style="max-width:1180px;display:flex;flex-direction:column;gap:14px"><a class="backlink">‹ Import report</a>
<div style="display:flex;align-items:baseline;gap:14px;margin-top:-8px"><h1 class="bigtitle" style="flex:none">All changes</h1><span class="sub" style="font-size:14px">Upload Sun 25 Oct vs Sun 18 Oct · 115 changes</span></div>
<div style="display:flex;gap:10px;align-items:center"><div class="searchbox" style="width:280px;height:40px;background:var(--surface);border:1px solid var(--line)">SEARCHICON Search a Pokémon</div>{CHIPS[21:-6]}</div>
<div class="card" style="overflow:hidden"><table class="ch"><tr><th>Pokémon</th><th>IVs</th><th>Change</th><th>What changed</th><th></th></tr>{rows}</table></div></div></div></div>
<script src="../chrome.js"></script><script>document.body.innerHTML=document.body.innerHTML.replaceAll("SEARCHICON",icon("search",18));</script></body></html>'''
(HERE / 'E2c-phone.html').write_text(phone); (HERE / 'E2c-desktop.html').write_text(desk)
