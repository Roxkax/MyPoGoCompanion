"""Generates B4 Move page (Earth Power), phone + desktop."""
import pathlib
HERE = pathlib.Path(__file__).parent
# sprite, name, types, attack, has Mega that also learns it
# sprite, name, types, raid score with its best fast move + Earth Power, fast move, versions {kind: score}
ELITE = [("445", "Garchomp", ["dragon", "ground"], 34.2, "Mud Shot", {"Shadow": 40.8, "Mega": 58.4}),
         ("330", "Flygon", ["ground", "dragon"], 27.1, "Sand Attack", {"Shadow": 30.5})]
NORMAL = [("645_11", "Landorus", ["ground", "flying"], 32.5, "Mud Shot", {"Shadow": 38.8}),
          ("485", "Heatran", ["fire", "steel"], 32.1, "Fire Spin", {"Shadow": 35.9}),
          ("721", "Volcanion", ["fire", "water"], 30.1, "Incinerate", {}),
          ("450", "Hippowdon", ["ground"], 29.0, "Sand Attack", {"Shadow": 32.7}),
          ("623", "Golurk", ["ground", "ghost"], 28.6, "Mud Slap", {"Shadow": 33.5}),
          ("905_THERIAN", "Enamorus (Therian)", ["fairy", "flying"], 28.4, "Fairy Wind", {}),
          ("812", "Rillaboom", ["grass"], 28.2, "Razor Leaf", {}),
          ("249", "Lugia", ["psychic", "flying"], 27.3, "Extrasensory", {"Shadow": 30.9}),
          ("034", "Nidoking", ["poison", "ground"], 26.9, "Poison Jab", {"Shadow": 30.9}),
          ("844", "Sandaconda", ["ground"], 26.7, "Mud Shot", {}),
          ("930", "Arboliva", ["grass", "normal"], 26.3, "Razor Leaf", {}),
          ("142", "Aerodactyl", ["rock", "flying"], 26.0, "Rock Throw", {"Shadow": 29.4, "Mega": 40.9}),
          ("537", "Seismitoad", ["water", "ground"], 25.8, "Mud Shot", {}),
          ("770", "Palossand", ["ghost", "ground"], 25.0, "Sand Attack", {"Shadow": 28.2}),
          ("031", "Nidoqueen", ["poison", "ground"], 24.2, "Mud Slap", {"Shadow": 28.0}),
          ("323", "Camerupt", ["fire", "ground"], 23.7, "Incinerate", {"Shadow": 27.7, "Mega": 41.3}),
          ("423_11", "Gastrodon", ["water", "ground"], 23.6, "Mud Slap", {}),
          ("949", "Toedscruel", ["ground", "grass"], 23.3, "Mud Slap", {}),
          ("344", "Claydol", ["ground", "psychic"], 18.7, "Mud Slap", {"Shadow": 21.9}),
          ("536", "Palpitoad", ["water", "ground"], 16.0, "Mud Shot", {}),
          ("929", "Dolliv", ["grass", "normal"], 15.7, "Tackle", {}),
          ("769", "Sandygast", ["ghost", "ground"], 15.0, "Sand Attack", {"Shadow": 16.7}),
          ("843", "Silicobra", ["ground"], 13.3, "Mud Shot", {}),
          ("948", "Toedscool", ["ground", "grass"], 12.6, "Mud Shot", {}),
          ("928", "Smoliv", ["grass", "normal"], 10.2, "Tackle", {}),
          ("235", "Smeargle", ["normal"], 5.4, "Mud Shot", {})]
T = lambda ts: ''.join(f'<span class="type {t}">{t.capitalize()}</span>' for t in ts)
def lr(r):
    sp, n, ts, sc, fast, vs = r
    v = ''.join(f'<span class="ver {k.lower()}">{k} {x}</span>' for k, x in vs.items())
    return f'<div class="lr"><img class="sprite" src="../sprites/{sp}.png" width="38" height="38" alt=""><div class="nm"><b>{n}</b><span class="tt">{T(ts)}</span><span class="with">with {fast}{" · " if v else ""}{v}</span></div><div class="atk">{sc}<span>RAID SCORE</span></div></div>'
CAL = '<svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round"><rect x="3" y="5" width="18" height="16" rx="3"/><path d="M3 10h18M8 3v4M16 3v4"/></svg>'
NOTE = f'<div class="note">{CAL}<span>Free during <b>Community Day: Gible</b> — evolve Gabite Sat 7 Nov, 14:00–22:00</span></div>'
MORE = '<div style="border-top:1px solid var(--line);padding:12px 16px;text-align:center;font-weight:800;color:var(--accent);font-size:14.5px">Show all 26 ›</div>'
def nums():
    return '''<div class="card ncard"><h3>Raids &amp; gyms</h3><div class="nums"><div><span>Power</span><b>100</b></div><div><span>Time</span><b>3.5 s</b></div><div><span>Energy</span><b>−50</b></div></div>
<div class="ebar"><i></i><i></i><span style="margin-left:6px">2 bars</span></div></div>
<div class="card ncard"><h3>PvP (trainer battles)</h3><div class="nums"><div><span>Power</span><b>90</b></div><div><span>Energy</span><b>−50</b></div><div><span>Turns</span><b>1</b></div></div>
<div class="effect">Effect: <b>10% chance</b> to lower the opponent's Defense by 1</div></div>'''
def groups(grid=False, limit=None):
    wrap = (lambda rows: f'<div class="lgrid">{rows}</div>') if grid else (lambda rows: rows)
    return f'''<div class="card" style="overflow:hidden"><div class="grph"><b>Elite TM only</b><span>2 Pokémon</span></div>{NOTE}{wrap(''.join(lr(r) for r in ELITE))}</div>
<div class="card" style="overflow:hidden"><div class="grph" style="padding-bottom:12px"><b>Legacy</b><span>none</span></div></div>
<div class="card" style="overflow:hidden"><div class="grph"><b>Can learn it</b><span>26 Pokémon · with a Charged TM</span></div>{wrap(''.join(lr(r) for r in NORMAL[:limit]))}{MORE if limit else ''}</div>'''
head = '<!doctype html><html lang="en"><head><meta charset="utf-8"><title>B4 Move page</title><link rel="stylesheet" href="../base.css"><link rel="stylesheet" href="_b1.css"><link rel="stylesheet" href="_b4.css"></head><body>'
mhead = f'<div class="mhead"><h1>Earth Power</h1><div class="meta"><span class="type ground">Ground</span>Charged move</div></div>'
phone = head + f'''<div class="stage"><div class="device"><div class="screen"><div data-statusbar></div><div data-dethead="Moves|"></div>{mhead}
<div class="scroll"><div style="padding:14px 12px 20px;display:flex;flex-direction:column;gap:12px">{nums()}
<div style="margin:10px 4px 0"><h2 style="margin:0;font-size:18px">Who learns it</h2><div class="sub" style="margin-top:2px">Best first: raid score with its best fast move + Earth Power</div></div>{groups(limit=8)}</div></div>
<div data-tabbar="pokedex"></div></div></div></div><script src="../chrome.js"></script></body></html>'''
desk = head + f'''<div class="desktop"><div data-sidebar="moves"></div><div class="main"><div data-topbar></div>
<div class="content" style="max-width:1180px;display:flex;flex-direction:column;gap:18px">
<a class="backlink">‹ Moves</a><div style="display:flex;flex-direction:column;gap:6px;margin-top:-6px"><h1 class="bigtitle">Earth Power</h1><div class="mhead" style="padding:0;background:none;border:0"><div class="meta"><span class="type ground">Ground</span>Charged move</div></div></div>
<div style="display:grid;grid-template-columns:1fr 1fr;gap:16px">{nums()}</div>
<div style="margin-top:8px"><h2 style="margin:0;font-size:20px">Who learns it</h2><div class="sub" style="margin-top:2px;font-size:14px">Best first: raid score with its best fast move + Earth Power. Shadow and Mega scores shown when they exist.</div></div>{groups(True)}</div></div></div><script src="../chrome.js"></script></body></html>'''
(HERE / 'B4-phone.html').write_text(phone)
(HERE / 'B4-desktop.html').write_text(desk)
