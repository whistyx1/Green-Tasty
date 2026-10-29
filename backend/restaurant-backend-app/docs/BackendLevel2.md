# Backend homework — level 2

Level 1 was deployed and manually checked before this work. The operator ran
the **entire backend suite before level 2: 182 passed, 0 failed, 0 skipped**.
The build succeeded with 66 warnings. This is the regression baseline, not the
result for the new code. Level 3 remains outside this change.

## Implemented behavior

- `GET /reservations/{reservationId}/guest-session-id` requires the reservation
  Owner's Cognito ID token. It returns a stable `guestSessionId`, creating the
  Owner's empty session for older bookings if needed. Repeating the request
  must return the same ID. This GET therefore can write session metadata.
- `POST /group-reservations/{groupReservationId}/submit` takes
  `{ "reservationId": "..." }` and the Owner's Cognito ID token. It merges
  submitted participant carts into the existing reservation preorder, combines
  equal dish IDs, and excludes draft carts. It preserves the Owner's existing
  ordinary preorder. Its response is the final list of `dishId`/`quantity` pairs.
- The order becomes `SubmittedByCustomer` and the group becomes `Submitted`
  together. Repeating a successful submission must not add quantities again.

The owner-session ID is an identifier, not a login token. This homework does not
change guest-token authentication or add an Owner cart editor. The Owner can use
the existing reservation preorder UI. Their separate session begins empty;
their ordinary preorder is not copied into it and counted a second time.

Missing reservations/groups must return 404. No submitted carts, no dishes in
submitted carts, and a missing order must return 400. Unrelated customers must
receive 403; anonymous requests must receive 401. Invalid requests must not
change the order or group status.

## Verification and deployment

After the implementation and review, run the full suite again from repository
root. Do not rely only on the new tests:

```bash
dotnet test backend/restaurant-backend-app/Tests/APIHandler.Tests.csproj
python3 scripts/check-deployment.py
python3 scripts/build-lambdas.py
terraform -chdir=infra/terraform validate
terraform -chdir=infra/terraform plan -out=backend-level2.tfplan
```

Review the saved plan before applying. Lambda ZIP objects and the immutable
API Gateway deployment may be replaced. Database tables, Cognito pools, buckets,
and existing application data must not be replaced or destroyed for this change.

```bash
terraform -chdir=infra/terraform apply backend-level2.tfplan
```

No frontend rebuild is required for these API endpoints.

The Lambda IAM policy gains `dynamodb:ConditionCheckItem`, scoped to the
Reservations and GuestSessions tables, so the transaction can verify their
unchanged state. Existing item permissions authorize its order/group updates;
there is no separate `dynamodb:TransactWriteItems` IAM action. See
[AWS transaction permissions](https://docs.aws.amazon.com/amazondynamodb/latest/developerguide/transaction-apis-iam.html).

## Optional extended manual reference

The completed acceptance results are recorded under Status below. This broader
reference is not a list of remaining required checks; no further manual testing
is required to close level 2. Its four-salad example differs from the actual
three-salad scenario used for acceptance.

Use future reservations more than 30 minutes away and test-only guest names.
Keep IDs, invitations, and credentials local. Find reservation IDs in the
browser's Network tab in the `GET /reservations` response; find the associated
group ID in the `GET /guest/invites/...` response when opening the invite.
If the booking confirmation is already closed, run
`python3 scripts/check-group-preorder.py --invite-link` as Owner and enter the
reservation ID. It checks ownership, retrieves the existing token, and prints
the deployed invitation locally; it does not create another booking or submit
an order. It may ensure empty Owner-session metadata during its ownership check.

1. Check an ordinary reservation first: create a preorder, edit its quantities,
   submit it, and unsubmit it. This checks the existing customer flow.
2. Create a separate four-person group booking. Run
   `python3 scripts/check-group-preorder.py` twice as its Owner. Expect HTTP 200
   and the same `guestSessionId` both times. Verify that creating this session
   does not consume an extra guest place: three guests can still join.
3. Use another group's invite for a controlled submission example. In the
   Owner's ordinary preorder add one salad. Guest A adds two salads and submits;
   Guest B adds one salad and submits; Guest C adds one pasta and leaves it draft.
   Keep the Owner's ordinary preorder unsubmitted before group submission.
4. Run `python3 scripts/check-group-sessions.py --group-id GROUP_ID` as Owner.
   Verify that all guest carts remain visible, including the draft. The empty
   Owner row should not change their totals.
5. Run `python3 scripts/check-group-preorder.py --submit`. This command changes
   the selected order/group. Enter the reservation and group IDs when prompted.
   Expect HTTP 200 with **four salads**, and no pasta from Guest C's draft.
   The existing order must show `SubmittedByCustomer`; the group must show
   `Submitted`. Check in the waiter view that the order remains visible and can
   be processed.
6. Repeat the submit command. Expect a rejection with unchanged quantities,
   rather than eight salads. A submitted group's guest cart must not remain
   editable.
7. On separate fresh bookings, test another customer (403), anonymous access
   (`--anonymous`, 401), nonexistent IDs (404), mismatched reservation/group
   IDs (400), no submitted guests (400), and submitted guests but no existing
   order (400). Confirm that rejected submissions leave stored data unchanged.

Creating an empty Owner session can occur only after ownership is checked.
Neither helper saves credentials or tokens. Share status codes and test totals,
not complete responses containing real private data.

## Practical limits

Group membership is read through an existing DynamoDB secondary index, whose
updates are eventually consistent. A just-joined or just-submitted guest may take
a moment to appear. Refresh the Owner snapshot and verify the expected submitted
guests before sending the group order. The transaction checks the carts it read;
it does not lock membership against a new guest submitting at that same instant.
This is a remaining concurrency limit, not proof of complete snapshot isolation.

If a network request times out, check the order/group status before retrying:
the server might already have completed it. A successful submission cannot be
appended again by the same endpoint. Existing ordinary-order and guest-cart
writers retain their previous concurrency behavior; this homework does not
redesign every writer. After a group closes concurrently with an Owner lookup,
empty Owner metadata can still be created, but lookup cannot submit an order.

## Status

**Level 2 complete: implemented, independently reviewed, deployed to AWS, and
its main flow manually verified.** Level 3 remains separate. No additional
manual tests are required to close this level.

### Automated verification

- Before level 2: **182 passed, 0 failed, 0 skipped**.
- Final full suite, run by the operator: **231 passed, 0 failed, 0 skipped**
  (49 added cases), 0.5-second test duration. Build succeeded with 48 warnings
  in 1.0 seconds.
- Two initial new-test failures were corrected: the assertions expected BOOL,
  while the application's existing V1 conversion stores true/false as numeric
  1/0. Production serialization settings were not changed.
- Deployment contract: **69 API methods, 13 tables**. Terraform formatting,
  JSON parsing, Python helper syntax, and whitespace checks passed.
- The agent environment could compile but could not start VSTest because local
  socket binding was denied. The passing full-suite result above was supplied
  by the operator running it locally.

### Deployment

The operator applied the reviewed Terraform plan successfully: **2 added,
5 changed, 2 destroyed**. Replacements were the API Lambda package S3 object
and the API Gateway deployment snapshot.

### Manual acceptance results

| Check | Confirmed result |
| --- | --- |
| Owner lookup repeated for an existing booking | HTTP 200 twice; same owner-session ID |
| Level 1 snapshot after owner-session creation | Submitted two-salad guest cart, draft one-pasta guest cart, empty Owner row; total 30 |
| Group submit with one Owner salad already saved | HTTP 200; exactly three salads; draft pasta excluded |
| Repeat group submit | HTTP 409; already-submitted group rejected |
| Assigned Waiter opens the preorder | Three demo salads visible, confirming persisted quantity and waiter visibility |
| Owner lookup as non-owner / anonymous | HTTP 403 / 401 |
| Owner lookup for nonexistent reservation | HTTP 404 |
| Group submit as non-owner / anonymous | HTTP 403 / 401 |
| New group with no submitted carts | HTTP 400: No submitted sessions |
| Submitted guest cart but no reservation order | HTTP 400: No order found for this reservation |

### Checks not performed manually

Mismatched reservation/group IDs and additional validation paths are covered
by automated tests and were not repeated manually. Guest edit locking after
group submission was not verified manually because the guest's private browser
session had been closed. Exact persisted status values, waiter confirmation/
cancellation actions, full guest-capacity filling, and the complete ordinary
preorder edit/submit/unsubmit cycle were not independently confirmed in this
manual round. These are verification limits, not claims of additional passed
checks or remaining requirements for level 2 completion.

### Frontend follow-ups

Missing-image presentation, full technical preorder IDs, and Waiter image/button
layout issues were recorded in
[FollowUpTasks.md](../../../frontend/restaurant-frontend-app/docs/FollowUpTasks.md)
(FE-003 through FE-005). No frontend implementation was changed during level 2.
No real credentials, invite tokens, participant IDs, or reservation IDs are
recorded in this document.
