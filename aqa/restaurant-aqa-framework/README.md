# Green & Tasty AQA — EPAM Direction 1

This .NET 10 project implements EPAM's group pre-order end-to-end homework with Reqnroll, TUnit, Playwright, and the Screenplay pattern. Level 1 covers Owner participant viewing, level 2 consolidated submission, and level 3 guest removal. The optional ReportPortal/AWS CodeBuild integration has a verified **Level 1** cloud run; Levels 2–3 have no claimed cloud run. The [live regression record](../../docs/epam-homework/aqa/FinalRegression.md) preserves the earlier Level 2 waiter-dashboard failures and records two successful 12/12 full Level 2 runs after the frontend fix was deployed. Levels 1 and 3 were not rerun after that deployment. The [repository validation](../../docs/epam-homework/Validation.md) records the earlier offline checks.

| Level | Requirement and evidence |
| --- | --- |
| 1 | [Participant view](../../docs/epam-homework/aqa/AqaLevel1.md) |
| 2 | [Group submission](../../docs/epam-homework/aqa/AqaLevel2.md) |
| 3 | [Guest removal](../../docs/epam-homework/aqa/AqaLevel3.md) |
| Bonus | [ReportPortal and CodeBuild](../../docs/epam-homework/aqa/BonusReportPortalCodeBuild.md) |

The `Features/` files hold Gherkin scenarios; `StepDefinitions/` connects them to `Screenplay/` tasks and questions; `Ui/` and `Api/` contain browser/API access; `Hooks/` owns lifecycle and reporting. [CLAUDE.md](CLAUDE.md) has contributor guidance. Unrelated original starter scenarios remain in the project and are **not** part of the 36 homework cases.

## Private configuration

Copy `.env.example` to ignored `.env` in this directory and fill `UI_URL`, `API_URL` (including `/develop`), and dedicated Owner, Other Customer, Waiter, and Admin account pairs as needed. Environment variables override `.env`; CI uses secret variables. Never commit or paste `.env`, passwords, tokens, invitation links, raw traces, or reports containing personal data. The level runners avoid printing configured values, redact retained text reports, and use masked failure screenshots, but retained artifacts should still be treated as private.

Each homework scenario creates its own future booking, table, and dishes in the standing Waiter's location. The runners remove owned guest sessions, dishes, and tables; cancelled reservation/group history may remain because the public API has no hard-delete route. Shared-prefix sweeping is disabled. A live run therefore mutates application data and requires an intentional choice of environment.

## Offline quality checks

From this directory, with .NET SDK 10 and Node.js installed:

```bash
dotnet restore restaurant-aqa-framework.csproj
dotnet format restaurant-aqa-framework.csproj --verify-no-changes --no-restore
dotnet build restaurant-aqa-framework.csproj --configuration Release --no-restore
npx --yes gherkin-lint@4.2.4 -c .gherkin-lintrc "Features/*.feature"
dotnet bin/Release/net10.0/restaurant-aqa-framework.dll --list-tests
```

The first build must complete before the assembly discovery command. The six Level 3 whitespace diagnostics were fixed in a formatting-only follow-up, and the format check now passes; see the [validation record](../../docs/epam-homework/Validation.md). `.github/workflows/aqa-quality.yml` runs restore, format, build, Gherkin lint, and discovery on matching pushes/PRs. Test discovery checks binding/loadability; it does **not** execute scenarios or access AWS.

From the repository root, the level runners support safe configuration checking and offline discovery:

```bash
python3 scripts/run-aqa-level1.py --check
python3 scripts/run-aqa-level1.py --list
python3 scripts/run-aqa-level2.py --list
python3 scripts/run-aqa-level3.py --list
```

## Live runs and CI

After configuring dedicated test accounts and confirming the target app is current, run a selected level intentionally:

```bash
python3 scripts/run-aqa-level1.py --live
python3 scripts/run-aqa-level2.py --live --scope api
python3 scripts/run-aqa-level3.py --live --scope ui
```

Each runner filters to its level, discovers cases before running, writes a fresh ignored `TestResults/levelN-*` directory, and verifies the resulting TRX has no failures or skipped cases. `--scope api` and `--scope ui` narrow a level. The default without `--live` does not run E2E. The `.github/workflows/aqa-tests.yml` workflow is manual opt-in; it is not the offline quality workflow. No current live GitHub Actions dispatch is claimed.

The optional manual CodeBuild project is described in the [bonus record](../../docs/epam-homework/aqa/BonusReportPortalCodeBuild.md). ReportPortal delivery is separate from a passing TRX. CodeBuild, hosted ReportPortal, and AWS application activity can incur cost; no job is scheduled automatically.
