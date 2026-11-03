# EPAM AQA starter review

> Historical starter review. The implemented suite and bonus are described in
> the [current AQA guide](../../../aqa/restaurant-aqa-framework/README.md).

Source: `marathon2026-main/aqa/restaurant-aqa-framework/`.
Status: read-only inspection of the starter; no framework copied into the main
application, dependencies installed, hooks enabled, workflows enabled, or live
scenarios executed.

## Reusable parts

The starter targets .NET 10 with Playwright for .NET, Reqnroll, TUnit and a
Screenplay structure. Read README.md, CLAUDE.md, the project file, configuration,
workflow definitions and representative scenarios/hooks. Existing Features cover
smoke/auth/registration/menu/location/seed examples; Reservations is marked wip.
No group-preorder endpoint/scenario implementation was found in the inspected
framework. Our Direction 1 levels must be implemented against our actual UI/API.

A future import should copy only the AQA framework and adapt the two AQA
workflows (`aqa-quality.yml`, `aqa-tests.yml`), preserving the existing main
frontend/backend/Terraform. Do not import deploy/pr/terraform workflows blindly.
Keep the attached Marathon folder intact as reference.

## Configuration and live effects

Use private .env from the empty .env.example: UI_URL, API_URL (including stage),
standing Waiter credentials, and optional assigned table/waiter. Admin credentials
are optional in the starter, not automatically required for every homework case.
CI uses GitHub variables/secrets; credentials and result artifacts are not source.

PreRunCleanupHooks sweeps and deletes matching AQA-Sample- records before/after
runs, including force-delete of dishes/locations. Adapt cleanup to isolated
run-owned data and actual route permissions before any live run. No blanket live
run is justified just by importing the starter. Browser installation also contains
local old-browser-cache cleanup; review/adapt shared-cache effects before use.

The AQA workflow includes a nightly schedule plus manual runs. Begin with
manual opt-in runs while configuring our environment. It uploads reports/traces
even if tests fail, then explicitly fails the job when the test step failed;
this matches continuing report publication without hiding test failure.

The starter README permits known-defect failures and wip skips. These are not
proof of completed homework; report each honestly and implement all selected
Direction 1 scenarios. Report Portal integration still needs separate work for
the optional star. This inspection is not a full security audit or runtime pass.

## Prerequisite

Deploy our latest verified frontend before accepting E2E against CloudFront.
The real API/backend is already deployed; localhost currently carries the newest
frontend. The attached starter does not change this deployment status.
