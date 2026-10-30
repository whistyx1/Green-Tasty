# EPAM acceptance and regression review — 2026-10-30

## Conclusion

Independent read-only review found no blocking mismatch with EPAM functional
requirements and no confirmed new production regression in the inspected changes.
This is not a claim that every application behavior is correct. The updated
full frontend/backend suites and production build have not completed here.

## Evidence

| Check | Result and limitation |
| --- | --- |
| Homework + targeted regression suite | Operator reported **129 SUCCESS** after repairing the timezone constructor mock. Selects 22 of 80 frontend spec files; 58 files are outside this command. |
| Full frontend suite | Attempted `npm test -- --watch=false --browsers=ChromeHeadless`; bundling aborted with native Node `pointer being freed was not allocated`, before a test result. |
| Complete backend suite | `dotnet test backend/restaurant-backend-app/Tests/APIHandler.Tests.csproj --no-restore` compiled, then VSTest aborted with SocketException (13), Permission denied, while binding its communication socket. No current runtime pass claimed. Prior operator result: 271 passed. |
| App/spec TypeScript and Angular template compilation | Passed. |
| Full frontend lint | 0 errors, 38 warnings. |
| Deployment contract | Passed: 69 API methods, 13 tables, management authentication enforced. No AWS calls/deployments. |
| Production build | Attempted; same native Node bundling failure. No new successful bundle output claimed. |

The backend build also reports existing nullability/deprecation and NuGet
security warnings. This review does not upgrade dependencies or assert their
safety. No test was skipped or weakened to obtain a passing result.

## Independent agents

Two temporary agents were explicitly delegated for this review:
`epam_acceptance_audit` compared requirements, implementation and supplied tests;
`regression_audit` inspected changed contracts, source isolation and test scope.
Neither edited source or executed tests. The main agent ran the checks above.
These are temporary agents, not a new persistent custom-agent configuration.

## EPAM requirement mapping

| Requirement | Verified source / existing acceptance evidence |
| --- | --- |
| Practice and level 1 card/Owner screen | Semantic participant cards, image fallback, prices/statuses, reservation details and responsive styles. Historical static level progresses to the API page as required. See FrontendLevel1.md. |
| Level 2 | Existing endpoint service, Owner identification, submitted-only consolidation, pending warning, loading/error/retry/empty states, shared confirmation and mutation guards. Supplied level 2 cases preserved; operator mock acceptance recorded in FrontendLevel2.md. |
| Level 3 | 15-second Open-only polling, shared in-flight guard, cleanup, Updated at/Refresh, retained data/quiet background errors and real Cognito/API adaptation. Earlier real integrated flow recorded in FrontendLevel3.md. |
| Remove Guest bonus | Named shared confirmation, Owner excluded, Open-only actions, response updates membership and totals. Operator reported $32 to $24 after removal. |
| Test preservation | All eight supplied homework fixture files match green-and-tasty byte-for-byte. Seven active practice/level 2/level 3/bonus copies match; level 1 page spec was replaced by level 2 as EPAM instructs. |
| Production separation | Default homework provider arrays empty. Only development file replacement imports demo auth/mock interceptor; runtime dev guard also applied. Postbuild rejects demo markers. Final emitted bundle still needs a successful updated build. |

A minor source-instruction inconsistency remains: design README requests a
disabled Submit button after submission, while the supplied level 2 spec
requires its absence. Current code follows the supplied spec and cannot
resubmit. No functional blocker was found.

## Remaining regression limits

The full run must include the general reservation-card, header, submitted-cart,
and 11 existing Edit Reservation tests excluded by test:hw. Some unchanged
original tests have setup defects: the dish-modal creation spec, for example,
does not supply the dish its template reads. Both files match HEAD; an actual
full-suite failure still needs execution and classification before calling it
a regression. CSS compile/tests do not establish measured mobile appearance.

The existing invite retrieval feature request and backend Submit/Delete race
remain documented follow-ups. No complete regression-free or isolation guarantee
is implied by a passing suite.

## Operator commands

From repository root, run the full checks and keep the logs local for review:

```bash
(cd frontend/restaurant-frontend-app && npm test -- --watch=false --browsers=ChromeHeadless) > frontend-all-tests.log 2>&1
dotnet test backend/restaurant-backend-app/Tests/APIHandler.Tests.csproj > backend-all-tests.log 2>&1
(cd frontend/restaurant-frontend-app && BACKEND_URL="$(terraform -chdir=../../infra/terraform output -raw backend_url)" npm run build) > frontend-build.log 2>&1
```

The Terraform command reads the existing output; it does not deploy. Review
full-suite failures before changing expectations: fix introduced defects,
repair proven pre-existing test setup separately, and never suppress failures.
No additional manual booking/permission matrix is requested for this review.

## Operator full-run results

The operator completed all three commands and supplied logs in repository root.
The backend suite passed **271, failed 0, skipped 0**. Production build passed
with initial total **650.05 kB** (134.37 kB estimated transfer) and the postbuild
configuration/demo-marker check passed. Bundle-size/CommonJS warnings remain.
The full frontend suite executed **352 tests: 307 passed, 45 failed**. This
supersedes the previous execution blockers for the operator environment, but
is not a clean release-test result.

The failures include missing input/provider/auth test setup, an obsolete CLI
welcome-title assertion, and 32 GeneralInformationComponent cases sharing
`spyOnProperty: result is not declared configurable`. The profile spec/source
are unchanged from HEAD, but complete baseline execution and classification
have not been performed. Do not describe all 45 as harmless or proven legacy
failures, and do not hide them by narrowing the full-suite command.

No source fixes, permission changes, commit, push or AWS publication were
performed as part of reviewing these logs.

## Repair and verified clean full run — version 1.1.6

The failures were not EPAM levels 1/2 demo data. All 14 failing spec files matched
HEAD before repair. Counts: **32** profile mock setup failures, **6** missing
required input/data, **5** missing routing, **1** missing auth and **1** obsolete
welcome-title assertion. These were not 45 independent website bugs.

`frontend_failure_classification` repaired the profile FileReader fake without
changing its32 test bodies/assertions. Once executable,15 of those 32 cases exposed
existing missing profile validation/save behavior; the focused profile TS/HTML
repair now validates MIME/nonempty/5 MiB/decode/4096 px and preserves previews,
avoids unnecessary name saves and supports photo-only partial-save retry.
These frontend limits come from existing tests, not claimed backend limits.
`frontend_legacy_test_repair` supplied proper inputs/router/auth/HTTP setup in
13 existing specs and replaced the obsolete welcome assertion with RouterOutlet
verification. It independently reviewed profile changes without blockers.
Main agent coordinated, reviewed assertions/diffs, bumped only package version
metadata and executed tests. No backend, dependency, mock-separation or AWS
resource changes were made by this repair. No tests skipped, removed or reduced
to fabricate a clean result; total remains 352.

After the user selected Ask for approval, explicitly approved commands outside
the sandbox ran successfully. Actual final checks:

- Narrow profile suite: **32 passed, 0 failed**, exit 0.
- Full `npm test -- --watch=false --browsers=ChromeHeadless`: **352 passed, 0 failed**,
  all 352 executed in Chrome Headless 156, exit 0. This includes the homework cases.
- Full backend `dotnet test .../Tests/APIHandler.Tests.csproj --no-restore`: 
  **271 passed, 0 failed, 0 skipped**, exit 0.
- Production `npm run build` with existing AWS API URL: exit 0, initial 651.40 kB,
  estimated 134.76 kB transfer. Bundle guard passed: no configuration placeholders
  or development demo markers. Warning-level bundle/style/CommonJS debt remains.
- App/spec TypeScript, Angular template compilation, full lint passed. Full lint
  remains 0 errors/38 warnings. CRLF-aware whitespace check passed.

The clean result supersedes the earlier 45 failed run. Build success does not
publish the frontend to AWS. Existing package security warnings and documented
backend concurrency limitations remain; no complete regression-free guarantee
is implied. Final operator visual check and commit/push are separate steps.

### Requirement evidence

| Requirement | Evidence |
| --- | --- |
| Profile image validation without weakening prior assertions | `rejects files larger than 5 MB before reading`, `accepts the exact size and dimension limits`, `rejects images that cannot be decoded`, plus preserved MIME/empty/dimension cases; narrow 32/32 and full 352/352 |
| Safe profile save/retry and cleanup | `uploads a selected photo without resaving unchanged names`, `reports a partial save and retries only the failed photo`, `aborts an unfinished FileReader when destroyed`, `does not mark a newer selection saved when an earlier upload finishes`; full 352/352 |
| Repair legacy component contracts | Exact tests below; full 352/352 |
| Keep EPAM demo data development-only | Provider replacement unchanged; production build and demo-marker guard passed |

| Repaired spec | Exact behavior evidence |
| --- | --- |
| `src/app/app.component.spec.ts` | `should create the app`; `renders the application router outlet` |
| `src/app/auth/components/register/register.component.spec.ts` | `renders the registration form and login route with required fields` |
| `src/app/components/dish-modal/dish-modal.component.spec.ts` | `renders the supplied dish name and image` |
| `src/app/components/place-order-waiter/dish-select-card/dish-select-card.component.spec.ts` | `renders the supplied dish name and image` |
| `src/app/components/place-order-waiter/place-order-waiter.component.spec.ts` | `initializes the authenticated waiter and customer names` |
| `src/app/components/reservations/reservation-confirmed/reservation-confirmed.component.spec.ts` | `renders confirmation details for the supplied reservation` |
| `src/app/components/tables/make-reservation/make-reservation.component.spec.ts` | `renders the selected table and guest capacity` |
| `src/app/components/tables/time-slot/time-slot.component.spec.ts` | `renders the supplied time slot label` |
| `src/app/components/tables/time-slots-modal/time-slots-modal.component.spec.ts` | `renders available slots for the supplied table` |
| `src/app/pages/menu-page/menu-page.component.spec.ts` | `loads an empty menu through the routed page` |
| `src/app/pages/reporting-dashboard/reporting-dashboard.component.spec.ts` | `renders report controls with no authenticated report data` |
| `src/app/pages/reservation-dashboard/reservation-dashboard.component.spec.ts` | `redirects an unauthenticated visitor without loading reservations` |
| `src/app/pages/waiter-dashboard/waiter-dashboard.component.spec.ts` | `finishes loading without reservations when the session is absent` |
