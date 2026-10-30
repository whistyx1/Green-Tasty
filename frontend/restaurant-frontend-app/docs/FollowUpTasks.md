# Frontend follow-up tasks

Reported during backend manual testing. These are additional frontend
items, separate from the EPAM Owner-screen homework. They are recorded for later
work; recording an item does not mean it has been reproduced or fixed.

## FE-001 — Edit Reservation dialog layout overflows

**Type:** Bug. **Status:** Reported; reproduction pending.

**Reported behavior:** after opening Edit Reservation, the dialog appeared
compressed and text extended outside the reservation window.

**Reproduction to confirm:**

1. Sign in as a Customer with an existing editable reservation.
2. Open the reservations page and select Edit Reservation.
3. Observe dialog width, wrapping, labels, controls, and buttons.
4. Record browser, viewport size, and the exact overflowing content; capture a
   test-only screenshot without personal data.

**Expected behavior / acceptance:**

- Text and controls remain inside the dialog without overlap or clipping.
- The dialog works on desktop and at 375 px width; long labels/values wrap.
- Scrolling and keyboard access keep all fields/actions reachable.
- Opening, saving, and cancelling edits preserve their existing behavior.
- Add a regression check for the reproduced layout problem, including visual
  verification at the affected viewport.

**Starting points:** reservation dashboard modal wrapper and
`components/reservations/edit-reservation-modal/`. Inspect the actual cause
before changing widths or global modal styles.

## FE-002 — Retrieve/copy an invite link from the reservation card

**Type:** Usability improvement. **Status:** Requested; implementation pending.

**Reported behavior:** the Owner sees the invite link in the booking confirmation
flow, but cannot easily retrieve it later from their saved reservation.

**Requested behavior:** retain an accessible Invite Link or Copy Invite Link
action on the Owner's reservation card, so closing the confirmation or reloading
the website does not lose access to an active group's invitation.

**Acceptance:**

- Available to the Owner for eligible group reservations (two or more people,
  with an active invitation); use the backend's eligibility information.
- Retrieves the existing invitation for that reservation, rather than generating
  another group or depending on a previously open confirmation dialog.
- Owner can reopen the link and copy it after a page reload or fresh login.
- Clear loading, copy-success, and failure/retry behavior; accessible controls.
- Single-person, cancelled, completed, closed, or otherwise ineligible groups
  do not offer an invitation action that cannot work. Match actual backend rules.
- Verify backend ownership enforcement when retrieving the token. Hiding a
  button alone does not prevent another user from requesting the endpoint.
- Test eligible/ineligible cards, reload/retrieval, clipboard behavior, and API
  failure. Never log invite tokens or embed them in screenshots/test reports.

**Existing pieces to inspect/reuse:** `GuestInviteService.getInviteToken$`,
`reservation-invite-modal`, and the reservation's `guestInviteLinkAvailable`
field. The token endpoint already exists:
`GET /reservations/{reservationId}/invite-token`.

## FE-003 — Missing preorder dish image needs a fallback

**Type:** UI robustness bug. **Status:** Reported during level 2; missing-image
data path confirmed in source, browser reproduction pending.

The operator saved an Owner preorder and reported that its dish image did not
load. Demo seeding deliberately supplies `imageBase64: None`, so these dishes
have no uploaded image. The unsubmitted preorder template binds `dish.imageUrl`
directly to the image source without a fallback. Do not assume an S3 permissions
or deployment problem solely from a missing demo image.

**Acceptance:** show an intentional placeholder for null/empty URLs and failed
image requests, preserve layout and accessible dish names, and keep valid images
working. Check Owner, guest, and waiter dish presentations during frontend work.

**Starting point:**
`components/cart/components/not-submitted-order/not-submitted-order.component.html`.

## FE-004 — Review long technical preorder ID in customer cart

**Type:** Usability concern. **Status:** Reported during level 2; design review
pending.

The operator reported seeing the preorder ID in the cart. Both submitted and
unsubmitted order templates intentionally display `Pre-order #` plus the full
internal `orderId`. Confirm the exact screen with the operator and compare the
homework design before deciding whether to shorten, replace, or hide this label.

**Acceptance:** a useful customer-facing heading/reference that does not overflow
on mobile; retain the original ID internally for API actions. Changing a label
must not change submission, editing, cancellation, or order selection behavior.

**Starting points:** `components/cart/components/not-submitted-order/` and
`components/cart/components/submitted-order/`.

## FE-005 — Waiter preorder image overflow and Cancel button layout

**Type:** Layout bug. **Status:** Visually confirmed in the supplied real API
acceptance screenshot on 2026-10-30; implementation fix pending.

After group submission, the assigned Waiter opened the preorder and saw the
expected three demo salads, but reported an image extending outside its borders
and a malformed Cancel button. Do not assume an undeployed frontend change is
the cause; no frontend implementation was changed during this backend task.

**Acceptance:** reproduce at the operator's browser/viewport; keep images or
missing-image placeholders within their container; render the Cancel label and
button without overlap, clipping, or distortion on desktop and mobile. Preserve
confirmation/cancellation behavior and verify against the homework design.

**Starting point:** `pages/waiter-pre-order-page/` and its dish/action components.

**Evidence:** [Waiter preorder screenshot](../../../docs/epam-homework/screenshots/frontend-level3/waiter-preorder-layout.png).
The missing image renders clipped alternative text inside a large circular image
area. Cancel has overlapping green/outline layers and poor text contrast. Source
inspection shows styling on both the custom-button host and its inner button,
including deep selectors; this is a likely contributor, not a tested root cause.
The displayed Quantity: 3 is correct. No image/button fix has been made.

## Current work

Frontend homework levels 1–3 and the Remove Guest bonus are complete, with real
API acceptance documented. These broader UI fixes remain separate and pending;
the updated frontend has not been published to AWS.

## FE-006 — Shared header navigation spacing at narrower desktop widths

**Type:** Layout issue. **Status:** Observed in supplied level 3 screenshot;
viewport reproduction pending.

The Sent to Restaurant screenshot shows Main page and Book a Table touching in
the shared header. Their spacing is distinct in the Open overview screenshot.
Record exact viewport/zoom when reproducing, preserve keyboard/navigation
behavior, and adjust the existing header's responsive spacing without global
changes to the Owner page. This is not evidence that the group-order data flow
failed. Reference:
`docs/epam-homework/screenshots/frontend-level3/sent-to-restaurant.png`.

## FE-007 — Waiter preorder loses the reservation's table label

**Type:** Presentation/data-mapping bug. **Status:** Visually confirmed; fix pending.

The reservation card shows Table 1, but opening its preorder produces
`Table Unknown table`. Compare the
[reservation screenshot](../../../docs/epam-homework/screenshots/frontend-level3/waiter-reservations.png)
with the [preorder screenshot](../../../docs/epam-homework/screenshots/frontend-level3/waiter-preorder-layout.png).
Check the selected table label, navigation state, and reload behavior before
changing the mapping. Preserve the table ID internally; show a useful label
without duplicating the word Table in fallback text.

## FE-008 — Reservation and Waiter preorder time presentation differs

**Type:** Timezone presentation issue. **Status:** Visually confirmed; fix pending.

For the same booking, the reservation card shows 10:30 AM–12:00 PM while the
preorder heading shows 8:30 AM–10:00 AM. A two-hour timezone conversion can
explain the difference; the screenshots do not demonstrate corrupt stored times.
Choose the existing product's display-timezone policy, apply it consistently,
and label the timezone when necessary. Cover date/daylight-saving boundaries
without altering the reservation's stored UTC timestamps. Evidence: the two
Waiter screenshots linked in FE-007.

## Display cleanup — 2026-10-30

FE-003/004/005/006/007/008 have focused frontend implementations in version
1.1.5; final browser recheck is pending. FE-001 received a narrow-screen stacking
fix, but its original reported overflow has not been reproduced. FE-002 remains
unimplemented. Earlier descriptions/screenshots above are pre-fix evidence,
not the current implementation status. See
[display fix record](../../../docs/epam-homework/FrontendUiFixes.md) for delegation,
verification limits, and the single remaining visual check.
