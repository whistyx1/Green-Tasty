# Repository validation — 2026-11-02

This is a local, non-deploying validation of the current checkout. It does not replace earlier [live AQA regression evidence](aqa/FinalRegression.md) or [DevOps deployment evidence](../../infra/terraform/DEPLOYMENT-VERIFICATION.md). No live E2E scenario, Terraform plan/apply, AWS mutation, or destructive cleanup was run for this review. Test output is not a guarantee about the current AWS deployment.

## Results

| Area | Check | Result |
| --- | --- | --- |
| Backend | `dotnet test backend/restaurant-backend-app/Tests/APIHandler.Tests.csproj --no-restore --verbosity quiet` | **Passed:** 277/277, 0 skipped. A sandboxed first attempt could not open VSTest's local socket; the allowed rerun completed. |
| Backend | `dotnet build backend/restaurant-backend-app/restaurant-backend-app.sln --no-restore --verbosity minimal` | **Passed:** 0 errors, 35 warnings. |
| Deployment contract | `python3 scripts/check-deployment.py` | **Passed:** 69 API methods, 13 tables; management authentication checks passed. |
| Local Python helpers | `python3 -m py_compile scripts/*.py` | **Passed:** syntax compilation only; no AWS behavior was exercised. |
| Frontend | `npm run lint` | **Passed with warnings:** 0 errors, 38 warnings. |
| Frontend | `npm test -- --watch=false --browsers=ChromeHeadless` | **Passed:** 365/365 in Chrome Headless. Test output included expected HTTP-error logs and an Angular duplicate-track-key warning from a fixture. |
| Frontend homework | `npm run test:hw -- --browsers=ChromeHeadless` | **Passed:** 130/130. A duplicate-track-key warning appeared in test output. |
| Frontend production build | `BACKEND_URL=https://example.invalid/develop npm run build` | **Passed:** Angular build and postbuild bundle guard; no placeholders or development demo markers. CommonJS optimization warnings remain. The generated bundle has a deliberately non-routable API URL and must not be published. |
| Infrastructure | `terraform -chdir=infra/terraform fmt -check -recursive` and `terraform -chdir=infra/terraform validate -no-color` | **Passed.** Terraform validation checks configuration, not a live account or a deployment plan. |
| AQA | `dotnet build aqa/restaurant-aqa-framework/restaurant-aqa-framework.csproj --configuration Release --no-restore --verbosity quiet` | **Passed:** 0 warnings, 0 errors. |
| AQA | `npx --yes gherkin-lint@4.2.4 -c .gherkin-lintrc 'Features/*.feature'` from the AQA directory | **Passed.** Initial sandboxed attempt could not resolve the package registry; the allowed rerun completed. |
| AQA | `dotnet format restaurant-aqa-framework.csproj --verify-no-changes --no-restore` from the AQA directory | **Passed after follow-up:** the original review found six whitespace diagnostics in two Level 3 files. A later whitespace-only formatting pass fixed them, and the same verification command exited 0. The quality workflow has not been dispatched in this follow-up. |
| AQA offline discovery | `python3 scripts/run-aqa-level{1,2,3}.py --list --no-build` | **Passed:** 12 discovered tests per homework level; no live requests. |
| AQA assembly discovery | `dotnet bin/Release/net10.0/restaurant-aqa-framework.dll --list-tests` from the AQA directory | **Passed:** 118 discovered tests, including unrelated starter scenarios. Discovery is not execution. |

Some frontend test/build attempts aborted in the initial sandbox before reporting results; the allowed reruns above completed. The backend build also needed a direct rerun after a sandboxed wrapper stalled. These initial attempts are not counted as test failures, but they are not hidden as successful executions.

## Open verification and maintenance points

- **Live behavior not checked in this pass:** AWS API responses, CloudFront publication/current bundle, browser workflows against production, SES delivery, scheduled reporting, the manual AQA GitHub workflow, and the optional CodeBuild job. Earlier operator-confirmed runs are recorded in their linked reports; they are historical evidence, not a fresh run today.
- **AQA formatting follow-up:** `dotnet format --verify-no-changes --no-restore` now passes locally. The full GitHub quality workflow has not been run here.
- **Backend dependency advisories:** build/test output reported NuGet warnings for transitive `SixLabors.ImageSharp 3.1.12`, `System.Security.Cryptography.Pkcs 8.0.1`, and `System.Security.Cryptography.Xml 8.0.4`; one vulnerability-feed request also failed. Review package updates separately. The warning output alone does not establish exploitability in this application.
- **Existing warnings:** backend build has 35 warnings; frontend lint has 38 warnings, and Angular build emitted CommonJS optimization warnings. These are not zero-warning gates.
- **Reference documents:** the original EPAM assignment text and starter snapshots retain instructions for their original state. Use the active README files for current setup and deployment.

## Secret exposure audit

On 2026-11-02, the staging area was empty. A pattern scan of current tracked and untracked text and 854 reachable historical text blobs found no AWS access key, private-key block, GitHub token, JWT, or literal secret assignment. The ignored AQA `.env` contains private account settings; five configured secret fields and four username fields were compared against current nonignored files and reachable text history without printing them. No exact matches were found. This is a targeted scan, not proof that every possible secret format is absent.

The AQA `.env`, Terraform `terraform.tfvars` and `backend.local.hcl`, generated frontend production environment, AQA `TestResults/`, and ReportPortal diagnostics are ignored and untracked. The local AQA result tree contained 353 files, none tracked; sampled text-pattern checks found no email, invite URL, bearer token, JWT, or AWS key pattern. CI workflows use secret references and upload selected reports and masked screenshots. Their retained artifacts and hosted ReportPortal data still require access control.

The two repository hygiene findings were resolved in a follow-up:

1. Four root-level test/build logs are now covered by exact, root-anchored `.gitignore` rules. `git check-ignore -v` confirmed all four; unrelated log paths are not covered by those new rules.
2. The internal order and table IDs in `docs/epam-homework/screenshots/frontend-live-fixes/cart-unknown-status-before.png` are covered by three opaque masks. The image remains 2940×1404, and a decoded-pixel comparison found no changes outside the masks. On 2026-11-03, personal labels and profile cues in two ReportPortal screenshots, the Lambda console screenshot, and the report-email screenshot were replaced with uniform opaque pixels. All four retained their dimensions, and decoded-pixel comparison found no changes outside the selected regions. These edits protect the current files; previously committed image versions still require history cleanup before publication.

Frontend/API URLs, CloudFront hosts, resource names, and Cognito IDs are configuration identifiers, not authentication secrets. The tracked development API URL and example configuration values were not treated as leaked credentials. All 20 current evidence PNGs were visually inspected; their metadata contained standard image-schema URLs but no email, key, invite link, or location tag pattern. Historical image blobs had one version each. Binary and compressed artifacts were not exhaustively decoded, so review any newly staged image or report before pushing.
