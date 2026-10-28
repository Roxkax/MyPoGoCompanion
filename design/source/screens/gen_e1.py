"""Generates E1 My collection (phone list, phone empty state, desktop)."""
import pathlib
HERE = pathlib.Path(__file__).parent
STAR = '<svg width="12" height="12" viewBox="0 0 24 24"><path d="M12 2l2.2 6.6L21 11l-6.8 2.4L12 20l-2.2-6.6L3 11l6.8-2.4z" fill="#C08A00"/></svg>'
# sprite, name, marks, level, ivs, cp, gl, ul, ml, raid, "as" note, caught
ROWS = [("443", "Gible", "", "20", "0/15/14", "565", "#1", "—", "—", "", "as Garchomp", "Sun 25 Oct"),
        ("443", "Gible", "", "20", "15/15/14", "634", "—", "—", "#3", "", "as Garchomp", "Sun 25 Oct"),
        ("443", "Gible", "", "15", "1/13/15", "424", "#11", "—", "—", "", "as Garchomp", "Sat 24 Oct"),
        ("443", "Gible", "", "8", "10/4/2", "218", "—", "—", "—", "", "", "Sat 24 Oct"),
        ("444", "Gabite", "", "25", "12/12/13", "1,296", "—", "—", "—", "", "", "Thu 22 Oct"),
        ("445", "Garchomp", "", "23", "2/15/13", "2,471", "—", "—", "—", "Ground 39%", "", "Wed 21 Oct"),
        ("334", "Altaria", "lucky", "29", "0/14/15", "1,497", "#1", "—", "—", "", "", "Sun 18 Oct"),
        ("383", "Groudon", "", "40", "15/15/13", "4,096", "—", "—", "—", "Ground 59%", "", "Sat 10 Oct"),
        ("260", "Swampert", "ready", "40", "14/13/15", "2,945", "—", "—", "—", "Ground 71%", "as Mega", "Mon 5 Oct"),
        ("254", "Sceptile", "ready", "40", "14/15/13", "2,731", "—", "—", "—", "Grass 100%", "as Mega", "Thu 1 Oct"),
        ("184", "Azumarill", "", "45.5", "0/15/14", "1,496", "#6", "—", "—", "", "", "Tue 15 Sep"),
        ("308", "Medicham", "lucky", "50", "1/15/15", "1,451", "—", "—", "—", "", "", "Sat 2 May")]
pct = lambda iv: round(sum(int(x) for x in iv.split("/")) / 45 * 100)
MK = {"lucky": '<span class="mk lucky">Lucky</span>', "ready": '<span class="mk ready">Mega ready</span>', "": ""}
def vals(r):
    gl, ul, ml, raid = r[6], r[7], r[8], r[9]
    out = [f'<span class="val pvp">{lg} {v}</span>' for lg, v in (("GL", gl), ("UL", ul), ("ML", ml)) if v != "—"]
    if raid: out.append(f'<span class="val raid">{raid}</span>')
    if not out: out = ['<span class="val none">No league or raid use</span>']
    if r[10]: out.append(f'<span class="as">{r[10]}</span>')
    return ''.join(out)
def prow(r):
    sp, n, mk, lv, iv, cp = r[:6]
    return f'''<div class="cr"><img class="sprite" src="../sprites/{sp}.png" width="46" height="46" alt=""><div class="nm"><b>{n}{MK[mk]}</b>
<span class="st">CP {cp} · L{lv} · {iv} ({pct(iv)}%)</span></div><div class="vals">{vals(r)}</div></div>'''
def drow(r):
    sp, n, mk, lv, iv, cp, gl, ul, ml, raid, asn, caught = r
    c = lambda v: f'<span class="val pvp">{v}</span>' if v != "—" else '<span class="faint">—</span>'
    return f'''<tr><td><span style="display:flex;align-items:center;gap:10px"><img class="sprite" src="../sprites/{sp}.png" width="34" height="34" alt="">{n}{MK[mk]}{f' <span class="as">{asn}</span>' if asn else ''}</span></td>
<td class="n">{cp}</td><td class="n">{lv}</td><td class="n">{iv}</td><td class="n">{pct(iv)}%</td><td>{c(gl)}</td><td>{c(ul)}</td><td>{c(ml)}</td><td>{f'<span class="val raid">{raid}</span>' if raid else '<span class="faint">—</span>'}</td><td style="color:var(--text-3);font-size:13.5px">{caught}</td></tr>'''
head = '<!doctype html><html lang="en"><head><meta charset="utf-8"><title>E1 My collection</title><link rel="stylesheet" href="../base.css"><link rel="stylesheet" href="_b1.css"><link rel="stylesheet" href="_e1.css"></head><body>'
phead = '''<header class="apphead"><div class="titlerow"><h1 class="bigtitle">Collection</h1><span data-ask></span><div class="avatar">A</div></div>
<div class="searchbox">SEARCHICON Search your Pokémon</div></header>'''
script = '<script src="../chrome.js"></script><script>document.body.innerHTML=document.body.innerHTML.replaceAll("SEARCHICON",icon("search",19));</script>'
SUM = '<div class="sum"><div style="display:flex;flex-direction:column;gap:2px"><b>2,148 Pokémon</b><span class="sub">Last upload Sun 25 Oct, 21:14</span></div><button class="up">Upload new</button></div>'
FIL = '<svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round"><path d="M4 6h16M7 12h10M10 18h4"/></svg>'
TOOLS = f'<div class="toolbar"><span class="tbtn">{FIL}Filters</span><span class="tbtn">Sort: Recently caught</span><span class="tbtn">Shiny</span><span class="tbtn">Lucky</span><span class="tbtn">Shadow</span></div>'
LEG = '<div class="sub" style="margin:0 4px;line-height:1.4"><span class="val pvp">GL #1</span> = rank of its IVs in that league (only the top 2%: within 98% of the best) · <span class="val raid">Ground 59%</span> = raid score vs the best of its type</div>'
phone = head + f'''<div class="stage"><div class="device"><div class="screen"><div data-statusbar></div>{phead}
<div class="scroll"><div style="padding:12px 10px 20px;display:flex;flex-direction:column;gap:10px">{SUM}{TOOLS}{LEG}<div class="card" style="overflow:hidden">{''.join(prow(r) for r in ROWS)}</div></div></div>
<div data-tabbar="collection"></div></div></div></div>{script}</body></html>'''
EMPTY = '''<div class="empty"><h2>Bring your Pokémon in</h2><span class="sub" style="font-size:14px;line-height:1.45">The game doesn't let other apps read your account, so your collection comes from a <b>Poke Genie</b> export. It takes a minute.</span>
<ol class="steps"><li>In <b>Poke Genie</b>, open your scan history and <b>export it as CSV</b> (all your scanned Pokémon).</li><li>Save the file to your phone or computer.</li><li>Upload it here. Upload a newer export whenever you want to refresh.</li></ol>
<div class="drop">Drop the CSV here or</div><button class="btn">Choose file</button>
<span class="sub" style="line-height:1.45">Your collection is stored only in your account. It powers "your counters", "your team" and "only what I own" everywhere in the app.</span></div>'''
phonee = head + f'''<div class="stage"><div class="device"><div class="screen"><div data-statusbar></div><header class="apphead"><div class="titlerow"><h1 class="bigtitle">Collection</h1><span data-ask></span><div class="avatar">A</div></div></header>
<div class="scroll"><div style="padding:12px 10px 20px;display:flex;flex-direction:column;gap:10px">{EMPTY.replace('<div class="drop">Drop the CSV here or</div>', '')}</div></div>
<div data-tabbar="collection"></div></div></div></div>{script}</body></html>'''
desk = head + f'''<div class="desktop"><div data-sidebar="collection"></div><div class="main"><div data-topbar></div>
<div class="content" style="max-width:1180px;display:flex;flex-direction:column;gap:14px">
<div style="display:flex;align-items:center;gap:16px"><h1 class="bigtitle" style="flex:none">Collection</h1><span class="sub" style="font-size:14px">2,148 Pokémon · last upload Sun 25 Oct, 21:14</span><span style="flex:1"></span><button class="up" style="height:40px">Upload new export</button></div>
<div style="display:flex;gap:10px;align-items:center"><div class="searchbox" style="width:320px;height:40px;background:var(--surface);border:1px solid var(--line)">SEARCHICON Search your Pokémon</div>{TOOLS[21:-6]}<span style="flex:1"></span></div>
<div class="card" style="overflow:hidden"><table class="col"><tr><th>Pokémon</th><th class="n">CP</th><th class="n">Level</th><th class="n">IVs</th><th class="n">%</th><th>Great</th><th>Ultra</th><th>Master</th><th>Raids</th><th>Caught ↓</th></tr>{''.join(drow(r) for r in ROWS)}</table></div>
<p class="sub" style="margin:0">League columns: rank of these IVs among all 4,096 combinations, judged as its final evolution ("as Garchomp"); shown only for the top 2% (within 98% of the best IVs), the same rule as the keeper filters. Raids: raid score as a % of the best attacker of its type. Click a Pokémon for its page with your copy's details.</p></div></div></div>{script}</body></html>'''
(HERE / 'E1-phone.html').write_text(phone); (HERE / 'E1e-phone.html').write_text(phonee); (HERE / 'E1-desktop.html').write_text(desk)
