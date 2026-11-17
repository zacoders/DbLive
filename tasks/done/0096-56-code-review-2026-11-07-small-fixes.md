---
type: chore
priority: low
created: 2026-11-07
closed: 2026-11-07
---
# Small fixes from the code review of 2026-11-07

## Release note
None. `IDbLiveDA.GetMigrationHashAsync` and `FindCodeItemAsync` are removed; they are not among the members of `IDbLiveDA` that the docs list (docs/versioning.md).

## Problem
Small findings of the code review of 2026-11-07. Larger ones have their own tasks: 0096-51 to 0096-55, and 0096-57.

### 1. Unused members of `IDbLiveDA`
`GetMigrationHashAsync` was called nowhere, and `FindCodeItemAsync` only by tests since the code deployment reads all rows of `dblive.code` with one query (0096-44). Both providers implemented and kept both queries (finding 12).

- `src/DbLive/Adapter/IDbLiveDA.cs`, `src/DbLive.MSSQL/MsSqlDA.cs`, `src/DbLive.PostgreSQL/PostgreSqlDA.cs`
- [x] Remove both. The integration tests find the row of a code file in `GetCodeItemsAsync` by its stored path.

### 2. Task 0096-10 is closed with unticked items
`tasks/done/0096-10-demo-clearoutput-wrong-folder.md` has `closed:`, but `## Done when` had two unticked items. The task was replaced by 0096-31, which removed `ClearOutput` from the demos and the docs (finding 15).

- [x] Tick both items and name 0096-31 as the task that did them.

## Done when
- [x] Items 1 and 2 done

## Result
- Item 1: both members removed from `IDbLiveDA` and both providers. `CrossPlatformDeployTests` (both providers) use a helper over `GetCodeItemsAsync`; `CodeBreakingDeployerTests` checks that the code breaking deployment does not read `dblive.code`.
- Item 2: 0096-10 has both items ticked, each naming 0096-31.
