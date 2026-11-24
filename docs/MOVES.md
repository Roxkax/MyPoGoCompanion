# Moves (E03-F03 / E03-F04)

The public `/pokedex/moves` screen implements the approved B3 design. Open **Pokédex → Moves** on a phone, or **Moves** in the desktop sidebar.

- Switch between Fast and Charged moves, search English or Portuguese names, filter by type, and sort the displayed numbers.
- On a phone, switch between Raids and PvP. Desktop shows both groups together.
- Raid duration is displayed in seconds. Energy is signed: fast moves gain it and charged moves spend it. Fast PvP moves show turns; charged moves show their stat changes and chance.
- Missing Portuguese names fall back to English. Missing raid data is shown as unavailable, rather than inventing a value.
- The address retains search, filters, sorting and the phone's numbers view. The game-data freshness warning follows the same rules as the Pokédex.

## API and data

`GET /moves` returns `MovesResponse`, containing every imported move as a `PageMove`, the same move contract used by the Pokémon page. It includes English and Portuguese names, type, kind, raid power/duration/energy, PvP power/energy/turns, and the chance and stages for self/opponent Attack/Defense changes.

The endpoint uses the existing published-import marker and compressed response cache, with ETag/304 and one-hour browser caching. Filtering and sorting run in the browser. It needs no new migration, import source or scheduled job.

The generated TypeScript client comes from `api/openapi.json`; follow [API.md](API.md) when changing the contract. Move details use the same published move numbers.

## Validation

Backend unit and PostgreSQL integration tests cover the move response, effects, cache invalidation, compression and conditional requests. Web tests cover filtering, sorting, URL state and screen behavior. `tests/ui/specs/moves.spec.ts` exercises the screen on phone and desktop in light and dark, including the real endpoint and deterministic data for ordering and effects.

## Move page

Open any move from the list or a Pokémon’s movepool to see screen B4 at `/pokedex/moves/:move`.

- Raid and PvP numbers appear in separate cards, with signed energy, charged-move energy bars, and all imported stat effects. Missing raid numbers or PvP turn counts remain unavailable.
- Learners are grouped into **Elite TM only**, **Legacy**, and **Can learn it**, each ordered by descending base Attack. Each row opens that Pokémon’s specific form.
- Distinct forms remain separate. A Mega or Primal shares its base’s row only when both learn the move with the same availability; the base then shows **Mega too** or **Primal too**. The list includes all stored learners, including unreleased forms, as the approved B4 design does.
- Phones initially show eight learners per group, with **Show all** to expand. Desktop shows the full grid.
- Event notes show the receiving Pokémon, event dates, and the supplied acquisition instructions. An event’s end is not necessarily the exclusive move’s deadline: for example, the seeded Community Day ends at 17:00 while evolution can give the move until 22:00. Dated past notes remain available; the app does not infer acquisition windows from free text.

`GET /moves/{id}` returns `MovePageResponse` with the move, learners and non-removed event notes. IDs are case-insensitive and accept hyphens in place of underscores. Unknown IDs return 404 problem details. Its bounded cache holds up to 300 move answers, invalidates when game or live data is published, and supports ETag/304 plus Brotli/gzip compression. The endpoint uses the existing tables and needs no migration.
