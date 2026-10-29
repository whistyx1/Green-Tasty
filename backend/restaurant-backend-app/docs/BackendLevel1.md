# Backend homework — level 1

Only level 1 is in scope here. Test it manually before implementing owner-session
lookup/group submission (level 2) or completing removal behavior (level 3).

## Behavior

`GET /guest/groups/{groupReservationId}/sessions` takes the reservation Owner's
Cognito ID token and returns `participants` plus a group `total`. Every existing
session is included, whether submitted or still choosing. Participant totals are
the sum of each dish's current price times its quantity; group total is their sum.
The response uses lower-camel JSON properties. Session `type` is `Owner` or `Guest`.

Owner means `Reservation.CustomerId` matches the authenticated Cognito `sub`.
It does not mean the Cognito Admin role. The endpoint checks ownership before
reading sessions or their dishes. It does not create, submit, or delete anything.

Current booking code does not automatically create Owner sessions. An Owner row
appears only if that session already exists. An empty group therefore normally
starts as `{ "participants": [], "total": 0 }`. Fixing Owner-session creation is
part of the later lifecycle work, not a side effect of this GET.

## Build, check, and deploy

Verification so far: the operator ran the focused command below and confirmed
**22 tests total, 22 succeeded, 0 failed, 0 skipped**. Test duration was 0.5 seconds;
the build succeeded with 71 warnings in 17.7 seconds. Local API compilation and
the route/table contract checks also passed. The operator applied the update and
confirmed **4 added, 8 changed, 4 destroyed**: replacement of the API Gateway
deployment snapshot and three Lambda package S3 objects.

Deployed manual checks confirmed by the operator:

- Owner reading a group before guests join: HTTP 200, empty participants, total 0.
- Owner reading two guest carts: HTTP 200; two salads at $8 give total 16 with
  `isSubmitted: true`; one pasta at $14 gives total 14 with `isSubmitted: false`;
  both participants appear and group total is 30.

- Authenticated non-owner using the same group: HTTP 403, owner-only error.
- Anonymous request to the same group: HTTP 401, Unauthorized.
- Authenticated request for a generated nonexistent group: HTTP 404, group not found.

**Level 1 status: implemented, 22 focused automated tests passed, deployed, and
the manual acceptance checks above passed.** This records the level 1 checkpoint;
subsequent level 2 work is tracked in [BackendLevel2.md](BackendLevel2.md).
No real invite tokens, participant IDs, emails, or credentials are recorded here.

Run from the repository root. Keep the existing xUnit packages/framework.

```bash
dotnet test backend/restaurant-backend-app/Tests/APIHandler.Tests.csproj --filter 'FullyQualifiedName~GetGroupSessionsTests|FullyQualifiedName~GetGroupSessionsHandlerTests'
python3 scripts/check-deployment.py
python3 scripts/build-lambdas.py
terraform -chdir=infra/terraform validate
terraform -chdir=infra/terraform plan -out=backend-level1.tfplan
```

Review the plan: the API definition/deployment/stage and API Lambda package/
version/alias may change. A new API Gateway deployment replaces the old immutable
snapshot; that is expected. Database tables, buckets, Cognito users, and demo data
must not be destroyed/replaced. ZIPs for unchanged functions should keep the same
hash when built with the same SDK/dependencies. Review any unrelated change.

Once reviewed, apply the saved plan:

```bash
terraform -chdir=infra/terraform apply backend-level1.tfplan
```

The existing frontend does not need rebuilding for this API endpoint. Its Owner
review screen belongs to the later frontend homework. Never use `syndicate clean`
for this Terraform deployment.

## Manual acceptance checks

1. Sign in as the separate Customer account (not Admin or Waiter). Book a future
   table for **4 people**, within table capacity. Obtain the reservation's
   invite link. If yesterday's reservation is expired, create a new one.
2. Before guests join, run `python3 scripts/check-group-sessions.py`. Enter the
   Customer/Owner credentials and paste the invite link. Expect HTTP 200 with an
   empty participants list and total 0 when no sessions exist.
3. Open the invite in a second browser/private session. Join with a test guest
   name and add two demo salads at $8 each. Submit that guest cart.
4. Open the invite in a separate isolated guest session (use another browser so
   guest session storage is not reused). Add one demo pasta at $14 and leave that
   cart **unsubmitted**.
5. Rerun the helper as Owner. Expect both participants, correct names/quantities,
   first `isSubmitted: true`, second `isSubmitted: false`, participant totals 16
   and 14, and group total **30**. Draft carts count toward this listing; filtering
   to submitted carts applies to level 2, not level 1. Existing extra sessions
   or dishes legitimately change the expected totals. Use a four-person booking:
   the existing invite lookup rejects a full group, so leave one guest place
   unused while reusing the helper's invite-link lookup.
6. Rerun the same helper with the **same invite link**, but sign in with an
   unrelated Customer or the Waiter account. Expect HTTP 403, with no carts
   returned. An Admin account also has no automatic owner access.
7. Run `python3 scripts/check-group-sessions.py --anonymous`, paste the same
   invite link, and expect HTTP 401. This uses no sign-in token.
8. Run `python3 scripts/check-group-sessions.py --missing-group`, sign in, and
   expect HTTP 404. The helper generates a random nonexistent ID without exposing
   your account IDs or tokens. If an invite is already inactive/full and the
   existing invite endpoint rejects it, use `--group-id` with the known group ID
   to test the listing directly.

The helper only signs in and reads data. It does not create sessions or populate
carts for you; do those steps through the existing website so the checks remain
manual. Passwords, Cognito tokens, and invite links are not printed or saved.
Expected rejection checks exit nonzero because their HTTP status is not 200.
Do not paste real tokens, invite links, credentials, or private guest data into
chat. Report HTTP status and totals, or use redacted test-only screenshots.
