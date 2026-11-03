# Frontend working instructions

This is the **active** Angular 22 application in `frontend/restaurant-frontend-app/`. The root [README](../../README.md) and this app's [README](README.md) describe the current Terraform-owned deployment and local setup. The copies under `green-and-tasty/` and ignored `marathon2026-main/` are EPAM reference snapshots; their matching `CLAUDE.md` files describe the starter and contain outdated measurements. The backend cannot be run with a simple local host command in this repo; its Lambda source can be built and unit-tested locally. Backend changes are packaged and deployed through Terraform, not Syndicate.

## Commands and configuration

From this directory:

```bash
npm ci
npm start
npm run lint
npm test -- --watch=false --browsers=ChromeHeadless
npm run test:hw -- --browsers=ChromeHeadless
BACKEND_URL=https://example.invalid/develop npm run build
```

`npm start` uses `src/environments/environment.development.ts`, which currently points to a deployed `us-west-2` API; check reachability before real-login work. The `?demo` group-order route uses development-only mock providers. Production `npm run build` generates the ignored `environment.production.ts` from `BACKEND_URL`; `postbuild` rejects placeholders and demo markers. Rebuild with the actual Terraform `backend_url` before publishing. An example-URL build is only a compilation check. See the [latest validation](../../docs/epam-homework/Validation.md) for current test and lint results.

## Architecture and placement

- Route-level screens are under `src/app/pages/`; the Owner homework screen is `pages/group-order-page/`.
- Group participant cards and dialogs are under `src/app/components/group-orders/`.
- Domain HTTP services are under `src/app/services/`; group-order API calls are in `services/group-orders/`.
- Shared models and helpers are under `src/app/models/` and `src/app/shared/`.
- Authentication and token interception are under `src/app/auth/`.
- EPAM's original homework tests are preserved in `homework-tests/`; active Jasmine specs are in `src/app/`.

Use the existing Angular standalone, `inject()`, signals, SCSS, and RxJS patterns in the area being changed. Keep HTTP calls in services and presentation state in the owning page or component. Preserve Owner Cognito authentication versus opaque guest-token behavior. For API contracts, check [ApiRoutes.md](../../backend/restaurant-backend-app/docs/ApiRoutes.md), the backend route registry, and actual response models. Do not invent request fields from Figma or the old starter mocks.

## Tests and behavior

- Add focused tests for changed behavior, including success, error, loading, empty, and authorization states where relevant.
- Use `provideHttpClient()` plus `provideHttpClientTesting()` for HTTP service tests and `provideRouter([])` where a test injects the router.
- For polling or subscriptions, release resources on navigation/destruction and avoid overlapping refresh requests. The Owner group page refreshes while Open and stops after submission.
- Keep test fixtures and demo providers out of the production build. Do not hardcode credentials, tokens, invitation URLs, or private account details.
- Run the full suite and homework subset before claiming a change is complete. A passing `test:hw` run does not cover every unrelated frontend feature. Report actual counts and warnings.

## Current limits

The older starter copy of this file listed 216 lint errors, 56 specs, a different AWS region, and Syndicate deployment. Those figures and instructions are stale. The 2026-11-02 local validation found 0 lint errors and 38 warnings, 365 passing full-suite tests, 130 passing homework-subset tests, and a passing production build with a placeholder-safe example URL. The build still emitted CommonJS optimization warnings. The [frontend follow-up list](docs/FollowUpTasks.md) and [homework records](../../docs/epam-homework/README.md) track UI fixes and historical acceptance separately; do not treat their earlier pending notes as the latest deployment status.

Do not commit generated `dist/`, `node_modules/`, `environment.production.ts`, local `.env`, private evidence, or credentials. Do not commit or push unless the user requests it.
