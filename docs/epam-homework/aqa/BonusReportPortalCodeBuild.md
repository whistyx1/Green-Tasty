# AQA bonus: ReportPortal and AWS CodeBuild

## Status and evidence

The opt-in ReportPortal Reqnroll agent and a manual CodeBuild project are
prepared. `dotnet build --configuration Release` passed with zero warnings;
offline discovery still finds 12 scenarios in each of levels 1, 2, and 3.
`terraform validate` passed. On 2026-11-02, the configured local Level 1 API
scope ran against the deployed app: 9 discovered, 9 passed, 0 failed, 0 skipped
in `TestResults/level1-20261102T110852Z-d3f05d67/`. Its TRX passed the
runner's verification. The [ReportPortal launch screenshot](../screenshots/aqa/reportportal-level1-api-9-passed.png)
shows the corresponding `Green-Tasty AQA #1` launch in the personal project,
with 9 total and 9 passed. This confirms the local run reached ReportPortal.
The demo instance flushes data after 24 hours, so the screenshot preserves the
evidence. On 2026-11-02 the manual AWS CodeBuild Level 1 run completed with
all phases successful. Its uploaded private result ZIP contains one TRX with
12 total, 12 passed, 0 failed, and 0 skipped, plus `summary.json`; there were
no failure screenshots. An initial cloud run stopped at source download
because the accounts secret had a mistyped JSON key. The key was corrected in
Secrets Manager and the successful run needed no Terraform change. The
first successful cloud launch had 12 passing test cases but was labeled
`FAILED` by ReportPortal. The Reqnroll agent finished the two feature suites
as `SKIPPED` even though their child scenarios passed. The project now records
the actual scenario outcomes and finishes each feature suite accordingly in
`Hooks/ReportPortalFeatureStatusHooks.cs`; failed or skipped scenarios still
produce failed or skipped suite statuses. A focused local API rerun passed 9/9
and ReportPortal marked its launch `PASSED`. After Terraform updated only the
private CodeBuild source ZIP, a second cloud Level 1 run succeeded: the uploaded
TRX has 12 passed, 0 failed, 0 skipped, and ReportPortal's API reports a
`PASSED` launch with 12 passed and all 67 reported items passed. The
[CodeBuild project screenshot](../screenshots/aqa/codebuild-level1-succeeded.png)
shows the project success status. The
[ReportPortal cloud launch screenshot](../screenshots/aqa/reportportal-codebuild-level1-passed-12.png)
shows `Passed 100.00%` and `Total: 12`. Neither screenshot contains credentials.

## ReportPortal setup

1. Create a reachable ReportPortal instance/project and an API key. A local
   Docker instance is suitable for a local run, but CodeBuild needs a publicly
   reachable server or private network access configured separately.
2. In the ignored `aqa/restaurant-aqa-framework/.env`, set
   `REPORTPORTAL_ENABLED=true`, `REPORTPORTAL_URL`,
   `REPORTPORTAL_PROJECT`, and `REPORTPORTAL_API_KEY`. Do not share or commit
   the key. `ReportPortal.json` in the repository contains no credentials and
   has reporting disabled by default.
3. Run one selected level with `python3 scripts/run-aqa-level1.py --live`.
   The local TRX remains the test pass criterion. Check the ReportPortal launch
   and scenario count in its dashboard separately; do not infer delivery from
   a green local TRX. The runner keeps reporting off during discovery.

The agent can forward scenario output and attachments to the ReportPortal
server before local redaction runs. Use dedicated synthetic test accounts and
review the ReportPortal project's access permissions. The runner disables raw
Playwright traces and masks failure screenshots; generated guest names and
test data can still be visible to ReportPortal viewers. Agent HTTP diagnostics
(`ReportPortal.*.log`) are ignored by Git.

## AWS CodeBuild setup

CodeBuild is optional Terraform (`enable_aqa_codebuild=false` by default) and
has no webhook or schedule. It reads a source ZIP in the existing private S3
artifacts bucket and runs only when started manually. Build the ZIP with
`python3 scripts/package-aqa-codebuild.py` before Terraform planning. The
script includes only the AQA source, its analyzer settings, buildspec, and
three level runners; it excludes local `.env`, generated code, results,
browser cache, and compiled files. The ZIP stays in ignored `.build/` locally
and is uploaded by Terraform
to `aqa-source/current.zip`. This permits a cloud run before the final GitHub
commit and push.

In AWS Secrets Manager, create a JSON secret in the same region as CodeBuild
with these keys, using dedicated test account values:

```text
OWNER_USERNAME  OWNER_PASSWORD
OTHER_CUSTOMER_USERNAME  OTHER_CUSTOMER_PASSWORD
WAITER_USERNAME  WAITER_PASSWORD
ADMIN_USERNAME  ADMIN_PASSWORD
```

Keep its ARN private in ignored `infra/terraform/terraform.tfvars` as
`aqa_ci_accounts_secret_arn`; do not put the JSON secret value or account
passwords into Terraform. Set `enable_aqa_codebuild=true` and configure the
account secret ARN. For ReportPortal in the cloud, create a second JSON
secret containing `REPORTPORTAL_API_KEY`, set its ARN in
`aqa_reportportal_secret_arn`, and set `aqa_reportportal_url` and
`aqa_reportportal_project`. The URL must be reachable from AWS. Terraform
references secret ARNs only; it does not manage secret values.

Review the full Terraform plan, then apply it. Keep the source ZIP unchanged
between a saved plan and its apply. This creates one CodeBuild project,
service role/policy, a seven-day CloudWatch log group, a prefix-scoped
seven-day S3 lifecycle rule, and the source ZIP object. The existing private,
encrypted artifacts bucket stores the selected run's results ZIP under `aqa/`.
No application Lambda, API, or frontend change is required by
the bonus configuration. Run level 1 manually with the project's default
`AQA_LEVEL=1`; to run level 2 or 3, override `AQA_LEVEL` to `2` or `3` when
starting a build. The job builds the .NET suite, installs Chromium and Linux
dependencies, then runs the selected level against the deployed CloudFront
and API Gateway URLs. Failed tests fail the job, while CodeBuild still attempts
artifact upload. Inspect the fresh TRX and `summary.json`; require all
discovered cases to pass with zero skips. Check the ReportPortal launch too
when enabled.

To start a default Level 1 run:

```bash
aws codebuild start-build --project-name epam-restaurant-aqa --region us-west-2
```

The CodeBuild console shows the
build status and CloudWatch log link. Start another level by supplying an
`AQA_LEVEL` environment variable override. Check the run's result ZIP in the
private artifacts bucket under `aqa/` and the ReportPortal dashboard if enabled.

CodeBuild runtime minutes, CloudWatch logs, S3 source/results, and Secrets Manager
can cost money. A $10 budget notification does not stop spending. The job is
manual to avoid automatic live test data creation and unexpected runs. Results
are private and retained only as long as your S3 lifecycle policy permits;
the CodeBuild setup does not delete existing application artifacts.
