"""Generates D2 Filter builder (phone, phone add-condition sheet, desktop)."""
import pathlib
HERE = pathlib.Path(__file__).parent
STRING = '<span class="p">+gible</span>&amp;age0&amp;3-4attack&amp;!shiny'
CONDS = [("Pokémon", '<span>Gible</span><span class="chip">whole family</span>'),
         ("Caught", '<span>Today</span>'),
         ("Attack bar", '<span>3–4 bars</span><span class="faint" style="font-size:12.5px;font-weight:700">(11–15)</span>'),
         ("Leave out", '<span class="chip not">not Shiny</span>')]
def conds():
    rows = '<div class="joiner" style="padding-top:2px">and</div>'.join(f'<div class="cond"><span class="k">{k}</span><span class="v">{v}</span><span class="x">×</span></div>' for k, v in CONDS)
    return f'<div class="card" style="overflow:hidden"><div class="pad" style="padding:12px 14px 4px;display:flex;justify-content:space-between"><b>Conditions</b><a style="font-weight:800;font-size:13.5px">Clear</a></div>{rows}<div class="addc">+ Add a condition</div></div>'
def result():
    return f'''<div class="card" style="padding:14px;display:flex;flex-direction:column;gap:10px"><div style="display:flex;justify-content:space-between;align-items:center"><b>Search string</b><span class="sub">Tap to edit by hand</span></div>
<div class="strbox">{STRING}</div><div class="meta"><span class="ok">✓ The game will accept this</span><span>27 / 1,950</span></div>
<div class="btns"><button class="btn sec">Save</button><button class="btn pri">Copy</button></div></div>'''
CATS = [("Pokémon", "Names, numbers, families (+), evolutions"), ("Types", "Any of 18, one or several"), ("Appraisal", "Attack, Defense, HP bars; stars; hundos"),
        ("CP &amp; HP", "Ranges, e.g. CP up to 1,500"), ("Special", "Shiny, lucky, Shadow, purified, costume"), ("Rarity", "Legendary, mythical, Ultra Beast"),
        ("Caught", "Days ago, year, distance, traded"), ("Moves", "Move names, legacy moves, 2nd move"), ("Can do", "Evolve, Mega, Dynamax, trade, buddy"),
        ("Tags &amp; more", "Your tags, favourites, other keywords")]
cats = '<div class="cats">' + ''.join(f'<div class="cat"><b>{a}</b><span>{b}</span></div>' for a, b in CATS) + '</div>'
head = '<!doctype html><html lang="en"><head><meta charset="utf-8"><title>D2 Filter builder</title><link rel="stylesheet" href="../base.css"><link rel="stylesheet" href="_d2.css"></head><body>'
phead = '''<header class="apphead"><div class="titlerow"><h1 class="bigtitle">Filters</h1><span data-ask></span><div class="avatar">A</div></div>
<div class="seg"><span>Keepers</span><span class="on">Builder</span><span>Saved</span></div></header>'''
def phone(extra=""):
    return head + f'''<div class="stage"><div class="device"><div class="screen"><div data-statusbar></div>{phead}
<div class="scroll"><div style="padding:12px 10px 20px;display:flex;flex-direction:column;gap:12px">{conds()}{result()}</div></div>
<div data-tabbar="filters"></div>{extra}</div></div></div><script src="../chrome.js"></script></body></html>'''
sheet = f'''<div class="sheetbg"></div><div class="sheet"><div class="grab"></div><div style="display:flex;justify-content:space-between;align-items:center"><b style="font-size:19px">Add a condition</b><a style="font-weight:800">Cancel</a></div>
<div class="searchbox" style="height:40px">SEARCHICON Search conditions, e.g. "lucky"</div>{cats}
<div class="sub" style="line-height:1.4">Each condition you add must match (AND). Inside one condition you can pick several values, which match either way (OR), e.g. Fire or Water.</div></div>'''
desk = head + f'''<div class="desktop"><div data-sidebar="filters"></div><div class="main"><div data-topbar></div>
<div class="content" style="max-width:1180px;display:flex;flex-direction:column;gap:16px">
<div style="display:flex;align-items:center;gap:20px"><h1 class="bigtitle" style="flex:none">Filters</h1><div class="seg" style="width:360px"><span>Keepers</span><span class="on">Builder</span><span>Saved</span></div></div>
<div style="display:grid;grid-template-columns:minmax(0,1fr) minmax(0,1fr) 380px;gap:16px;align-items:start">
<div class="card" style="padding:14px;display:flex;flex-direction:column;gap:10px"><b>Add a condition</b><div class="searchbox" style="height:40px">SEARCHICON Search conditions</div>{cats}
<div class="sub" style="line-height:1.4">Conditions must all match (AND); values inside one condition match either way (OR).</div></div>
<div>{conds()}</div><div>{result()}</div></div></div></div></div><script src="../chrome.js"></script>
<script>document.body.innerHTML=document.body.innerHTML.replaceAll("SEARCHICON",icon("search",18));</script></body></html>'''
(HERE / 'D2-phone.html').write_text(phone())
(HERE / 'D2a-phone.html').write_text(phone(sheet).replace('</body>', '<script>document.body.innerHTML=document.body.innerHTML.replaceAll("SEARCHICON",icon("search",18));</script></body>'))
(HERE / 'D2-desktop.html').write_text(desk)
