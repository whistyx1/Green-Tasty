# AQA Direction 1 — level 3

**Scope:** remove a guest from an open group pre-order, recalculate the Owner's
view, and prove the removed dishes do not reach the restaurant. The suite uses
our deployed backend and frontend through the existing .NET 10 Reqnroll,
Screenplay, TUnit and Playwright framework. A later live report revealed that
an open removed guest page could keep editing and the Owner could see that
guest reappear. The follow-up fix changes the API and guest page; see
`GuestRemovalSyncFix.md`.

## Requirement coverage

| Requirement | Scenario and assertion |
| --- | --- |
| Remove one of several guests and recalculate total | `Owner removes one choosing guest and the total is recalculated` checks the removed ID is absent, the confirmed guest remains with salad ×2, and the persisted total is $16.50. |
| Remove a guest whose cart was confirmed | `Owner removes a guest who confirmed a cart` checks the draft guest remains with pasta ×1 and the persisted total is $12.40. |
| Remove the last guest | `Owner removes the last guest` checks `participants: []` and `total: 0`. The public API may also show a separate Owner session if one was created; this API scenario does not create one. |
| Removed guest stays removed | `A removed guest cannot add dishes again` uses the old session token, expects HTTP 401, and checks the Owner snapshot repeatedly for several seconds to catch a delayed reappearance. |
| Reject a guest from another group | `A guest from another group cannot be removed` creates two independent group bookings, expects HTTP 400, and checks both groups remain unchanged. |
| Reject a nonexistent guest | `A nonexistent guest cannot be removed` expects HTTP 404 and checks the real guest and total remain unchanged. |
| Only the Owner may remove | Four `Only the Owner may remove a guest` examples check another Customer and Waiter receive 403, and a guest and anonymous caller receive 401; the real guest remains. |
| Full Owner-to-Waiter flow | `Owner removes a confirmed guest and the Waiter sees only the remaining order` creates a booking in the browser, has guests choose and submit, removes a confirmed guest through the confirmation dialog, waits for the guest's inactive page and absence of **Create New Pre-Order**, checks the Owner's total falls from $37.15 to $20.65, sends the order, and checks the Waiter sees salad ×1 and pasta ×1. |
| Removed guest's open tab | `Removed choosing guest loses the open menu without a reload` removes a draft guest, waits for the inactive message in that guest's open tab, reloads the guest tab to confirm it stays inactive, and reloads the Owner view to check the guest remains absent. |

The backend's current behavior for removing the last guest is HTTP 200 with
an empty guest list and zero total. Removing a guest from another group is 400;
a missing guest is 404. A non-Owner is rejected with 403 if authenticated as
another Customer or Waiter, and 401 as a guest or anonymous caller.

## Isolation and reliability

Every scenario creates its own table, dishes and future booking. The cross-group
scenario creates a second booking on its owned table at a different time. Only
tracked guest sessions are eligible for removal or cleanup; a removed guest is
not deleted a second time. Cleanup cancels owned bookings and removes owned
dishes and tables. Cancelled reservation/group history remains because the
public API has no hard-delete route. No shared-prefix sweep is used.

The Waiter dashboard reads reservations and then fetches their orders
separately. Before opening it, the browser task waits for the submitted order to
be readable by the assigned Waiter. This prevents a newly created card from
loading once with `Pre-order: 0 dishes` while the order index is catching up.
No write is retried. The runner retains redacted structured reports and masked
failure screenshots; private `.env` values are never printed.

## Run

From the repository root, with the ignored AQA `.env` populated:

```bash
python3 scripts/run-aqa-level3.py --check
python3 scripts/run-aqa-level3.py --list
python3 scripts/run-aqa-level3.py --live --scope api
python3 scripts/run-aqa-level3.py --live --scope ui
```

The runner filters to `@level3` only, discovers the selected cases first,
creates a fresh ignored `TestResults/level3-*` directory, and requires a fresh
TRX with every discovered case passed and none skipped. The manual AQA workflow
offers Level 3/API/UI choices; its default remains Level 1. No live CI dispatch,
commit, push or deployment was performed for this level.

## Verification

The initial API-only live run passed **9/9**
(`TestResults/level3-20261101T170419Z-45cd5817/`). The initial UI-only live run
passed **1/1** (`TestResults/level3-20261101T171125Z-5016670a/`). Each result
was verified from its fresh TRX with zero failures and zero skipped cases.
The .NET Release build passed with zero warnings/errors. The API and UI slices
were run separately; no combined Level 3 or live CI run is claimed. Pinned
Gherkin lint, formatter verification, and workflow YAML parsing passed.

After the shared AQA fixture changes, the Level 2 UI regression passed **1/1**
(`TestResults/level2-20261101T171513Z-71d92e47/`). A Level 1 UI regression
run passed **2/3** (`TestResults/level1-20261101T171640Z-2115de79/`). Its
`Owner sees cart changes without reloading the group` case observed a successful
guest cart confirmation, but the Owner page continued to show that guest as
`Still choosing` through its polling window. That failed run is not reported as
green. The subsequent guest-removal backend change verifies current session
records instead of using stale group-index values. A focused repository test
and a fresh post-fix Level 1 UI run (3/3) passed; see `AqaLevel1.md` for the
evidence and the limit of this diagnosis.

After the guest-removal synchronization follow-up, the rebuilt live Level 3
API suite passed **10/10**
(`TestResults/level3-20261101T183425Z-65b65574/`) and the live browser
suite passed **2/2**
(`TestResults/level3-20261101T185717Z-50224ccb/`). The first browser attempt
failed; the corrected assertion and read-only Waiter dashboard refresh are
described in `GuestRemovalSyncFix.md`. Neither the first failure nor the
earlier Level 1 intermittent failure is hidden by the later green run.

A later manual report exposed a removed-session reload gap. After the frontend
follow-up, the live Level 3 browser slice passed **2/2** again
(`TestResults/level3-20261101T194758Z-1b54e303/`), with an added assertion
that the guest remains inactive after reloading. Details and limitations are
in `GuestRemovalSyncFix.md`.

The submitted-guest follow-up was covered by a live browser run that passed
**2/2** (`TestResults/level3-20261101T203602Z-e1882714/`). That version
attempted the success-screen **Create New Pre-Order** action after Owner
removal. The current test instead waits for the inactive page and verifies the
button is absent: the app can remove the button between a visibility check and
a click. The repaired UI slice passed **2/2** twice, and the full Level 3 suite
passed **12/12** in the later local runs recorded in [FinalRegression.md](FinalRegression.md).
The screenshots and session-vs-invitation limitation are in
[GuestRemovalSyncFix.md](GuestRemovalSyncFix.md).
