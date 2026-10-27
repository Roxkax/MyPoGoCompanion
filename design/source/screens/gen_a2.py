"""Generates A2 Calendar (phone + desktop)."""
import pathlib
HERE = pathlib.Path(__file__).parent

def sp(ids, size=40):
    return '<span class="csprites">' + ''.join(f'<img class="sprite" src="../sprites/{i}.png" width="{size}" height="{size}" alt="">' for i in ids) + '</span>'

def item(time, kind, title, chips=(), ids=(), ev=False, until=None, phone=False):
    k = f'<span class="kind{" ev" if ev else ""}">{kind}</span>'
    if until:
        k = f'<span style="display:flex;gap:6px;align-items:center"><span class="pill live" style="height:20px"><span class="dot"></span>Live</span>{k}</span>'
    ch = ('<div class="chips">' + ''.join(f'<span class="chip">{c}</span>' for c in chips) + '</div>') if chips else ''
    u = f'<div class="until">Until {until}</div>' if until else ''
    t = '' if (until and phone) else f'<div class="ctime">{"Until " + until.replace(" ", "&nbsp;", 2).replace("&nbsp;", " ", 1) if until else time}</div>'
    if until and not phone:
        u = ''
    ids = list(ids)[:2] if phone else ids
    return f'<div class="citem">{t}<div class="cbody">{k}<div class="ctitle">{title}</div>{u}{ch}</div>{sp(ids) if ids else ""}<span class="chev">›</span></div>'

def days(phone):
    return [
        ('Now', '', [
            item('', 'Event', 'Halloween event', ['Ghost-types spawn more', '2× catch candy'], ['094'], True, 'Sat 31 Oct 20:00', phone),
            item('', 'Raids', '7 raid bosses' if phone else 'Kyogre, Mega Gengar, Shadow Absol and 4 more', [], ['382', '094_51', '359'], until='Tue 3 Nov 10:00', phone=phone),
            item('', 'Max Battles', '2 Max bosses' if phone else 'Gigantamax Venusaur, Dynamax Charmander', [], ['003', '004'], until='Sun 1 Nov 10:00', phone=phone)]),
        ('Wed 28 Oct', 'Tomorrow', [item('18:00–<br>19:00', 'Raid Hour', 'Kyogre', ['5★ raids in most gyms'], ['382'])]),
        ('Sat 31 Oct', '', [item('10:00', 'Debut', 'Mega Chandelure in Mega raids', [], ['609'], True),
                            item('20:00', 'Event ends', 'Halloween event ends')]),
        ('Sun 1 Nov', '', [item('10:00', 'Max Battles', 'New: 6★ Gigantamax Lapras', [], ['131'])]),
        ('Mon 2 Nov', '', [item('18:00–<br>19:00', 'Max Monday', 'Gigantamax Lapras', ['More Power Spots'], ['131'])]),
        ('Tue 3 Nov', '', [item('10:00', 'Raids', 'New 5★: Groudon', [], ['383']),
                           item('18:00–<br>19:00', 'Spotlight Hour', 'Snorunt', ['2× catch Stardust'], ['361'])]),
        ('Wed 4 Nov', '', [item('18:00–<br>19:00', 'Raid Hour', 'Groudon', [], ['383'])]),
        ('Sat 7 Nov', '', [item('14:00–<br>17:00', 'Community Day', 'Gible', ['Earth Power for Garchomp', '3× catch Stardust'], ['443'], True)]),
    ]

def agenda(phone):
    return ''.join(f'<div class="day"><b>{d}</b>{rel}</div><div class="group" style="margin:0 12px 6px">' + ''.join(items) + '</div>' for d, rel, items in days(phone))

chips = '<div class="filterchips"><span class="fchip on">All</span><span class="fchip">Raids</span><span class="fchip">Max Battles</span><span class="fchip">Events</span></div>'
fresh = '<p class="fresh" style="margin:14px 16px 4px">From LeekDuck, fetched by the AI researcher Sun 25 Oct 22:00 · Times in Lisbon time</p>'
head = '<!doctype html><html lang="en"><head><meta charset="utf-8"><title>A2 Calendar</title><link rel="stylesheet" href="../base.css"><link rel="stylesheet" href="_a2.css"></head><body>'
phone = head + f'''<div class="stage"><div class="device"><div class="screen">
<div data-statusbar></div><div data-dethead="Today|Calendar"></div>
<div style="background:var(--surface);padding:10px 12px 12px;border-bottom:1px solid var(--line)">{chips}</div>
<div class="scroll"><div style="padding:4px 0 18px">{agenda(True)}{fresh}</div></div>
<div data-tabbar="today"></div></div></div></div><script src="../chrome.js"></script></body></html>'''
desk = head + f'''<div class="desktop"><div data-sidebar="calendar"></div><div class="main"><div data-topbar></div>
<div class="content" style="max-width:960px"><a class="backlink">‹ Today</a>
<div style="display:flex;align-items:flex-end;gap:16px;margin:6px 0 18px"><h1 class="bigtitle" style="flex:none">Calendar</h1><span style="flex:1"></span>{chips}</div>
<div style="margin:0 -12px">{agenda(False)}</div>{fresh}</div></div></div><script src="../chrome.js"></script></body></html>'''
(HERE / 'A2-phone.html').write_text(phone)
(HERE / 'A2-desktop.html').write_text(desk)
