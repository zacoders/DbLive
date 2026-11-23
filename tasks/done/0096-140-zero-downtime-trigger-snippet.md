---
type: docs
priority: low
created: 2026-11-23
closed: 2026-11-23
---
# `zero-downtime.md`: the trigger of step 1 is not valid T-SQL

## Release note
None. A correction of the docs: step 1 of "Zero-Downtime Deployment" shows the real sync trigger of DemoMSSQL migration 007 instead of an empty one that SQL Server rejects, and a migration without a transaction that copies the existing rows in batches. DbLive behaves as before.

## Problem
`docs/deployment/zero-downtime.md:41-43`:

```sql
as
begin
    -- keep dbo.PersonPhones in sync with dbo.Person.PhoneNumber
end
```

A T-SQL `BEGIN ... END` block must hold at least one statement: with only a comment SQL Server reports "Incorrect syntax near 'end'". It is a placeholder, but a reader who copies it gets an error. The demo has the real trigger (`src/Demo/DemoMSSQL/Scripts/Migrations/007.migration.sql`).

The maintainer writes this page by hand.

## Fix
- [x] Put the body of the demo trigger there, or `set nocount on;` before the comment?
  - Decided by the maintainer on 2026-11-23: the code of the demo migration 007 as a whole (table, trigger, copy of the existing rows).
- [x] The copy of the existing rows: one `update` locks a large table.
  - Decided by the maintainer on 2026-11-23: show a loop that updates the rows in batches. It is a migration of its own with `"TransactionWrapLevel": "None"` (008 on the page), as `docs/deployment/migrations.md` ("Long-running migrations") says: in the transaction of its migration a loop would still hold the locks of every batch until the commit. The demo keeps its one `update` in 007 (few rows, as `docs/examples/table-split.md` says), so 007 on the page is the demo file without it.
- [x] Which loop?
  - Decided by the maintainer on 2026-11-23: each batch first reads its last `PersonId` (`max` of `top (1000) ... order by PersonId` after the last one done), then updates the range up to it. Exactly 1000 rows per batch whatever the gaps in the key, with two plain statements.

## Docs to update
- `docs/deployment/zero-downtime.md`

## Done when
- [x] Page updated by the maintainer or with the maintainer's agreement

## Notes
- `set nocount on;` alone was dropped: the copied trigger would deploy and sync nothing, which is worse than the syntax error it replaces.
- The old snippet also differed from the demo it is the worked example of: `PhoneNumber nvarchar(30) not null`, no `IsPrimary`, no copy of the existing rows. No other page shows the body of the sync trigger: `docs/examples/table-split.md` links to the files.
- Verified: the code of the 007 snippet is identical to `007.migration.sql` without the comments, the whitespace and the final `update`, plus the index on `PersonId` (compared with `diff`).
- Loops considered for 008, each verified on SQL Server 2025 (container `mssql/server:2025-latest`): `dbo.Person` of demo migration 006 with 3500 rows, every 7th without a phone, ids 1200-2600 deleted (2099 rows, 1799 phones), the 007 snippet of the page, then the loop without a transaction. Every loop copied 1799 of 1799 phones with none mismatched, and a second run changed nothing.
  - Fixed ranges of 1000 ids up to the maximum read at the start: the shortest, but a gap in the key makes a batch smaller (1000, 199, 400, 500 rows).
  - The chosen one, the bound of the batch first, then its range: 1000, 1000 and 99 rows. The code on the page is the tested script with one comment more (compared with `diff`).
  - The maintainer's own: an `update` through a CTE of `top (100) ... order by PersonId` with `max(PersonId) over ()`, `@PersonId = MaxPersonId` in its `set`, `@@rowcount = 0` to stop and `waitfor delay` of 100 ms between batches: 20 batches of 100 and one of 99, 3 s. Correct, but harder to read; without `order by` the `top` may take other rows and skip some.
  - `update top (1000)` of the persons with a phone and no row in `dbo.PersonPhones` until `@@rowcount = 0`: the shortest, and a second run goes on where the first stopped, but every batch reads again all the rows copied before it, so the time grows with the square of the table.
- Verified on the same server: after an `update` that fires a trigger, `@@rowcount` is the number of rows of the `update` (5), not of the statement in the trigger (a `merge` of 0 rows).
- [x] `dbo.PersonPhones` of the demo has no index on `PersonId`, which the trigger filters by.
  - Decided by the maintainer on 2026-11-23: the 007 snippet adds `IX_PersonPhones_PersonId`; the demo stays as it is. The `update` of 008 says in a comment that it fires the trigger.
  - Verified on SQL Server 2025, 200 000 persons with ids 50 000-90 000 deleted (137 143 phones), the snippets of the page: one write to `dbo.Person` reads 9 pages of `dbo.PersonPhones` with the index and 730 without it (the whole table, `set statistics io on`). The copy of 008 took 6 s with the index and 5 s without it, 159 batches of 1000 and one of 999: at this size the index pays off on the writes of the application during the transition, not on the copy.

## Result
Docs only, no change in behavior.
- `docs/deployment/zero-downtime.md`, step 1: the 007 snippet is the code of DemoMSSQL `Migrations/007.migration.sql` without its final `update`: the `dbo.PersonPhones` table as in the demo, an index on its `PersonId` that the demo does not have (said on the page), and the trigger `dbo.TMP_DataMigration_Person_To_PersonPhones` (only the primary phone synced, the row deleted when `PhoneNumber` is cleared). A new 008 snippet (`008.migration.settings.json` with `"TransactionWrapLevel": "None"`, `008.migration.sql`) copies the existing rows with a no-op update in batches of 1000 persons, each committed on its own: a loop reads the last `PersonId` of the next batch, then updates the range up to it, and a comment says that the `update` fires the trigger; the text says why, that it is re-runnable, and that the demo copies its few rows with one `update` in 007. The text of the step adds "copy the existing rows"; the sentence on large tables now names only an index built without a transaction.
