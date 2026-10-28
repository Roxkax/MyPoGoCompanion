"""Generates G2 Admin: Fix fetched entries (phone list, phone edit sheet, desktop)."""
import pathlib
HERE = pathlib.Path(__file__).parent
SRC = "leekduck.com"
RAIDS = [("382", "Kyogre", "5★ · until Tue 3 Nov 10:00 · shiny", ""), ("094_51", "Mega Gengar", "Mega · until Sat 31 Oct 10:00 · shiny", "chg"),
         ("359", "Shadow Absol", "Shadow 3★ · until Tue 3 Nov 10:00", ""), ("006", "Charizard", "3★ · until Tue 3 Nov 10:00", ""),
         ("131", "Lapras", "3★ · until Tue 3 Nov 10:00", ""), ("443", "Gible", "1★ · until Tue 3 Nov 10:00 · shiny", ""),
         ("004", "Charmander", "1★ · until Tue 3 Nov 10:00", ""), ("609", "Mega Chandelure", "Mega · from Sat 31 Oct 10:00", "new"),
         ("383", "Groudon", "5★ · from Tue 3 Nov 10:00 · shiny", "new")]
EVENTS = [("Halloween event", "Until Sat 31 Oct 20:00", ""), ("Raid Hour: Kyogre", "Wed 28 Oct 18:00–19:00", ""),
          ("Community Day: Gible", "Sat 7 Nov 14:00–17:00", "new"), ("Spotlight Hour: Snorunt", "Tue 3 Nov 18:00–19:00", "fixed")]
FL = {"chg": '<span class="flag chg">Changed this week</span>', "new": '<span class="flag new">New</span>', "fixed": '<span class="flag fixed">Fixed by you</span>', "": ""}
def ent(sp, n, d, f):
    img = f'<img class="sprite" src="../sprites/{sp}.png" width="38" height="38" alt="">' if sp else '<span style="width:38px;height:38px;border-radius:10px;background:var(--accent-soft);display:grid;place-items:center;color:var(--accent)">IC_calendar</span>'
    return f'<div class="ent">{img}<div class="t"><b>{n}{FL[f]}</b><span>{d}</span></div><span class="src">{SRC} ↗</span><span class="chev">›</span></div>'
SEG = '<div class="seg"><span>Data status</span><span class="on">Fix entries</span></div>'
KINDS = '<div class="toolbar">' + ''.join(f'<span class="tbtn{" on" if i == 0 else ""}">{t}</span>' for i, t in enumerate(["Raid bosses 9", "Max bosses 2", "Events 9", "Eggs 1", "Research 4"])) + '</div>'
NOTE = '<span class="sub" style="margin:0 4px;line-height:1.4">Published by the AI researcher on Sun 25 Oct. Your fixes are kept: the next run won\'t overwrite an entry marked "Fixed by you".</span>'
LIST = f'{SEG}{KINDS}{NOTE}<div class="kh"><h2>Raid bosses</h2></div><div class="card" style="overflow:hidden">{"".join(ent(*r) for r in RAIDS)}</div>'
EV = f'<div class="kh"><h2>Events</h2><span class="sub">4 of 9</span></div><div class="card" style="overflow:hidden">{"".join(ent("", n, d, f) for n, d, f in EVENTS)}</div>'
SHEET = '''<div class="sheetbg"></div><div class="sheet"><div class="grab"></div><div style="display:flex;justify-content:space-between;align-items:center"><b style="font-size:19px">Mega Gengar</b><a style="font-weight:800">Cancel</a></div>
<div class="srcbox">Fetched Sun 25 Oct 22:00 from <a>leekduck.com/raid-bosses ↗</a><br>Changed this week: end date moved from Tue 3 Nov to <b>Sat 31 Oct 10:00</b>, when Mega Chandelure arrives.</div>
<div class="two"><div class="fld"><label>Pokémon</label><div class="in">Gengar (Mega)</div></div><div class="fld"><label>Tier</label><div class="in">Mega ▾</div></div></div>
<div class="two"><div class="fld"><label>Starts</label><div class="in">Tue 27 Oct 10:00</div></div><div class="fld"><label>Ends</label><div class="in focus">Sat 31 Oct 10:00</div></div></div>
<div class="fld"><label>Shiny available</label><div class="in">Yes<span class="tog"></span></div></div>
<div class="fld"><label>Note (optional, only you see it)</label><div class="in" style="color:var(--text-3)">e.g. checked in game</div></div>
<div style="display:flex;gap:10px;margin-top:auto"><button class="btn del">Remove</button><button class="btn pri">Save fix</button></div></div>'''
head = '<!doctype html><html lang="en"><head><meta charset="utf-8"><title>G2 Fix entries</title><link rel="stylesheet" href="../base.css"><link rel="stylesheet" href="_b1.css"><link rel="stylesheet" href="_g2.css"></head><body>'
FIX = '<script>document.body.innerHTML=document.body.innerHTML.replace(/IC_(\\w+)/g,(m,k)=>icon(k,20));</script>'
def phone(extra=""):
    return head + f'''<div class="stage"><div class="device"><div class="screen"><div data-statusbar></div><div data-dethead="Settings|Admin"></div>
<div class="scroll"><div style="padding:12px 10px 24px;display:flex;flex-direction:column;gap:10px">{LIST}{EV}</div></div><div class="homebar"></div>{extra}</div></div></div><script src="../chrome.js"></script>{FIX}</body></html>'''
desk = head + f'''<div class="desktop"><div data-sidebar="admin"></div><div class="main"><div data-topbar></div>
<div class="content" style="max-width:1180px;display:flex;flex-direction:column;gap:14px">
<div style="display:flex;align-items:center;gap:20px"><h1 class="bigtitle" style="flex:none">Admin</h1>{SEG.replace('class="seg"', 'class="seg" style="width:300px"')}</div>
<div style="display:grid;grid-template-columns:minmax(0,1fr) 420px;gap:20px;align-items:start">
<div style="display:flex;flex-direction:column;gap:10px">{KINDS}{NOTE}<div class="card" style="overflow:hidden">{"".join(ent(*r) for r in RAIDS).replace('<div class="ent"><img class="sprite" src="../sprites/094_51.png"', '<div class="ent" style="background:var(--accent-soft)"><img class="sprite" src="../sprites/094_51.png"')}</div></div>
<div class="card" style="position:relative;overflow:hidden;min-height:620px">{SHEET.replace('<div class="sheetbg"></div>', '').replace('class="sheet"', 'class="sheet" style="position:static;border-radius:0;min-height:620px"').replace('<div class="grab"></div>', '')}</div></div></div></div></div>
<script src="../chrome.js"></script>{FIX}<script>document.querySelectorAll('.navitem').forEach(a=>{{if(a.textContent.trim()==='Admin')a.classList.add('on')}});</script></body></html>'''
(HERE / 'G2-phone.html').write_text(phone()); (HERE / 'G2e-phone.html').write_text(phone(SHEET)); (HERE / 'G2-desktop.html').write_text(desk)
