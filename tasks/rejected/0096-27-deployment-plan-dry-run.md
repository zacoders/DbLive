---
type: feature
priority: low
created: 2026-10-31
rejected: 2026-11-03
---
# Show what a deployment would do, without running it

## Rejected
Rejected by the maintainer on 2026-11-03. The dry run of a DbLive project is its deployment inside the unit tests, together with the SQL unit tests; then come the tests of the application against the database, and the QA and UAT environments. A plan adds no control on top of that:
- DbLive scripts are written by people and reviewed in the pull request, so a plan of pending files repeats the git diff between the deployed tag and the new build. A plan earns its place in state-based tools (Terraform, SSDT), where the tool generates the change script and people must see what it generated.
- An approver sees file names, and cannot judge the risk of a change from them.
- The cases the plan was meant to show already fail before anything runs: a downgrade without undo scripts or without `AllowDatabaseDowngrade`, a checksum mismatch, a ProjectId mismatch.

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
