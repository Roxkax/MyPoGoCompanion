"""Generates A4 Eggs & research (phone + desktop)."""
import pathlib
HERE = pathlib.Path(__file__).parent
POOL = [("443", "Gible", ["dragon", "ground"], "635"), ("633", "Deino", ["dark", "dragon"], "606"),
        ("246", "Larvitar", ["rock", "ground"], "594"), ("374", "Beldum", ["steel", "psychic"], "558"),
        ("704", "Goomy", ["dragon"], "557"), ("782", "Jangmo-o", ["dragon"], "553"),
        ("885", "Dreepy", ["dragon", "ghost"], "438"), ("996", "Frigibax", ["dragon", "ice"], "712")]
TASKS = [("Win 3 raids", "443", "Gible", "477"), ("Catch 10 Ghost-type Pokémon", "607", "Litwick", "431"),
         ("Make 3 Great Throws", "361", "Snorunt", "380"), ("Power up Pokémon 5 times", "001", "Bulbasaur", "477")]

def cells(size):
    return ''.join(f'''<div class="ecell"><img class="sprite" src="../sprites/{i}.png" width="{size}" height="{size}" alt=""><span class="n">{n}</span>
<span class="types">{''.join(f'<span class="type {t}">{t.capitalize()}</span>' for t in ts)}</span><span class="cp">100% CP {cp}</span></div>''' for i, n, ts, cp in POOL)

def tasks():
    return '<div class="card">' + ''.join(f'''<div class="task"><img class="sprite" src="../sprites/{i}.png" width="40" height="40" alt=""><div class="q">{q}<span>Reward: {n} · 100% CP {cp}</span></div><span class="chev">›</span></div>''' for q, i, n, cp in TASKS) + '</div>'

dist = '<div class="dist filterchips">' + ''.join(f'<span class="fchip{" on" if d == "10 km" else ""}">{d}</span>' for d in ["2 km", "5 km", "7 km", "10 km", "12 km", "Route", "Adventure"]) + '</div>'
eggs = lambda cols, size: f'''<section class="section"><h2 class="sectitle">Eggs</h2>{dist}
<div class="egg" style="grid-template-columns:repeat({cols},minmax(0,1fr))">{cells(size)}</div>
<p class="note" style="margin:0">100% CP of a hatch (level 20). 8 Pokémon can hatch from 10 km eggs right now.</p></section>'''
research = f'''<section class="section"><h2 class="sectitle">Field Research</h2>{tasks()}
<p class="note" style="margin:0">100% CP of a research encounter (level 15). Tap a reward to open its Pokémon page.</p></section>'''
head = '<!doctype html><html lang="en"><head><meta charset="utf-8"><title>A4 Eggs & research</title><link rel="stylesheet" href="../base.css"><link rel="stylesheet" href="_a4.css"></head><body>'
phone = head + f'''<div class="stage"><div class="device"><div class="screen">
<div data-statusbar></div><div data-dethead="Today|Eggs &amp; research"></div>
<div class="scroll"><div style="padding:14px 14px 20px;display:flex;flex-direction:column;gap:24px">{eggs(3, 52)}{research}</div></div>
<div data-tabbar="today"></div></div></div></div><script src="../chrome.js"></script></body></html>'''
desk = head + f'''<div class="desktop"><div data-sidebar="egg"></div><div class="main"><div data-topbar></div>
<div class="content" style="max-width:1120px"><a class="backlink">‹ Today</a><h1 class="bigtitle" style="margin:6px 0 20px">Eggs &amp; research</h1>
<div style="display:grid;grid-template-columns:minmax(0,1.35fr) minmax(0,1fr);gap:28px;align-items:start">{eggs(4, 72)}{research}</div>
</div></div></div><script src="../chrome.js"></script></body></html>'''
(HERE / 'A4-phone.html').write_text(phone)
(HERE / 'A4-desktop.html').write_text(desk)
