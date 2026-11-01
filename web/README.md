# web/

The web app: a Vite single-page React + TypeScript app, installable (PWA) with a read-only offline cache. Calls the API through the client generated from its OpenAPI description. Deployed to Azure Static Web Apps (production and the `qa` environment).

QA and local copies show which copy they are (orange "QA" strip, grey "Local" strip, tab title and icon); production shows nothing extra. Set at build time with `VITE_APP_ENV` (`production`, `qa`, otherwise local) plus `VITE_PR_NUMBER` and `VITE_APP_VERSION` on QA: `src/environment.ts`.

Unit and component tests (Vitest) sit next to the code they test as `*.test.ts(x)`. End-to-end tests are in `tests/ui/`.

The screens it implements are designed in `design/` (25 MVP screens, phone and desktop, light and dark).

## App shell and design system (E01-F03)

| Where                   | What                                                                                                                                                                                                                                        |
| ----------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `src/app/routes.tsx`    | Every screen's address (React Router 7). Screens not built yet show "Coming soon" inside the real shell; each feature swaps in its screen here. `/design` (all shared components on one page) exists on QA and local copies only.           |
| `src/layout/`           | The shell: phone (screen header + bottom tab bar) or desktop (sidebar + top bar) from 900 px wide; `TabScreen` / `DetailScreen` frames; search and Ask panels, which open over the page without an address of their own (Back closes them). |
| `src/styles/tokens.css` | Design tokens from `design/source/base.css`. Every colour is `light-dark(<light>, <dark>)`, so it always has a dark version (checked by `tokens.test.ts`); type colours are the same in both.                                               |
| `src/theme/`            | Settings → Theme (Follow device · Light · Dark), saved per device in `localStorage` (`mpgc.theme`). `public/theme.js` applies it before the page draws.                                                                                     |
| `src/ui/`               | Shared components: card, chip, pill, segmented switch and section links, type badge, version marker, sprite box.                                                                                                                            |

## TypeScript: two versions side by side

`tsc` (type check and build) is **TypeScript 7**, the fast native compiler. TypeScript 7.0 has no programmatic API yet, and `typescript-eslint` needs one, so the package named `typescript` is Microsoft's compatibility package with the **TypeScript 6** API (`@typescript/typescript6`, which also gives a `tsc6` command). In `package.json`:

| Entry                                             | Installs                  | Used by                                                               |
| ------------------------------------------------- | ------------------------- | --------------------------------------------------------------------- |
| `"@typescript/native": "npm:typescript@^7…"`      | TypeScript 7 (`tsc`)      | `npm run typecheck`, `npm run build`                                  |
| `"typescript": "npm:@typescript/typescript6@^6…"` | TypeScript 6 API (`tsc6`) | ESLint (`typescript-eslint`) and other tools that import `typescript` |

When `typescript-eslint` supports TypeScript 7 (planned for 7.1, typescript-eslint issue #10940), drop the alias and depend on `typescript` 7 directly. `tests/ui/` has no ESLint and uses TypeScript 7 directly.
