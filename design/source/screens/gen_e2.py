"""Generates E2 Upload & import report (phone uploading, phone report, desktop)."""
import pathlib
HERE = pathlib.Path(__file__).parent
DONE = '<div class="done"><span style="font-size:22px">✓</span><div style="display:flex;flex-direction:column;gap:2px"><b>Upload complete</b><span class="file">pokegenie-export.csv · Sun 25 Oct, 21:14</span></div></div>'
TILES = '''<div class="tiles"><div class="tile"><b>2,148</b><span>Pokémon imported</span></div><div class="tile warn"><b>3</b><span>Not recognised</span></div>
<div class="tile"><b>63 · 40</b><span>New · gone since last upload</span></div><div class="tile"><b>12</b><span>Changed (power-ups, evolutions, moves)</span></div></div>'''
UNM = [("Row 1,204", "Pikachu (Witch Hat 2025)", "A costume the Pokédex doesn't have yet"),
       ("Row 1,877", "Gimmighoul (Roaming Form)", "A form the Pokédex doesn't have"),
       ("Row 2,012", "Sandaconda?", "Name not recognised (Poke Genie couldn't read it)")]
unm = f'''<div class="sec2"><h2>Not recognised (3)</h2><div class="card" style="overflow:hidden">{''.join(f'<div class="ur"><code>{r} · {n}</code><span>{w}</span></div>' for r, n, w in UNM)}
<div class="ur"><span style="line-height:1.4">These rows were left out. They're added automatically on your next upload once the Pokédex knows them.</span></div></div></div>'''
CHG = [("443", "Gible ×3", "Caught Sat 24 – Sun 25 Oct", "new", "New"),
       ("444", "Gabite 12/12/13", "Evolved from Gible", "", "Evolved"),
       ("334", "Altaria 0/14/15", "L27.5 → L29 · CP 1,420 → 1,497", "up", "Powered up"),
       ("383", "Groudon 15/15/13", "L35 → L40 · CP 3,804 → 4,096", "up", "Powered up"),
       ("308", "Medicham 1/15/15", "New move: Psychic", "", "New move"),
       ("530", "Excadrill 7/9/12", "No longer in your export", "gone", "Gone")]
chg = f'''<div class="sec2"><h2>What changed since Sun 18 Oct</h2><div class="card" style="overflow:hidden">{''.join(f'<div class="chg"><img class="sprite" src="../sprites/{sp}.png" width="38" height="38" alt=""><div class="t"><b>{n}</b><span>{d}</span></div><span class="kind {k}">{l}</span></div>' for sp, n, d, k, l in CHG)}
<div class="ur" style="border-top:1px solid var(--line)"><a style="font-weight:800;font-size:14px">See all 115 changes ›</a></div></div>
<span class="sub" style="margin:0 4px;line-height:1.4">Changes are found by matching the same Pokémon between uploads (same family, IVs and catch date), since exports have no ID per Pokémon.</span></div>'''
head = '<!doctype html><html lang="en"><head><meta charset="utf-8"><title>E2 Upload &amp; import report</title><link rel="stylesheet" href="../base.css"><link rel="stylesheet" href="_e2.css"></head><body>'
def phone(body, title="Import report"):
    return head + f'''<div class="stage"><div class="device"><div class="screen"><div data-statusbar></div><div data-dethead="Collection|{title}"></div>
<div class="scroll"><div style="padding:12px 10px 20px;display:flex;flex-direction:column;gap:14px">{body}</div></div>
<div data-tabbar="collection"></div></div></div></div><script src="../chrome.js"></script></body></html>'''
report = phone(f'{DONE}{TILES}{unm}{chg}<button class="btn">Go to my collection</button>')
UPL = '''<div class="card" style="padding:18px;display:flex;flex-direction:column;gap:16px"><b style="font-size:19px">Importing your collection</b><span class="file" style="font-family:var(--mono);font-size:12px;color:var(--text-2)">pokegenie-export.csv · 2,151 rows</span>
<div class="prog"><i style="width:68%"></i></div>
<div class="stp"><span class="d ok">✓</span>Reading the file</div><div class="stp"><span class="d ok">✓</span>Checking it's a Poke Genie export</div>
<div class="stp" style="color:var(--text)"><span class="d now"></span>Matching to the Pokédex · 1,463 of 2,151</div><div class="stp"><span class="d wait"></span>Working out ranks and raid scores</div><div class="stp"><span class="d wait"></span>Comparing with your last upload</div>
<span class="sub" style="line-height:1.4">You can leave this screen; the report will be waiting under Collection.</span></div>'''
uploading = phone(UPL, "Upload")
desk = head + f'''<div class="desktop"><div data-sidebar="collection"></div><div class="main"><div data-topbar></div>
<div class="content" style="max-width:1180px;display:flex;flex-direction:column;gap:16px"><a class="backlink">‹ Collection</a><h1 class="bigtitle" style="margin-top:-8px">Import report</h1>
<div style="display:grid;grid-template-columns:minmax(0,1fr) minmax(0,1fr);gap:24px;align-items:start">
<div style="display:flex;flex-direction:column;gap:14px">{DONE}{TILES.replace('repeat(2,', 'repeat(2,')}{unm}</div>
<div style="display:flex;flex-direction:column;gap:14px">{chg}<button class="btn" style="width:260px;align-self:flex-end">Go to my collection</button></div></div></div></div></div><script src="../chrome.js"></script></body></html>'''
(HERE / 'E2-phone.html').write_text(report); (HERE / 'E2u-phone.html').write_text(uploading); (HERE / 'E2-desktop.html').write_text(desk)
