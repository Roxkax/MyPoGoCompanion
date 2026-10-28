"""Join the per-Pokédex boxes into as few search strings as possible -> build/pvp_strings.json.

A string is `<allowed dex ranges>&<clause per stat rule>...&cp-<cap>`, where each clause is
`<dex ranges allowed to skip this rule>,<rule>`. Because the game has no brackets and always
evaluates commas inside ampersands, this AND-of-ORs form is the only one available.
Pokédex numbers outside the string's allowed set are "don't care" for every clause, as are
Pokémon whose own box already implies a rule; they are used to merge ranges.
"""
import bisect
import sys

import numpy as np

from common import BUILD, LEAGUES, MAX_DEX, MAX_LEN, VERIFY_DEX, fmt_ranges, load_json, ranges, save_json
from searchsim import evaluate

STAT_NAMES = ("attack", "defense", "hp")


def bar_rule(lo, hi, stat):
    return f"{lo}{STAT_NAMES[stat]}" if lo == hi else f"{lo}-{hi}{STAT_NAMES[stat]}"


def encode(must, mustnot):
    """Shortest comma list of ranges covering `must` and nothing in `mustnot`."""
    must = sorted(set(must))
    if not must:
        return ""
    bad = sorted(set(mustnot))
    runs = [[must[0], must[0]]]
    for n in must[1:]:
        a = runs[-1][1]
        # merge if no forbidden number lies strictly between a and n
        i = bisect.bisect_right(bad, a)
        if i < len(bad) and bad[i] < n:
            runs.append([n, n])
        else:
            runs[-1][1] = n
    return fmt_ranges(runs)


def _clause(skip, rule):
    return f"{skip},{rule}" if skip else rule


def stat_clauses(boxes, dexes, stat):
    ivals = {d: tuple(boxes[d][stat]) for d in dexes}
    # style A: one clause per distinct interval
    a = []
    for iv in sorted(set(ivals.values()) - {(0, 4)}):
        lo, hi = iv
        must_skip = [d for d, j in ivals.items() if not (lo <= j[0] and j[1] <= hi)]
        obey = [d for d, j in ivals.items() if j == iv]
        a.append(_clause(encode(must_skip, obey), bar_rule(lo, hi, stat)))
    # style B: one clause per lower bound and per upper bound
    b = []
    for lo in sorted({j[0] for j in ivals.values()} - {0}):
        must_skip = [d for d, j in ivals.items() if j[0] < lo]
        obey = [d for d, j in ivals.items() if j[0] == lo]
        b.append(_clause(encode(must_skip, obey), bar_rule(lo, 4, stat)))
    for hi in sorted({j[1] for j in ivals.values()} - {4}):
        must_skip = [d for d, j in ivals.items() if j[1] > hi]
        obey = [d for d, j in ivals.items() if j[1] == hi]
        b.append(_clause(encode(must_skip, obey), bar_rule(0, hi, stat)))
    la, lb = sum(len(x) + 1 for x in a), sum(len(x) + 1 for x in b)
    return a if la <= lb else b


def build(boxes, dexes, cap):
    """Search string matching exactly `boxes[d]` for every d in `dexes`, under the CP cap.

    boxes: {dex(int): [[alo, ahi], [dlo, dhi], [hlo, hhi]]}. Importable; reused by the page data.
    """
    dexes = sorted(set(dexes))
    parts = [fmt_ranges(ranges(dexes))]
    for stat in range(3):
        parts += stat_clauses(boxes, dexes, stat)
    parts.append(f"cp-{cap}")
    return "&".join(parts)


def chunk(boxes, cap, limit=MAX_LEN, start=1, end=MAX_DEX):
    out, a = [], start
    while a <= end:
        # length grows (almost) monotonically with the range: binary-search the last end that fits
        lo, hi = a, end
        while lo < hi:
            mid = (lo + hi + 1) // 2
            if len(build(boxes, range(a, mid + 1), cap)) <= limit:
                lo = mid
            else:
                hi = mid - 1
        b = lo
        s = build(boxes, range(a, b + 1), cap)
        if len(s) > limit:
            raise SystemExit(f"A single Pokédex number (#{a}) does not fit in {limit} chars")
        out.append((a, b, s))
        a = b + 1
    return out


def split_balanced(boxes, cap):
    greedy = chunk(boxes, cap)
    k = len(greedy)
    lo, hi = max(len(s) for *_, s in greedy) // 2, MAX_LEN
    best = greedy
    while lo < hi:  # smallest limit that still needs only k strings
        mid = (lo + hi) // 2
        c = chunk(boxes, cap, mid)
        if len(c) <= k:
            best, hi = c, mid
        else:
            lo = mid + 1
    return best


def verify(boxes, strings, cap):
    """Simulate the game for dex 1..VERIFY_DEX x all 125 bar combos x CP at/above the cap."""
    dex = np.arange(1, VERIFY_DEX + 1)[:, None, None]
    bars = np.array([[a, d, h] for a in range(5) for d in range(5) for h in range(5)])[None, :, None, :]
    cp = np.array([cap, cap + 1])[None, None, :]
    expect = np.zeros((VERIFY_DEX, 125, 2), bool)
    for d, box in boxes.items():
        inbox = np.all([(bars[0, :, 0, s] >= box[s][0]) & (bars[0, :, 0, s] <= box[s][1]) for s in range(3)], axis=0)
        expect[d - 1, :, 0] = inbox
    hits = np.zeros(expect.shape, int)
    for s in strings:
        if len(s) > MAX_LEN:
            raise SystemExit(f"String longer than {MAX_LEN}: {len(s)}")
        hits += np.broadcast_to(evaluate(s, dex, bars, cp), expect.shape)
    bad = (hits > 1) | ((hits == 1) != expect)
    if bad.any():
        d, c, k = np.argwhere(bad)[0]
        raise SystemExit(f"Verification failed (cap {cap}): dex {d + 1}, bars {bars[0, c, 0].tolist()}, "
                         f"cp {'over' if k else 'under'} cap: matched {hits[d, c, k]}x, expected {int(expect[d, c, k])}")
    return int(bad.size)


def load_boxes(league):
    raw = load_json(BUILD / "pvp_boxes.json")["leagues"][league]["boxes"]
    return {int(d): v["box"] for d, v in raw.items()}


def main():
    out = {}
    for league, cap in LEAGUES.items():
        boxes = load_boxes(league)
        parts = split_balanced(boxes, cap)
        strings = [s for *_, s in parts]
        checked = verify(boxes, strings, cap)
        out[league] = {"cap": cap, "strings": [{"from": a, "to": b, "text": s, "length": len(s)} for a, b, s in parts]}
        print(f"pvp_strings {league}: {len(parts)} strings, lengths {[len(s) for s in strings]}, "
              f"ranges {[(a, b) for a, b, _ in parts]}; verified {checked:,} cases")
    save_json(out, BUILD / "pvp_strings.json", indent=1)
    return 0


if __name__ == "__main__":
    sys.exit(main())
