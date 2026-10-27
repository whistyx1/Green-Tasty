# Terraform deployment — DevOps homework option 2

This path replaces Syndicate with Terraform for the existing backend. Use the
top-level application (`backend/` and `frontend/`); `green-and-tasty/` is the
separate frontend homework starter. Do not overwrite one with the other.

The root README describes the original Terraform + Syndicate path. **Do not run
`syndicate deploy`, `update`, or `clean` against this Terraform-owned deployment.**
The existing Syndicate metadata remains a reference for the separate lesson path.

## What owns what

- `scripts/bootstrap-state.sh`: explicitly creates/configures the state S3 bucket.
- `scripts/build-lambdas.py`: builds Linux x64 ZIPs locally; no AWS operations.
- Terraform: tables, queues, user pool/client/groups, application buckets,
  Lambda code/roles/aliases/log groups, API Gateway, CloudFront, and Scheduler.
- npm + AWS CLI: build and publish the existing frontend.
- Manual setup: SES verification, initial Admin membership, restaurant data.

Configuration map:

| File | Purpose |
| --- | --- |
| `tables.json`, `dynamodb.tf` | Table/index keys; on-demand billing |
| `lambda-environment.tf` | Handler strings and environment references |
| `lambda.tf`, `lambda-iam.tf` | ZIP deployment, versions/aliases, roles, logs, SQS trigger |
| `api-routes.json`, `api-gateway.tf` | Implemented routes, authentication, OpenAPI integration, CORS, stage |
| `s3.tf`, `cloudfront.tf`, `artifacts.tf` | Website, images, Swagger bucket, deployment packages |
| `cognito.tf`, `sqs.tf`, `eventbridge.tf` | Authentication, background processing, weekly schedule |
| `budget.tf` | Optional account-wide monthly spending notification |

Tables use on-demand capacity rather than the lesson's provisioned capacity.
Each Lambda has 512 MiB and a 100-second timeout; API Gateway integration waits
at most 29 seconds. No VPC/NAT is needed. Logs expire after 30 days. Weekly email
starts disabled. AWS provider 6.x is required for `dotnet10`; initialization must
upgrade the old 5.x lockfile. Commit the regenerated lockfile after review.

## 1. Check the account (read-only)

All commands below are run from the repository root unless stated otherwise.
Use .NET SDK 10, Python 3, Terraform >=1.10, AWS CLI, and Node matching `.nvmrc`.
Set `AWS_PROFILE` if using a non-default profile. Account IDs are discovered
locally; you do not need to paste them into a conversation.

```bash
python3 scripts/preflight-aws.py
python3 scripts/check-deployment.py
```

Preflight reports names, not account IDs/ARNs. Deleted Lambdas may leave their
IAM roles and log groups behind. If collisions appear, inspect ownership and
import resources or choose an isolated naming scheme; do not delete blindly.
Preflight covers common name collisions, not every AWS permission or quota.

## 2. Build Lambda packages (local)

```bash
python3 scripts/build-lambdas.py
dotnet test backend/restaurant-backend-app/Tests/APIHandler.Tests.csproj
```

The script restores NuGet dependencies, publishes with `linux-x64` and
`--self-contained false`, and places the assembly/deps/runtimeconfig files at
each ZIP root. There is no Lambda CLI or Syndicate dependency. A matching managed
`dotnet10` Lambda runtime runs the code. API Gateway v1 events are preserved.
The worker handler is `ReportsHandler::ReportsHandler.Function::FunctionHandler`.
The three ZIPs are in ignored `artifacts/lambdas/`. Build before planning;
Terraform hashes these local packages and uploads them to its private artifacts
bucket. Keep packages unchanged between a saved plan and its apply.

## 3. Supply local settings

```bash
cp infra/terraform/terraform.tfvars.example infra/terraform/terraform.tfvars
```

Edit the copy: set a real notification email and your budget. The budget is
account-wide and only sends notifications (80% actual spend); it does not cap
spending. An empty `alert_emails` list creates no budget. Keep the schedule
disabled until SES and recipients are ready. No credentials go in tfvars.

## 4. Bootstrap remote state (first AWS mutation)

```bash
bash scripts/bootstrap-state.sh
terraform -chdir=infra/terraform init -upgrade -backend-config=backend.local.hcl
```

The script creates/configures `restaurant-terraform-state-<account>` in
`us-west-2`, enabling versioning, private access, and SSE-S3 encryption. It writes
ignored `backend.local.hcl`. This bucket exists outside the application state so
destroying the application cannot delete its own state. Terraform uses an S3
lockfile, not a DynamoDB locking table. Use one AWS account/region consistently.
If changing region, set `AWS_REGION` for bootstrap and change `aws_region` too.

## 5. Review a plan, then apply that plan

```bash
terraform -chdir=infra/terraform fmt -check -recursive
terraform -chdir=infra/terraform validate
terraform -chdir=infra/terraform plan -out=deployment.tfplan
```

For a fresh deployment, expect additions and **no replacements/deletions**.
Review resource names, region, IAM permissions, public image/Swagger policies,
and expected costs. Preserve a failed/partial deployment's state and re-plan;
do not clean it using Syndicate. Only after reviewing the plan:

```bash
terraform -chdir=infra/terraform apply deployment.tfplan
```

This creates billable resources. On-demand tables do not reserve idle capacity,
but reads/writes/storage, S3, API traffic, Lambda, logs, queue requests, CDN and
email activity can still cost money. Inspect Billing; budgets are delayed alerts.
Account-level S3 Block Public Access may reject public image/Swagger policies.
Review those policies and the account setting deliberately; do not turn off
account-wide protections as a generic workaround. Frontend/artifacts/profile
buckets remain private.

## 6. Smoke-test the API

```bash
BACKEND_URL=$(terraform -chdir=infra/terraform output -raw backend_url)
curl --fail-with-body "$BACKEND_URL/locations"
```

On empty data expect an empty response, not preloaded restaurants. Verify public
reads, registration/login, and protected routes. API Gateway checks Cognito ID
tokens; the five previously public create/waiter-management operations additionally
require the `Admin` group in Lambda. Bootstrap an Admin through the Cognito console
after registering a user, then log in again to obtain updated group claims.
Guest bearer tokens continue to be checked by the existing guest services.

Use CloudWatch log groups `/aws/lambda/api-handler`, `reports-handler`, and
`report-sender` to investigate failures. Do not paste passwords/tokens/logs with
personal data. This deployment does not complete the backend homework: its
missing owner endpoints still need implementation before the frontend level 3.

## 7. Publish the existing frontend

```bash
export BACKEND_URL=$(terraform -chdir=infra/terraform output -raw backend_url)
FRONTEND_BUCKET=$(terraform -chdir=infra/terraform output -raw frontend_bucket_name)
cd frontend/restaurant-frontend-app
npm ci
npm run build
aws s3 sync dist/restaurant-frontend-app/browser "s3://$FRONTEND_BUCKET/" --delete
cd ../..
terraform -chdir=infra/terraform output cloudfront_distribution_domain_name
```

Open the HTTPS CloudFront host. S3 sync's `--delete` removes remote files absent
from the local build. For later updates, invalidate `/index.html` (and `/` if
cached), or wait for expiry. Hashed JS/CSS filenames handle changed assets.
After adding your registered user to the Cognito `Admin` group, run from the
repository root:

```bash
python3 scripts/seed-demo.py
```

It prompts for the existing Admin login (hidden password), then creates one demo
location, three four-person tables, and three dishes through the API. Tokens and
credentials are not written to disk. IDs/progress are kept in ignored
`.build/demo-data.json`; keep it to resume interrupted runs. Run only one copy at
a time. Refresh menu/locations afterward. This is sample application data, not
Terraform-managed infrastructure. Register a separate waiter account through the
website, then run:

```bash
python3 scripts/seed-demo.py --assign-waiter
```

Log in with the Admin credentials at the first prompts, then enter the registered
waiter email. This calls `POST /waiters` to create the DynamoDB assignment and add
the Cognito Waiter group. It does not reseed data or require the waiter's password.
Sign in again as the waiter to obtain updated group claims. An existing waiter
assignment returns HTTP 409 and needs review rather than automatic reassignment.

Swagger bucket provisioning does not upload Swagger UI; treat UI publication as
optional. The supplied Swagger spec has an old API URL and must be updated first.

## Updates and cleanup

### Enable email notifications

Budget emails are independent of SES. The deployed `epam-restaurant-monthly`
budget has a direct-email notification when actual account-wide monthly spending
exceeds 80% of `monthly_budget_usd`. With a $10 budget, that threshold is $8.
Confirm its recipients/notification under Billing and Cost Management > Budgets.
Billing updates and alerts are not immediate; an alert does not stop spending.

Weekly restaurant reports use SES in `us-west-2` and go to the email attributes of
all Cognito `Admin` group members. To enable them:

1. In ignored `terraform.tfvars`, set `email_from` to an address you control.
   For this demo, use the Admin user's email as both sender and recipient. Keep
   `enable_weekly_reports = false` for this first apply.
2. Validate, create/review a new saved plan, and apply it. Terraform creates an
   SES email identity, updates the sender's Lambda configuration/alias, and
   restricts its SES send permission to that identity. AWS sends a verification
   email; open its link. SES identity verification is separate from Cognito's
   `email_verified` user attribute. If the identity was already created manually,
   review/import it instead of creating conflicting ownership.
3. Confirm the SES identity is verified in Oregon. If SES is in the sandbox,
   verify every report recipient as well. With just one Admin using the same
   verified address as sender, a second identity is not necessary. Do not add
   unverified Admin recipients while relying on sandbox sending.
4. Verify SES sending is enabled in its account dashboard. Set
   `enable_weekly_reports = true`, create/review a fresh plan, and apply it.
   The schedule runs Fridays at 08:00 UTC (the local time shifts with DST).
5. Invoke the `report-sender` Lambda's `dev` alias once to test email delivery.
   This sends a real email to all Admin recipients. Inspect the invocation result
   and CloudWatch logs, then check the inbox/spam folder for the report. With no
   completed activity aggregates, the existing app can produce empty CSV files.
   Do not label report content/aggregation verified merely because email arrived.

Changing Terraform files alone does not enable delivery. Record the actual SES
verification, schedule apply, and delivery test results in the verification
report only after completing them. SES sends and Lambda invocations can incur
usage charges. To stop future scheduled emails, set `enable_weekly_reports` back
to false and apply that change.

### Application updates and teardown

Code update: rebuild ZIPs, run tests, plan, review, apply. API changes: update the
runtime route registry and `api-routes.json`; run `scripts/check-deployment.py`.
The OpenAPI hash triggers a new deployment; `develop` points at that snapshot.
API Gateway normalizes `/locations/{locationId}` to `/locations/{id}` internally
because sibling variable names must match; Lambda restores its own parameter names.

Review a destroy plan before cleanup. Empty populated frontend/image/profile/
Swagger buckets first. Terraform removes its tracked Lambda package objects,
but unrelated objects may still block artifacts bucket deletion. Do not disable
bucket safety with `force_destroy` just to bypass a review. Application teardown
does not remove the state bucket; retain it until teardown has completed, then
remove all state versions/delete markers before deleting it separately.
Destroy deletes application data, user accounts, queues, and logs: export needed
data first. There is no automated backup/recovery workflow in this training stack.
