"""Generates D1 Keeper filters (phone + desktop) from today's real build output."""
import json, pathlib
HERE = pathlib.Path(__file__).parent
ROOT = HERE.parents[2]
pvp = json.loads((ROOT / "build/pvp_strings.json").read_text())
raid = json.loads((ROOT / "build/data_raid.json").read_text())
mega = json.loads((ROOT / "build/data_mega.json").read_text())
esc = lambda s: s.replace("&", "&amp;")
GL = pvp["GL"]["strings"]; UL = pvp["UL"]["strings"]
RAID = raid["raid"]["11-15"] + "&3-4attack"
SAFE = "!#GL&!#UL&!#Raid&!#Max&!#Mega&!shiny&!lucky&!legendary&!mythical&!favorite&!4*&!costume"
ICON = lambda txt, col: f'<span class="ic" style="background:{col}">{txt}</span>'
def part(lab, text, done=False):
    return f'<div class="part"><span class="lab">{lab}</span><code>{esc(text)}</code><button class="copy{" done" if done else ""}">{"Copied ✓" if done else "Copy"}</button></div>'
def card(icon, title, tag, desc, parts, extra=""):
    tg = f'<span class="tagname">#{tag}</span>' if tag else ''
    return f'<div class="kf"><div class="hd">{icon}<h3>{title}</h3>{tg}</div><div class="ds">{desc}</div>{extra}{"".join(parts)}</div>'
seg = lambda ks, on: '<div class="seg">' + ''.join(f'<span class="{"on" if k == on else ""}">{k}</span>' for k in ks) + '</div>'
CARDS = [
    card(ICON("GL", "#2766BF"), "Great League", "GL", "Pokémon whose IVs rank high under 1,500 CP. Pre-evolutions count when their evolution qualifies. All 1,025 Pokémon, split in 2 parts.",
         [part(f"Part {i + 1}<br>#{s['from']}–{s['to']}", s["text"], i == 0) for i, s in enumerate(GL)]),
    card(ICON("UL", "#5B4380"), "Ultra League", "UL", "The same for 2,500 CP, in 3 parts.",
         [part(f"Part {i + 1}<br>#{s['from']}–{s['to']}", s["text"]) for i, s in enumerate(UL)]),
    card(ICON("R", "#BF3F26"), "Raid attackers", "Raid", f"Top 10 attackers of every type, their Megas and their pre-evolutions: {raid['dexCount']} Pokémon.",
         [part("Attack<br>11–15", RAID)], f'<div class="opt">Attack bar {seg(["11–15", "15 only"], "11–15")}</div>'),
    card(ICON("M", "#9C2A5E"), "Max Battle attackers", "Max", "Every Pokémon that can Dynamax or Gigantamax, with a high Attack bar. Never needs updating.",
         [part("Attack<br>11–15", raid["max"]["11-15"])]),
    card(ICON("Me", "#0B7A72"), "Megas", "Mega", f"Every Pokémon with a Mega in the game ({len(mega['released'])}), plus {len(mega['notYet'])} announced but not in the game yet.",
         [part("With pre-<br>evolutions", mega["stringWithPre"])], f'<div class="opt">Include pre-evolutions {seg(["Yes", "No"], "Yes")}</div>'),
]
SAFEC = card(ICON("✓", "#A35A00"), "Safe to transfer", "", "Everything you haven't tagged with the five filters above, minus shinies, luckies, legendaries, mythicals, favourites, hundos and costumes.",
             [part("Use<br>last", SAFE)], '<div class="warnbox">Tag with all five filters first: this one finds what is left. Check the results before transferring.</div>')
HOWTO_SHORT = '<div class="howto"><span style="font-size:18px">?</span>How to use these in the game<span style="margin-left:auto;font-size:18px">›</span></div>'
STEPS = '''<ol class="steps"><li><b>Copy</b> a string here.</li><li>In the game, open your Pokémon storage and <b>paste</b> it in the search bar.</li><li>Tap <b>Select all</b>, then tag them (e.g. <code>GL</code>).</li><li>For filters with several parts, repeat with each part using the same tag.</li><li>Later, search <code>#GL</code> to see them all again.</li></ol>'''
head = '<!doctype html><html lang="en"><head><meta charset="utf-8"><title>D1 Keeper filters</title><link rel="stylesheet" href="../base.css"><link rel="stylesheet" href="_d1.css"></head><body>'
phead = '''<header class="apphead"><div class="titlerow"><h1 class="bigtitle">Filters</h1><span data-ask></span><div class="avatar">A</div></div>
<div class="seg"><span class="on">Keepers</span><span>Builder</span><span>Saved</span></div></header>'''
import re
def compact(html):
    def repl(m):
        lab = re.sub(r"<br>", " · ", m.group(1)); done = "done" in m.group(2)
        return f'<button class="copy{" done" if done else ""}" style="flex:1;height:auto;min-height:40px;padding:6px 8px;line-height:1.2">{"Copied ✓" if done else "Copy"}<br><span style="font-size:11px;font-weight:700;opacity:.85">{lab}</span></button>'
    html = re.sub(r'<div class="part"><span class="lab">(.*?)</span><code>.*?</code><button class="(copy[^"]*)">.*?</button></div>', repl, html)
    def grp(m):
        g = m.group(1)
        if g.count("<button") == 1: g = re.sub(r"<br><span.*?</span>", "", g).replace("min-height:40px", "min-height:44px")
        return f'<div style="display:flex;gap:6px">{g}</div>'
    return re.sub(r'((?:<button class="copy[^>]*>.*?</button>)+)', grp, html)
phone = head + f'''<div class="stage"><div class="device"><div class="screen"><div data-statusbar></div>{phead}
<div class="scroll"><div style="padding:12px 10px 20px;display:flex;flex-direction:column;gap:10px">{HOWTO_SHORT}{compact("".join(CARDS) + SAFEC)}</div></div>
<div data-tabbar="filters"></div></div></div></div><script src="../chrome.js"></script></body></html>'''
desk = head + f'''<div class="desktop"><div data-sidebar="filters"></div><div class="main"><div data-topbar></div>
<div class="content" style="max-width:1180px;display:flex;flex-direction:column;gap:16px">
<div style="display:flex;align-items:center;gap:20px"><h1 class="bigtitle" style="flex:none">Filters</h1><div class="seg" style="width:360px"><span class="on">Keepers</span><span>Builder</span><span>Saved</span></div></div>
<div style="display:grid;grid-template-columns:minmax(0,1fr) minmax(0,1fr) 300px;gap:16px;align-items:start">
<div style="display:flex;flex-direction:column;gap:14px">{CARDS[0]}{CARDS[2]}{CARDS[4]}</div>
<div style="display:flex;flex-direction:column;gap:14px">{CARDS[1]}{CARDS[3]}{SAFEC}</div>
<div class="card" style="padding:16px;display:flex;flex-direction:column;gap:12px;position:sticky;top:0"><b style="font-size:16px">How to use them</b>{STEPS}
<div class="sub" style="line-height:1.45">These lists are rebuilt automatically whenever rankings or the game change. Strings stay under the game's limit of about 1,950 characters, so long ones come in parts.</div></div></div></div></div></div><script src="../chrome.js"></script></body></html>'''
(HERE / 'D1-phone.html').write_text(phone)
(HERE / 'D1-desktop.html').write_text(desk)
