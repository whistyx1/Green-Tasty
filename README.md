# Green & Tasty

Green & Tasty is a restaurant booking and group pre-order application. Customers can browse restaurants and menus, reserve a table, invite guests to choose dishes, and submit one combined order. Waiters can view reservations and submitted pre-orders; admins manage restaurant data.

## Architecture

| Part | Technology | Purpose |
| --- | --- | --- |
| Web app | Angular 22, TypeScript, SCSS | Customer, waiter, and admin interfaces; hosted on S3 behind CloudFront |
| API | .NET 10 on AWS Lambda and API Gateway | Reservations, menus, group orders, user and waiter operations |
| Identity and data | Amazon Cognito and DynamoDB | Authentication, roles, and application records |
| Background processing | SQS, Lambda, EventBridge Scheduler, SES | Reports and email delivery |
| Infrastructure | Terraform | AWS resources and deployment configuration |

## Repository

- [Frontend](frontend/restaurant-frontend-app/README.md) — Angular app, local setup, and UI checks.
- [Backend](backend/restaurant-backend-app/README.md) — Lambda source, API routes, and .NET tests.
- [Infrastructure](infra/terraform/README.md) — Terraform configuration and deployment steps.
- [End-to-end tests](aqa/restaurant-aqa-framework/README.md) — API and browser test framework.
- [Feature records](docs/features/README.md) — implemented features and verification notes.
- `scripts/` — packaging, validation, and deployment helpers.

## Run locally

Use the Node.js version in [the frontend `.nvmrc`](frontend/restaurant-frontend-app/.nvmrc). To start the web app:

```bash
cd frontend/restaurant-frontend-app
npm ci
npm start
```

Open `http://localhost:4200/`. The development configuration connects to a deployed API, so real sign-in and data operations require that API to be available. See the frontend guide for development settings and build configuration.

## Check the project

With .NET SDK 10 and Python 3 installed, run backend checks from the repository root:

```bash
dotnet test backend/restaurant-backend-app/Tests/APIHandler.Tests.csproj
python3 scripts/check-deployment.py
```

Run frontend checks from `frontend/restaurant-frontend-app/`:

```bash
npm run lint
npm test -- --watch=false --browsers=ChromeHeadless
```

Browser tests need Chrome or Chromium. The [end-to-end test guide](aqa/restaurant-aqa-framework/README.md) covers its separate setup; live scenarios require a running environment and private test credentials.

## Deploy to AWS

Terraform defines the API, identity and data services, background workers, storage, and CloudFront distribution. Follow the [deployment guide](infra/terraform/README.md) to configure an AWS account, build Lambda packages, review a plan, and publish the frontend. Production builds must use the actual API URL from Terraform. AWS resources can incur charges.

Keep credentials, local `.env` files, Terraform state and plans, and generated build or test artifacts out of Git. Use the configuration examples in the component guides as starting points.
