# Design conventions

Every screen follows these rules. Screens are HTML/CSS in `design/source/screens/`, rendered to PNG in `design/screens/` by `shot.js` (Playwright). Run `./fetch_sprites.sh` first (sprites are not committed).

## Visual style (decided 2026-10-27)

- **Look:** clean and data-focused; **comfortable** density.
- **Font:** Nunito (rounded, friendly) for everything; JetBrains Mono only for search strings. Numbers in tables use tabular figures.
- **Accent:** **teal** — buttons, links, the active tab, selected states. Orange is used **only for warnings**; red only for errors and destructive actions.
- **Themes:** light and dark, following the device (switch in Settings). Screens are reviewed in light mode; a dark check happens at the end.
- **Type colours:** on type badges, move chips and charts everywhere. **Pokémon pages (B2) and raid boss pages (C2) are tinted with the Pokémon's main type** (header band and accents).
- **Sprites:** trimmed to their content and scaled to **fill their box equally** (a small Pokémon like Gible looks as big as Kyogre).
- **Sizes:** each screen is shown at **phone** (393×852, inside an iPhone frame) and **desktop** (1440 wide). Scrolling phone screens also get a `-full` image showing the whole page.

## Layout rules

- **Phone:** status bar · header · content · bottom tab bar (Today, Pokédex, Battle, Filters, Collection).
- **Desktop:** left sidebar (logo, the five tabs, then secondary links) · top bar (search, Ask, profile) · content up to 1,120 px wide.
- **Tab screens** (A1, B1, C1, D1, E1): large title, the search bar, the **Ask** button (AI chat) and the profile picture in the header.
- **Detail screens** (everything else): no large title; a back button labelled with the screen it returns to (e.g. "‹ Pokédex"), the screen's name, and Ask.
- Touch targets at least 44 px. Every fact that comes from fetched or computed data can show its source on tap.

## Shared sample dataset

All screens show the same moment and the same trainer, so they tell one consistent story. It is **sample data**, not real announcements.

- **Now:** Tuesday 27 October 2026, 18:30 (Lisbon).
- **Trainer:** Ash_PT · level 48 · Team Mystic · signed in · language English.
- **Data freshness:** game data and rankings 27 Oct 06:00; bosses & events fetched by the AI researcher Sun 25 Oct 22:00 (source: LeekDuck).
- **Raids now** (until Tue 3 Nov 10:00):
  - 5★ **Kyogre** (Water) — 100% CP 2,351 / 2,939 boosted (Rainy) — shiny available.
  - Mega **Mega Gengar** (Ghost/Poison) — shiny available.
  - Shadow 3★ **Shadow Absol** (Dark).
  - 3★ **Charizard** (Fire/Flying), **Lapras** (Water/Ice).
  - 1★ **Gible** (Dragon/Ground), **Charmander** (Fire).
- **Max Battles now** (until Sun 1 Nov 10:00): 6★ **Gigantamax Venusaur**; 1★ **Dynamax Charmander**.
- **Events:**
  - Live: **Halloween event** — until Sat 31 Oct 20:00 — Ghost-types spawn more often; 2× catch candy.
  - Next: **Mega Chandelure debut** — Sat 31 Oct 10:00 — Mega Chandelure in Mega raids.
  - Next: **Community Day: Gible** — Sat 7 Nov 14:00–17:00 — evolve Gabite for Garchomp with Earth Power; 3× catch Stardust.
- **Calendar entries** (A2; all sample):
  - Wed 28 Oct 18:00–19:00 — Raid Hour: Kyogre.
  - Sat 31 Oct 10:00 — Mega Chandelure debut (Mega raids); 20:00 — Halloween event ends.
  - Sun 1 Nov 10:00 — new Max Battles: 6★ Gigantamax Lapras.
  - Mon 2 Nov 18:00–19:00 — Max Monday: Gigantamax Lapras.
  - Tue 3 Nov 10:00 — new raids: 5★ Groudon (Ground); 18:00–19:00 — Spotlight Hour: Snorunt (2× catch Stardust).
  - Wed 4 Nov 18:00–19:00 — Raid Hour: Groudon.
  - Sat 7 Nov 14:00–17:00 — Community Day: Gible.
- **Community Day: Gible details** (A3; sample): Sat 7 Nov 14:00–17:00; shiny Gible more often; evolve Gabite 14:00–22:00 → Garchomp learns **Earth Power** (raids 100 power · 3.5 s · −50 energy; PvP 90 power · −50 energy); bonuses 3× catch Stardust, ¼ egg hatch distance, Lure Modules and Incense last 3 hours; Field Research "Catch 15 Gible → Gible encounter"; hundo CPs Gible 954 (L30) / 1,033 (L35 boosted, Windy or Sunny), Garchomp after evolving 3,396 / 3,679.
- **Pokédex (B1):** 1,025 rows, one per species; version markers on each row; stats and max CP of the base form (level 50, 15/15/15). Filters look at every version. Example: Fire + Attack ≥ 230 + released → 24 Pokémon (incl. Charizard via Mega X/Y, Hisuian Arcanine, Hisuian Typhlosion, Mega Houndoom, Primal Groudon, Mega Camerupt).
- **Eggs (A4):** 10 km pool (8): Gible 635 · Deino 606 · Larvitar 594 · Beldum 558 · Goomy 557 · Jangmo-o 553 · Dreepy 438 · Frigibax 712 (100% CP at L20). Other distances listed but not opened in the sample.
- **Field Research (A4):** Win 3 raids → Gible (100% CP 477, L15) · Catch 10 Ghost-type Pokémon → Litwick (431) · Make 3 Great Throws → Snorunt (380) · Power up Pokémon 5 times → Bulbasaur (477).
- **Collection:** 2,148 Pokémon; last import Sun 25 Oct 21:14 (63 new, 40 gone, 12 changed). Includes a Garchomp 2/15/13 L23 (UL rank 232, 97.9%, CP 2,471); the Gible family: Gible 15/15/14 (→ Garchomp ML rank 3, 99.5%), Gible 0/15/14 (→ GL rank 1, CP 1,495), Gible 1/13/15 (→ GL rank 11, 99.4%), Gible 10/4/2 (no league above 95%), Gabite 12/12/13 (→ ML rank 160, 96.6%), a Medicham 1/15/15 L49.5 (GL rank 12), Shadow Zekrom, Zeraora, Mega-ready Sceptile.
- **Kyogre counters** (from today's raid model, same numbers on every screen): Mega Sceptile 57.3 (Mega slot) · Shadow Zekrom 44.6 · Zeraora 41.5 · Shadow Raikou 41.2 · Shadow Electivire 39.8 · Shadow Chesnaught 39.3 · Zarude 39.2 · Regieleki 39.2 · Xurkitree 39.1.
- **Garchomp** (B2 example): Atk 261 · Def 193 · Sta 239 · max CP 3,962 / 4,479 / 4,532 (L40/50/51) · 100% CP 1,698 (L15) / 2,264 (L20) / 2,830 (L25) / 3,396 (L30) / 3,679 (L35) · ML #31 (Shadow #26) · Ground raids #12 (Shadow #5, Mega #2) · GL best IVs 0/15/14.
- **Earth Power** (B4 example): Ground charged move · raids 100 power · 3.5 s · −50 (2 bars) · PvP 90 · −50 · 10% chance foe Defense −1. Learners sorted by raid score with their best fast move (Ground fast when they have one) + Earth Power: Elite TM only Garchomp 34.2 (Shadow 40.8, Mega 58.4) and Flygon 27.1; no legacy learners; 26 learn it normally, led by Landorus 32.5, Heatran 32.1, Volcanion 30.1.
