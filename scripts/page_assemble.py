"""Assemble dist/pvp-keeper-filters.html from web/ templates and the build/ JSON."""
import datetime
import json
import re
import sys

from common import BUILD, DIST, WEB, data_dates, load_json


def inline(obj):
    # safe inside <script type="application/json">
    return json.dumps(obj, ensure_ascii=False, separators=(",", ":")).replace("</", "<\\/")


def main():
    head = (WEB / "page_head.html").read_text(encoding="utf-8")
    body = (WEB / "page_body.html").read_text(encoding="utf-8")
    dates = data_dates()
    subs = {
        "{{DATA_PVP}}": inline(load_json(BUILD / "data_pvp.json")),
        "{{DATA_RAID}}": inline(load_json(BUILD / "data_raid.json")),
        "{{DATA_MEGA}}": inline(load_json(BUILD / "data_mega.json")),
        "{{DATE_PVPOKE}}": dates["pvpoke"],
        "{{DATE_GM}}": dates["gamemaster"],
        "{{BUILT}}": datetime.date.today().isoformat(),
    }
    for k, v in subs.items():
        if k not in body:
            raise SystemExit(f"Placeholder {k} missing from page_body.html")
        body = body.replace(k, v)
    html = head + body
    DIST.mkdir(exist_ok=True)
    out = DIST / "pvp-keeper-filters.html"
    out.write_text(html, encoding="utf-8")
    # Artifact copy: the claude.ai artifact host supplies doctype/html/head/body itself
    art = html
    for tag in ("<!doctype html>", '<html lang="en">', "<head>", "</head>", "<body>", "</body>", "</html>"):
        art = art.replace(tag, "", 1)
    art = re.sub(r'<meta (charset|name="viewport")[^>]*>\n', "", art)
    (BUILD / "artifact.html").write_text(art.strip() + "\n", encoding="utf-8")
    print(f"page_assemble: wrote {out.relative_to(DIST.parent)} ({len(html.encode()) / 1024:.0f} KB) and build/artifact.html")
    return 0


if __name__ == "__main__":
    sys.exit(main())
