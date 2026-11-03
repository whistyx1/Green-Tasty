# Green & Tasty frontend

The active Angular 22 single-page app serves restaurant browsing, booking, pre-orders, and role-specific Customer, Waiter, and Admin views. The EPAM Owner group-order homework levels 1–3 and Remove Guest bonus are implemented in `src/app/pages/group-order-page/`, `src/app/components/group-orders/`, and `src/app/services/group-orders/`. See the [homework index](../../docs/epam-homework/README.md) and [level 3 record](../../docs/epam-homework/FrontendLevel3.md) for behavior and acceptance evidence.

## Setup and development

Use Node.js matching `.nvmrc` or the `engines` range in `package.json`, then from this directory:

```bash
npm ci
npm start
```

Open `http://localhost:4200/`. The development environment in `src/environments/environment.development.ts` points to a deployed API; real login therefore requires that API to be reachable. For the isolated EPAM homework demo, open `http://localhost:4200/reservations/demo-reservation/group-order?demo`. Demo authentication and mock group data are development-only and do not require a real account.

## Checks

```bash
npm run lint
npm test -- --watch=false --browsers=ChromeHeadless
npm run test:hw -- --browsers=ChromeHeadless
BACKEND_URL=https://example.invalid/develop npm run build
```

The full Karma/Jasmine suite and the homework subset are separate checks. The production build runs `scripts/set-env.mjs` before Angular and `scripts/check-bundle.mjs` afterward; the latter rejects placeholders and development demo markers. An example-URL bundle is useful for compilation only. For deployment, rebuild with the real API URL from Terraform, then follow the [AWS deployment guide](../../infra/terraform/README.md). Build output alone does not publish to CloudFront.

The current [validation record](../../docs/epam-homework/Validation.md) includes actual pass counts and lint warnings. `homework-tests/` retains EPAM's supplied test fixtures; active tests are under `src/app/`. The [frontend follow-up list](docs/FollowUpTasks.md) tracks remaining UI issues separately from homework acceptance.
