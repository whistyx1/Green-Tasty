# Manual QA #7 — Part 1: requirements analysis

## Sources and scope

- [EPAM draft US11: Guest Viewing Reservation Menu](https://drive.google.com/file/d/13MZpQFxvRLquSdIPF7-wPU95x8gAVElv/view?usp=sharing), one-page PDF, read 2026-11-02.
- [EPAM Figma: Placing pre-order by guest | Menu](https://www.figma.com/design/P9rkBNLJZ3WabXSQX9b5DO/EPAM-IT-Marathon-6.0?node-id=111683-86716), inspected 2026-11-02.

This is a review of requirements and design, not execution of test cases or a
defect report against the application. The source story is marked **Draft**.

## Testable requirements stated in the story

| ID | Requirement | Source |
| --- | --- | --- |
| R1 | A guest who joined the session can open the restaurant menu while the parent reservation or group is in the stated Created/Reserved condition. | Role and preconditions |
| R2 | The guest can view the full menu arranged by category. | AC 1 |
| R3 | Each dish displays its name, price, serving weight in grams, image, and current availability. | AC 2 |
| R4 | An available dish has a clear action to add it to the guest's personal subcart. | AC 3 |
| R5 | An On Stop dish is visibly unavailable and cannot be selected or added. | AC 4 |
| R6 | Opening or refreshing the screen shows current menu data. | AC 5 |

The Figma frame also shows a reservation banner, breadcrumb, three category
tabs (Appetizers, Main Courses, Desserts), a Sort by control, and dish cards.
These are visible design elements; the story does not define all of their
behavior. The unavailable example is faded, labeled **Sold Out**, and has no
button. One available example visibly has a Pre-Order button; two other
available-looking cards in the static frame do not. This does not establish a
hover or selection rule.

## Gaps and clarification questions

Priority indicates how much the answer could change test expectations. No
proposed answer below is treated as an approved requirement.

| ID | Priority | Gap / ambiguity | Question for product or design |
| --- | --- | --- | --- |
| G1 | High | The story says **On Stop**; Figma says **Sold Out**. | Are these the same status? Which label must guests see, and are other unavailable states possible? |
| G2 | High | The precondition combines reservation and group states as Created/“Reserved” without defining the allowed status combinations or transitions. | Which exact reservation and group statuses permit menu access, and what happens when either status changes while the guest is viewing it? |
| G3 | High | “Full restaurant menu” does not say how the correct restaurant/location is selected from a joined session. | Is the menu always limited to the reservation's location, and should On Stop dishes remain visible in that full menu? |
| G4 | High | The story says an available dish can be added, but does not define quantity, repeat additions, confirmation, or failure handling. | Does each click add one unit, increment an existing line, or open a quantity chooser? What feedback and error state should appear? |
| G5 | High | Menu data must be current on open/refresh, but stock can change between display and add. | If a dish becomes On Stop after the screen loaded, should the add be rejected and the card refreshed? Is automatic polling required, or only open/refresh? |
| G6 | Medium | Figma shows the action on one available card only. | Should Pre-Order be visible on every available card, appear on hover/focus, or depend on another selection state? What is the touch/keyboard behavior? |
| G7 | Medium | Category examples in the story and three tabs in Figma do not define the full category set, order, or empty categories. | Which categories must exist, in what order, and should empty categories be shown? |
| G8 | Medium | Figma shows Sort by options, but the story has no sorting rule. Its annotation calls this a “Filter” component. | Is sorting in US11 scope? If so, what are the default and tie-break rules for Popular, Newest, and price order? Is any filtering intended? |
| G9 | Medium | Required card fields may be missing or malformed in menu data. | What should appear for a missing image, weight, price, or availability value? What currency and price format are required? |
| G10 | Medium | “Personal subcart” ownership is stated, but guest removal, expired links, and another guest's cart are not covered. | What should the guest see if their session is removed or expires, and how is access to another guest's subcart prevented? |

## Boundary for the next homework parts

Part 2 should turn R1–R6 and the resolved design rules into test cases, marking
unresolved G1–G10 expectations explicitly. Part 3's supplied implementation
screenshot can establish only visible states and layout; it cannot prove cart
updates, refresh behavior, permissions, or disabled actions. A mismatch in that
screenshot should become a defect report only when the expected result is
supported by the story or an unambiguous design detail.
