# Frontend homework — level 2

## Scope and baseline

**Level 2 complete:** implemented, independently reviewed, 65 homework/helper
tests passed, production build/postbuild passed, and the operator confirmed the
agreed local mock submission flow. The updated frontend has not been deployed.

Implement the Owner review flow against EPAM's development mock API, reusing the
main application's Angular architecture, existing API models, invitation service,
modal, toast service, and supplied `consolidatePreOrder()` helper.

Completed level 1 baseline: **42 homework/helper tests passed**, production
build/postbuild passed, and the operator confirmed responsive visual behavior.
This is the prior checkpoint, not a claim that level 2 tests have run.

No backend changes, real AWS acceptance flow, polling, guest removal UI, or
deployment are in scope. Those belong to later work. Development demo providers
remain isolated from production through the existing module replacement.

## Implementation and contracts

`GroupOrdersService` reuses `API_BASE_URL` and `GuestInviteService`:

| Method | Existing contract |
| --- | --- |
| `getGroupInvite$` | Reservation invite token, then invite details |
| `getSessions$` | GET `/guest/groups/{groupId}/sessions` |
| `getOwnerSessionId$` | GET `/reservations/{reservationId}/guest-session-id`, mapped to the ID string |
| `getStatus$` | GET `/guest/groups/{groupId}/status` |
| `submit$` | POST `/group-reservations/{groupId}/submit`, body `{ reservationId }` |

For `demo-reservation` in development, EPAM's existing mock interceptor handles
these calls. Production does not register that interceptor. This level does not
claim real-API integration is verified merely because the URL contracts exist.

The page loads its reservation ID from the route, identifies the Owner card as
**My Pre-Order**, shows loading/error/retry/empty states, and calculates the
consolidated list from submitted carts only. The all-participant API total is
not the amount sent to the restaurant. Draft carts remain visible with a warning.

The supplied demo data has all-cart total **$74.50**, but submitted-only total
**$50.50** (three Spring Salads plus one Avocado Pine Nut Bowl). The supplied
page tests use a different fixture and require **$540.00**, excluding $220 in
draft dishes from the API's $760 total.

Submission uses the shared confirmation modal; cancelling sends no request.
Only one in-flight submit is allowed. On success, the page shows **Sent to
Restaurant**, becomes read-only, and shows one success toast. On failure, it
remains usable for retry; the existing interceptor owns error toasts.

## Skill and temporary agents

- `angular-developer`: Angular/signal/HTTP/styling guidance, with the project's
  existing Jasmine/Karma and naming conventions preserved.
- `frontend_level2`: service, page, participant-card changes, and focused specs.
- `frontend_level2_review`: independent read-only contract/state/accessibility
  and regression review.
- Main agent: coordination, verification, version metadata, and this document.

## Verification

Run from `frontend/restaurant-frontend-app`:

```bash
npx tsc -p tsconfig.app.json --noEmit
npx tsc -p tsconfig.spec.json --noEmit
npm run test:hw -- --browsers=ChromeHeadless
BACKEND_URL="$(terraform -chdir=../../infra/terraform output -raw backend_url)" npm run build
```

The supplied level 2 page spec replaces the level 1 page spec as instructed by
EPAM. Practice card and image-fallback tests remain. Test execution results must
be recorded separately from compilation and inspection.

## One local manual flow

Open the existing development demo URL with `npm start`:

`http://localhost:4200/reservations/demo-reservation/group-order?demo`

Confirm cards, **My Pre-Order**, a draft warning, and consolidated total $50.50.
Open Submit Pre-Order, cancel once, then open again and confirm. Expect **Sent
to Restaurant**, one success toast, and no enabled submission action. Check the
same page/dialog while narrowing the window. Reload resets EPAM's mock store.
No AWS booking or repeated backend permission/error checks are needed here.

## Known level 3 integration gaps

The existing interceptor skips Cognito authentication and silent-error handling
for `/guest/` paths; the real Owner routes need that distinction addressed in
level 3 while preserving guest-token behavior. Invite-based group lookup also
cannot reopen every full/submitted group. The real backend stores the Owner's
ordinary preorder separately from its empty owner session; its preview must be
reconciled before real submission integration. None of those concerns is fixed
or claimed verified by this mock-only stage.

## Results

Implementation and independent review are complete, with no blocking review
findings. Frontend version is **1.1.3**; dependency versions are unchanged and
the lockfile changes only matching application version metadata.

Confirmed verification:

- App and spec TypeScript checks and Angular template compilation passed.
- Scoped ESLint: **0 errors, 0 warnings**.
- Sass compilation passed: compressed card CSS **1,593 bytes**, page CSS
  **3,034 bytes**. Actual production bundle/style sizes remain unverified.
- All 18 supplied level 2 test cases are unchanged copies. The five static
  level 1 page cases were replaced as EPAM instructs; practice/image cases
  remain. Seven added page cases and three service cases cover state/lifecycle,
  cancellation, retry, HTTP errors, and encoded identifiers.
- AWS environment files and environment-generation script are unchanged.
- Page and HTTP service have no direct preview or mock-store imports. Existing
  production mock-provider isolation is unchanged.
- Whitespace and targeted file-preservation checks passed.

Production build was attempted and stopped during bundling with the same native
Node **malloc: pointer being freed was not allocated** diagnostic seen in the
agent environment during level 1. The homework test command also stopped during
bundling with that same native diagnostic, before reporting any executed tests.
Compilation/review results do not establish a passing test execution.

The operator confirmed the post-change homework/helper suite: **65 tests
succeeded**. Production build completed through its postbuild guard:
**Bundle check passed: no configuration placeholders or development demo
markers.** No production bundle-size figure or full frontend-suite result was
supplied. The operator subsequently confirmed completion of the agreed local
demo flow: review My Pre-Order, draft warning and $50.50 total; open and cancel
confirmation; reopen and submit; verify Sent to Restaurant, success feedback,
and no remaining Submit button. This confirmation was supplied as “done”; no
screenshots or independently observed browser trace were collected. No further
manual checks are required to close level 2. No AWS deployment or real API
acceptance result is claimed.
Existing route snapshot behavior also means a reused page
instance is not yet verified for changing reservation IDs without navigation
recreation; address that with level 3's synchronization/lifecycle work.
