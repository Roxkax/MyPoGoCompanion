"""Generates D3 Saved filters (phone, phone name sheet, phone row menu, desktop)."""
import pathlib
HERE = pathlib.Path(__file__).parent
SAVED = [("CD Gible prep", "+gible&age0&3-4attack&!shiny", "Saved today"),
         ("Hundos to power up", "4*&!#GL&!#UL&!#Raid", "Saved Sun 25 Oct"),
         ("Shadows to purify?", "shadow&!#Raid&!legendary&!mythical", "Saved Sun 25 Oct"),
         ("Lucky trade candidates", "year2016,year2017,year2018&!lucky&!shiny&!legendary", "Saved Wed 21 Oct"),
         ("Halloween costumes", "costume&!shiny&!favorite", "Saved Mon 19 Oct")]
esc = lambda s: s.replace("&", "&amp;")
row = lambda n, s, w: f'<div class="sv"><div class="nm"><b>{n}</b><code>{esc(s)}</code><span class="when">{w}</span></div><button class="copy">Copy</button><span class="more">⋯</span></div>'
head = '<!doctype html><html lang="en"><head><meta charset="utf-8"><title>D3 Saved filters</title><link rel="stylesheet" href="../base.css"><link rel="stylesheet" href="_d3.css"></head><body>'
phead = '''<header class="apphead"><div class="titlerow"><h1 class="bigtitle">Filters</h1><span data-ask></span><div class="avatar">A</div></div>
<div class="seg"><span>Keepers</span><span>Builder</span><span class="on">Saved</span></div></header>'''
LIST = f'<div class="sub" style="margin:0 4px">5 saved filters · tap one to open it in the builder</div><div class="card" style="overflow:hidden">{"".join(row(*r) for r in SAVED)}</div>'
def phone(body, extra=""):
    return head + f'''<div class="stage"><div class="device"><div class="screen"><div data-statusbar></div>{phead}
<div class="scroll"><div style="padding:12px 10px 20px;display:flex;flex-direction:column;gap:10px">{body}</div></div>
<div data-tabbar="filters"></div>{extra}</div></div></div><script src="../chrome.js"></script></body></html>'''
NAME = '''<div class="sheetbg"></div><div class="sheet"><div class="grab"></div><b style="font-size:19px">Save this filter</b>
<div style="display:flex;flex-direction:column;gap:6px"><span class="sub" style="font-weight:800">NAME</span><div class="inp">CD Gible prep<span style="width:2px;height:22px;background:var(--accent);margin-left:2px"></span></div></div>
<code style="font-family:var(--mono);font-size:13px;background:var(--surface-2);border-radius:10px;padding:10px">+gible&amp;age0&amp;3-4attack&amp;!shiny</code>
<div style="display:flex;gap:10px"><button class="btn sec">Cancel</button><button class="btn pri">Save</button></div></div>'''
MENU = '<div class="sheetbg" style="background:rgba(10,20,18,.12)"></div><div class="menu"><div>Open in builder</div><div>Rename</div><div>Duplicate</div><div class="del">Delete</div></div>'
desk = head + f'''<div class="desktop"><div data-sidebar="filters"></div><div class="main"><div data-topbar></div>
<div class="content" style="max-width:1180px;display:flex;flex-direction:column;gap:16px">
<div style="display:flex;align-items:center;gap:20px"><h1 class="bigtitle" style="flex:none">Filters</h1><div class="seg" style="width:360px"><span>Keepers</span><span>Builder</span><span class="on">Saved</span></div><span style="flex:1"></span><button class="copy" style="background:var(--accent-soft);color:var(--accent)">+ New filter</button></div>
<div class="card" style="overflow:hidden"><table class="svt"><tr><th>Name</th><th>Search string</th><th>Saved</th><th></th></tr>
{''.join(f'<tr><td>{n}</td><td><code>{esc(s)}</code></td><td style="color:var(--text-3);font-size:13.5px;white-space:nowrap">{w.replace("Saved ", "").capitalize() if w == "Saved today" else w.replace("Saved ", "")}</td><td style="text-align:right;white-space:nowrap"><button class="copy">Copy</button><span class="more" style="display:inline-grid;vertical-align:middle">⋯</span></td></tr>' for n, s, w in SAVED)}</table></div>
<p class="sub" style="margin:0">Saved filters are kept with your account, so they're on every device you sign in on. Click a name to open it in the builder.</p></div></div></div><script src="../chrome.js"></script></body></html>'''
(HERE / 'D3-phone.html').write_text(phone(LIST))
(HERE / 'D3n-phone.html').write_text(phone(LIST, NAME))
(HERE / 'D3m-phone.html').write_text(phone(LIST, MENU))
(HERE / 'D3-desktop.html').write_text(desk)
