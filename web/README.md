# web/

The web app: a Vite single-page React + TypeScript app, installable (PWA), with a "You're offline" screen (read-only offline comes later). Calls the API through the client generated from its OpenAPI description (`src/api/`). Deployed to Azure Static Web Apps (production and the `qa` environment).

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

## Languages (E01-F04)

English and Português (Portugal), with i18next. Both languages are in the bundle, so switching is instant and works offline.

| Where                                                                 | What                                                                                                                                                                                                                                            |
| --------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `src/i18n/messages/en.ts`                                             | Every interface text in English: the source. Plurals use i18next's suffixes (`daysLeft_one`, `daysLeft_other`); values go in `{{placeholders}}`.                                                                                                |
| `src/i18n/messages/pt-PT.ts`                                          | The Portuguese texts. The type check fails if a key is missing or extra; `messages.test.ts` also checks placeholders. Vocabulary: `planning/GLOSSARY-PT.md`.                                                                                    |
| `src/i18n/format.ts`                                                  | Dates and numbers per language (`useFormats()`): British English ("Tue 27 Oct, 18:30", "1,234.5"), Portugal's Portuguese ("terça, 27/10, 18:30", "1234,5").                                                                                     |
| `src/i18n/language.ts`, `AccountLanguage.tsx`, `useLanguageChoice.ts` | The language: saved on the device (`mpgc.language`; first visit follows the browser: any Portuguese gives pt-PT), and signed in with the account (`PUT /me/language`), whose language wins at sign-in and on every refresh. Sets `<html lang>`. |

**Adding text:** add the key to `en.ts` and `pt-PT.ts`, then `const { t } = useTranslation()` and `t('section.key')` (keys are type-checked). Routes and navigation pass keys (`titles.<key>`, `nav.<key>`, `sections.<key>`), not text. `everyScreen.test.tsx` opens every address in Portuguese and fails if an English text is left on screen. Addresses, search strings, Pokémon GO's search keywords and backlog feature names stay in English.

## API client (E01-F05)

Typed from the API's OpenAPI document: `src/api/generated/schema.ts` is generated from `api/openapi.json` (`npm run api:generate`) and committed; `src/api/client.ts` has the client (`api.GET('/me')`) and the TanStack Query hooks (`$api.useQuery('get', '/health')`), and adds the access token while signed in. The `build-and-unit` check fails when the generated file is out of date. Details: `docs/API.md`.

## Installable app and offline screen (E01-F06)

The app installs on a phone or computer (own icon, full screen) and shows "You're offline" (screen H3) without a connection. API answers are not saved for offline use: read-only offline is a later feature.

| Where                                                      | What                                                                                                                                                                                                                                                                                                                                                                                                                                                                          |
| ---------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `src/pwa/manifest.ts`                                      | The web app manifest, written at build time per copy: production "MyPoGoCompanion", QA "QA · MyPoGoCompanion" with the orange icon, local grey. Icons in `public/icons/`, drawn by `scripts/make-icons.mjs`. iOS reads its icon and name from `<link rel="apple-touch-icon">` and `apple-mobile-web-app-title`, added to `index.html` by `vite.config.ts`.                                                                                                                    |
| `src/sw.ts`, `src/pwa/worker.ts`                           | The service worker (`/sw.js`, built by `vite-plugin-pwa` with the list of the build's files). Pages come from the network first, so online the app is always the latest deploy; the saved page is used only when the network fails or takes over 6 s. The build's scripts, styles, Latin fonts and icons come from the saved copy. Each build saves under its own name and removes older copies. Registered by `src/pwa/register.ts` (built copies only, not the dev server). |
| `src/pwa/connection.ts`                                    | Whether the device is offline: the browser says so, or an API request gets no answer and the app's own site (`/online-check`) doesn't answer either. While offline it checks every 10 s, when the browser says it's back and on Try again; once the site answers it reloads the app. `src/screens/OfflineScreen.tsx` is H3, shown by `AppShell` in place of the screen.                                                                                                       |
| `src/pwa/install.ts`, `InstallRow.tsx`, `InstallGuide.tsx` | Settings → More → Install the app: Chrome's own install window where the browser announced one (`beforeinstallprompt`), the Share → Add to Home Screen steps on iPhone and iPad, the browser's menu elsewhere; hidden once installed.                                                                                                                                                                                                                                         |
| `src/pwa/installHint.ts`, `InstallHint.tsx`                | The one-time install hint above the phone's tab bar: from the second visit (`mpgc.visits`, one per browser session), only where the browser can really install; Install or ✕ hides it for good (`mpgc.installHint`).                                                                                                                                                                                                                                                          |

**Trying it locally:** the dev server has no service worker. Build and preview: `npm run build && npm run preview` (http://localhost:4173), then in the browser's developer tools go offline (Network → Offline). The UI tests for the service worker run against a built copy only (`BASE_URL=http://localhost:4173` in `tests/ui/`); on the dev server they skip. To see the hint again, clear the site's data.

## TypeScript: two versions side by side

`tsc` (type check and build) is **TypeScript 7**, the fast native compiler. TypeScript 7.0 has no programmatic API yet, and `typescript-eslint` needs one, so the package named `typescript` is Microsoft's compatibility package with the **TypeScript 6** API (`@typescript/typescript6`, which also gives a `tsc6` command). In `package.json`:

| Entry                                             | Installs                  | Used by                                                               |
| ------------------------------------------------- | ------------------------- | --------------------------------------------------------------------- |
| `"@typescript/native": "npm:typescript@^7…"`      | TypeScript 7 (`tsc`)      | `npm run typecheck`, `npm run build`                                  |
| `"typescript": "npm:@typescript/typescript6@^6…"` | TypeScript 6 API (`tsc6`) | ESLint (`typescript-eslint`) and other tools that import `typescript` |

`openapi-typescript` (the API client generator) also imports `typescript` and declares TypeScript 5 as a peer; the `overrides` entry in `package.json` gives it the same TypeScript 6 API package, which it works with.

When `typescript-eslint` supports TypeScript 7 (planned for 7.1, typescript-eslint issue #10940), drop the alias and depend on `typescript` 7 directly. `tests/ui/` has no ESLint and uses TypeScript 7 directly.
