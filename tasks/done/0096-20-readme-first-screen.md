---
type: docs
priority: high
created: 2026-10-31
closed: 2026-11-02
---
# README: show the license, safety defaults and project status on the first screen

## Release note
None. README and documentation only.

## Problem
An outside review (2026-10-31) said the license was not specified and asked whether there is a safeguard against deploying an old build by mistake. Both exist, but they are not visible:
- License: MIT in `LICENSE` and `PackageLicenseExpression` in all four packages. `README.md` mentions it only in the last lines (`:323`), with a link to opensource.org instead of the file.
- Downgrade guard: without `AllowDatabaseDowngrade = true`, deploying an older build fails with `DowngradeNotAllowedException` and changes nothing (`src/DbLive/Deployers/Migrations/DowngradeDeployer.cs:32`). But the "Rollback" rows in `README.md:23` and `docs/index.md`, and the "Downgrade" bullet in `docs/llms-full.txt` §1, say only that an older build reverts newer versions. That reads as "an old artifact deployed by mistake loses data".

## Plan
- [x] Badges at the top: NuGet version, CI status, license (link to `LICENSE`).
- [x] A short "Safe by default" list: downgrade blocked unless `AllowDatabaseDowngrade`; changed applied migrations block deployment (`MigrationChecksumMode.Strict`); `ProjectId` blocks deployment into another project's database; tests do not run during deployment (after 0096-17).
- [x] "Rollback" rows: say that downgrade is opt-in.
- [x] A "Status" line: current version, supported .NET and engines, link to the changelog and versioning policy (0096-19).
- [x] License line at the end links to `LICENSE`.
- [x] Is DbLive used in production somewhere? If yes, say so (ask the maintainer).
- [x] Link the comparison page (0096-21): moved to 0096-21, whose "Done when" already has "Linked from README and docs home".

## Docs to update
- `README.md`
- `docs/index.md` ("What you get": Rollback, Safety)
- `docs/llms-full.txt` §1 (Downgrade bullet)
- `src/*/README.md` (package READMEs): license and changelog links

## Done when
- [x] README and docs home updated
- [x] Package READMEs updated

## Notes
- The texts were not only hidden but wrong: `docs/index.md` and `docs/llms-full.txt` §1 said "deploying an older build reverts newer versions", and `README.md` said "downgrade to a target version". There is no target version parameter: a downgrade happens only when a build that lacks applied migrations is deployed, and only with `AllowDatabaseDowngrade = true`. The detailed sections (`docs/deployment/migrations.md` "Downgrade", `docs/llms-full.txt` §5.5, `docs/features/index.md`) were already right.
- 0096-19 replaced the changelog with GitHub Releases and `docs/versioning.md`, so "changelog" in the plan became a link to the release notes. The README already had a "Releases" line from 0096-19.
- The Status line does not name a version: the NuGet badge shows it, and a version written in the text goes stale with every merge to `main`.
- Is DbLive used in production? Maintainer, 2026-11-02: not yet. The README does not claim production use.
- "Before any of your scripts runs" in "Safe by default" follows the pipeline order (`docs/llms-full.txt` §12): ProjectId (4) and checksum validation (7) come before the downgrade check (9), and all three before BeforeDeploy (10).

## Result
- `README.md`: badges (NuGet version, CI, license → `LICENSE`); new "Safe by default" section (downgrade refused unless `AllowDatabaseDowngrade`, `Strict` checksums, `ProjectId`, breaking changes opt-in, no SQL tests in a deployment); "Status" section (release on every merge, breaking changes only in a major version, release notes, versioning policy, .NET 8/9/10, engines, license); "Rollback" row, "Deployment flow" (new "Checks" step, downgrade without "target version") and "Design goals" say that downgrade is opt-in; license link at the end goes to `LICENSE`.
- `docs/index.md`: "Rollback" and "Safety" rows rewritten the same way; release notes, versioning policy and license under "Packages".
- `docs/llms-full.txt` §1: the Downgrade bullet says opt-in and names `DowngradeNotAllowedException`.
- Package READMEs (`src/DbLive*/README.md`): links to the release notes, the versioning policy and `LICENSE`.
- `mkdocs build --strict` passes.
