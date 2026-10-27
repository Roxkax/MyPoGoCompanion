"""Generates B2 Pokémon page (Garchomp), phone + desktop."""
import pathlib
HERE = pathlib.Path(__file__).parent
T = lambda ts: ''.join(f'<span class="type {t}">{t.capitalize()}</span>' for t in ts)
def stats():
    rows = [("Attack", 261), ("Defense", 193), ("Stamina", 239)]
    bars = ''.join(f'<div class="statrow"><span class="muted">{n}</span><div class="bar"><i style="width:{v/350*100:.1f}%"></i></div><b style="text-align:right">{v}</b></div>' for n, v in rows)
    return f'''<section class="sec" id="overview"><h2>Stats</h2><div class="card pad" style="display:flex;flex-direction:column;gap:10px">{bars}
<div class="kv" style="margin-top:4px"><div><span>Max CP · L40</span><b>3,962</b></div><div><span>Max CP · L50</span><b>4,479</b></div><div><span>Max CP · L51</span><b>4,532</b></div></div></div></section>'''
def ranks():
    rows = [("Great League", "#471", ""), ("Ultra League", "#323", ""), ("Master League", "#31", "Shadow #26"),
            ("Raids · Ground", "#12", "Shadow #5 · Mega #2"), ("Raids · Dragon", "#25", "Shadow #8 · Mega #2")]
    return '<section class="sec"><h2>Rankings</h2><div class="card">' + ''.join(
        f'<div class="lrow"><div><div>{n}</div>{f"<div class=sub>{s}</div>" if s else ""}</div><span class="r">{r}</span><span class="chev">›</span></div>' for n, r, s in rows) + '</div></section>'
def cps():
    rows = [("Research", "L15", "1,698"), ("Raids &amp; eggs", "L20", "2,264"), ("Weather-boosted raids", "L25", "2,830"), ("Wild", "L30", "3,396"), ("Wild, weather-boosted", "L35", "3,679")]
    return '<section class="sec"><h2>100% CP cheat sheet</h2><div class="card">' + ''.join(
        f'<div class="lrow"><span>{n}</span><span class="faint" style="font-size:12.5px">{l}</span><span class="r">{v}</span></div>' for n, l, v in rows) + '</div></section>'
def matchups():
    weak = [("ice", "×2.56"), ("dragon", "×1.6"), ("fairy", "×1.6")]
    res = [("electric", "×0.24"), ("fire", "×0.63"), ("poison", "×0.63"), ("rock", "×0.63")]
    chip = lambda t, m: f'<span class="type {t}" style="height:24px;font-size:12px;padding:0 9px">{t.capitalize()} {m}</span>'
    return f'''<section class="sec"><h2>Type matchups</h2><div class="card pad" style="display:flex;flex-direction:column;gap:10px">
<div><div class="faint" style="font-size:12.5px;font-weight:800;margin-bottom:6px">Weak to</div><div class="mm">{''.join(chip(t, m) for t, m in weak)}</div></div>
<div><div class="faint" style="font-size:12.5px;font-weight:800;margin-bottom:6px">Resists</div><div class="mm">{''.join(chip(t, m) for t, m in res)}</div></div></div></section>'''
def moves(compact=False):
    best = [("Raids · Ground", "Mud Shot + Earth Power"), ("Raids · Dragon", "Dragon Tail + Breaking Swipe"),
            ("Great &amp; Ultra League", "Dragon Tail + Twister, Sand Tomb"), ("Master League", "Dragon Tail + Twister, Earth Power")]
    C = {"dragon": "var(--t-dragon)", "ground": "var(--t-ground)", "fire": "var(--t-fire)"}
    fast = [("Dragon Tail", "dragon", "14", "1.0 s", "+8", "9", "+12 (3 turns)", False), ("Mud Shot", "ground", "4", "0.5 s", "+6", "3", "+9 (2 turns)", False)]
    ch = [("Earth Power", "ground", "100", "3.5 s", "−50", "90", "−50", True), ("Outrage", "dragon", "110", "4.0 s", "−50", "110", "−60", False),
          ("Earthquake", "ground", "140", "3.5 s", "−100", "120", "−65", False), ("Breaking Swipe", "dragon", "45", "1.0 s", "−33", "50", "−50", False),
          ("Fire Blast", "fire", "140", "4.0 s", "−100", "140", "−80", False), ("Sand Tomb", "ground", "60", "4.0 s", "−33", "55", "−45", False),
          ("Twister", "dragon", "50", "3.0 s", "−33", "70", "−40", False)]
    r = lambda m: f'<tr><td><span class="dot" style="background:{C[m[1]]}"></span>{m[0]}{"<span class=etm>Elite TM</span>" if m[7] else ""}</td><td>{m[2]}</td><td>{m[3]}</td><td>{m[4]}</td><td>{m[5]}</td><td>{m[6]}</td></tr>'
    cr = lambda m: f'<div class="mrow"><div class="mname"><span class="dot" style="background:{C[m[1]]}"></span>{m[0]}{"<span class=etm>Elite TM</span>" if m[7] else ""}</div><div class="mstat"><span><b>Raids</b> {m[2]} · {m[3]} · {m[4]}</span><span><b>PvP</b> {m[5]} · {m[6]}</span></div></div>'
    table = (f'''<div class="card"><div class="mgrp">Fast</div>{''.join(cr(m) for m in fast)}<div class="mgrp">Charged</div>{''.join(cr(m) for m in ch)}</div>''' if compact else
      f'''<div class="card"><table class="mt"><tr><th></th><th colspan="3" class="hg">Raids</th><th colspan="2" class="hg">PvP</th></tr><tr><th>Move</th><th>Power</th><th>Time</th><th>Energy</th><th>Power</th><th>Energy</th></tr>
<tr class="grp"><td colspan="6">Fast</td></tr>{''.join(r(m) for m in fast)}<tr class="grp"><td colspan="6">Charged</td></tr>{''.join(r(m) for m in ch)}</table></div>''')
    return f'''<section class="sec" id="moves"><h2>Moves</h2><div class="card">{''.join(f'<div class="lrow" style="flex-direction:column;align-items:flex-start;gap:1px"><span class="sub" style="font-weight:800">{n}</span><span>{m}</span></div>' for n, m in best)}</div>
{table}</section>'''
def evo():
    p = lambda sp, n, here=False: f'<div class="p"><img class="sprite" src="../sprites/{sp}.png" width="56" height="56" alt="" style="{"" if here else "opacity:.85"}">{n}</div>'
    a = lambda c: f'<div class="arrow"><span style="font-size:18px">→</span>{c}</div>'
    return f'''<section class="sec" id="evolution"><h2>Evolution</h2><div class="card"><div class="evo">{p("443", "Gible")}{a("25 candy")}{p("444", "Gabite")}{a("100 candy")}{p("445", "Garchomp", True)}</div>
<div class="lrow" style="font-size:13.5px"><span class="muted">Buddy distance</span><span class="r">5 km</span></div></div></section>'''
def getit():
    now = [("1★ raids", "Gible · until Tue 3 Nov"), ("10 km eggs", "Gible"), ("Field Research", "Win 3 raids → Gible"), ("Community Day", "Gible · Sat 7 Nov")]
    usual = [("Wild", "Gible, rare"), ("Eggs", "10 km"), ("Raids", "Gible 1★ · Mega Garchomp")]
    li = lambda rows: ''.join(f'<div class="lrow"><span>{a}</span><span class="r" style="font-weight:700;text-align:right;color:var(--text-2)">{b}</span></div>' for a, b in rows)
    return f'''<section class="sec" id="get"><h2>How to get it</h2><div class="card"><div class="lrow" style="padding-bottom:4px"><span class="pill live"><span class="dot" style="width:7px;height:7px;margin:0"></span>Right now</span></div>{li(now)}</div>
<div class="card"><div class="lrow" style="padding-bottom:4px"><b style="font-size:13px" class="faint">USUALLY</b></div>{li(usual)}</div></section>'''
def versions():
    v = [("445", "Garchomp"), ("445_shiny", "Shiny"), ("445", "Shadow"), ("445_51", "Mega"), ("445_51_shiny", "Shiny Mega")]
    def cell(sp, n):
        fl = ''
        return f'<div><span style="position:relative"><img class="sprite" src="../sprites/{sp}.png" width="54" height="54" alt="" style="{'filter:drop-shadow(0 0 3px #7B3FB0) drop-shadow(0 0 3px #7B3FB0)' if n == 'Shadow' else ''}">{fl}</span>{n}</div>'
    return f'''<section class="sec" id="versions"><h2>Versions</h2><div class="card"><div class="vgrid" style="grid-template-columns:repeat(5,minmax(0,1fr))">{''.join(cell(*x) for x in v)}</div>
<div class="lrow" style="font-size:13px;color:var(--text-2)">Mega Garchomp: Attack 339 · Defense 222 · Stamina 239 · max CP 6,132 (L50)</div></div></section>'''
def ivs():
    lg = [("Great League", "0/15/14", "Level 14 · CP 1,495", "Atk 0–5 · Def 11–15 · HP 6–15", "443-445&amp;0-1attack&amp;3-4defense&amp;2-4hp&amp;cp-1500"),
          ("Ultra League", "0/14/13", "Level 23.5 · CP 2,499", "Atk 0–5 · Def 11–15 · HP 6–15", "443-445&amp;0-1attack&amp;3-4defense&amp;2-4hp&amp;cp-2500"),
          ("Master League", "15/15/15", "Level 50 · CP 4,479", "Atk 15 · Def 15 · HP 15", None)]
    rows = ''.join(f'''<div class="ivrow"><div style="display:flex;justify-content:space-between;align-items:baseline"><b>{n}</b><b class="num">{iv}</b></div>
<div class="boxes"><span>{l}</span></div><div class="boxes"><span>Keep: {b}</span></div>{f'<div class="str"><code>{s}</code><button class="copy">Copy</button></div>' if s else ''}</div>''' for n, iv, l, b, s in lg)
    checker = '''<div class="card pad" style="display:flex;flex-direction:column;gap:10px"><b>Check any IVs</b>
<div class="ivin"><label>Attack<input value="2"></label><label>Defense<input value="15"></label><label>HP<input value="13"></label></div>
<div class="lrow" style="padding:6px 0 0"><span>Great League</span><span class="r">#2,042 <span class="faint">· 94.9%</span></span></div>
<div class="lrow" style="padding:6px 0 0;border-top:0"><span>Ultra League</span><span class="r" style="color:var(--accent)">#232 <span class="faint">· 97.9% · CP 2,471</span></span></div>
<div class="lrow" style="padding:6px 0 0;border-top:0"><span>Master League</span><span class="r">#590 <span class="faint">· 94.4%</span></span></div></div>'''
    return f'<section class="sec" id="ivs"><h2>Best IVs</h2><div class="card">{rows}</div>{checker}</section>'

jump = '<div class="jump">' + ''.join(f'<span class="{"on" if i == 0 else ""}">{n}</span>' for i, n in enumerate(["Overview", "Moves", "Evolution", "Get it", "Versions", "IVs"])) + '</div>'
hero = lambda size: f'''<div class="hero"><img class="sprite" src="../sprites/445.png" width="{size}" height="{size}" alt="Garchomp"><div style="display:flex;flex-direction:column;gap:6px"><span class="no">#445</span><h1>Garchomp</h1><span style="display:flex;gap:5px">{T(["dragon", "ground"])}</span></div></div>
<div class="vswitch"><span class="on">Garchomp</span><span>Shadow</span><span>Mega</span></div>'''
head = '<!doctype html><html lang="en"><head><meta charset="utf-8"><title>B2 Pokémon page</title><link rel="stylesheet" href="../base.css"><link rel="stylesheet" href="_b2.css"></head><body>'
phone = head + f'''<div class="stage tint"><div class="device"><div class="screen">
<div data-statusbar></div><div class="band"><div data-dethead="Pokédex|"></div>{hero(104)}</div>{jump}
<div class="scroll"><div style="padding:14px 12px 20px;display:flex;flex-direction:column;gap:22px">
{stats()}{ranks()}{cps()}{matchups()}{moves(True)}{evo()}{getit()}{versions()}{ivs()}</div></div>
<div data-tabbar="pokedex"></div></div></div></div><script src="../chrome.js"></script>
<script>document.querySelector('.statusbar').style.background='transparent';document.querySelector('.statusbar').parentElement.querySelector('.band').before(document.querySelector('.statusbar'));document.querySelector('.statusbar').style.background='color-mix(in srgb, var(--t-dragon) 13%, var(--surface))';</script></body></html>'''
desk = head + f'''<div class="desktop tint"><div data-sidebar="pokedex"></div><div class="main"><div data-topbar></div>
<div class="band" style="padding:18px 32px 4px"><a class="backlink">‹ Pokédex</a><div style="display:flex;align-items:flex-end;gap:24px">{hero(150).replace('class="vswitch"', 'class="vswitch" style="padding-bottom:22px;margin-left:auto"')}</div></div>
<div class="content" style="max-width:1200px;display:grid;grid-template-columns:minmax(0,1fr) minmax(0,1fr);gap:24px;align-items:start">
<div style="display:flex;flex-direction:column;gap:22px">{stats()}{ranks()}{moves()}{versions()}</div>
<div style="display:flex;flex-direction:column;gap:22px">{ivs()}{cps()}{matchups()}{evo()}{getit()}</div></div></div></div><script src="../chrome.js"></script></body></html>'''
(HERE / 'B2-phone.html').write_text(phone)
(HERE / 'B2-desktop.html').write_text(desk)
