# AQA project instructions

- Work in `restaurant-aqa-framework`; read its `README.md` and `CLAUDE.md` before changes.
- The EPAM source is in `../marathon2026-main` as local reference. Do not overwrite the main frontend/backend/Terraform from it.
- Direction 1 levels 1, 2 and 3 and the opt-in ReportPortal/CodeBuild bonus are implemented. Live runs require private configuration and the current deployed frontend; use the level-specific runner in scripts/. The bonus cloud run has only been verified for Level 1.
- Preserve the chosen .NET 10 / Reqnroll / TUnit / Playwright / Screenplay structure and exact starter package versions.
- Keep credentials in ignored `.env` or CI secrets. Never print tokens, passwords or invitation URLs in reports.
- Keep shared-prefix cleanup disabled until run-owned cleanup and permissions are adapted.
- Use actual backend API contracts and Admin checks; never change production authorization to accommodate starter helpers.
- Tag/filter future levels independently and preserve real UI happy paths plus API authorization/validation cases. Do not claim unit tests or skipped/WIP examples complete E2E homework.
- Do not enable local hooks, schedule live tests, deploy, commit or push as part of AQA implementation unless separately requested.
