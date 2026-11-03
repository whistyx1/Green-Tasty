# Frontend display fixes after homework acceptance

> This is a chronological record. Early pending statements below were
> superseded by later build, publication, screenshot, and regression entries.
> The [2026-11-02 validation record](Validation.md) gives current local checks.


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

## Live visual follow-up, 2026-11-01

After the operator published the frontend and passed all 12 AQA level1 cases
against CloudFront, two screenshots exposed display issues outside those E2E
assertions:

- Long `AQA-Sample-...` test dish names could push the price outside a narrow
  dish card. The name now shrinks and wraps, while the price stays on one line.
  A Chrome geometry assertion checks the long-name layout at 316px card width.
- Submitted preorder items displayed `(Unknown)` because the current backend
  `OrderItemResponse` does not include an item state, while the frontend expected
  one. The frontend now omits a status that the API did not supply. When state 2
  is supplied, it uses the backend's actual `Canceled` label. The backend
  contract remains unchanged.

Focused Angular tests: 12/12 passed. Production build and bundle guard passed.
The agent uploaded this follow-up build to the existing frontend S3 bucket,
waited for CloudFront invalidation, and verified that CloudFront's `index.html`
references the same `main-QEXEBXHQ.js` bundle as the local build. A user-side
hard refresh remains the final visual check. The screenshots also show
`AQA-Sample` dishes left by interrupted diagnostics; a read-only AWS count found
six such dishes and eight six-digit table numbers. Their cleanup requires
item-level review because the interrupted runs did not journal their IDs.

### Screenshot evidence and menu spacing follow-up

The screenshots supplied for these fixes are organized here:

- [Long dish name and price overflow, before the first fix](screenshots/frontend-live-fixes/menu-price-overflow-before.png)
- [Submitted cart showing an unknown status, before the first fix](screenshots/frontend-live-fixes/cart-unknown-status-before.png)
- [Price fixed but unused space on the right of the menu, before the grid fix](screenshots/frontend-live-fixes/menu-price-fixed-grid-gap-before.png)
- [Balanced menu grid after the fix, confirmed by the operator](screenshots/frontend-live-fixes/menu-balanced-grid-after.png)

The last screenshot shows four fixed-width cards aligned to the left of a wider
menu row. The menu now uses responsive grid columns so its cards fill the row
between equal left and right page margins. The narrow-screen card is centered
within its grid column. A browser geometry test checks the outer spacing.

Verification: the focused menu/card tests passed 8/8 and the full frontend
Chrome suite passed 356/356. The production build and bundle guard passed. The
new bundle was uploaded to the existing S3 site bucket; after CloudFront
invalidation completed, the live homepage referenced the same
`main-L5FWKTPY.js` file as the local build. The operator refreshed the live
page and confirmed the spacing; the final screenshot above records that view.

## Booking-success pre-order navigation, 2026-11-02

The booking-success modal's **Make Pre-Order** button previously opened plain
`/menu`, without the new reservation ID or pre-order mode. The reservation
card's **Pre-order** button opened `/menu` with both values, so the two paths
looked different and the new booking's menu could not add dishes to its order.
Both buttons now use the same query builder and open the reservation-aware
menu. Focused button tests passed 13/13; the full frontend suite passed
365/365 and the production build passed its bundle check.
The build was published to the existing frontend S3 bucket, CloudFront
invalidation completed, and the live CloudFront index referenced the same
bundle as the local production build. The operator can visually confirm the
booking-success button on the next booking.
