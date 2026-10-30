# Frontend homework — level 3 and Remove Guest bonus

## Scope and acceptance checklist

**Level 3 and Remove Guest bonus complete:** implemented, independently reviewed,
271 backend tests and 112 frontend feature tests passed, production build passed,
the essential backend update is deployed, and the operator confirmed the real
Owner/guest/removal/submission/reload/Waiter flow. Evidence is linked below.
The finished frontend is not yet published to AWS. Confirmed existing Waiter UI
defects are tracked separately; their correction is not claimed here.

Implement against EPAM's [level 3 and bonus requirements](homework-frontend.md),
using the deployed API contracts and preserving the main application's Angular
architecture. Baselines: **65 frontend homework/helper tests** and **263 backend
tests** passed before this work. This document separates implementation evidence
from operator test/build/deployment results and real browser acceptance.

| EPAM requirement | Verification to record |
| --- | --- |
| Real Customer login and reservation; no demo credentials sent to Cognito API | Owner-route interceptor tests and real browser flow |
| Refresh sessions and status every 15 seconds while Open | Supplied fakeAsync/tick specs plus overlap/lifecycle tests |
| Stop polling after Submit and on leaving/retrying/changing reservations | Supplied and additional lifecycle tests |
| No overlapping refresh requests | In-flight refresh tests |
| Background updates preserve visible content | Page-state tests and browser flow |
| Updated at time and manual Refresh | Supplied sync tests and browser flow |
| Keep last data and show refresh failure time; no repeated error toast | Silent-context/interceptor/page tests |
| API Submit errors remain visible without breaking/redundant requests | Interceptor/submit regression tests |
| Remove Guest only for Guests while Open, with named accessible confirmation | Supplied bonus tests and browser flow |
| DELETE response updates list and computed totals | Supplied bonus and mutation/refresh tests |
| No new Subject/BehaviorSubject UI state; standalone/signals/inject patterns | Source/lint review |
| App/spec type checks, production build, homework tests | Exact command results below |
| Short description, screenshots of states, AI use and verification | This record plus operator screenshots |

No row is claimed fully verified solely because code or a test exists. Runtime
results, real API acceptance, and screenshots must be recorded when available.

### Real-login configuration correction

During the real browser flow, localhost displayed an incorrect-credentials
message. Inspection found that the tracked development environment still used
an older API URL, while the operator-generated production environment used the
current deployment URL supplied by the Terraform-output build command. The
development URL was aligned with that generated production URL. Terraform output
could not be read in the agent environment, so direct live authentication is not
claimed verified by this correction. Credentials and authentication code were
not changed. After restarting `npm start`, the operator confirmed that real
Customer login works. The subsequently completed real group-order flow and screenshots are recorded below.

## Essential backend compatibility change

The invitation lookup cannot resolve an already submitted/closed or full group.
Parsing an opaque owner-session ID or relying on local browser cache would not
provide a durable API contract. Therefore the existing Owner-only endpoint gains
one additive response field:

```json
{
  "guestSessionId": "opaque-owner-session-id",
  "groupReservationId": "group-id"
}
```

`GET /reservations/{reservationId}/guest-session-id` keeps its path, Cognito and
ownership checks, lazy creation behavior, and existing field. The service's
original ID-only method remains available. No new endpoint, table, route, or
Terraform resource is introduced. This requires rebuilding/deploying the API
Lambda before stable real-group bootstrap can use the added field.

The frontend validates Owner context first, then uses that context with the
existing reservation metadata endpoint. EPAM's demo/older response without the
additional field retains the invitation chain; no test-specific ID branches
or session-ID parsing are required. An inactive legacy group with no existing
Owner session still returns the pre-existing conflict; universal migration of
all historical groups is not claimed.

## Existing data semantics to preserve

The real backend stores the Owner's ordinary preorder separately from the empty
Owner session, then merges it once with submitted guest carts. The screen must
show that saved preorder without rewriting the raw participant submission flag
or counting it twice. After submission, the saved order is the authoritative
consolidated summary; its quantities must not be added to guest carts again.
Order 404 means no saved preorder; other failures must not silently undercount.

The real Owner session ID and group ID remain distinct opaque identifiers.
Reservation metadata lacks location ID/table number; adapters must not invent
those values for presentation. Expected API error messages belong to the
existing interceptor; background requests use `SKIP_ERROR_TOAST`.

## Agents and skill

- `frontend_level3`: polling, service data adapter, page/card bonus and tests.
- `frontend_level3_review`: independent read-only requirement/contract review.
- `frontend_level3_backend`: the essential additive context field and tests.
- Main agent: auth-token isolation, booking navigation, coordination, version
  metadata, verification, and documentation.

The installed `angular-developer` skill guides Angular work. Existing Angular,
Jasmine/Karma, SCSS, signals, and naming conventions take precedence over generic
new-app defaults. These are temporary subagents, not custom agent config files.

## Local verification and API deployment

From repository root:

```bash
dotnet test backend/restaurant-backend-app/Tests/APIHandler.Tests.csproj
cd frontend/restaurant-frontend-app
npx tsc -p tsconfig.app.json --noEmit
npx tsc -p tsconfig.spec.json --noEmit
npm run test:hw -- --browsers=ChromeHeadless
BACKEND_URL="$(terraform -chdir=../../infra/terraform output -raw backend_url)" npm run build
```

The homework command also includes the changed interceptor and booking-navigation
regressions. Supplied level 2 specs remain; level 3/bonus specs are added rather
than replacing them. No dependency upgrades or test-framework migration apply.

After passing tests, rebuild only through the existing packaging workflow and
review the Terraform plan before applying it:

```bash
python3 scripts/check-deployment.py
python3 scripts/build-lambdas.py
terraform -chdir=infra/terraform validate
terraform -chdir=infra/terraform plan -out=frontend-level3-api.tfplan
# Review first, then:
terraform -chdir=infra/terraform apply frontend-level3-api.tfplan
```

The Lambda ZIP object/version/alias may change; databases, Cognito, buckets, and
application records must not be replaced. Publishing the finished frontend to
S3/CloudFront remains a separate deployment step; a local build does not publish.

## One real end-to-end flow

Sign out of demo mode and log in as the real Customer. Use a future group booking
more than 30 minutes away, with room for the Owner and test guests. Keep the
guest browser sessions open. Save one Owner salad in the ordinary preorder.

Open Group Pre-Order from the booking. Have a guest add and submit two salads;
verify that the Owner sees them within 15 seconds without a full-page loader,
with an updated time. The Owner's own salad must be included once. Use Refresh
once. A second disposable test guest can be removed through the confirmation
dialog before submission, checking the list/total changes in the same flow.

Confirm the consolidated order, then verify the read-only Sent to Restaurant
state, no further polling, successful reload of that submitted group, and the
assigned Waiter's correct saved quantities. This is one integrated flow, not a
repeat of the backend error/permission matrix. Detailed errors/races stay in
automated tests.

Keep test-only screenshots under `screenshots/frontend-level3/` here. Avoid
tokens, private names, emails, or invite URLs. EPAM requests screen-state
artifacts; the supplied Open and Sent evidence is linked below. The removal
dialog was not captured and is not claimed as screenshot evidence.

### Supplied screenshot evidence

The operator supplied five PNG screenshots, inspected and moved from repository
root into the evidence folder without altering image bytes:

- [Open overview](screenshots/frontend-level3/open-overview.png): Collecting
  pre-orders, reservation details, Updated at/Refresh, draft warning, and the
  Owner's one saved salad.
- [Open summary](screenshots/frontend-level3/open-summary.png): guest's two
  salads, an empty draft guest, and three consolidated salads totaling $24.
- [Sent to Restaurant](screenshots/frontend-level3/sent-to-restaurant.png):
  submitted status and read-only visible participant cards. This crop does not
  include Grand Total, so the retained $24 after reload is operator-reported,
  not independently demonstrated by this image.
- [Waiter reservations](screenshots/frontend-level3/waiter-reservations.png):
  the matching future booking and its Pre-order action, with Table 1 shown.
- [Waiter preorder](screenshots/frontend-level3/waiter-preorder-layout.png):
  Marathon Demo Salad, price $8 and **Quantity: 3**, confirming the saved combined
  order. It also records the existing missing-image/Cancel-button defects,
  Unknown table fallback, and inconsistent time presentation.

These are desktop crops, not proof of a measured 375 px viewport or an exact
polling interval. Removal ($32 to $24) is verified by the operator's report and
automated cases; these images do not independently show that mutation.

## Results

Implementation and independent review are complete. No assignment-blocking
findings remain in code review. Frontend version is **1.1.4**; dependency versions
and the AWS deployment setup are unchanged. The stale development API URL was
corrected during real-login troubleshooting as recorded above.

Confirmed verification:

- Frontend app/spec TypeScript checks and Angular template compilation passed.
- Scoped ESLint for the new/changed feature, interceptor, and navigation specs:
  **0 errors, 0 warnings**.
- Sass compilation passed: compressed participant-card CSS **1,822 bytes**,
  page CSS **3,240 bytes**. Actual Angular bundle/style output remains pending.
- All supplied practice/level 2/level 3/bonus spec copies remain unchanged.
  Level 3 adds **15 supplied cases** plus **18 integration/reliability cases**.
  Interceptor/navigation coverage and the existing interceptor tests are also
  included in `test:hw`. No runtime passing count is inferred from source.
- The minimal backend context update adds **8 regression cases**; the complete
  backend test project compiles with **47 warnings, 0 errors**. Runtime backend
  tests were not executed in the agent environment (established socket limit).
- Deployment contract checks pass: **69 API methods, 13 tables**. No API routes,
  Terraform resources, or table schemas were added. Whitespace and configuration
  preservation checks passed.

The implementation and review agents were interrupted by a usage limit, then
resumed to finish the same work. Final code review verified polling lifecycle,
quiet errors, mutation guards, single snapshot status, Owner preorder projection,
and final response quantities. Runtime tests, browser appearance, AWS behavior,
and screenshots are separate evidence and are not claimed by that review.

The agent attempted production build and homework tests once after the final
implementation. Both aborted during bundling with native Node
**malloc: pointer being freed was not allocated**, before a build/test result.
This matches the agent-environment failure seen in earlier frontend levels;
no passing runtime count or production bundle check is claimed for level 3.
The operator subsequently confirmed successful local execution:

- Complete backend suite: **271 passed, 0 failed, 0 skipped**, duration 0.5
  seconds; build succeeded with 66 warnings in 2.9 seconds.
- Frontend homework/helper/interceptor/navigation suite in Chrome Headless 156:
  **112 executed, 112 succeeded**, reported elapsed 0.151 / 0.129 seconds.
- Production frontend build completed through postbuild:
  **Bundle check passed: no configuration placeholders or development demo
  markers.** No production bundle-size figure or full unrelated frontend-suite
  result was supplied.

The operator reviewed and applied the API deployment plan successfully:
**3 added, 6 changed, 3 destroyed**. The replacements were the three Lambda ZIP
S3 objects; the updates were the three Lambda functions and their aliases. No
database, bucket, Cognito, route, or table-schema replacement was listed in the
reviewed changes. The additive Owner context field is now deployed.

The real end-to-end flow is partly confirmed: after correcting the development
API URL and signing in as the real Customer, the operator saw their saved salad
in My Pre-Order and the consolidated preview. A guest then submitted two salads;
the operator confirmed seeing the guest order and the consolidated **$24** total
in the requested automatic-update flow (one Owner salad plus two guest salads).
No independently timed browser/network trace was collected. The operator then
joined a second test guest, submitted one salad, and saw the total become **$32**.
Removing that guest through the Owner screen succeeded and returned the total
to **$24**, confirming real DELETE membership and nonzero total recalculation.
The operator then confirmed completing final submission and reload as requested:
Sent to Restaurant with the $24 total retained. The assigned Waiter also opened
the same reservation and confirmed three salads; supplied screenshot visibly
shows **Quantity: 3**. All five supplied screenshots are linked above. No
independent browser/network trace or measured mobile screenshot was collected.
**Level 3 and the bonus are complete.** No further manual homework checks are
required. The finished frontend has not been published to AWS; local build output
is not an AWS frontend deployment. Waiter image/button, table-label, and timezone
defects were recorded in the project's frontend follow-ups; subsequent fixes and their verification status are in [the display cleanup record](FrontendUiFixes.md).

Known verification/consistency limits: DynamoDB secondary-index reads can lag;
a Submitted group with a missing/still-Created order is shown as awaiting a
verified final summary rather than displaying a false total. Read-only reload
can retry that read without resubmitting. The pre-existing backend concurrent
Submit/Delete race and inactive legacy groups without an Owner session remain
outside this frontend change; no complete snapshot-isolation claim is made.

## Subsequent display cleanup

Version 1.1.5 contains focused fixes for the observed existing UI defects.
See [FrontendUiFixes.md](FrontendUiFixes.md); updated runtime checks and visual
recheck remain pending and do not change the earlier homework acceptance record.
