"""Generates B4 Move page (Earth Power), phone + desktop."""
import pathlib
HERE = pathlib.Path(__file__).parent
# sprite, name, types, attack, has Mega that also learns it
ELITE = [("445", "Garchomp", ["dragon", "ground"], 261, True), ("330", "Flygon", ["ground", "dragon"], 205, False)]
NORMAL = [("645_11", "Landorus", ["ground", "flying"], 261, False), ("323", "Camerupt", ["fire", "ground"], 194, True),
          ("721", "Volcanion", ["fire", "water"], 252, False), ("485", "Heatran", ["fire", "steel"], 251, False),
          ("905_THERIAN", "Enamorus (Therian)", ["fairy", "flying"], 250, False), ("812", "Rillaboom", ["grass"], 239, False),
          ("623", "Golurk", ["ground", "ghost"], 222, False), ("142", "Aerodactyl", ["rock", "flying"], 221, True),
          ("930", "Arboliva", ["grass", "normal"], 219, False), ("034", "Nidoking", ["poison", "ground"], 204, False),
          ("844", "Sandaconda", ["ground"], 202, False), ("450", "Hippowdon", ["ground"], 201, False),
          ("249", "Lugia", ["psychic", "flying"], 193, False), ("537", "Seismitoad", ["water", "ground"], 188, False),
          ("031", "Nidoqueen", ["poison", "ground"], 180, False), ("770", "Palossand", ["ghost", "ground"], 178, False),
          ("423_11", "Gastrodon", ["water", "ground"], 169, False), ("949", "Toedscruel", ["ground", "grass"], 166, False),
          ("344", "Claydol", ["ground", "psychic"], 140, False), ("929", "Dolliv", ["grass", "normal"], 137, False),
          ("536", "Palpitoad", ["water", "ground"], 128, False), ("769", "Sandygast", ["ghost", "ground"], 120, False),
          ("843", "Silicobra", ["ground"], 103, False), ("928", "Smoliv", ["grass", "normal"], 100, False),
          ("948", "Toedscool", ["ground", "grass"], 97, False), ("235", "Smeargle", ["normal"], 40, False)]
NORMAL.sort(key=lambda r: -r[3])
T = lambda ts: ''.join(f'<span class="type {t}">{t.capitalize()}</span>' for t in ts)
def lr(r):
    sp, n, ts, a, mega = r
    m = '<span class="ver mega">Mega too</span>' if mega else ''
    return f'<div class="lr"><img class="sprite" src="../sprites/{sp}.png" width="38" height="38" alt=""><div class="nm"><b>{n}</b><span class="tt">{T(ts)}{m}</span></div><div class="atk">{a}<span>ATTACK</span></div></div>'
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
<h2 style="margin:10px 4px 0;font-size:18px">Who learns it</h2>{groups(limit=8)}</div></div>
<div data-tabbar="pokedex"></div></div></div></div><script src="../chrome.js"></script></body></html>'''
desk = head + f'''<div class="desktop"><div data-sidebar="moves"></div><div class="main"><div data-topbar></div>
<div class="content" style="max-width:1180px;display:flex;flex-direction:column;gap:18px">
<a class="backlink">‹ Moves</a><div style="display:flex;flex-direction:column;gap:6px;margin-top:-6px"><h1 class="bigtitle">Earth Power</h1><div class="mhead" style="padding:0;background:none;border:0"><div class="meta"><span class="type ground">Ground</span>Charged move</div></div></div>
<div style="display:grid;grid-template-columns:1fr 1fr;gap:16px">{nums()}</div>
<h2 style="margin:8px 0 0;font-size:20px">Who learns it</h2>{groups(True)}</div></div></div><script src="../chrome.js"></script></body></html>'''
(HERE / 'B4-phone.html').write_text(phone)
(HERE / 'B4-desktop.html').write_text(desk)
