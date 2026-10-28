"""Generates F2 Settings (phone settings, phone About, desktop)."""
import pathlib
HERE = pathlib.Path(__file__).parent
CH = '<span class="chev">›</span>'
ACCT = '<div class="card" style="overflow:hidden"><div class="acct"><div class="pic">A</div><div style="display:flex;flex-direction:column;gap:2px;min-width:0"><b>Ash Silva</b><span class="sub">ash.silva@gmail.com · Google</span></div><span class="badge" style="margin-left:auto">Admin</span></div></div>'
LANG = f'<div class="grp"><span class="lab">Language</span><div class="card" style="overflow:hidden"><div class="row">English<span class="r"><span class="check">✓</span></span></div><div class="row">Português (Portugal)</div></div><span class="sub" style="margin:0 6px;line-height:1.4">Changes the whole app, including Pokémon and move names. Search strings stay in English, to match your game.</span></div>'
THEME = '<div class="grp"><span class="lab">Theme</span><div class="seg"><span class="on">Follow device</span><span>Light</span><span>Dark</span></div></div>'
MORE = f'<div class="grp"><span class="lab">More</span><div class="card" style="overflow:hidden"><div class="row">About &amp; credits<span class="r">{CH}</span></div><div class="row">Install the app<span class="r">Add to home screen {CH}</span></div><div class="row">Admin<span class="r"><span class="badge">Admin only</span>{CH}</span></div></div></div>'
OUT = '<div class="card" style="overflow:hidden"><div class="row danger" style="justify-content:center">Sign out</div></div>'
ABOUT = '''<div class="card ab"><b style="font-size:17px">MyPoGoCompanion</b><span>Version 1.0 · a free fan project.</span>
<span>Not affiliated with, endorsed or sponsored by Niantic, Nintendo, Creatures, GAME FREAK or The Pokémon Company. Pokémon and Pokémon GO are trademarks of their respective owners.</span></div>
<div class="grp"><span class="lab">Data and credits</span><div class="card" style="overflow:hidden">
<div class="cr2"><b>PvPoke</b><span>PvP rankings and movesets. MIT licence, © PvPoke.</span></div>
<div class="cr2"><b>Game data</b><span>Stats, moves and text from datamined game files (game_masters project).</span></div>
<div class="cr2"><b>Images</b><span>Pokémon images from datamined game assets (PokeMiners).</span></div>
<div class="cr2"><b>Events and raids</b><span>Gathered weekly from public community sites such as LeekDuck.</span></div>
<div class="cr2"><b>Open-source software</b><span>Licences of the libraries the app uses ›</span></div></div></div>'''
head = '<!doctype html><html lang="en"><head><meta charset="utf-8"><title>F2 Settings</title><link rel="stylesheet" href="../base.css"><link rel="stylesheet" href="_f2.css"></head><body>'
def phone(title, back, body):
    return head + f'''<div class="stage"><div class="device"><div class="screen"><div data-statusbar></div><div data-dethead="{back}|{title}"></div>
<div class="scroll"><div style="padding:14px 12px 24px;display:flex;flex-direction:column;gap:18px">{body}</div></div><div class="homebar"></div></div></div></div><script src="../chrome.js"></script></body></html>'''
p1 = phone("Settings", "Back", ACCT + LANG + THEME + MORE + OUT)
p2 = phone("About", "Settings", ABOUT)
desk = head + f'''<div class="desktop"><div data-sidebar="settings"></div><div class="main"><div data-topbar></div>
<div class="content" style="max-width:1100px;display:flex;flex-direction:column;gap:16px"><h1 class="bigtitle">Settings</h1>
<div style="display:grid;grid-template-columns:minmax(0,1fr) minmax(0,1fr);gap:28px;align-items:start">
<div style="display:flex;flex-direction:column;gap:18px">{ACCT}{LANG}{THEME.replace('class="seg"', 'class="seg" style="width:380px"')}<div class="card" style="overflow:hidden"><div class="row">Install the app<span class="r">Add to your computer {CH}</span></div></div>{OUT.replace('justify-content:center', 'justify-content:flex-start')}</div>
<div style="display:flex;flex-direction:column;gap:18px"><div class="grp"><span class="lab">About</span>{ABOUT}</div></div></div></div></div></div>
<script src="../chrome.js"></script><script>document.querySelectorAll('.navitem').forEach(a=>{{if(a.textContent.trim()==='Settings')a.classList.add('on')}});</script></body></html>'''
(HERE / 'F2-phone.html').write_text(p1); (HERE / 'F2a-phone.html').write_text(p2); (HERE / 'F2-desktop.html').write_text(desk)
