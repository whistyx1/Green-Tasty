# AQA framework preparation

> Historical preparation checkpoint. Levels 1–3 and the optional bonus were
> implemented later; see the [current AQA guide](../../../aqa/restaurant-aqa-framework/README.md).

**Status:** framework preparation complete; no AQA homework scenarios completed.

## Imported

- `aqa/restaurant-aqa-framework/`: EPAM's 164-file starter with its existing
  example features, Screenplay/page/API layers, reporting, hooks and configuration.
- `.github/workflows/aqa-quality.yml`: build/lint/discovery, no live execution.
- `.github/workflows/aqa-tests.yml`: future manual opt-in live runs only,
  artifact publication and truthful failure status; no nightly schedule.

Main frontend, backend, dependencies and AWS Terraform remain untouched by this
import. The attached `marathon2026-main/` remains intact and ignored as local
reference. `.env.example` is explicitly unignored; private `.env`, browser cache,
build outputs and reports stay ignored. No `.env` credentials have been created.

## Integration bindings

- Shared-prefix cleanup defaults off, including when no .env flag is set. The
  hook loads local AQA configuration before checking the flag.
- Dotenv no longer probes parent directories. Browser cache is project-local;
  CI uses its own workspace cache. No shared browser-cache cleanup was executed.
- Parallelism starts at one. Hooks are not enabled in git configuration.
- CI workflow inputs are passed through environment variables and quoted shell
  arguments. Missing/zero-executed reports fail instead of reporting a green run.
- Known-defect and WIP examples remain explicit; they are not completed homework.
- API helpers still need our Admin/Owner authorization, group routes and
  run-owned setup/cleanup adaptation during the actual assignment.

## Future levels (not implemented)

| Level | UI acceptance to implement later | API acceptance/setup to implement later |
| --- | --- | --- |
| 1 | Owner participants, dishes/quantities/totals, changes/drafts/empty carts | Isolated guests/carts, empty group, role denials, missing group |
| 2 | Submitted-only consolidation, Submit and Waiter order | Each rejection condition separately, repeated Submit, non-Owner, saved statuses |
| 3 | Remove Guest, updated totals, full flow through Waiter | Last/submitted/foreign/missing guest, permissions and isolation |

Before live acceptance: publish the verified frontend; provide private UI/API
configuration and standing test accounts/table; implement level adapters and
run-owned cleanup. Existing unit-test results do not establish these E2E levels.
Report Portal and optional cloud extension are not configured by this preparation.

## Checks

Verified locally:

| Check | Result |
| --- | --- |
| .NET Release restore/build | Passed, 0 warnings and 0 errors |
| Read-only .NET format verification | Passed outside the sandbox; initial sandbox attempt failed on named-pipe permissions |
| Test discovery only | 82 existing starter cases discovered; none executed |
| Gherkin lint 4.2.4 | Passed on all supplied feature files |
| Workflow validation | Both YAML files parsed; Bash blocks syntax-checked; embedded report Python compiles |
| Ignore/config checks | Private .env/results/cache ignored; blank .env.example not ignored; no private .env created |
| Preservation | All supplied Features/*.feature files byte-identical; existing frontend/backend/Terraform untouched |

The Gherkin linter is pinned to 4.2.4 in quality CI and optional hooks. Its npm
installation reported deprecated transitive packages; no application dependency
was changed. This is not a dependency security audit.

No live tests, browser installation, AWS changes, CI dispatch, hooks activation,
commit or push were performed. Workflow parsing is not a GitHub Actions run;
Linux browser/system dependencies and credentials still require later live-CI
validation. Failure traces can contain account/session data: settle artifact
access/redaction before enabling live runs against real accounts.

## Remaining user configuration (later, not required for build/discovery)

- Private .env: UI_URL, API_URL including stage, standing Waiter credentials and
  assigned test table/waiter. Admin credentials only when selected setup requires
  protected management operations. Never copy credentials into this document.
- GitHub variables: AQA_UI_URL, AQA_API_URL, AQA_TESTTABLE_ID. Matching account
  values go in AQA_WAITER_*, AQA_ADMIN_* and AQA_TESTTABLE_WAITER_* secrets.
- .env.example contains no deployment addresses/passwords; it can be copied when
  ready but is deliberately not populated during preparation.
- Choose which levels and optional star to implement. The roadmap above covers
  all three levels without pretending any scenario is already finished.
- Keep the new workflows local until reviewed/committed. Live execution remains
  manual opt-in after deployment/data/permissions/artifact configuration.
