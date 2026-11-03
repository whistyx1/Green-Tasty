# AQA final live regression — 2026-11-02

The implemented Direction 1 homework suites were run sequentially against the
then-deployed frontend and backend after the guest-removal and
booking-success navigation fixes. Each runner built the .NET AQA project,
discovered its level, ran live tests, and verified a fresh TRX with no skipped
cases. These are local runs against AWS, not a GitHub Actions run.

| Level | Result | Local report |
| --- | --- | --- |
| 1 — Owner participant view | 12/12 passed | `aqa/restaurant-aqa-framework/TestResults/level1-20261102T094140Z-dce31faf/` |
| 2 — group submission | 12/12 passed | `aqa/restaurant-aqa-framework/TestResults/level2-20261102T094615Z-f5c954fd/` |
| 3 — guest removal | 12/12 passed | `aqa/restaurant-aqa-framework/TestResults/level3-20261102T094935Z-197bafae/` |

**Total: 36/36 passed, 0 failed, 0 skipped.** All three `summary.json` files
report `process_exit_code: 0` and `verified_all_passed: true`. The runner
reports remain ignored local artifacts and contain no committed credentials.
This check did not run unrelated starter scenarios, deploy code, or dispatch CI.

## Follow-up after the Level 3 test synchronization fix

Later on 2026-11-02, the Level 3 UI test stopped trying to click **Create New
Pre-Order** after removal. The guest page can become inactive and remove that
button between the test's visibility check and click. The test now waits for
the inactive message and verifies the button is absent. No application code
or AWS resources were changed for this test fix.

| Check | Fresh result | Local report |
| --- | --- | --- |
| Level 3 UI, first run | 2/2 passed | `aqa/restaurant-aqa-framework/TestResults/level3-20261102T193555Z-87cc3839/` |
| Level 3 UI, second run | 2/2 passed | `aqa/restaurant-aqa-framework/TestResults/level3-20261102T193852Z-0c219b2d/` |
| Level 3 full suite | 12/12 passed | `aqa/restaurant-aqa-framework/TestResults/level3-20261102T194126Z-10ca4a9e/` |
| Level 1 full suite | 12/12 passed | `aqa/restaurant-aqa-framework/TestResults/level1-20261102T195107Z-91f561f0/` |
| Level 2 full suite, first run | **11/12; 1 failed** | `aqa/restaurant-aqa-framework/TestResults/level2-20261102T195545Z-629c23f9/` |
| Level 2 UI only | 1/1 passed | `aqa/restaurant-aqa-framework/TestResults/level2-20261102T200114Z-0c09d217/` |
| Level 2 full suite, second run | **11/12; 1 failed** | `aqa/restaurant-aqa-framework/TestResults/level2-20261102T200304Z-74919c20/` |

Each row has a fresh TRX; no cases were skipped. Both Level 2 failures were
`Owner confirms the group order and the Waiter sees the same dishes`. The
Waiter's API returned a submitted order, but the browser test timed out waiting
for the reservation card's Pre-order button. The failure screenshots show the
assigned card with **Pre-order: 0 dishes**. The focused pass does not clear the
two full-suite failures. The cause of this intermittent dashboard/read-path
difference was unverified at that point, and that cross-level regression was
**not all green**. The Level 3 test edit touched only its UI step and guest page
helper.

Other local checks after that edit: backend tests **277/277**, frontend unit
tests **365/365**, frontend homework tests **130/130**, and AQA format
verification passed. The live runs did not deploy code or run hosted CI;
ReportPortal upload was disabled for these runs.

## Frontend deployment and Level 2 verification — 2026-11-03

The waiter dashboard now limits reservation enrichment to three concurrent
requests, preserves card order, treats an order HTTP 404 as no pre-order, and
shows a retryable error for unexpected order failures. The production frontend
was rebuilt with Terraform's `backend_url` for the `us-west-2` API Gateway
`/develop` stage. The bundle check passed, and the built and CloudFront-served
JavaScript both contained that API URL rather than the example URL. The S3
sync dry run showed 27 uploads and four deletions of stale hashed JavaScript
files; the frontend-only upload and CloudFront invalidation completed. The
served site and its public `/locations` API request returned HTTP 200.

| Live Level 2 run | Result | Local report |
| --- | --- | --- |
| First full run | 12/12 passed, 0 skipped | `aqa/restaurant-aqa-framework/TestResults/level2-20261103T090636Z-4237b6fc/` |
| Second full run | 12/12 passed, 0 skipped | `aqa/restaurant-aqa-framework/TestResults/level2-20261103T091011Z-ca0cca9a/` |

Both fresh TRX reports and runner summaries confirm all 12 discovered cases
passed with process exit code 0. In each run, the waiter browser recorded two
order reads: both HTTP 200 with two order items. CloudWatch `AWS/Lambda`
metrics for `api-handler` showed **0 Throttles and 0 Errors** in each run's
four-minute window (508 and 505 Invocations, respectively). These measured
windows contained no observed transient order-loading failure. ReportPortal
was disabled. The live suites created test application data; no backend or
Terraform deployment occurred. Levels 1 and 3 were not rerun after this
frontend deployment, and two passing runs cannot rule out every intermittent
failure.
