---
type: feature
priority: low
created: 2026-10-31
---
# Show what a deployment would do, without running it

## Problem
There is no way to see what `DeployAsync` would change before it runs (no dry run or plan in `src`, verified by search). Flyway has `info` and `validate`. A plan is useful as an approval step in a release pipeline, and before a rollback.

## Fix
A read-only call, for example `IDbLive.GetPlanAsync(DeployParameters)`, that returns:
- versions to undo, and whether each has an undo script;
- migrations to apply;
- code items that changed;
- pending breaking items;
- checksum mismatches and the `ProjectId` check result.

Open questions:
- [ ] What if the database or the `dblive` schema does not exist yet? The plan must not create or upgrade them.
- [ ] Output: a typed result only, or also a logged summary?

## Docs to update
- `docs/llms-full.txt` §11.2, §12, §18.8, §18.10
- `docs/deployment/index.md`

## Done when
- [ ] Implemented with unit tests and an integration test on both engines
- [ ] Docs updated
