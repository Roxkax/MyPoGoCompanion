# Moves list (E03-F03)

The public `/pokedex/moves` screen implements the approved B3 design. Open **Pokédex → Moves** on a phone, or **Moves** in the desktop sidebar.

- Switch between Fast and Charged moves, search English or Portuguese names, filter by type, and sort the displayed numbers.
- On a phone, switch between Raids and PvP. Desktop shows both groups together.
- Raid duration is displayed in seconds. Energy is signed: fast moves gain it and charged moves spend it. Fast PvP moves show turns; charged moves show their stat changes and chance.
- Missing Portuguese names fall back to English. Missing raid data is shown as unavailable, rather than inventing a value.
- The address retains search, filters, sorting and the phone's numbers view. The game-data freshness warning follows the same rules as the Pokédex.

## API and data

`GET /moves` returns `MovesResponse`, containing every imported move as a `PageMove`, the same move contract used by the Pokémon page. It includes English and Portuguese names, type, kind, raid power/duration/energy, PvP power/energy/turns, and the chance and stages for self/opponent Attack/Defense changes.

The endpoint uses the existing published-import marker and compressed response cache, with ETag/304 and one-hour browser caching. Filtering and sorting run in the browser. It needs no new migration, import source or scheduled job.

The generated TypeScript client comes from `api/openapi.json`; follow [API.md](API.md) when changing the contract. Move detail pages remain the separate E03-F04 feature.

## Validation

Backend unit and PostgreSQL integration tests cover the move response, effects, cache invalidation, compression and conditional requests. Web tests cover filtering, sorting, URL state and screen behavior. `tests/ui/specs/moves.spec.ts` exercises the screen on phone and desktop in light and dark, including the real endpoint and deterministic data for ordering and effects.
