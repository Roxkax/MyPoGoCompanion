"""Generates C1 Raid bosses (phone + desktop)."""
import pathlib, sys
HERE = pathlib.Path(__file__).parent
sys.path.insert(0, str(HERE))
from gen_b5 import eff, TYPES
STAR = '<svg class="shiny" width="16" height="16" viewBox="0 0 24 24" aria-label="Shiny available"><path d="M12 2l2.2 6.6L21 11l-6.8 2.4L12 20l-2.2-6.6L3 11l6.8-2.4z" fill="currentColor"/></svg>'
FLAME = '<span class="shadowflame"><svg width="11" height="11" viewBox="0 0 24 24"><path d="M12 2c1 4 6 6 6 12a6 6 0 0 1-12 0c0-3 2-5 3-7 1 2 3 3 3 3s-1-4 0-8z" fill="#E9D5FF"/></svg></span>'
# tier, tier class, sprite, name, types, shiny, shadow, cp20, cp25, note
NOW = [("5★", "five", "382", "Kyogre", ["water"], True, False, "2,351", "2,939", "Raid Hour Wed 18:00"),
       ("Mega", "mega", "094_51", "Mega Gengar", ["ghost", "poison"], True, False, "1,644", "2,055", "~Until Sat 31 Oct 10:00"),
       ("Shadow 3★", "shadow", "359", "Shadow Absol", ["dark"], False, True, "1,443", "1,805", None),
       ("3★", "", "006", "Charizard", ["fire", "flying"], False, False, "1,651", "2,064", None),
       ("3★", "", "131", "Lapras", ["water", "ice"], False, False, "1,509", "1,886", None),
       ("1★", "", "443", "Gible", ["dragon", "ground"], True, False, "635", "794", None),
       ("1★", "", "004", "Charmander", ["fire"], False, False, "560", "700", None)]
NEXT = [("Mega", "mega", "609", "Mega Chandelure", ["ghost", "fire"], False, False, "1,867", "2,335", "From Sat 31 Oct 10:00"),
        ("5★", "five", "383", "Groudon", ["ground"], True, False, "2,351", "2,939", "From Tue 3 Nov 10:00")]
def weak(ts):
    out = []
    for a in TYPES:
        m = 1.0
        for d in ts: m *= eff(a, d)
        if m > 1.01: out.append((a, m))
    out.sort(key=lambda x: -x[1])
    return ''.join(f'<span class="type {a}">{a.capitalize()}{" ×2.56" if m > 2 else ""}</span>' for a, m in out)
T = lambda ts: ''.join(f'<span class="type {t}">{t.capitalize()}</span>' for t in ts)
def sprite(sp, shadow, size):
    return f'<span class="spritewrap"><img class="sprite" src="../sprites/{sp}.png" width="{size}" height="{size}" alt="">{FLAME if shadow else ""}</span>'
def pill(note):
    if not note: return ''
    return f'<span class="pill soft" style="height:20px;font-size:11px">{note[1:]}</span>' if note.startswith("~") else f'<span class="hour">{note}</span>'
def prow(b, upcoming=False):
    tier, tc, sp, n, ts, sh, sd, c20, c25, note = b
    nt = pill(note)
    return f'''<div class="br">{sprite(sp, sd, 52)}<div class="nm"><span class="top"><span class="tier {tc}">{tier}</span><b>{n}</b></span>
<span class="types">{T(ts)}</span><span class="weak">Weak to {weak(ts)}</span><span class="pcp">100% CP <b>{c20}</b> · {c25} boosted</span>{f'<span style="display:flex">{nt}</span>' if nt else ''}</div>{STAR if sh else ''}<span class="chev">›</span></div>'''
def dcard(b):
    tier, tc, sp, n, ts, sh, sd, c20, c25, note = b
    return f'''<div class="bcard"><div class="hd"><span class="tier {tc}">{tier}</span>{STAR if sh else ''}</div>
<div class="mid">{sprite(sp, sd, 72)}<div style="display:flex;flex-direction:column;gap:5px;min-width:0"><b class="n">{n}</b><span class="types" style="display:flex;gap:3px">{T(ts)}</span></div></div>
<span class="weak">Weak to {weak(ts)}</span>{f'<span style="display:flex">{pill(note)}</span>' if note else ''}
<div class="cp"><span>100% CP</span><span><b>{c20}</b> · {c25} boosted</span></div></div>'''
FILT = '<div class="toolbar">' + ''.join(f'<span class="tbtn{" on" if i == 0 else ""}">{t}</span>' for i, t in enumerate(["All", "5★", "Mega", "Shadow", "3★", "1★"])) + '</div>'
head = '<!doctype html><html lang="en"><head><meta charset="utf-8"><title>C1 Raid bosses</title><link rel="stylesheet" href="../base.css"><link rel="stylesheet" href="_b1.css"><link rel="stylesheet" href="_c1.css"></head><body>'
phead = '''<header class="apphead"><div class="titlerow"><h1 class="bigtitle">Battle</h1><span data-ask></span><div class="avatar">A</div></div>
<div class="seg"><span class="on">Raids</span><span>Tier lists</span><span>Teams</span></div></header>'''
phone = head + f'''<div class="stage"><div class="device"><div class="screen"><div data-statusbar></div>{phead}
<div class="scroll"><div style="padding:12px 10px 20px;display:flex;flex-direction:column;gap:10px">{FILT}
<div class="sech"><h2>Now</h2><span>until Tue 3 Nov 10:00</span></div><div class="card" style="overflow:hidden">{''.join(prow(b) for b in NOW)}</div>
<div class="sech" style="margin-top:8px"><h2>Coming up</h2></div><div class="card" style="overflow:hidden">{''.join(prow(b) for b in NEXT)}</div></div></div>
<div data-tabbar="battle"></div></div></div></div><script src="../chrome.js"></script></body></html>'''
def dsec(title, sub, bs): return f'<div class="sech"><h2 style="font-size:20px">{title}</h2><span>{sub}</span></div><div class="bgrid">{"".join(dcard(b) for b in bs)}</div>'
desk = head + f'''<div class="desktop"><div data-sidebar="battle"></div><div class="main"><div data-topbar></div>
<div class="content" style="max-width:1180px;display:flex;flex-direction:column;gap:16px">
<div style="display:flex;align-items:center;gap:20px"><h1 class="bigtitle" style="flex:none">Battle</h1><div class="seg" style="width:360px"><span class="on">Raids</span><span>Tier lists</span><span>Teams</span></div><span style="flex:1"></span>{FILT}</div>
{dsec("Now", "until Tue 3 Nov 10:00", NOW)}<div style="height:6px"></div>{dsec("Coming up", "", NEXT)}</div></div></div><script src="../chrome.js"></script></body></html>'''
(HERE / 'C1-phone.html').write_text(phone)
(HERE / 'C1-desktop.html').write_text(desk)
