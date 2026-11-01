# Pokémon GO search filters — project context

This project generates copy-paste **Pokémon GO storage search strings** that help a player decide which Pokémon to keep, and publishes them in a single self-contained web page (`dist/pvp-keeper-filters.html`). The owner wants to turn it into a product.

**Location:** the pipeline lives in `legacy/keeper-filters/` (moved there in Sprint 0 so the new app could use `web/`). Every path in this file (`build.py`, `scripts/`, `web/`, `dist/`, `data/`, `build/`) is relative to that folder. It is removed once the C# port matches its golden files.

It started as a Claude chat session (Oct 2026). This file carries over everything decided there, so read it before changing anything.

## What the page offers (5 tabs)

| Tab | What the string finds | Built by |
|---|---|---|
| Great League (CP ≤ 1500) | Every Pokémon whose appraisal bars sit in its high-ranking PvP zone, plus pre-evolutions judged by their evolution. 2 strings, split by Pokédex range. | `pvp_*` scripts |
| Ultra League (CP ≤ 2500) | Same for UL. 3 strings. | `pvp_*` scripts |
| Raids | Top raid attackers per type, their Megas and all pre-evolutions, with Attack bar 11–15 (or 15 only). One string, about 750 chars. A per-type "numbers" table shows how the ranking was calculated, with a "Your best Mega" selector. | `raid_*` scripts |
| Max Battles | `maxmove1-&3-4attack` (or `&4attack`): every Max-Battle-eligible Pokémon with high Attack. | static |
| Megas | `mega0-,<Pokédex numbers of Megas not in GO yet>` (optionally + pre-evolutions). | `mega_page_data.py` |

Each tab also tells the user to tag the results (GL / UL / RAID / MAX / MEGA) so `#GL` etc. shows them together later.

## How to build

```
cd legacy/keeper-filters
pip install -r requirements.txt
python build.py              # fetch/update data (git), rebuild everything, write dist/pvp-keeper-filters.html
python build.py --no-fetch   # rebuild from data already in ./data
python build.py --only pvp|raid|mega|page
```
The full build takes about 20 s. Every stage verifies its strings and exits non-zero on a mismatch, so a passing build means the strings are correct.

**Golden files** (E00-F10): the C# port (E02-F03) must produce exactly what this pipeline produces. `python scripts/golden.py freeze` (after `python build.py`) saves the source data, trimmed to what the pipeline reads, and the `build/` JSON outputs plus search-simulator cases to `tests/golden/keeper-filters/`, with the source commits in `manifest.json`. `python scripts/golden.py check` rebuilds from those frozen inputs, without network, and compares; the `build-and-unit` check runs it on every pull request, so the pipeline stays runnable until the C# port matches. `KEEPER_DATA` and `KEEPER_BUILD` point the pipeline at other data and build folders; `--only` takes several stages (`--only pvp,raid,mega`). Details: `tests/golden/keeper-filters/README.md`.

Pipeline (all in `scripts/`, the intermediate output goes to `build/`):
1. `pvp_compute.py` → `pvp_boxes.json`: the best appraisal box per Pokédex number per league.
2. `pvp_strings.py` → `pvp_strings.json`: joined, split and verified strings. Also importable, since `build()` is reused.
3. `pvp_page_data.py` → `data_pvp.json`: page rows, each with its own single-family string.
4. `raid_rank.py` → `raiders.json`: the raid ranking.
5. `raid_page_data.py` → `data_raid.json`: raid and Max strings, per-type lists, the numbers table.
6. `mega_page_data.py` → `data_mega.json`.
7. `page_assemble.py`: `web/page_head.html` + `web/page_body.html`, with the JSON inlined into `<script type="application/json">` tags and the data dates filled in.

The page's UI is plain HTML/CSS/JS in `web/page_body.html`, with no framework. `web/page_head.html` holds the `<title>`, the Google Fonts link and the base CSS tokens (light and dark themes).

## Data sources

- **PvPoke** (`github.com/pvpoke/pvpoke`, MIT licence): `src/data/gamemaster.json` (species, forms, shadows, megas, movepools incl. elite moves, `released` flags, families), `src/data/rankings/all/overall/rankings-1500.json` and `-2500.json`, and `src/js/pokemon/Pokemon.js` (the CP multiplier table is parsed from it).
- **Game master** (`github.com/alexelgt/game_masters`, `GAME_MASTER.json`, about 19 MB, a datamined Niantic file): **raid (PvE) move stats** come from the `V0xxx_MOVE_*` templates (`moveSettings`: power, durationMs, damageWindowStartMs, energyDelta). The `COMBAT_*` templates are PvP stats; don't mix them up. Fast moves carry a `_FAST` suffix there. Aliases: FUTURE_SIGHT→FUTURESIGHT, PYRO_BALL→PYROBALL, TECHNO_BLAST_DOUSE→TECHNO_BLAST_WATER. Hidden Power and the Aegislash charge moves have no PvE entry and are skipped.
- Both repos are fetched as shallow, sparse git clones into `data/` (gitignored).

## Pokémon GO search syntax: facts the encoding depends on

Source: Pokémon GO Hub search-strings page and Leidwesen/SearchPhrases (github), both checked Oct 2026.
- `&` (or `|`) = AND, `,` (or `;` `:`) = OR, `!` = NOT. **No brackets.** Commas are always evaluated inside the ampersands, so a string is an AND of OR-clauses (conjunctive normal form).
- IV bars: `0attack` = 0, `1` = 1–5, `2` = 6–10, `3` = 11–14, `4` = 15. Ranges work: `0-1attack`, `3-4hp`. The same applies to `defense` and `hp`.
- Pokédex numbers and ranges: `147-149`. `cp-1500` = CP ≤ 1500.
- Star ratings: `0*` = total IVs 0–22, `1*` = 23–29, `2*` = 30–36, `3*` = 37–44, `4*` = 45.
- `maxmove1-` = everything that can enter Max Battles (Dynamax, Gigantamax, Crowned Zacian and Zamazenta, Eternatus).
- `mega{N}` = Mega level. `mega0-` = every Pokémon that can Mega Evolve in GO; it only returns mega-capable Pokémon. Use `mega0-` rather than the bare `mega` shortcut.
- Keywords are **English**. Other game languages use other keywords, which is a localisation to-do.
- Length: iOS text replacement caps a phrase at 2000 chars, so every string is kept at 1950 or less. The search bar itself takes much more (about 25k reported).
- `+name` (family search) only works at the start or after a comma, and names depend on language. That's why everything uses Pokédex numbers.
- Pokédex numbers can't tell forms apart (Alolan or Galarian forms, Darmanitan modes, Necrozma fusions…). A known, accepted limitation.

## PvP methodology (`pvp_compute.py`, `pvp_strings.py`)

- **All 1,025 Pokédex numbers** are included, on purpose: the owner wanted filters that survive meta changes, not just today's top 100. Megas, shadows (same Pokédex number) and battle-only forms (Aegislash Blade, Busted Mimikyu) are excluded as separate entries. Unreleased Pokémon are included, so new releases are covered.
- For each species and league: all 4,096 IV spreads, each at the highest level ≤ 50 under the CP cap, scored by stat product (Atk × Def × floor(HP)). A spread is a "keeper" if it scores at least 98% of the rank-1 spread.
- The game can only search by appraisal **bars**, so each Pokédex number gets one "box" (a contiguous bar range per stat) that maximises F1 against its keeper set. **The rank-1 spread must always be inside the box.**
- **Families** (policy `meta`): a base or middle stage uses the union of its reachable final evolutions' keepers. A middle stage adds its own keepers only if it ranks in PvPoke's top 150 for that league (e.g. Charjabug, Dusclops, Gligar, Doublade). The policies `all`, `final` and `cap` exist for comparison; `all` makes about 300 filters accept any Attack, which is why it was rejected. Forms that share a Pokédex number are merged, with the union of their keepers.
- **Encoding**: the string is `<allowed dex ranges>&<clause per stat rule>…&cp-1500`, where each clause is `<dex ranges allowed to skip this rule>,<rule>`. Two clause styles are tried per stat (one per distinct band interval, or one per upper and lower bound) and the shorter wins. The strings are split into Pokédex-range chunks of ≤ 1950 chars, then rebalanced.
- **Verification**: the game's logic is simulated for every Pokédex number 1–1100 × every appraisal-band combination (5³). Each Pokémon must match exactly its own box, in exactly one string, and nothing above the CP cap. The build fails on any mismatch.

## Raid methodology (`raid_rank.py`)

- Every released PvPoke entry, including shadows (Atk ×1.2, Def ×5/6) and Megas/Primals, is scored for each of the 18 types. The moveset is the best one where **both** moves are of that type, Elite TM moves included. Return and Frustration are excluded.
- GamePress-style comprehensive DPS: level 40, 15/15/15, target Defense 180, neutral effectiveness, STAB 1.2. A Mega gets ×1.3 on its own types and ×1.1 otherwise. Boss DPS against the attacker y = 900 / Def. For one-bar charged moves, CE += 0.5·FE + 0.5·y·damageWindowStart.
  - DPS0 = (FDPS·CEPS + CDPS·FEPS)/(CEPS+FEPS); DPS = DPS0 + (CDPS−FDPS)/(CEPS+FEPS)·(0.5 − x/HP)·y with x = 0.5·CE + 0.5·FE; if CDPS < FDPS, DPS = FDPS.
  - TDO = DPS·HP/y. **Score (ER) = (DPS³·TDO)^¼.**
- **Selection**: per type, the top 10 **distinct Pokédex numbers** among regular and shadow forms, plus **every Mega/Primal that scores above the 10th**, plus all pre-evolutions. That gives 330 Pokédex numbers, 162 of them pre-evolutions.
- **Audited Oct 2026**: the move values match the game master (e.g. Psycho Boost really is 130 power for −33 energy in raids, and Dynamax Cannon really is 215 power in 1.5 s). PvPoke's "released" Mega list matches GO's released Megas as of 14 Oct 2026 (The Click). Shadows *can* learn Elite TM moves once Frustration is removed (Bulbapedia).
- Cross-checked against Mein-MMO's "best attackers by type 2026" (based on GamePress, Pokebattler and GO Hub); Ground, Dragon and Ice agree.
- Why some picks look surprising: Deoxys (Attack) tops Psychic thanks to the cheap Psycho Boost, even though it lasts only 6.4 s. Eternatus tops Dragon. Crowned Zamazenta edges out Zacian because of bulk. Xerneas and Shadow Donphan qualify for Normal and Fairy because those types are weak. The page's numbers table explains all of this.

## Max Battles and Megas

- Max: `maxmove1-` + an Attack bar. Max Move damage scales with Attack. The keyword picks up new releases automatically.
- Megas: `mega0-` covers every released Mega (59 Pokémon in Oct 2026) and any future GO release. The **hand-maintained list** `NOT_YET` / `SOON` in `scripts/mega_page_data.py` adds Megas from Legends: Z-A and the Mega Dimension DLC that GO hasn't released yet (30 Pokémon in Oct 2026; Chandelure arrives 31 Oct 2026). Kyogre and Groudon (382–383) are also listed by number in case `mega0-` skips Primal Reversion. **Update that list when GO releases or the main games announce new Megas.** The game master's `tempEvoOverrides` show which Megas the GO files already contain (Chandelure appeared before its release).

## Decisions and preferences from the owner (keep these)

1. PvP: compute the best IVs for each Pokémon, join everything into as few strings as possible, split if too long, and join families when only the last evolution matters.
2. Cover **all** Pokémon, because rankings change over time.
3. Raids: top raiders by type, with a good Attack stat. Max Battles: Dynamax/Gigantamax with good Attack.
4. The owner asked to see the values behind the raid ranking → the numbers table, formula and worked example (Shadow Mewtwo) were added.
5. Only one Mega fits in a raid team. The owner **only wanted confirmation** of that, not a change. The filter stays as it was (every Mega above the 10th). The numbers table has a "Your best Mega" selector per type (stored in localStorage) that pins that Mega as the "Mega slot", hides the other Megas and ranks the rest 1…N.
6. Megas tab: all Megas in GO plus the future ones.
7. **When the owner asks a question, answer it; don't change the deliverable unless asked.**

## Known limitations and ideas for the product

- English search keywords only. Localising (pt, es, de, fr…) needs each language's keyword list.
- Pokédex-number granularity (regional forms). Level 51 (best buddy) is ignored. Raid level is fixed at 40.
- The raid selection depends on current rankings; refresh it when big new raiders arrive. The PvP and Max strings are designed to stay valid.
- Product ideas: a scheduled rebuild (e.g. GitHub Actions, daily) and static hosting; user options (IV keeper %, level cap 50/51, Master League, Little Cup or other cups, "trash" strings as the inverse); choosing which Megas you own; language picker; installable PWA.
- Legal: the data is datamined or community-made. PvPoke is MIT; the game master repo states that the content belongs to The Pokémon Company/Niantic and is for educational use. Avoid Pokémon/Niantic logos and trademarks in branding, and check this before any commercial launch.

## Rebuild notes (26 Oct 2026)

The scripts were rebuilt from this file in a new repo and reproduce the original numbers: GL 2 strings, UL 3 strings, raid string 750 chars with 330 Pokédex numbers (168 attackers + 162 pre-evolutions), 59 released Megas + 30 not yet in GO; `--policy all` gives ~300 boxes accepting any Attack.
- `scripts/searchsim.py` is an independent simulator of the search syntax, used by every verification step.
- `scripts/common.py`: paths, CPM table, `BATTLE_ONLY` (Aegislash Blade, Busted Mimikyu, Cramorant Gulping/Gorging, Hangry Morpeko).
- PvPoke Mega entries often lack `family.parent`, so `raid_rank.py` finds a Mega's pre-evolutions through its base form.
- Raid table: only the first form of each top-10 Pokédex number is labelled "Top 10"; other forms of the same number are labelled "Same Pokédex #".
