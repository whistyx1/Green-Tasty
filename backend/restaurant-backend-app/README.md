# Restaurant backend

The active API is a .NET 10 Lambda application deployed behind API Gateway. It uses Cognito authentication, DynamoDB persistence, and SQS/reporting Lambdas. Terraform owns the deployed AWS resources; the legacy Syndicate files and [local setup guide](local-setup-guide.md) document the original EPAM lesson path and are **not** instructions for updating this Terraform deployment.

## Code and contracts

- `dnapp/lambdas/api-handler/` contains HTTP routing, handlers, services, and repositories.
- `dnapp/lambdas/reports-handler/` and `dnapp/lambdas/report-sender/` handle background reports.
- [`docs/ApiRoutes.md`](docs/ApiRoutes.md) records the API routes; `infra/terraform/api-routes.json` is the Terraform API Gateway route map.
- [`BackendLevel1.md`](docs/BackendLevel1.md), [`BackendLevel2.md`](docs/BackendLevel2.md), and [`BackendLevel3.md`](docs/BackendLevel3.md) record the completed Owner participant list, Owner session/group submission, and guest removal homework, including authorization and manual acceptance limits.

## Local verification

From the repository root, with .NET SDK 10 installed:

```bash
dotnet restore backend/restaurant-backend-app/restaurant-backend-app.sln
dotnet build backend/restaurant-backend-app/restaurant-backend-app.sln --no-restore
dotnet test backend/restaurant-backend-app/Tests/APIHandler.Tests.csproj --no-restore
python3 scripts/check-deployment.py
```

The xUnit suite is local; it does not prove that a deployed API is healthy. The [latest repository validation](../../docs/epam-homework/Validation.md) records the current pass count and dependency warnings. To package or update the deployed functions, follow the [Terraform guide](../../infra/terraform/README.md) and review a plan before applying it. Do not use `syndicate clean` on Terraform-owned resources.
