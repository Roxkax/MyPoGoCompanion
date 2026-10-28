# web/

The web app: a Vite single-page React + TypeScript app, installable (PWA) with a read-only offline cache. Calls the API through the client generated from its OpenAPI description. Deployed to Azure Static Web Apps (production and the `qa` environment).

Unit and component tests (Vitest) sit next to the code they test as `*.test.ts(x)`. End-to-end tests are in `tests/ui/`.

The screens it implements are designed in `design/` (25 MVP screens, phone and desktop, light and dark).
