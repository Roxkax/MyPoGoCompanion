# MyPoGoCompanion — UI/UX decisions

Decided with the owner (Oct 2026). Wireframes are iterated on a design canvas, screen by screen.

## Structure & navigation

- Desktop: left sidebar with every section. Phone: bottom tab bar — **Home, Pokédex, Raids, Collection, More**. Filters, Rankings, Events, Moves, AI and the rest live under More (and in the desktop sidebar).
- Global search in the header on every page (Pokémon, moves, bosses, events); Ctrl+K on desktop.
- Home page: **Today in the game** — current raid bosses, Max Battles, active and next events.

## Visual direction

- Clean and data-focused: calm neutrals, strong typography, clear tables and charts.
- Light and dark themes, following the system setting, with a toggle.
- Comfortable density.
- Pokémon type colours only on type badges, move chips and charts; the rest of the UI stays neutral.

## Screen patterns

- Pokédex list on phones: compact rows (sprite, name, types, 2–3 key numbers chosen by the user); tap for details. Desktop: table.
- Pokémon detail: header (sprite, types, key stats), then tabs — Overview, Moves, Evolution, Rankings, Versions, History.
- Raid boss: header (boss, tier, weaknesses, estimated minimum players) and the counters list with a boss-moveset picker first; tips and simulation below.

## Wireframe order

1. Home (Today in the game)
2. Pokédex list (desktop + phone)
3. Pokémon detail
4. Raid boss + counters

Then: filter builder, collection, tier lists, events, admin.
