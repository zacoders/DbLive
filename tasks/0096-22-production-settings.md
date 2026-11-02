---
type: docs
priority: medium
created: 2026-10-31
---
# Production settings in one place

## Problem
Advice for production deployments is spread over many pages: tests (`docs/llms-full.txt` §0 rule 12, tip in `docs/deployment/index.md`), tests failing after the commit (`docs/unit-testing/index.md`, "Running all tests without xUnit"), downgrade (`docs/deployment/migrations.md`), `ProjectId` (`docs/features/project-id.md`), `TransactionWrapLevel.Deployment` not for 24/7 (`docs/project/configuration.md`). A reader who wants "how do I configure this for production" has to collect it.

`docs/deployment/zero-downtime.md` has a "Pre-flight checklist", but it is about backward compatibility of a release, and the maintainer writes that page (ask before changing it).

## Plan
A "Production" section in `docs/deployment/index.md`, the one home for production settings; other pages link to it:
- [ ] Tests: a deployment never runs them (0096-17); a production deployer does not call `RunAllTestsAsync()`, tests run in CI against a disposable database. Tests after a deployment run after the commit: they report a bad release, they do not stop it.
- [ ] `AllowDatabaseDowngrade` only in the rollback pipeline (or a target version after 0096-26).
- [ ] `ProjectId` set.
- [ ] `MigrationChecksumMode` stays `Strict`.
- [ ] `TransactionWrapLevel`: `Migration` (default) or `None` per item for long operations; not `Deployment` for large or 24/7 databases.
- [ ] Timeouts: `MigrationTimeout`, `CodeItemTimeout` and per-item `Timeout`.
- [ ] Ask the maintainer: link to this section from the pre-flight checklist in `zero-downtime.md`?

## Docs to update
- `docs/deployment/index.md` (new section)
- `docs/llms-full.txt` §18.10 (link the same settings)
- `README.md` ("Typical CI/CD pipeline", link)

## Done when
- [ ] Section written, other pages link to it instead of repeating it
- [ ] `mkdocs build --strict` passes
