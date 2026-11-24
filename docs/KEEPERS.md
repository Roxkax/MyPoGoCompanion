# Keeper filters

Open **Filters → Keepers** and copy one search string at a time. Paste it into Pokémon GO storage search, select the results and apply the suggested tag. For a multipart league, repeat with the same tag for every part. Search for the tag later, such as `#GL`.

| Filter | Suggested tag | What it finds |
|---|---|---|
| Great League | `GL` | High-ranking appraisal ranges under 1,500 CP |
| Ultra League | `UL` | High-ranking appraisal ranges under 2,500 CP |
| Raid attackers | `Raid` | The top ten distinct Pokédex numbers per type, qualifying Mega and Primal forms, and their pre-evolutions. Choose Attack 11–15 or 15 only. |
| Max Battle attackers | `Max` | Max Battle-eligible Pokémon using `maxmove1-`. Choose Attack 11–15 or 15 only. |
| Megas | `Mega` | Mega-capable Pokémon using `mega0-`, plus explicit Pokédex numbers for Primal Reversions and announced species. Pre-evolutions are optional. |

The interface is available in English and Portuguese (Portugal). The strings use English in-game search keywords. The game matches Pokédex numbers shared by different forms, so check matches before acting on them.

## Data and verification

The daily import calculates the strings from PvPoke and game-master data and verifies them with the game-search simulator. A released Mega or Primal widens the PvP appraisal ranges for its species and pre-evolutions. The original encoding is checked against the frozen Python outputs. The expanded frozen Pokédex uses two parts for Great League and three for Ultra League. The part count can change with the data, and each part is capped at 1,950 characters.

The import publishes all parts to `keeper_filters` in the game-data transaction. A change in the number of generated parts replaces the previous parts automatically. The public `GET /keeper-filters` returns the current parts ordered by filter, variant and part number. It returns an empty list if the database has yet to publish any parts.

The list of announced Megas lives in `OtherKeeperFilters.cs`. Add species when new Mega Evolutions are confirmed. As PvPoke marks a Mega as released, the generated filter uses the game's Mega keyword for it.
