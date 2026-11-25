# Filter builder

Open **Filters → Builder**. On a phone, tap **Add a condition** to open the picker. On a desktop, the picker stays beside the condition list and the search string. Search the ten groups by name or keyword, then pick one or more values and apply the condition. Conditions are joined by AND. Values in the same condition are joined by OR. For example, choosing Fire or Water and adding an excluded Shiny condition produces `fire,water&!shiny`.

Use **Minimum** by itself for an open-ended range, **Maximum** by itself for a cap, or the same value in both fields for an exact match. Toggle **Exclude values I add** before selecting an excluded value. You can edit or remove any condition. The Pokédex provides species and move names, and the generated string uses English game terms regardless of the display language. Picking a species generates its Pokédex number. Choosing its whole family generates a `+name` term.

You can also edit the search string in place. Supported terms return to their groups and mixed or unknown alternatives remain together as a custom condition. The checker uses the game-search simulator's grammar and published Pokémon and move names. Nicknames or other unverified terms remain copyable with a message to try them in the game. The checker verifies search syntax without reading your in-game collection. See the [Pokémon GO search guide](https://niantic.helpshift.com/hc/en/5-pokemon-go/faq/1126-searching-filtering-your-pokemon-inventory/).

Each copyable part fits within 1,950 characters. If needed, the builder splits a list of alternatives and keeps the other conditions in each part. Paste every part separately to find all matching Pokémon. Expressions such as one very long AND-only search need to be shortened by hand.

The builder calls the public `POST /filter-search/check` endpoint. The result contains a status, the copyable parts and the first unverified or invalid term when applicable.

## Saved filters

When Builder shows a copyable search, choose **Save filter**, enter a name and select **Save**. Saving requires Google sign-in. If you sign in from Builder, the draft stays in the same tab for your return.

Under **Filters → Saved**, select a filter's name to reopen its search and edit its conditions in Builder. Saving again creates a new entry. Use **Copy** to copy a short search or open the copyable parts of a long one. Use **⋯** to rename, duplicate or delete a filter. Saved filters belong to your account and appear on any device where you sign in.

The signed-in API exposes these operations under `/saved-filters`. It stores the original search string, and Builder reconstructs the conditions from that string when it reopens. The database uses the existing saved-filter table.
