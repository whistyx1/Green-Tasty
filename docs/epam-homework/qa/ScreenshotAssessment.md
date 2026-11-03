# Manual QA #7 — Parts 2 and 3 review

## Evidence and method

- [Draft US11 guest menu story](https://drive.google.com/file/d/13MZpQFxvRLquSdIPF7-wPU95x8gAVElv/view?usp=sharing)
- [Figma guest menu frame](https://www.figma.com/design/P9rkBNLJZ3WabXSQX9b5DO/EPAM-IT-Marathon-6.0?node-id=111683-86716)
- [EPAM implementation screenshot](https://drive.google.com/file/d/1qWITmyQg35sTWr9MLnzWQs1DJ6wpp_OA/view?usp=drive_link), 1912 × 1064 pixels
- [Part 1 requirements analysis](RequirementsAnalysis.md) and [Part 2 test cases](US11-TestCases.xlsx)

The 21 test cases were reviewed against all six story requirements. The review
added a case for the Pre-Order action on available cards across categories and
a separate first-open freshness case. Fifteen cases remain **Not run**; six
remain **Blocked** until the stated behavior is clarified. The spreadsheet is
a test design deliverable, not an execution report.

Part 3 uses one supplied static screenshot. It can establish visible content
and differences from Figma. It cannot establish successful clicks, disabled
controls, permissions, cart changes, or data freshness. No live application
interaction was performed for this part.

## Screenshot observations against the story

| Requirement | Visible evidence | Result from this screenshot |
| --- | --- | --- |
| R1 — joined Guest and allowed parent status | Browser URL has a `/join/` path and the guest menu is shown. The actual session and reservation states are not visible. | Partially observable; preconditions not verified. |
| R2 — full menu by category | The page shows `All dishes`, `Appetizers`, and `Main Courses` tabs and three dish cards. The Figma frame also has `Desserts`, but the screenshot does not establish whether dessert dishes exist in this restaurant. | Partially observable; full menu and category behavior not verified. |
| R3 — name, price, grams, image, availability | Each visible card has a name, price, gram weight, and photo. Two are faded and say `Out Of Stock`; the third has an active-looking action. The three different dishes appear to use the same salad photo. | Visible fields present; photo mismatch recorded as D2. Exact status wording remains unresolved under G1. |
| R4 — add available dish to personal subcart | The Margherita Pizza card has a green `Pre-Order` button and a cart icon is visible. | Action is visible; addition and subcart ownership not tested. |
| R5 — On Stop cannot be selected | Two cards are faded and show grey `Pre-Order` buttons. | Unavailable styling visible; click prevention not tested. `Out Of Stock`/`On Stop`/`Sold Out` wording needs clarification. |
| R6 — current data on open or refresh | One point-in-time screenshot only. | Not testable from the screenshot. |

## Part 3 result by test case

The case IDs refer to `US11-TestCases.xlsx`. **Partial** means a visible part
of the expected result can be inspected, while the case's actions and full
expected result have not been executed. None of the 21 cases is marked Passed
or Failed from this screenshot alone.

| Case | Screenshot result | Reason |
| --- | --- | --- |
| TC01 | Partial | Guest menu is visible at a `/join/` URL; joined identity and Reserved status are not shown. |
| TC02 | Partial | Category tabs and three cards are visible; full menu membership and grouping behavior cannot be checked. |
| TC03 | Not testable | A static screenshot cannot show category switching. |
| TC04 | Partial | Available-looking and unavailable cards coexist; completeness of the menu is unknown. |
| TC05 | Visual issue D2 | All three cards show the required field types, but the photos appear reused for different dishes. Correct source data is unavailable. |
| TC06 | Partial | An available-looking card has a Pre-Order button; a click and personal subcart update are not shown. |
| TC07 | Not testable | The screenshot contains only one available-looking card, so action availability across categories cannot be checked. |
| TC08 | Not testable | Another Guest's subcart is not shown. |
| TC09 | Partial | Two cards are faded with `Out Of Stock` badges; the required customer-facing wording is unresolved under G1. |
| TC10 | Not testable | Grey buttons do not prove a click is blocked or that the cart stays unchanged. |
| TC11 | Not testable | No before-and-after refresh evidence for a newly published dish. |
| TC12 | Not testable | No before-and-after evidence for the first menu opening. |
| TC13 | Not testable | No before-and-after refresh evidence for price or weight changes. |
| TC14 | Not testable | No before-and-after refresh evidence for availability changes. |
| TC15 | Not testable | No evidence of leaving and reopening the menu. |
| TC16 | Blocked by G2 | The permitted parent statuses are undefined; no status transition is visible. |
| TC17 | Blocked by G3 | Location scoping is undefined; only one menu is visible. |
| TC18 | Blocked by G4 | Repeat-add behavior is undefined; no action sequence is visible. |
| TC19 | Blocked by G5 | The response to an availability change during an add is undefined and not shown. |
| TC20 | Blocked by G8 | The sorting rule is undefined; opening the selector and reordering are not shown. |
| TC21 | Blocked by G9 | Missing-image behavior is undefined; all three cards have an image. |

## Visual comparisons with Figma

| ID | Observed mismatch | Disposition |
| --- | --- | --- |
| D1 | Figma's reservation banner includes a date between table and time. The implementation banner shows the address, `Table 1`, then `15:30 - 17:00`, with no date. | Confirmed visual mismatch; defect report. |
| D2 | Figma uses distinct dish photos. The implementation shows the same salad photo for Fresh Avocado Toast, Truffle Mushroom Risotto, and Margherita Pizza. | Confirmed visual/content mismatch; defect report. Cause could be source data or image binding and is not determined by the screenshot. |
| G1 | Story says `On Stop`, Figma says `Sold Out`, implementation says `Out Of Stock`. | Requirement wording conflict; not counted as a confirmed application defect. |
| G7/G8 | Figma shows a `Desserts` tab and a sort option `Popular First`; implementation shows no `Desserts` tab and `Recommended` sorting. | Clarify data-dependent category visibility and sort scope before calling these defects. |

The supplied screenshot includes an invitation URL. It is linked from EPAM's
Drive rather than copied into this public repository, to avoid publishing the
visible invite token. See [Part 4 defect reports](US11-DefectReports.xlsx) for
the two confirmed visual issues.
