"""Independent simulator of Pokémon GO's storage search for the keywords we emit.

Supported terms: Pokédex numbers and ranges (`147`, `147-149`), appraisal bars
(`3attack`, `3-4defense`, `0-1hp`), `cp-N`, star ratings (`3*`), `maxmove1-`, `mega0-`,
optionally negated with `!`. `&`/`|` = AND, `,`/`;`/`:` = OR, OR binds tighter.
"""
import re

import numpy as np

STATS = {"attack": 0, "defense": 1, "hp": 2}
_term = re.compile(r"^(!?)(cp)?(?:(\d*)-(\d*)|(\d+))(attack|defense|hp|\*)?$")


def parse(s):
    return [[t.strip() for t in re.split(r"[,;:]", clause)] for clause in re.split(r"[&|]", s)]


def evaluate(s, dex, bars, cp=None, flags=None):
    """Return a boolean mask over the broadcast of the given arrays.

    dex: int array, bars: (..., 3) int array of appraisal bars, cp: int array or None,
    flags: dict of keyword -> bool array (for `maxmove1-`, `mega0-`).
    """
    flags = flags or {}
    result = None
    for clause in parse(s):
        cm = None
        for term in clause:
            neg = term.startswith("!")
            body = term[1:] if neg else term
            if body in flags:
                m = flags[body]
            else:
                mt = _term.match(body)
                if not mt:
                    raise ValueError(f"Unsupported search term {term!r} in {s[:60]!r}")
                _, cpkw, lo, hi, single, kw = mt.groups()
                kw = kw or cpkw
                if single is not None:
                    lo = hi = single
                lo = int(lo) if lo else None
                hi = int(hi) if hi else None
                if kw is None:
                    v = dex
                elif kw == "cp":
                    if cp is None:
                        raise ValueError("cp term needs a cp array")
                    v = cp
                elif kw == "*":
                    raise ValueError("star ratings are not simulated here")
                else:
                    v = bars[..., STATS[kw]]
                m = np.ones(np.broadcast(v).shape, bool)
                if lo is not None:
                    m = m & (v >= lo)
                if hi is not None:
                    m = m & (v <= hi)
            if neg:
                m = ~m
            cm = m if cm is None else (cm | m)
        result = cm if result is None else (result & cm)
    return result
