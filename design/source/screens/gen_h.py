"""Generates H1 AI chat, H2 Search results and H3 Offline (phone + desktop)."""
import pathlib
HERE = pathlib.Path(__file__).parent
head = lambda t: f'<!doctype html><html lang="en"><head><meta charset="utf-8"><title>{t}</title><link rel="stylesheet" href="../base.css"><link rel="stylesheet" href="_h.css"></head><body>'
FIX = '<script src="../chrome.js"></script><script>document.body.innerHTML=document.body.innerHTML.replace(/IC_(\\w+)/g,(m,k)=>icon(k,20));</script>'
AIH = '<span class="aihead">IC_ask Assistant</span>'
# ---------- H1 AI chat ----------
CONVO = f'''<div class="chat">
<div class="me">When does Kyogre leave raids, and who are the best counters?</div>
<div class="ai">{AIH}<span>Kyogre is in 5★ raids <b>until Tue 3 Nov, 10:00</b>, with a <b>Raid Hour tomorrow (Wed 28 Oct) 18:00–19:00</b>.</span>
<span>Best counters (Kyogre is weak to Electric and Grass):</span><ul><li><b>Mega Sceptile</b> · Bullet Seed + Frenzy Plant</li><li><b>Shadow Zekrom</b> · Charge Beam + Fusion Bolt</li><li><b>Zeraora</b> · Volt Switch + Plasma Fists</li></ul>
<span>If it has <b>Blizzard</b>, Grass counters take extra damage, so lean on Electric.</span>
<span class="used">Based on <a>Kyogre raid page</a><a>Calendar</a></span></div>
<div class="me">Qual é o melhor moveset do Garchomp para raids?</div>
<div class="ai">{AIH}<span>Para raids de Terra: <b>Tiro de Lama + Poder da Terra</b> (Mud Shot + Earth Power). Poder da Terra só se obtém com TM Elite, ou evoluindo um Gabite no <b>Dia Comunitário do Gible, sáb 7 nov</b>.</span>
<span class="used">Baseado em <a>Garchomp</a><a>Poder da Terra</a></span></div>
<div class="me">Does Kyogre have a special raid background this time?</div>
<div class="ai">{AIH}<span>I don't have that in our data, so I won't guess. Raid backgrounds aren't something the app tracks yet.</span></div></div>'''
COMPOSER = '<div class="composer"><div class="in">Ask about Pokémon GO…</div><span class="send">↑</span></div>'
def chat_phone(body, extra_top=""):
    return head("H1 AI chat") + f'''<div class="stage"><div class="device"><div class="screen"><div data-statusbar></div>
<header class="dethead"><a class="back">Close</a><span class="dettitle">Ask</span><a style="justify-self:end;font-weight:800;color:var(--accent)">New chat</a></header>
<div class="scroll"><div style="padding:14px 12px;display:flex;flex-direction:column;gap:12px">{extra_top}{body}</div></div>
<div class="fine" style="padding:6px 0;background:var(--surface)">Answers use the app's data and can still be wrong. This chat isn't saved.</div>{COMPOSER}</div></div></div>{FIX}</body></html>'''
h1 = chat_phone(CONVO)
CAP = '''<div class="chat"><div class="me">Which Max Battle is best this week?</div>
<div class="capbox">The assistant is paused until <b>1 November</b>: this month's AI budget is used up. Everything else in the app works as usual.</div></div>'''
h1c = chat_phone(CAP)
EMPTY = '''<div style="display:flex;flex-direction:column;gap:14px;padding-top:20px"><div style="text-align:center;display:flex;flex-direction:column;align-items:center;gap:8px"><span style="width:56px;height:56px;border-radius:18px;background:var(--accent-soft);color:var(--accent);display:grid;place-items:center">IC_ask</span><b style="font-size:20px">Ask anything about the game</b><span class="sub">In English or Portuguese. Answers come from the app's data.</span></div>
<div class="sugg"><span>Best non-legendary Ground attackers?</span><span>When does Kyogre leave raids?</span><span>What's in 10 km eggs right now?</span><span>Qual é o melhor moveset do Garchomp?</span></div></div>'''
h1e = chat_phone(EMPTY)
h1d = head("H1 AI chat") + f'''<div class="desktop"><div data-sidebar="today"></div><div class="main" style="opacity:.55"><div data-topbar></div><div class="content"><h1 class="bigtitle">Today</h1></div></div>
<aside class="panel"><div class="ph">IC_ask Ask<span style="flex:1"></span><a style="font-size:14px">New chat</a><span style="color:var(--text-3);margin-left:12px">✕</span></div>
<div style="flex:1;overflow:hidden;padding:16px">{CONVO}</div><div class="fine" style="padding:6px 0;background:var(--surface)">Answers use the app's data and can still be wrong. This chat isn't saved.</div>{COMPOSER.replace("30px", "16px")}</aside></div>{FIX}</body></html>'''
# ---------- H2 Search ----------
def sr(img, name, sub, kind):
    pic = f'<img class="sprite" src="../sprites/{img}.png" width="40" height="40" alt="">' if not img.startswith("IC_") else f'<span class="ic">{img}</span>'
    return f'<div class="sr">{pic}<div class="t"><b>{name}</b><span>{sub}</span></div><span class="kind">{kind}</span></div>'
GAR = [("445", "<mark>Gar</mark>chomp", "#445 · Dragon / Ground · 1★ raids now", "Pokémon"), ("282", "<mark>Gar</mark>devoir", "#282 · Psychic / Fairy", "Pokémon"),
       ("569", "<mark>Gar</mark>bodor", "#569 · Poison", "Pokémon"), ("934", "<mark>Gar</mark>ganacl", "#934 · Rock", "Pokémon")]
RAIDH = [("IC_calendar", "<mark>Raid Hour</mark>: Kyogre", "Tomorrow, Wed 28 Oct · 18:00–19:00", "Event"), ("IC_calendar", "<mark>Raid Hour</mark>: Groudon", "Wed 4 Nov · 18:00–19:00", "Event"),
         ("IC_calendar", "Calendar", "Every event, raid and <mark>Raid Hour</mark> by day", "Page")]
def search_phone(q, groups):
    body = ''.join(f'<span class="gh">{g}</span><div class="card" style="overflow:hidden">{"".join(sr(*r) for r in rows)}</div>' for g, rows in groups)
    return head("H2 Search") + f'''<div class="stage"><div class="device"><div class="screen"><div data-statusbar></div>
<div style="background:var(--surface);padding:6px 12px 12px;border-bottom:1px solid var(--line);display:flex;gap:10px;align-items:center"><div class="qbox" style="flex:1">IC_search <span>{q}</span><span class="x">✕</span></div><a style="font-weight:800">Cancel</a></div>
<div class="scroll"><div style="padding:12px 10px 20px;display:flex;flex-direction:column;gap:8px">{body}</div></div><div data-tabbar="today"></div></div></div></div>{FIX}</body></html>'''
h2 = search_phone("gar", [("Pokémon · 4", GAR), ("Recent searches", [("IC_search", "kyogre", "", ""), ("IC_search", "earth power", "", "")])])
h2r = search_phone("raid hour", [("Events · 2", RAIDH[:2]), ("Pages", RAIDH[2:])])
KYO = [("382", "<mark>Kyogre</mark>", "#382 · Water · Pokémon page", "Pokémon"), ("382", "<mark>Kyogre</mark> · 5★ raid", "Until Tue 3 Nov 10:00 · counters, your team, catch CP", "Raid boss"),
       ("IC_calendar", "Raid Hour: <mark>Kyogre</mark>", "Tomorrow, Wed 28 Oct · 18:00–19:00", "Event"), ("IC_moves", "Origin Pulse", "Water charged move · <mark>Kyogre</mark>'s signature move", "Move")]
h2d = head("H2 Search") + f'''<div class="desktop"><div data-sidebar="today"></div><div class="main"><div data-topbar></div><div class="content"><h1 class="bigtitle">Today</h1></div></div></div><div class="dim"></div>
<div class="palette"><div style="padding:14px;background:var(--surface);border-bottom:1px solid var(--line)"><div class="qbox">IC_search kyogre<span class="x" style="font-size:12px;font-family:var(--mono)">Esc</span></div></div>
<div style="padding:10px;display:flex;flex-direction:column;gap:6px"><span class="gh">Best matches · 4</span><div class="card" style="overflow:hidden">{"".join(sr(*r) for r in KYO)}</div>
<span class="fine" style="text-align:left;margin:4px 6px">↑ ↓ to move · Enter to open · type "raid hour" or "calendar" to jump to the Calendar</span></div></div>{FIX}</body></html>'''
# ---------- H3 Offline ----------
OFF = '''<div class="off"><span class="big"><svg width="44" height="44" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round"><path d="M2 8.5a15 15 0 0 1 20 0M5 12a10 10 0 0 1 14 0M8.5 15.5a5 5 0 0 1 7 0"/><circle cx="12" cy="19" r="1.2" fill="currentColor"/><path d="M3 3l18 18"/></svg></span>
<h1>You're offline</h1><p>MyPoGoCompanion needs a connection for now. Check your Wi-Fi or mobile data; the app reloads by itself when you're back.</p><button class="btn">Try again</button>
<p style="font-size:13px;color:var(--text-3)">Reading the Pokédex, your filters and your collection without signal is planned for a later version.</p></div>'''
h3 = head("H3 Offline") + f'''<div class="stage"><div class="device"><div class="screen"><div data-statusbar></div><div class="scroll">{OFF}</div><div data-tabbar="today"></div></div></div></div>{FIX}</body></html>'''
h3d = head("H3 Offline") + f'''<div class="desktop"><div data-sidebar="today"></div><div class="main"><div data-topbar></div><div class="content" style="display:grid;place-items:center;min-height:700px">{OFF}</div></div></div>{FIX}</body></html>'''
for n, src in [("H1-phone", h1), ("H1c-phone", h1c), ("H1e-phone", h1e), ("H1-desktop", h1d), ("H2-phone", h2), ("H2r-phone", h2r), ("H2-desktop", h2d), ("H3-phone", h3), ("H3-desktop", h3d)]:
    (HERE / f"{n}.html").write_text(src)
