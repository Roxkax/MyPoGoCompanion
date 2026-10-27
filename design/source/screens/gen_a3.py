"""Generates A3 Event details (Community Day: Gible), phone + desktop."""
import pathlib
HERE = pathlib.Path(__file__).parent
SH = '<svg width="14" height="14" viewBox="0 0 24 24"><path d="M12 2l2.2 6.6L21 11l-6.8 2.4L12 20l-2.2-6.6L3 11l6.8-2.4z" fill="#C08A00"/></svg>'
CAL = '<svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round"><rect x="3" y="5" width="18" height="16" rx="3"/><path d="M3 10h18M8 3v4M16 3v4"/></svg>'

def hero(size):
    return f'''<div class="card hero"><div style="flex:1;min-width:0">
 <span class="kind">Community Day</span>
 <div class="t">Gible Community Day</div>
 <div class="meta">{CAL}Sat 7 Nov · 14:00–17:00</div>
 <div class="meta" style="margin-top:6px"><span class="pill soft">In 11 days</span>{SH}Shiny Gible more often</div>
</div><img class="sprite" src="../sprites/443.png" width="{size}" height="{size}" alt="Gible"></div>'''

move = '''<section class="section"><h2 class="sectitle">Exclusive move</h2><div class="card">
 <div class="blk" style="display:flex;gap:12px;align-items:center"><img class="sprite" src="../sprites/445.png" width="52" height="52" alt="Garchomp">
  <div class="note"><b style="color:var(--text)">Evolve Gabite between 14:00 and 22:00</b> to get a Garchomp that knows <b style="color:var(--text)">Earth Power</b>.</div></div>
 <div class="blk"><div style="display:flex;align-items:center;gap:8px"><b style="font-size:16px">Earth Power</b><span class="type ground">Ground</span><span class="pill soft" style="height:20px;font-size:11px">Charged</span><span class="chev">›</span></div>
  <div class="note" style="margin-top:6px">Raids: 100 power · 3.5 s · 50 energy<br>PvP: 90 power · 50 energy</div></div>
 <div class="blk note">With it, Garchomp is a top Ground raid attacker: <b style="color:var(--text)">#12</b> (Shadow <b style="color:var(--text)">#5</b>, Mega <b style="color:var(--text)">#2</b>).</div>
</div></section>'''

bonuses = '''<section class="section"><h2 class="sectitle">Bonuses</h2><div class="card">
 <div class="li"><span class="ic">3×</span>Catch Stardust</div>
 <div class="li"><span class="ic">¼</span>Egg hatch distance</div>
 <div class="li"><span class="ic">3h</span>Lure Modules and Incense last 3 hours</div>
</div></section>'''

cps = '''<section class="section"><h2 class="sectitle">100% CP to look for</h2><div class="card"><table class="cpt">
 <tr><th>Pokémon</th><th>Normal</th><th>Boosted</th></tr>
 <tr><td>Gible (catch)</td><td>954</td><td>1,033</td></tr>
 <tr><td>Garchomp (after evolving)</td><td>3,396</td><td>3,679</td></tr>
</table><div class="blk note">Boosted means Windy or Sunny weather. Tap a catch's CP into the IV check on Gible's page.</div></div></section>'''

research = '''<section class="section"><h2 class="sectitle">Field Research</h2><div class="card">
 <div class="li" style="font-weight:700"><img class="sprite" src="../sprites/443.png" width="32" height="32" alt=""><div><div>Catch 15 Gible</div><div class="note">Reward: Gible encounter</div></div></div>
</div></section>'''

mine = """<section class="section"><div class="sechead"><h2 class="sectitle">Your Gible family</h2><a class="seclink">Collection</a></div><div class="card">
 <div class="mrow"><img class="sprite" src="../sprites/443.png" width="40" height="40" alt=""><div class="mtxt"><b>Gible 15/15/14</b><span>Garchomp for raids and Master League · ML rank 3 (99.5%)</span></div><span class="tag go">Evolve</span></div>
 <div class="mrow"><img class="sprite" src="../sprites/443.png" width="40" height="40" alt=""><div class="mtxt"><b>Gible 0/15/14</b><span>Great League rank 1 · Garchomp CP 1,495</span></div><span class="tag go">Evolve</span></div>
 <div class="mrow"><img class="sprite" src="../sprites/443.png" width="40" height="40" alt=""><div class="mtxt"><b>Gible 1/13/15</b><span>Great League rank 11 (99.4%) · backup</span></div><span class="tag go">Evolve</span></div>
 <div class="mrow"><img class="sprite" src="../sprites/444.png" width="40" height="40" alt=""><div class="mtxt"><b>Gabite 12/12/13</b><span>Master League rank 160 (96.6%) · if you have the candy</span></div><span class="tag maybe">Optional</span></div>
 <div class="mrow"><img class="sprite" src="../sprites/443.png" width="40" height="40" alt=""><div class="mtxt"><b>Gible 10/4/2</b><span>Not good in any league</span></div><span class="tag no">Transfer</span></div>
 <div class="blk note">Each Garchomp costs 25 + 100 candy. Poke Genie doesn't export candy, so check yours in the game.</div>
</div></section>"""
fresh = ''
head = '<!doctype html><html lang="en"><head><meta charset="utf-8"><title>A3 Event details</title><link rel="stylesheet" href="../base.css"><link rel="stylesheet" href="_a3.css"></head><body>'

phone = head + f'''<div class="stage"><div class="device"><div class="screen">
<div data-statusbar></div><div data-dethead="Calendar|Event"></div>
<div class="scroll"><div style="padding:14px 14px 20px;display:flex;flex-direction:column;gap:20px">
{hero(92)}{move}{mine}{bonuses}{cps}{research}{fresh}
</div></div><div data-tabbar="today"></div></div></div></div><script src="../chrome.js"></script></body></html>'''

desk = head + f'''<div class="desktop"><div data-sidebar="calendar"></div><div class="main"><div data-topbar></div>
<div class="content" style="max-width:1080px;display:flex;flex-direction:column;gap:18px"><a class="backlink">‹ Calendar</a>
{hero(132)}
<div style="display:grid;grid-template-columns:minmax(0,1.3fr) minmax(0,1fr);gap:24px;align-items:start">
 <div style="display:flex;flex-direction:column;gap:22px">{move}{bonuses}</div>
 <div style="display:flex;flex-direction:column;gap:22px">{mine}{cps}{research}</div>
</div>{fresh}</div></div></div><script src="../chrome.js"></script></body></html>'''

(HERE / 'A3-phone.html').write_text(phone)
(HERE / 'A3-desktop.html').write_text(desk)
