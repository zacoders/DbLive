---
type: docs
priority: low
created: 2026-11-24
---
# `docs/database-as-service.md`: rolling back to the previous build redeploys `_v1`

## Problem
`docs/database-as-service.md:67`, after the example of `MyStoredProc_v1` → `MyStoredProc_v2`: "Rolling back to the previous build redeploys `my-stored-proc.sql` with `_v1`; `_v2` stays and does not disturb the old application."

That holds only for a release without new migration versions and without a DbLive upgrade that raised the minimum DbLive version (`docs/llms-full.txt` §18.8, line 1214). The `_v2` of the example filters on `TenantId`, a column that usually comes with a migration of the same release: then the previous build fails with `DatabaseAheadOfProjectException` and changes nothing (§5.2, §18.8 line 1213). After such a rollback the breaking phase of the newer build must never run (§18.8, line 1214), which the page does not say. The ways that always work: roll back the application and keep the database, or revert the file and deploy a new release (§6, line 352).

The page is the maintainer's: ask before rewriting its text.

## Docs to update
- `docs/database-as-service.md:67`

## Done when
- [ ] Wording agreed with the maintainer and updated

## Notes
- Found by the code review of 2026-11-24.
