# Backend homework — level 3

## Scope and status

Level 3 removes a guest session and returns the updated participant snapshot:

`DELETE /guest/groups/{groupReservationId}/sessions/{guestSessionId}`

The baseline from completed level 2 is **231 tests passed, 0 failed, 0 skipped**.
**Level 3 complete:** implementation/review, full-suite execution, AWS deployment,
and both agreed manual acceptance checks are complete, as recorded below.
No frontend changes are in scope.

## Behavior

- Only the reservation Owner, identified by trusted Cognito claims, may remove
  a guest. The same rule applies to Admin and Waiter accounts.
- Missing guest session returns 404; a session belonging to another group
  returns 400. Missing parent group/reservation returns 404.
- The Owner session cannot be removed (400). Removal requires an open group
  and an active reservation; submitted/closed groups are rejected (409), so
  removal does not deliberately change membership of a finalized order.
- Submitted and draft guests may both be removed while the group is open.
- HTTP 200 returns lower-camel `participants` and `total`, using the same dish,
  name, Owner/Guest type, submission status, and decimal-total mapping as level 1.
- The response excludes the removed ID explicitly, since a DynamoDB secondary
  index may briefly show stale data. The remaining snapshot is built before
  deletion so a mapping failure does not occur after a successful mutation.

## Verification and deployment

Run the full suite once after implementation; detailed success/error cases
belong in automated tests rather than repeated manual checks:

```bash
dotnet test backend/restaurant-backend-app/Tests/APIHandler.Tests.csproj
python3 scripts/check-deployment.py
python3 scripts/build-lambdas.py
terraform -chdir=infra/terraform validate
terraform -chdir=infra/terraform plan -out=backend-level3.tfplan
```

Review the plan before applying. This route and DynamoDB delete permission
already exist. The API Lambda package/version/alias may change; database tables,
buckets, and Cognito resources must not be replaced for this homework.

```bash
terraform -chdir=infra/terraform apply backend-level3.tfplan
```

## Two manual acceptance checks only

Use an active future group booking whose group order has **not** been submitted.
The finalized level 2 test booking is unsuitable. Reuse an eligible open booking
if available; otherwise create one. Join with a test guest and add one salad
($8 with unchanged demo data). Leave the Owner's ordinary preorder separate.

To retrieve a saved booking's invite link without relying on the one-time
confirmation screen, run `python3 scripts/check-group-preorder.py --invite-link`
as Owner with its reservation ID. That helper also ensures the Owner session;
for the current implementation, its `owner-` suffix gives the group ID. Keep
invitations and identifiers local.

1. **Owner success.** Run
   `python3 scripts/check-group-sessions.py --remove-guest`, sign in as Owner,
   and enter the group ID. At the guest-session prompt press Enter to choose
   from a numbered list, then select the test guest. Expect HTTP 200: that guest
   is absent, other participants remain, and the total falls by their cart total
   ($8 for one salad). With only an empty Owner session remaining, total is 0.
   Keep the printed selected guest ID locally for the second check.
2. **Non-owner rejection.** Run the same command as the Waiter or another
   non-owner account. Enter the same group ID and enter the selected guest ID
   directly, rather than pressing Enter to list guests. Expect HTTP 403 before
   session lookup, even though the guest has already been removed. This verifies
   the DELETE endpoint's permission check, not just GET permissions.

Stop after these two checks. Missing/wrong-group session IDs, malformed paths,
anonymous calls, Owner protection, inactive groups, and mapping failures are
covered automatically; do not add a manual matrix of those cases.

## Verification limits

The response is an optimistic snapshot, not a lock across concurrent guest edits
or group submission. Validation and deletion are separate repository operations;
an operation racing group closure/submission may pass an earlier state check.
Other secondary-index rows can also lag. This change does not redesign all
session/order writers or promise complete snapshot isolation.

## Results

Implementation and independent review are complete. Added **32 focused cases**
covering removal, snapshot mapping, authorization, invalid targets, lifecycle
guards, and failures before mutation. The full test project compiles with
**47 warnings, 0 errors**. No VSTest execution was attempted in the agent
environment because its local-socket restriction is already established.

Deployment contract checks pass (**69 API methods, 13 tables**), along with
Terraform formatting, whitespace checks, helper Python syntax/help, and an
offline mocked helper-selection check (no network or AWS changes).

The operator ran the complete backend suite after implementation: **263 passed,
0 failed, 0 skipped**, including the previous 231 cases and 32 new cases.
Test duration was 0.5 seconds; the build succeeded with 66 warnings in 2.8
seconds.

The operator applied the reviewed plan successfully: **1 added, 2 changed,
1 destroyed**. The replacement was the API Lambda package S3 object.
Deployment is complete. Both agreed manual acceptance checks passed:

1. Owner removal returned **HTTP 200**. The selected guest had an empty draft
   cart (total 0). The response excluded that guest, preserved the submitted
   guest's one salad and empty Owner session, and correctly retained group total
   **8**. This verifies removal and the returned snapshot; a nonzero manual
   total deduction was not exercised, and is covered by automated tests.
2. The Waiter directly called the **DELETE** endpoint for the same group and
   removed guest ID and received **HTTP 403**, confirming owner authorization
   before target lookup. An earlier helper list request also returned 403, but
   that GET rejection was not counted as the DELETE acceptance check.

No further manual checks are required for this level. No real guest names,
session IDs, group IDs, credentials, or invite tokens are recorded here.
