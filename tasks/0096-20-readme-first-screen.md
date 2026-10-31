---
type: docs
priority: high
created: 2026-10-31
---
# README: show the license, safety defaults and project status on the first screen

## Problem
An outside review (2026-10-31) said the license was not specified and asked whether there is a safeguard against deploying an old build by mistake. Both exist, but they are not visible:
- License: MIT in `LICENSE` and `PackageLicenseExpression` in all four packages. `README.md` mentions it only in the last lines (`:323`), with a link to opensource.org instead of the file.
- Downgrade guard: without `AllowDatabaseDowngrade = true`, deploying an older build fails with `DowngradeNotAllowedException` and changes nothing (`src/DbLive/Deployers/Migrations/DowngradeDeployer.cs:32`). But the "Rollback" rows in `README.md:23` and `docs/index.md`, and the "Downgrade" bullet in `docs/llms-full.txt` §1, say only that an older build reverts newer versions. That reads as "an old artifact deployed by mistake loses data".

## Plan
- [ ] Badges at the top: NuGet version, CI status, license (link to `LICENSE`).
- [ ] A short "Safe by default" list: downgrade blocked unless `AllowDatabaseDowngrade`; changed applied migrations block deployment (`MigrationChecksumMode.Strict`); `ProjectId` blocks deployment into another project's database; tests do not run during deployment (after 0096-17).
- [ ] "Rollback" rows: say that downgrade is opt-in.
- [ ] A "Status" line: current version, supported .NET and engines, link to the changelog and versioning policy (0096-19).
- [ ] License line at the end links to `LICENSE`.
- [ ] Is DbLive used in production somewhere? If yes, say so (ask the maintainer).
- [ ] Link the comparison page (0096-21).

## Docs to update
- `README.md`
- `docs/index.md` ("What you get": Rollback, Safety)
- `docs/llms-full.txt` §1 (Downgrade bullet)
- `src/*/README.md` (package READMEs): license and changelog links

## Done when
- [ ] README and docs home updated
- [ ] Package READMEs updated
