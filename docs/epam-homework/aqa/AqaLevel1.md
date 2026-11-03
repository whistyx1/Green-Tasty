# AQA Direction 1 — level 1

**Status: implemented and live-verified against both the current localhost frontend and the user-published CloudFront frontend with the deployed AWS backend (12/12 passed in each final run).**
Levels 2–3, group Submit/removal scenarios and Report Portal are not part of this
change. Individual guest cart confirmation is necessary level1 setup/state;
it does not submit the consolidated group order.

## Scope and contract decisions

[EPAM assignment](homework-aqa.md) requires browser main flows plus API
permission/validation checks. The suite reuses our deployed API contracts,
standalone Angular UI and EPAM .NET10/Reqnroll/TUnit/Playwright Screenplay layers.
No application/backend/Terraform changes or new test packages were introduced.

- Raw participant GET is side-effect free and includes draft cart values.
- UI consolidated Grand Total includes only submitted selections, with the
  existing ordinary Owner projection handled by the application.
- Raw empty group is tested BEFORE Owner-session bootstrap. UI empty group can
  legitimately show the automatically-created Owner session.
- Guest tokens are opaque random values, not Cognito JWTs. API authorization
  denials use the appropriate Cognito/guest/no-token identity.
- Dates are dynamic tomorrow 13:00 UTC, an available storefront slot; @level1 UI contexts use UTC so booking
  selection matches fixture timestamps. New run-owned tables avoid overlap with
  standing bookings; fresh browser contexts isolate guests/scenarios.
- Fixed expected prices are 8.25 and 12.40, not copied from the returned totals.

## Requirement coverage

All rows passed in the combined live run on 2026-11-01. The browser used the current Angular app on localhost; API requests used the deployed AWS backend.

| Requirement | Evidence | Independent expected observation |
| --- | --- | --- |
| Raw empty group | `A group without joined sessions has no participants or charges` | No Owner bootstrap before the read; participants=[], total=0. |
| Participants, items, exact sums and statuses | `The Owner sees confirmed draft and empty carts with exact amounts` | Three exact guest/session identities; quantities/prices/item counts and totals16.50+12.40+0=28.90; draft included in API total. |
| Multiple dish mapping | `Every distinct dish in a guest cart is retained` | Both independent dish IDs/items retained exactly once;8.25+12.40=20.65. |
| Draft update and unchanged other carts | `Changing a draft cart updates its amount without changing other carts` | Quantity increments to2; draft24.80, rawoverall41.30; other carts remain unchanged. |
| Other Customer denied | `Another identity cannot read the Owner participant list (another customer)` | 403, expected error, no participant payload. |
| Guest denied | `Another identity cannot read the Owner participant list (a guest)` | Opaque guest token rejected401, no participant payload. |
| Waiter denied | `Another identity cannot read the Owner participant list (a waiter)` | 403, expected error, no participant payload. |
| Anonymous denied | `Another identity cannot read the Owner participant list (an anonymous visitor)` | 401, no participant payload. |
| Missing group | `The Owner is told when the requested group does not exist` | 404 and expected missing-group error, no participant payload. |
| Real UI booking/invitation/guest carts | `Owner sees submitted, choosing and empty guest carts` | Real Owner login/search/booking/visible invite, separate guest contexts and cart actions; exactnames/dishes/quantities/states/subtotals; consolidated16.50. |
| Automatic UI updates and submitted-only total | `Owner sees cart changes without reloading the group` | No Owner Refresh/reload; draft12.40->24.80 while consolidated16.50, then41.30 after individual guest confirmation. |
| UI without joined guests | `Owner sees an empty group before any guest joins` | Bootstrap-created empty Owner card, no Guest cards, zero consolidated total. |

There are 9 API cases (the permission outline expands to 4) and 3 UI cases.
Features: `Features/GroupParticipantsApi.feature` and
`Features/GroupPreorderLevel1Ui.feature`, tagged `@level1` and layer-specific
`@level1api`/`@level1ui`. Existing starter feature files were not replaced.

## Data, cleanup and artifacts

Use four existing dedicated accounts: Owner Customer, another distinct Customer,
standing Waiter, Admin. Each is authenticated and its role validated before
creating fixtures. No Cognito accounts are registered. The Waiter's location
and assignment are preserved. Admin creates a new table plus two test dishes;
the owned dishes are temporarily linked to that existing test location menu.

Cleanup removes only recorded fixture guest IDs, cancels its reservation, then
removes owned dishes/table. Failures fail the scenario instead of silently
claiming a clean run. A bounded table-delete retry handles only the specific
stale reservation-index rejection after successful cancellation; other errors
remain failures. The shared AQA-Sample sweep remains disabled. Cancelled
reservation/group history is retained where no hard-delete route exists; this
uses the assignment's documented unique-data fallback, not full physical erase.

No setup POST is retried. Setup-only reads may wait for newly written table/menu
visibility or transient invitation-read 5xx; actual API assertion requests do
not retry error statuses. Snapshot waits target exact expected IDs/counts,
amounts and submitted state. UI uses retrying DOM assertions, not fixed sleeps.

Level 1 raw network traces are discarded to avoid retaining login/invite tokens.
Masked screenshots are captured on UI/cleanup failure before context closure.
The runner redacts known account values, JWT/Bearer strings and invitation paths
in retained text reports. Artifacts remain private and ignored; text redaction
is best-effort, not a guarantee for arbitrary embedded/binary payloads. CI uploads
only the runner's curated redacted reports/logs and masked screenshots, not raw
HTML/traces. New scenarios are neither WIP nor skipped to make them green.

## Verification on 2026-11-01

| Check | Actual result |
| --- | --- |
| Release build, no incremental/no restore | Passed: 0 warnings, 0 errors |
| Level1 discovery via the scoped runner | Passed: 12 cases found, none executed |
| Pinned Gherkin lint 4.2.4 | Passed for new feature files |
| Read-only formatter | Passed after fixing one whitespace issue |
| Runner validation | Python syntax, synthetic TRX completeness/failure/skip checks and redaction checks passed; these are NOT application E2E passes |
| Workflow checks | YAML/Bash syntax passed; still manual opt-in with no schedules/deployment |
| Independent review | No blocking source-level requirements/assertion gaps; all new TUnit assertions awaited, exact outcomes, no truthiness-only cases |
| Live API-only run | Passed: 9/9, 0 failed, 0 skipped; verified TRX under `TestResults/level1-20261101T084241Z-095d3edc/` |
| Live UI-only run | Passed: 3/3, 0 failed, 0 skipped, current localhost frontend with AWS backend; verified TRX under `TestResults/level1-20261101T085956Z-9c5abd77/` |
| Final combined live run | Passed: 12/12, 0 failed, 0 skipped; verified TRX under `TestResults/level1-20261101T090216Z-6b3a6989/` |
| CloudFront live run reported by operator | Passed: 12/12, 0 failed, 0 skipped; verified runner summary under `TestResults/level1-20261101T092802Z-e16be373/` |
| Post-fix Owner refresh browser run | Passed: 3/3, 0 failed, 0 skipped; verified fresh TRX under `TestResults/level1-20261101T191907Z-8ff12ae7/` |

A later Level 1 regression had failed once after a guest submitted a draft cart:
the Owner still showed `Still choosing`. That run occurred before the
guest-removal backend fix in `GuestRemovalSyncFix.md`. The old group-index query
could supply a stale `isSubmitted` value; the deployed repository now verifies
each index candidate against a strongly consistent base-table read. A focused
repository test, `GetAll_UsesCurrentSubmissionStatusInsteadOfStaleIndexStatus`,
passed 4/4 with the other guest-session repository tests. It supplies a stale
index candidate and a current submitted record and asserts the current status
is returned. The post-fix live browser run above passed all three Level 1 UI
cases, including the no-reload status change. The old failed run did not retain
the API response, so its exact cause remains an inference from the code and
subsequent evidence.

The user filled the ignored private `.env`. The runner checked populated names
without displaying account values; no account value or token is recorded here.
The live runner passes local values to its child process through the environment
because the framework's dotenv loader did not discover the file with probing
disabled. This change keeps values out of command arguments and retained logs.

The first diagnostic runs revealed fixture mistakes: a Waiter-only table read and
delete had used Customer/Admin tokens, the guest dish route was stale, and the UI
chose an unavailable 12:00 slot. The corrected fixture uses the Waiter token,
the backend's `/guest/sessions/{id}/dishes` route, and a 13:00 slot. The browser
flow waits for the Owner profile and review dialog before confirmation.

Several interrupted diagnostics created six-digit test tables before the
fixture's table cleanup was fixed. Failure screenshots show such tables in the
test location. Their exact IDs were not journaled, so their ownership cannot be
proven from the reports; they were left untouched instead of deleting a broad
name/number pattern. The passing final run reports successful scoped cleanup,
but it does not remove earlier diagnostic leftovers. Review these tables in AWS
before removing any of them.

At first inspection, CloudFront served `main-A4VMWMPS.js` while the local build
served `main-PTAGWNY2.js`. The operator then published the updated frontend and
ran all 12 cases successfully against CloudFront. That later live run supersedes
the earlier publication gap. It does not cover every existing visual detail:
subsequent screenshots exposed a long-dish-name price overflow and a missing
order-item-status fallback in older cart data.

## Run instructions

From repository root:

```bash
python3 scripts/run-aqa-level1.py --check
python3 scripts/run-aqa-level1.py --list
# Run against the UI_URL from private .env:
python3 scripts/run-aqa-level1.py --live
# Or test the current local Angular dev server without editing .env:
UI_URL=http://127.0.0.1:4200 python3 scripts/run-aqa-level1.py --live
# Narrow reruns:
python3 scripts/run-aqa-level1.py --live --scope api
python3 scripts/run-aqa-level1.py --live --scope ui
```

`--check` authenticates nobody and prints missing names only. The live runner
first discovers selected cases, then requires a fresh parseable TRX whose total
matches that discovery and whose cases all pass without skips. Reports are in
`aqa/restaurant-aqa-framework/TestResults/level1-<timestamp>-<run-id>/`.
The original Reqnroll formatter's fresh report is copied into that run directory;
raw original formatter outputs stay private under TestResults. Missing/partial
reports or cleanup/test process errors remain failures. CI uses the same runner
and OWNER/OTHER_CUSTOMER/WAITER/ADMIN repository secrets.

## Agents and skills

Three temporary agents were actually spawned: `aqa_level1_api` (API/model/fixture/
APIcases), `aqa_level1_ui` (page objects/browser tasks/questions/UIcases), and
`aqa_level1_review` (independent read-only requirement/assertion/privacy review).
The main agent owns coordination, runner/configuration/CI, strict cleanup,
artifacts, compilation and documentation. These are not custom-agent configs.
The installed code-testing and .NET run-tests guidance was used; clear analyzer
build failures were repaired without weakening diagnostics. The review identified
and closed the distinct-two-dish mapping gap. The live result above comes from
the final combined run and its TRX, not compilation or discovery.

## Remaining acceptance

Review likely leftover test tables and dishes before any targeted deletion.
The user published the frontend; the agent did not deploy, commit, or push this
AQA phase. Levels2–3 and bonus work remain separate.
