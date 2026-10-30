# Frontend display fixes after homework acceptance

Frontend homework levels 1–3 and Remove Guest bonus passed the documented real API flow before this cleanup. These changes address separate display defects; they do not repeat or replace that acceptance.

## Changes (version 1.1.5)

- Shared menu/Waiter dish cards and the Customer draft cart show accessible placeholders for missing/failed images; valid replacement URLs recover normally.
- Waiter Cancel/Confirm use the existing button variants in a responsive grid, removing conflicting wrapper/inner-button styling.
- Waiter navigation carries the selected table label and original reservation timestamps. The heading formats browser-local time consistently with the booking card and includes a timezone label. Reload uses the existing navigation/storage flow; older metadata without timestamps retains the server range explicitly labeled UTC.
- Customer preorder references/names wrap, cart actions fit narrow screens, header links retain spacing, and narrow Edit Reservation fields stack. The original Edit Reservation report has not been reproduced, so its exact resolution is not claimed.

No backend changes, new dependencies, AWS resource changes, production mock registration, or AWS deployment are part of this cleanup. Existing invite retrieval improvement FE-002 remains separate.

## Delegation

Three temporary agents performed the work: `frontend_display_fixes` (shared images/Waiter buttons), `frontend_booking_metadata` (table/time mapping and regressions), and `frontend_layout_review` (Customer cart/header/dialog). Agents also reviewed one another’s changes. The main agent integrated test selection, version metadata, checks, and this record. They used the installed Angular skill and preserved the existing project architecture.

## Verification

App/spec TypeScript checks passed during integration. Agents also passed Angular template compilation, scoped lint, Sass, and CRLF-aware whitespace checks. Regression specs cover missing/failed/replaced images and navigation/time metadata.

Browser tests and production build were attempted but aborted during bundling with the existing native Node error `pointer being freed was not allocated`. No passing runtime count is claimed for version 1.1.5. The earlier 112 frontend / 271 backend passes remain evidence for the homework implementation before these display fixes.

Run from `frontend/restaurant-frontend-app`:

```bash
npm run test:hw -- --browsers=ChromeHeadless
BACKEND_URL="$(terraform -chdir=../../infra/terraform output -raw backend_url)" npm run build
```

One final visual recheck is sufficient: reopen the existing Waiter preorder from its reservation, check the table/time, image placeholder and action buttons, then narrow the window and check the Customer cart/header/Edit Reservation dialog. No new booking, guest submission, or backend permission matrix is required. Visual recheck and successful updated build/tests remain pending. The frontend has not been published to AWS.

## Test-mock correction

The operator supplied a complete run with 126 passed and 3 failed of 129. All
three failures were in the new Waiter timezone tests (`target is not a
constructor`): the Intl.DateTimeFormat spy used a non-constructable arrow
function. Its replacement is a normal function; production code and expected
timezone values are unchanged. Spec TypeScript checks passed, and a standalone
Jasmine run of the same constructor spy passed DST, midnight, and stored-time
formatting cases (3/3). This is not a passing Angular browser-suite result.
The narrow Angular rerun still encountered the native Node bundling failure;
the corrected 129-test browser suite needs an operator rerun.

## Confirmed corrected browser run

After the constructor-mock repair, the operator reran the frontend homework
and display regression suite and reported **129 SUCCESS**. This supersedes the
earlier 126-pass/3-fail result. The updated production build and final visual
recheck remain unconfirmed; AWS frontend publication has not been performed.

## Operator visual confirmation

The operator checked the updated localhost Customer and Waiter views and
confirmed the button layout defects are fixed. The image-unavailable text is
expected for seeded demo dishes without uploaded images. This confirms those
reported views; no measured375px or exhaustive visual check is claimed.
See RegressionReview.md for the subsequent full352frontend/271backend passing
runs and production build. The frontend is still not published to AWS.
