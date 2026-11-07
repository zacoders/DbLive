---
type: docs
priority: medium
created: 2026-10-31
closed: 2026-11-03
---
# Production settings in one place

## Release note
None. Documentation only.

## Problem
Advice for production deployments is spread over many pages: tests (`docs/llms-full.txt` §0 rule 12, section "SQL tests" and its tip in `docs/deployment/index.md`), downgrade (`docs/deployment/migrations.md`), `ProjectId` (`docs/features/project-id.md`), `TransactionWrapLevel.Deployment` not for 24/7 (`docs/project/configuration.md`). A reader who wants "how do I configure this for production" has to collect it.

Some advice is written nowhere: that `AllowDatabaseDowngrade` belongs only in the rollback pipeline, and that a production project should set `ProjectId`. And two defaults were unsafe for production: `DeployParameters.Default` created a missing database, and nothing warned about a missing `ProjectId`; both are changed in 0096-34.

`docs/deployment/zero-downtime.md` has a "Pre-flight checklist", but it is about backward compatibility of a release, and the maintainer writes that page (ask before changing it).

## Plan
A "Production" section in `docs/deployment/index.md`, the one home for production settings; other pages link to it:
- [x] `ProjectId` set; without it every deployment logs a warning (0096-34).
- [x] `CreateDbIfNotExists` off, the default since 0096-34: the production database is created outside DbLive.
- [x] `AllowDatabaseDowngrade` only in the rollback pipeline (or a target version after 0096-26).
- [x] `RepairMigrationChecksums` only for one deliberate deployment.
- [x] `MigrationChecksumMode` stays `Strict`.
- [x] `TransactionWrapLevel`: `Migration` (default) or `None` per item for scripts that cannot run in a transaction; not `Deployment` for large or 24/7 databases.
- [x] Timeouts: `MigrationTimeout`, `CodeItemTimeout` and per-item `Timeout`.
- [x] Tests: a deployment never runs them (0096-17); the section links to "SQL tests" of the same page.
- [x] Ask the maintainer: link to this section from the pre-flight checklist in `zero-downtime.md`? No (2026-11-03).

## Docs to update
- `docs/deployment/index.md` (new section "Production", a link from "A deployer")
- `docs/llms-full.txt` §18.10 (the same settings as a list), rules 17 and 18
- `README.md` ("Typical CI/CD pipeline", link)

## Done when
- [x] Section written, other pages link to it instead of repeating it
- [x] `mkdocs build --strict` passes

## Notes
- 2026-11-02: the plan item "tests after a deployment run after the commit: they report a bad release, they do not stop it" is obsolete. Since 0096-17 a deployment does not run the tests at all; the section only says that they are not in the deployer and links to "SQL tests".
- 2026-11-02, maintainer: `CreateDbIfNotExists` off by default and a warning without `ProjectId`, done in 0096-34. The section lists both, because the defaults alone do not tell a reader why they matter.
- Added to the plan: `CreateDbIfNotExists` (with it on, a typo in the database name of a production connection string created an empty database and deployed the whole schema into it; `ProjectId` does not catch it, because a new database has no ProjectId) and `RepairMigrationChecksums` (the same risk as `AllowDatabaseDowngrade`: a flag for one deliberate deployment that stays in a pipeline).
- The section is a table with the production value and the reason for each setting, and links to the page with the details; the details stay on their pages.
- 0096-26 changes the downgrade row: its "Docs to update" lists the section.
- `docs/deployment/zero-downtime.md` not changed (the maintainer's page).
- 2026-11-03, maintainer: no link from the pre-flight checklist in `zero-downtime.md` to the section. The checklist is about backward compatibility of a release, not about the settings of the deployer, and the page already links to `index.md` twice ("What a deployment does", "SQL tests").

## Result
- `docs/deployment/index.md`: new section "Production", a table of the settings of a production deployment with the value to use, the reason and a link to the page with the details: `ProjectId`, `CreateDbIfNotExists`, `AllowDatabaseDowngrade`, `RepairMigrationChecksums`, `MigrationChecksumMode`, `TransactionWrapLevel`, `MigrationTimeout` / `CodeItemTimeout` / per-item `Timeout`, SQL tests. "A deployer" links to it. The details stay on their pages.
- `docs/llms-full.txt`: §18.10 lists the same settings; rules 17 (`ProjectId`) and 18 (no `CreateDbIfNotExists` or `AllowDatabaseDowngrade` in a production deployer) point to it.
- `README.md`: "Typical CI/CD pipeline" links to the section.
- The two unsafe defaults the problem names (a missing database created, no warning without `ProjectId`) were changed in 0096-34, in the same commit as the section.
- `docs/deployment/zero-downtime.md` not changed (maintainer's decision, see Notes).
- `mkdocs build --strict` passes.
