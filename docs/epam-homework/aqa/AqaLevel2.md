# AQA Direction 1 — level 2

**Scope:** automated consolidation and submission of the group pre-order in the
deployed Green-Tasty application. Level 1 scenarios are unchanged; the level 2
runner selects only `@level2` cases. The live tests exposed a Waiter reservation
list failure, which was fixed in the backend and deployed. Frontend application
code was unchanged for this task.

## Requirement coverage

| Requirement | Evidence |
| --- | --- |
| Owner and confirmed guests contribute dishes; identical dishes merge; draft selections stay out | `Confirmed guest and Owner dishes combine while draft dishes stay out` asserts the API result and Waiter-readable order have exactly salad ×3 and pasta ×1, and no draft pasta portions. |
| Persisted order and group statuses | `Confirmed guest and Owner dishes combine while draft dishes stay out` asserts `SubmittedByCustomer` and `Submitted`. |
| Real Owner and Waiter views | `Owner confirms the group order and the Waiter sees the same dishes` books in the UI, has guests select and confirm carts in the UI, submits through the Owner confirmation modal, checks the sent message, exact quantities and total, then signs in as the assigned Waiter and checks the pre-order view. |
| No confirmed guest cart | `The Owner cannot send a group with no confirmed cart` expects HTTP 400 and `No submitted sessions.` |
| Empty cart cannot become confirmed through the public API | `An empty guest cart cannot become a confirmed cart` expects HTTP 400 and `The cart is empty`. The service's separately existing backend test `Submit_InvalidInputFailsBeforeAnyWrite(noDishes)` checks the otherwise unreachable confirmed-empty state; this is **not** claimed as an AQA E2E pass. |
| No order for the reservation | `The Owner cannot send a group without an order` expects HTTP 400 and `No order found for this reservation.` |
| Missing group | `The Owner cannot send a group that does not exist` expects HTTP 404 and `Group reservation was not found`. |
| Reservation ID mismatches the group | `The Owner cannot pair a group with a different reservation` expects HTTP 400 and `Reservation does not belong to this group.` |
| Repeat Submit | `A submitted group cannot be sent a second time` expects HTTP 409 and `Only an open group with an active reservation can be submitted.` |
| Another Customer, guest, Waiter, anonymous caller | Four expanded cases of `Another identity cannot send the Owner's group pre-order` expect 403 for authenticated non-Owners and 401 for guest/anonymous callers; the group remains Open. |

The application's public API has no hard-delete route for a reservation or
group. A group whose linked reservation is physically missing cannot be
created by an isolated black-box test without changing storage directly. The
existing backend service test `Submit_MissingParentIsNotFoundWithoutWrites`
covers that internal branch. The live AQA scenario covers a nonexistent group,
which is the reachable public 404 case. These limits are explicit rather than
using a test-only production endpoint or deleting unrelated data.

## Test architecture and isolation

New Reqnroll features are `Features/GroupPreorderLevel2Api.feature` and
`Features/GroupPreorderLevel2Ui.feature`, tagged `@level2api`/`@level2ui`.
The tests reuse the Level 1 fixture, Screenplay actor, API clients, Playwright
guest pages, and scoped cleanup. Every case creates its own future reservation,
table and two uniquely named dishes. Submitted groups lock guest deletion; the
cleanup cancels the owned reservation and removes its dishes and table, leaving
only reservation/group history for which no hard-delete route exists. The
shared-prefix sweep stays disabled. Private configuration remains in the ignored
`.env`; the runner redacts structured reports and retains only masked failure
screenshots, not raw network traces.

The successful UI scenario signs in as the assigned Waiter, finds the new
reservation on the normal Waiter dashboard, clicks its Pre-order button and
checks the same submitted dishes. During verification, `GET /reservations`
returned HTTP 500 when an older cancelled reservation referred to a removed
test table. The backend now omits such unrenderable cancelled history from the
Waiter list while still reporting conversion failures for active reservations.
A backend regression test covers both cases. The focused Terraform apply
replaced only the API Lambda package and updated that Lambda and its alias;
the other Lambda packages and infrastructure were unchanged.

## Running and reporting

From the repository root, with private AQA configuration already populated:

```bash
python3 scripts/run-aqa-level2.py --check
python3 scripts/run-aqa-level2.py --list
python3 scripts/run-aqa-level2.py --live
python3 scripts/run-aqa-level2.py --live --scope api
python3 scripts/run-aqa-level2.py --live --scope ui
```

The script discovers the selected cases, runs only those cases, writes an
ignored `TestResults/level2-<timestamp>-<run-id>/` report directory, and rejects
missing, failed or skipped TRX results. CI's manual `aqa-tests.yml` workflow
offers level 2 and API/UI slices while retaining level 1 as its default. The
quality workflow compiles, formats, lints and discovers without contacting AWS.
No live CI dispatch, commit, or push was performed here. The focused backend
repair was deployed as described above.

## Verification

The final API-only live run passed **11/11**
(`TestResults/level2-20261101T135554Z-c7e2fd97/`); the final UI-only live run
passed **1/1** (`TestResults/level2-20261101T142018Z-bdf6bec5/`). Each runner
verified a fresh TRX with zero failures and zero skipped cases. Level 1's full
live regression run then passed **12/12**
(`TestResults/level1-20261101T135839Z-b3e5450b/`). The .NET Release build
passed with zero warnings/errors and pinned Gherkin lint passed. No combined
Level 2 run or live CI workflow dispatch is claimed; the API and UI slices were
run separately after their respective final changes. The backend regression
test passed both cancelled and active reservation cases; the full backend suite
passed **273/273**. A final Terraform plan found **no changes**, and the AQA
format verification passed.

A later cross-level live regression on 2026-11-02 found an intermittent
Owner-to-Waiter UI failure: the submitted order was readable through the
Waiter's API, but the dashboard card did not expose its Pre-order button in
two full Level 2 runs (11/12 each). A focused rerun passed 1/1. After the
frontend order-loading fix was deployed on 2026-11-03, two new full Level 2
runs passed **12/12** each, with zero API Lambda throttles or errors in their
CloudWatch windows. See [FinalRegression.md](FinalRegression.md) for the exact
reports and limits of that verification. The earlier results above remain
historical checkpoints.
