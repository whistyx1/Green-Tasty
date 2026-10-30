# EPAM starter synchronization — 2026-10-29

The working application remains
[`frontend/restaurant-frontend-app`](../../frontend/restaurant-frontend-app).
`green-and-tasty/` is retained unchanged as the original EPAM reference.

## What was synchronized

- Group-order API models, preview data, development mock login/API interceptor,
  mock-store tests, and the consolidation helper with its tests.
- The full `homework-tests/` directory, kept outside `src/app` until its
  corresponding level is implemented. Copying all levels into the active suite
  now would reference components/services that intentionally do not exist yet.
- The `test:hw` npm script and EPAM's reservation-card test correction (required
  input and router provider). Existing dependencies, lockfile, author metadata,
  API URLs, deployment scripts, and project follow-up notes were preserved.
- Original homework README, frontend/backend assignments, practice instructions,
  and `homework-design/` assets are copied here with their original names and
  hierarchy. They remain in the original archive too, as requested. The copies
  preserve EPAM's wording; commands referring to `frontend/restaurant-frontend-app`
  should be run from the repository root, not relative to this docs directory.

No homework route, page, participant card, group-orders HTTP service, polling,
submission UI, or removal UI has been implemented during synchronization.

## Where to read and work

- [EPAM README](README.md)
- [Practice](practice-frontend.md)
- [Frontend assignment](homework-frontend.md)
- [Original backend assignment](homework-backend.md)
- [Design scope and images](homework-design/README.md)
- [Supplied level tests](../../frontend/restaurant-frontend-app/homework-tests/README.md)
- [Actual completed backend contracts](../../backend/restaurant-backend-app/docs/ApiRoutes.md)
- [Project frontend follow-ups](../../frontend/restaurant-frontend-app/docs/FollowUpTasks.md)

The EPAM README's demo page will not exist until practice/level 1 is implemented.
The main frontend retains its configured AWS backend; do not replace it with the
starter's `YOUR-API-ID` placeholder. Demo authentication is for local development,
not the deployed Cognito API. Sign out of demo mode before real API testing.

## Production isolation

`app.config.ts` imports `shared/config/homework-providers.ts`, whose default
exports are empty and do not import mocks. Only the Angular **development**
configuration replaces that module with `homework-providers.development.ts`.
That module registers EPAM's demo initializer and interceptor, with a further
`isDevMode()` check. Production retains the existing generated environment and
does not register or import the mock implementation through this entry point.

The existing postbuild scan also rejects the demo issuer and demo-user markers,
in addition to the API placeholder, if found in shipped JS/CSS/HTML. This check
must run after a successful production build; static configuration inspection
alone is not a passing production artifact verification.

## Verification

Passed checks:

- Baseline and post-sync `tsc -p tsconfig.app.json --noEmit` and
  `tsc -p tsconfig.spec.json --noEmit`.
- Explicit TypeScript check of the development-only provider entry, which is
  otherwise outside the default production app graph.
- Protected AWS environment files, `set-env.mjs`, dependency lockfile,
  `CLAUDE.md`, and project follow-up notes match their Git HEAD bytes.
- Imported models/mocks/test fixtures and copied design assets match the starter
  bytes. Existing modified CRLF files retain CRLF; whitespace checks pass.
- Static configuration confirms the production/default provider entry imports
  no mocks and the replacement exists only in the development build config.
- Bundle-guard fixture checks: clean content passes; the API placeholder and
  each demo-auth marker correctly fail. This checks the guard, not the actual
  output of an Angular production build.

Agent-environment attempts (superseded by the operator results below where noted):

- Production `npm run build` with the existing configured AWS API URL.
- Full suite: `npm test -- --watch=false --browsers=ChromeHeadless`.
- Homework/helper suite: `npm run test:hw -- --browsers=ChromeHeadless`.

Angular processes aborted during bundling (exit code 134) without an actionable
diagnostic, including retries with one build worker. No tests reached a reported
execution result in the agent environment. The cause is not established.
The first build invocation also caught an API-placeholder selection in the
verification command; that command was corrected to use the actual configured
URL without changing either tracked environment file.

The operator subsequently confirmed successful local verification:

- Production `npm run build` completed through the postbuild check:
  **Bundle check passed: no configuration placeholders or development demo markers.**
- `npm run test:hw -- --browsers=ChromeHeadless`: **26 executed, 26 succeeded**
  in Chrome Headless 156. No full-suite result or bundle-size figure was supplied.

The synchronized starter is ready for frontend homework implementation. The
complete frontend suite and interactive development/production browser checks
remain unverified; the 26 passing tests do not establish those results.

From repository root, local verification can be run as follows:

```bash
export BACKEND_URL="$(terraform -chdir=infra/terraform output -raw backend_url)"
cd frontend/restaurant-frontend-app
npx tsc -p tsconfig.app.json --noEmit
npx tsc -p tsconfig.spec.json --noEmit
npm run build
npm run test:hw -- --browsers=ChromeHeadless
npm test -- --watch=false --browsers=ChromeHeadless
```

The supplied homework command covers the currently installed helper specs until
level specs are deliberately copied into `src/app`. Demo-auth/mock-store specs
are under `src/app/shared/mocks/` and are included by the full test command.
Existing unrelated suite failures should be reported separately from this
synchronization. Browser checks for local `?demo` behavior and confirmation that
a production build ignores `?demo` also remain pending. No AWS resources were
changed, no deployment was run, and no homework level was implemented.

This records the synchronization checkpoint. Subsequent level 1 implementation
and its verification are tracked separately in [FrontendLevel1.md](FrontendLevel1.md).
