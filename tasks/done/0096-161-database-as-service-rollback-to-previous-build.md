---
type: docs
priority: low
created: 2026-11-24
closed: 2026-11-24
---
# `docs/database-as-service.md`: rolling back to the previous build redeploys `_v1`

## Release note
None. A correction of the docs: "Database as a Service" no longer says that a release is rolled back by deploying the previous build of the database; it says to roll back the application and keep the database. DbLive behaves as before.

## Problem
`docs/database-as-service.md:67`, after the example of `MyStoredProc_v1` → `MyStoredProc_v2`: "Rolling back to the previous build redeploys `my-stored-proc.sql` with `_v1`; `_v2` stays and does not disturb the old application."

That holds only for a release without new migration versions and without a DbLive upgrade that raised the minimum DbLive version (`docs/llms-full.txt` §18.8, line 1214). The `_v2` of the example filters on `TenantId`, a column that usually comes with a migration of the same release: then the previous build fails with `DatabaseAheadOfProjectException` and changes nothing (§5.2, §18.8 line 1213). After such a rollback the breaking phase of the newer build must never run (§18.8, line 1214). The ways that always work: roll back the application and keep the database, or revert the file and deploy a new release (§6, line 352).

The page is the maintainer's: ask before rewriting its text.

## Docs to update
- `docs/database-as-service.md:67`

## Done when
- [x] Wording agreed with the maintainer and updated

## Notes
- Found by the code review of 2026-11-24.
- The page already says that the breaking phase never runs for a release that was rolled back ("The team must ensure", added by 0096-139); the Problem first said it did not.
- Even for a code-only release, deploying the previous build changes nothing that matters here: DbLive never drops code, so `_v1` is still in the database, and the redeploy runs the same `create or alter` again.
- Decided by the maintainer on 2026-11-24: replace the sentence with the main way of rolling back (roll back the application, keep the database) and link to "Roll back a release" in `docs/deployment/migrations.md`, which describes the code-only case.

## Result
Docs only, no change in behavior.
- `docs/database-as-service.md`: after the `_v1` → `_v2` example, "To roll back the release, roll back the application: `_v1` is still in the database, and `_v2` does not disturb the old application. See [Roll back a release](deployment/migrations.md#roll-back-a-release)." (was: rolling back to the previous build redeploys `my-stored-proc.sql` with `_v1`).
- `docs/llms-full.txt` needs no change: §18.8 already says this.
