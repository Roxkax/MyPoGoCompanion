# Pokédex list (B1, E03-F01)

How the Pokédex list gets its data, how a line chooses what to show, and what the search understands. Decisions in
CLAUDE.md's decision log (2026-11-08).

## Data: the whole Pokédex in one answer

`GET /pokedex` (public, no sign-in) sends every species with its versions and the moves they learn, only the fields
the list and its search use. The app searches, filters and sorts it itself, so results change as you type.

- **Built once per published daily import** and kept in memory by each API instance, already compressed: about
  **66 KB with Brotli, 91 KB with gzip** on the real data (745 KB of JSON). The first request after an import or a
  cold start builds it (about a second); later ones are answered from memory.
- **Cache**: `Cache-Control: public, max-age=3600` and an `ETag` (a hash of the JSON). Browsers keep the answer an
  hour, then ask again with `If-None-Match` and get **304 Not Modified** (no body) until the data really changed. An
  import that changed nothing keeps the same ETag. Held and failed imports publish nothing, so they change nothing.
- Code: `api/MyPoGoCompanion.Api/Pokedex/` (`PokedexEndpoints`, `PokedexNames`, `PokedexResponse`); web:
  `web/src/pokedex/` (data, search, filters, view) and `web/src/screens/pokedex/` (the screen).

What each form carries: id, kind, main form or not, the name to show (`Mega Charizard Y`, `Hisuian Arcanine`,
`Giratina (Origin)`; Portuguese from the game text), its marker's short text in both languages (`Mega Y` / `Mega Y`,
`Hisuian` / `Hisui`, `Origin` / `Forma Origem`), types, base stats, max CP at level 50, released, Shadow, Dynamax,
G-Max, picture names, **looks only** (same stats and types as the main form) and its fast and charged move ids.

## A line

One line per species. A line appears when **one of its versions** matches every filter and the search.

- **Versions in play**: with **Released** on (the default) only released versions count; a species with nothing
  released is hidden. Off, everything shows and an unreleased version says "Not in GO yet".
- **The version shown**: with a stat filter or a stat in the search, the matching version with the highest value of
  the first stat asked about (Attack, Defense, Stamina, Max CP, in that order); otherwise the main form when it
  matches; otherwise the first match (Megas and Primals, regional forms, other forms). It says "Showing …".
- **Markers** (only for matching versions): a marker each for Mega / Mega X / Mega Y / Primal and each regional form,
  and for other forms with their own stats or types (Origin, Therian, Armored…), all of which switch the line (hover
  on a computer, tap on a phone; tap again to go back). A species with **more than 4** other forms (Arceus, Silvally,
  Rotom…) gets one "N forms" marker that opens its page. Shadow, Dynamax and G-Max have the same stats: their marker
  shows a short note. Forms that only look different (costumes, Burmy's cloaks, Genesect's drives) get no marker.
- **Sorting** uses the numbers shown; switching a line with a marker doesn't re-sort. Pokédex order first; a column
  sorts numbers and names from the start and stats from the best, and a second tap reverses.

## Filters in the address

Search, filters and sort live in the address, so Back, reloading and sharing a link keep the view:

| Parameter | Meaning | Example |
|---|---|---|
| `q` | what was typed in the search box | `q=fire,water` |
| `type` | one or two types, both needed | `type=fire,flying` |
| `gen` | generations, any of them | `gen=1,4` |
| `atk`, `def`, `sta`, `cp` | stat ranges: from, up to, between, exact | `atk=230-`, `cp=-1500`, `def=200-300` |
| `has` | versions needed: `mega`, `shadow`, `dynamax`, `gmax`, `regional` | `has=mega,shadow` |
| `cat` | categories, any of them: `legendary`, `mythical`, `ultrabeast` | `cat=legendary` |
| `released=all` | show unreleased versions too | |
| `sort`, `order` | `num`, `name`, `atk`, `def`, `sta`, `cp`; `asc` / `desc` | `sort=atk&order=asc` |

Values the address gets wrong are ignored.

## The search

Everything is searchable, with the game's own rules: **a comma means either/or**, **`&` means both**, **`!` means
not**. A space also means both, but a phrase is tried as a whole name first (`mega charizard y`, `@fire punch`).
English and Portuguese, accents and capitals don't matter. Chips under the box show how each word was read (crossed
out when it matches nothing); the **?** in the box opens "How search works" with examples to tap.

| You type | Finds |
|---|---|
| `garchomp`, `garch`, `chomp`, `m2` | names and nicknames of species and forms (from the start of a word, or inside a name from 3 letters) |
| `445`, `1-151`, `387-`, `-151` | Pokédex numbers and ranges |
| `fire`, `fogo` | that type |
| `kanto` … `paldea`, `gen4`, `hisui` | that generation; `hisui` is #899–905, as in the game |
| `alolan`, `galarian`, `hisuian`, `paldean` (and `alolano`…) | has that regional form |
| `mega`, `primal`, `shadow`, `dynamax`, `gmax` (and `sombroso`, `primitivo`…) | has that version |
| `legendary`, `mythical`, `ultrabeast` (and `lendário`, `mítico`, `ultracriatura`) | that category |
| `released`, `unreleased` | released in GO or not |
| `@earthquake`, `@firepunch`, `@terremoto` | can learn that move (exact name first, then names starting with it, then containing it) |
| `@fire`, `@1fire`, `@2fire` (`@3` too) | has a Fire move / fast move / charged move |
| `+pikachu` | the whole family |
| `atk250-`, `def200-300`, `sta-150`, `hp207`, `cp4000-`, `pc4000-` | base stats and max CP (L50) |

"Not" is about the **species** for names, numbers, regions, categories, families and versions (`fire !mega`: Fire
Pokémon without a Mega) and about the **version** for types, stats, moves and released (`dragon !flying` finds
Charizard through Mega Charizard X).
