# EPAM IT Marathon 6.0 homework

This directory separates the original EPAM assignment material from the completed work and its verification records. The active application is in the repository's `backend/` and `frontend/` directories. The `green-and-tasty/` and ignored `marathon2026-main/` folders are supplied reference copies, not deployment sources. Start with the [repository README](../../README.md) for architecture and local setup. For current check results and limits, read the [dated validation record](Validation.md).

| Homework | Implementation and verification | Original assignment |
| --- | --- | --- |
| DevOps: Terraform deployment | [Terraform guide](../../infra/terraform/README.md), [deployment verification](../../infra/terraform/DEPLOYMENT-VERIFICATION.md) | EPAM lesson path remains in the reference snapshot |
| Backend levels 1–3 | [Level 1](../../backend/restaurant-backend-app/docs/BackendLevel1.md), [Level 2](../../backend/restaurant-backend-app/docs/BackendLevel2.md), [Level 3](../../backend/restaurant-backend-app/docs/BackendLevel3.md); [API routes](../../backend/restaurant-backend-app/docs/ApiRoutes.md) | [Backend assignment](homework-backend.md) |
| Frontend levels 1–3 and Remove Guest | [Level 1](FrontendLevel1.md), [Level 2](FrontendLevel2.md), [Level 3](FrontendLevel3.md), [regression review](RegressionReview.md), [UI fixes](FrontendUiFixes.md) | [Frontend assignment](homework-frontend.md), [practice](practice-frontend.md), [design](homework-design/README.md) |
| Manual QA #7 | [Requirements analysis](qa/RequirementsAnalysis.md), [test cases](qa/US11-TestCases.xlsx), [screenshot assessment](qa/ScreenshotAssessment.md), [defect reports](qa/US11-DefectReports.xlsx) | [QA assignment](qa/homework-manual-qa.md) |
| AQA #8, Direction 1 levels 1–3 | [Framework guide](../../aqa/restaurant-aqa-framework/README.md), [Level 1](aqa/AqaLevel1.md), [Level 2](aqa/AqaLevel2.md), [Level 3](aqa/AqaLevel3.md), [final live regression](aqa/FinalRegression.md) | [AQA assignment](aqa/homework-aqa.md) |
| AQA bonus | [ReportPortal and CodeBuild record](aqa/BonusReportPortalCodeBuild.md) | Bonus section of the AQA assignment |

The frontend starter migration is explained in [SYNCHRONIZATION.md](SYNCHRONIZATION.md). Homework-specific screenshots are under `screenshots/`; review them for private data before sharing. The QA screenshot exercise evaluates EPAM's supplied static image, so it does **not** prove interactive behavior. Historical level documents record the state at their checkpoints; later fixes and the final validation record provide the current status.

## Verification boundaries

Backend and frontend unit tests, lint, build, Terraform validation, and AQA test discovery can run locally. AQA `--live` runners create test data in a configured environment; they require private `.env` settings and must be invoked intentionally. An earlier live regression passed 36/36 across the three levels; two later full Level 2 runs failed one waiter UI scenario each. After the frontend fix was deployed on 2026-11-03, two fresh full Level 2 runs passed 12/12 each. Levels 1 and 3 were not rerun after that deployment. The optional CodeBuild bonus was manually verified for Level 1 only. See the linked records for exact evidence and limitations.
