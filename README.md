# Green & Tasty — EPAM IT Marathon 6.0

This repository contains a restaurant booking and group pre-order application, its AWS infrastructure, and the completed EPAM DevOps, backend, frontend, Manual QA, and test-automation homework. The active application is under `backend/` and `frontend/`; the deployed backend is managed by **Terraform**, not AWS Syndicate.

## Architecture

| Layer | Implementation |
| --- | --- |
| Web app | Angular 22 standalone components, SCSS, signals and RxJS; production files in private S3 behind CloudFront |
| API | .NET 10 Lambda functions behind API Gateway REST API (`develop` stage) |
| Identity and data | Cognito user pool; DynamoDB tables for restaurant, booking, order, and reporting data |
| Background work | SQS, report Lambdas, EventBridge Scheduler, SES for email |
| Infrastructure | Terraform in `infra/terraform/`, with remote S3 state and a separate artifacts bucket |
| Verification | xUnit backend tests, Karma/Jasmine frontend tests, Reqnroll/TUnit/Playwright AQA, Gherkin lint, and an optional manual AWS CodeBuild/ReportPortal bonus |

The Owner group-order screen uses the real API in normal mode, including a 15-second refresh while the group is open. Development demo mode uses EPAM mock data and providers; the production bundle check rejects demo markers. See the [frontend guide](frontend/restaurant-frontend-app/README.md) for the distinction.

## Repository map

| Path | Purpose |
| --- | --- |
| [`backend/restaurant-backend-app/`](backend/restaurant-backend-app/README.md) | Lambda source, API contracts, xUnit tests, backend homework evidence |
| [`frontend/restaurant-frontend-app/`](frontend/restaurant-frontend-app/README.md) | Active Angular app and frontend tests |
| [`infra/terraform/`](infra/terraform/README.md) | AWS resources, deployment instructions, DevOps verification |
| [`aqa/restaurant-aqa-framework/`](aqa/restaurant-aqa-framework/README.md) | End-to-end framework and level-specific runners |
| [`docs/epam-homework/`](docs/epam-homework/README.md) | Homework index, original EPAM assignments, implementation records, QA deliverables, screenshots |
| `scripts/` | Local packaging, contract checks, deployment helpers, AQA runners |
| `green-and-tasty/`, `marathon2026-main/` | Supplied EPAM reference copies; they are not the active deployment source. `marathon2026-main/` is ignored by Git. |

## Local setup and checks

Use .NET SDK 10, Node.js matching [`frontend/restaurant-frontend-app/.nvmrc`](frontend/restaurant-frontend-app/.nvmrc) (or a version allowed by `package.json`), npm, Python 3, and Chrome/Chromium for the frontend tests. Terraform and AWS CLI are needed for infrastructure work; a live AWS account is **not** needed for the checks below. Install npm dependencies with `npm ci` in the frontend directory. Restore .NET packages when the local cache is empty.

From the repository root:

```bash
dotnet test backend/restaurant-backend-app/Tests/APIHandler.Tests.csproj
dotnet build backend/restaurant-backend-app/restaurant-backend-app.sln
python3 scripts/check-deployment.py
terraform -chdir=infra/terraform fmt -check -recursive
terraform -chdir=infra/terraform validate
```

Frontend checks from `frontend/restaurant-frontend-app/`:

```bash
npm ci
npm run lint
npm test -- --watch=false --browsers=ChromeHeadless
npm run test:hw -- --browsers=ChromeHeadless
BACKEND_URL=https://example.invalid/develop npm run build
```

The example URL checks that a bundle can be built; **do not upload that bundle**. Build again with the real Terraform `backend_url` before publishing. The generated `src/environments/environment.production.ts` is ignored and contains the supplied URL. `npm start` serves the local development app at `http://localhost:4200/`; its committed development environment points to a particular deployed API, so use the [frontend guide](frontend/restaurant-frontend-app/README.md) before testing real login.

The AQA framework has separate [offline quality checks and live runners](aqa/restaurant-aqa-framework/README.md). Live E2E tests create and remove application data and require private account configuration; they are never part of the local command list above.

The [2026-11-02 validation record](docs/epam-homework/Validation.md) reports actual results, warnings, the AQA formatting follow-up, and what was not run. Passing local tests does not establish that the live AWS environment is healthy.

## Deployment overview

The active DevOps homework uses [Terraform deployment instructions](infra/terraform/README.md): check the account, build Linux Lambda ZIPs locally, supply ignored local Terraform settings, initialize remote state, review a plan, then apply it. Build the frontend with Terraform's real `backend_url`, upload the browser output to Terraform's frontend bucket, and account for CloudFront caching. The infrastructure also contains an optional, manually triggered AQA CodeBuild project; ReportPortal credentials stay in private configuration.

These are AWS-changing steps and can incur charges. Do not run `syndicate deploy`, `syndicate update`, or `syndicate clean` against this Terraform-owned stack. The old Syndicate lesson metadata remains in the backend as reference only. Deployment history and limitations are recorded in [DevOps verification](infra/terraform/DEPLOYMENT-VERIFICATION.md).

## Homework and evidence

Start at the [EPAM homework index](docs/epam-homework/README.md). Backend levels 1–3 are documented beside the backend API; frontend levels 1–3 and the Remove Guest bonus have implementation and acceptance records; Manual QA artifacts are in `docs/epam-homework/qa/`; AQA levels 1–3 and the optional ReportPortal/CodeBuild bonus are in `docs/epam-homework/aqa/`.

Original assignment text is retained separately from completion evidence. Earlier report sections describe checkpoints at the time they were written; use the [validation record](docs/epam-homework/Validation.md) and the latest linked follow-up records for current verification status.

## Private configuration

Do not commit credentials, passwords, tokens, invitation links, private screenshots, Terraform state/plans, Lambda artifacts, AQA `TestResults/`, `aqa/restaurant-aqa-framework/.env`, `infra/terraform/terraform.tfvars`, `infra/terraform/backend.local.hcl`, or generated frontend production environment files. Templates such as `.env.example` and `terraform.tfvars.example` contain placeholders. Before sharing evidence, check images and logs for account IDs, emails, ARNs, cookies, and personal guest data.
