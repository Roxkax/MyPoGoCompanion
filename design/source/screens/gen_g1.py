"""Generates G1 Admin: Data status & re-run (phone + desktop)."""
import pathlib
HERE = pathlib.Path(__file__).parent
DAILY = '''<div class="job"><div class="hd"><h3>Daily game data</h3><span class="ok">✓ Worked</span></div>
<div class="kvs"><span>Last run</span><span>Today 06:00 · took 4 min 12 s</span><span>Next run</span><span>Tomorrow 06:00</span></div>
<ul class="chg"><li>Game files: <b>no change</b></li><li>PvPoke rankings: <b>updated</b> (Great 14 moved, Ultra 9, Master 3)</li><li>Search strings: <b>8 rebuilt</b>, all passed the search check</li></ul>
<button class="run">Run now</button></div>'''
WEEKLY = '''<div class="job"><div class="hd"><h3>Weekly AI researcher</h3><span class="ok">✓ Worked</span></div>
<div class="kvs"><span>Last run</span><span>Sun 25 Oct 22:00 · took 6 min · cost $0.38</span><span>Next run</span><span>Sun 1 Nov 22:00</span></div>
<ul class="chg"><li>Published <b>7 raid bosses, 2 Max bosses, 9 events</b>, the 10 km egg pool and 4 research tasks</li><li><b>3 new</b> entries, <b>1 changed</b> (Mega Gengar now ends Sat 31 Oct)</li></ul>
<div style="display:flex;gap:8px"><button class="run">Run now</button><button class="run" style="background:var(--accent-soft);color:var(--accent)">Fix entries ›</button></div></div>'''
SPEND = '''<div class="job spend"><div class="hd"><h3>AI spending this month</h3><span style="margin-left:auto;font-weight:800">$1.82 of $5.00</span></div><div class="bar"><i></i></div>
<span class="sub" style="line-height:1.4">AI researcher $1.52 (4 runs) · chat $0.30. When it reaches $5 the AI stops until 1 Nov and you're told. The cap is changed in the configuration.</span></div>'''
HIST = [("Today 06:00", "Daily game data", "Worked · rankings updated", True),
        ("Mon 26 Oct 06:00", "Daily game data", "Worked · no changes", True),
        ("Sun 25 Oct 22:00", "AI researcher", "Worked · 3 new, 1 changed", True),
        ("Sun 25 Oct 06:00", "Daily game data", "Worked · retried after Saturday", True),
        ("Sat 24 Oct 06:00", "Daily game data", "Failed · old data kept live", False)]
hist = '<div class="card" style="overflow:hidden">' + ''.join(f'<div class="hist"><span class="w">{w}</span><span class="d"><b style="color:var(--text)">{j}</b> · {d}</span><span class="{"ok" if ok else "bad"}" style="margin:0">{"✓" if ok else "Failed"}</span></div>' for w, j, d, ok in HIST)
hist += '<div class="hist" style="display:block"><div class="errbox"><b>Sat 24 Oct · why it failed:</b> the Ultra League string part 2 matched 3 Pokémon it shouldn\'t in the search check, so nothing was published and yesterday\'s data stayed live. Sunday\'s run passed.</div></div></div>'
WARN = '''<div class="job"><h3 style="font-size:15px">What users see if updates stop</h3><span class="sub" style="line-height:1.4">Daily game data older than 3 days, or raids and events older than 9 days, show this where that data appears:</span>
<div class="warnban"><span>⚠</span><span>Raid bosses haven't updated in 9 days. Some may have changed in the game.</span></div></div>'''
head = '<!doctype html><html lang="en"><head><meta charset="utf-8"><title>G1 Data status</title><link rel="stylesheet" href="../base.css"><link rel="stylesheet" href="_g1.css"></head><body>'
SEG = '<div class="seg"><span class="on">Data status</span><span>Fix entries</span></div>'
phone = head + f'''<div class="stage"><div class="device"><div class="screen"><div data-statusbar></div><div data-dethead="Settings|Admin"></div>
<div class="scroll"><div style="padding:12px 10px 24px;display:flex;flex-direction:column;gap:12px">{SEG}{DAILY}{WEEKLY}{SPEND}<h2 style="margin:6px 4px 0;font-size:17px">Recent runs</h2>{hist}{WARN}</div></div>
<div class="homebar"></div></div></div></div><script src="../chrome.js"></script></body></html>'''
desk = head + f'''<div class="desktop"><div data-sidebar="admin"></div><div class="main"><div data-topbar></div>
<div class="content" style="max-width:1180px;display:flex;flex-direction:column;gap:16px">
<div style="display:flex;align-items:center;gap:20px"><h1 class="bigtitle" style="flex:none">Admin</h1>{SEG.replace('class="seg"', 'class="seg" style="width:300px"')}</div>
<div style="display:grid;grid-template-columns:1fr 1fr;gap:16px;align-items:start">{DAILY}{WEEKLY}</div>
<div style="display:grid;grid-template-columns:minmax(0,1.4fr) minmax(0,1fr);gap:16px;align-items:start">
<div style="display:flex;flex-direction:column;gap:8px"><h2 style="margin:0 4px;font-size:17px">Recent runs</h2>{hist}</div>
<div style="display:flex;flex-direction:column;gap:16px;padding-top:31px">{SPEND}{WARN}</div></div></div></div></div>
<script src="../chrome.js"></script><script>document.querySelectorAll('.navitem').forEach(a=>{{if(a.textContent.trim()==='Admin')a.classList.add('on')}});</script></body></html>'''
(HERE / 'G1-phone.html').write_text(phone); (HERE / 'G1-desktop.html').write_text(desk)
