# Frontend homework — level 1

## Scope

**Level 1 complete:** implemented, independently reviewed, production build and
42 homework/helper tests passed locally, and the operator confirmed responsive
visual behavior. The updated frontend has not been deployed to AWS.

Implement the practice participant card and the static Owner review page from
EPAM's level 1 assignment. This stage uses `GROUP_ORDER_PREVIEW`: no group API
service, submission/removal logic, or background polling is added. No backend
changes or AWS deployment belong to this stage.

The main application remains `frontend/restaurant-frontend-app`. The original
`green-and-tasty/` archive remains unchanged. Starter synchronization was
previously verified locally by the operator: production build/postbuild passed,
and **26 homework/helper tests passed** before this level was implemented.

## Skill and agent workflow

The installed `angular-developer` skill supplies Angular component, input,
styling, and routing guidance. Project conventions take precedence over generic
new-app guidance: existing Angular 22, standalone components, signal inputs and
state, SCSS color tokens, Jasmine/Karma, and existing file naming are preserved.

Two temporary agents are used for this level:

- `frontend_level1`: participant card, static page, route, and supplied specs.
- `frontend_level1_review`: independent read-only review of scope, rendering,
  accessibility, responsive styles, and production mock isolation.

The main agent coordinates the work, verification, and this record. No reusable
custom agent configuration is created. Agent compilation/review results and
operator runtime results must be distinguished below.

## Expected behavior

- Lazy route: `/reservations/:reservationId/group-order`, with the existing
  authentication guard/layout. In this stage all IDs show the same static
  preview; the page is not a real reservation review yet.
- Local demo entry:
  `http://localhost:4200/reservations/demo-reservation/group-order?demo`.
- Four participant cards, with names, dishes, quantities, prices, cart status,
  and subtotals. Owner title follows the practice contract (`Owner: Yevhenii`);
  `My Pre-Order` identification belongs to level 2.
- Header uses preview address/date/time/capacity; Grand Total is **$74.50**,
  including draft carts as required for level 1. Submitted-only consolidation
  belongs to level 2.
- Missing/failed dish images use a fallback, and images have explicit dimensions.
- Semantic main/heading/list/article structure, text cart statuses, and a layout
  intended to work at desktop and 375 px width.
- A displayed Submit control must not pretend to send an order at this stage.

## Verification

Run from `frontend/restaurant-frontend-app`:

```bash
npx tsc -p tsconfig.app.json --noEmit
npx tsc -p tsconfig.spec.json --noEmit
npm run test:hw -- --browsers=ChromeHeadless
BACKEND_URL="$(terraform -chdir=../../infra/terraform output -raw backend_url)" npm run build
```

Builds and tests do not deploy the website. The production postbuild guard must
still reject placeholder/demo-auth markers. Static preview data does not itself
register the development mock authentication or HTTP interceptor.

## One manual visual check

After the automated checks, start `npm start` and open the local demo URL above.
Confirm four cards, textual statuses, header details, and $74.50 total. Check
the same page at a 375 px viewport for overflow and clipped content. This is
one visual acceptance task; no AWS accounts, guest joining, or backend error
matrix is required for level 1. Demo login affects local-development storage;
sign out of demo mode before later real-backend testing.

## Results

Implementation, independent review, operator build/test execution, and the
agreed visual check are complete.

Confirmed checks:

- App/spec TypeScript compilation and Angular template compilation
  (`ngc -p tsconfig.app.json --noEmit`) passed.
- Targeted ESLint for the new component/page sources, templates, and specs:
  **0 errors, 0 warnings**.
- SCSS compilation passed: compressed card CSS **1,593 bytes**, page CSS
  **1,683 bytes**, each below the existing 4 kB component-style warning budget.
- EPAM's 8 practice-card and 5 level-1-page cases are copied unchanged. Three
  additional cases cover missing images, failed images, and replacement image
  URLs. Runtime results are not inferred from this inspection.
- Existing AWS development API configuration is unchanged. Frontend version
  is **1.1.2**, following the project rule for user-visible changes; the lockfile
  changes only matching version metadata, with no dependency changes.
- Independent agent review found no blocking issues. It did not claim rendered
  desktop/mobile verification from stylesheet inspection.

The coordinator attempted production `npm run build` and
`npm run test:hw -- --browsers=ChromeHeadless`. Both stopped during bundling with
a native Node diagnostic: **malloc: pointer being freed was not allocated**.
Neither reached a reported build/test result. This is distinct from a TypeScript
or template diagnostic; its cause has not been established. Do not treat the
earlier starter's successful 26-test run as a passing result for this new code.

The operator subsequently confirmed a successful production build through its
postbuild guard: **Bundle check passed: no configuration placeholders or
development demo markers.** No bundle-size figure was supplied.

The operator ran the post-change homework/helper suite in Chrome Headless 156:
**42 executed, 42 succeeded, 0 failed** (reported elapsed 0.051 seconds / 0.043
seconds). This includes the prior 26 cases, 13 supplied practice/level-1 cases,
and 3 added image-fallback cases.

The operator opened the localhost page and confirmed that resizing the window
kept the layout readable and contained, with no content escaping its blocks.
The exact viewport width was not supplied, so a measured 375 px screenshot is
not claimed. No AWS deployment, backend changes, or implementation of levels
2/3 occurred. No additional manual checks are required to close level 1.
