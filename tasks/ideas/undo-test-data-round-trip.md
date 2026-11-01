---
created: 2026-11-01
---
# Data checks for a migration version: forward and through undo

## Idea
The `[SqlDeployFact]` deployment test runs migrations on an empty database, so the data logic of a migration (moving a column, splitting a table) and of its undo (moving the data back) runs on empty tables and is never checked. Any `insert ... select` passes.

Per version, with the schema snapshot of 0096-32 extended to data:

```
arrange (inserts data into the schema as it is before the version)
→ snapshot of schema and data
→ migration → [assert: the data moved]
→ (breaking) → undo
→ snapshot → compare with the first one
```

The user writes only the arrange script. "The undo restored the data" is the equality of the two snapshots and needs no code. The assert after the migration is optional, for checking the forward move.

Came up while discussing 0096-15 (undo for code) and 0096-32.

## Open questions
- [ ] File names: `<version>.<something>.sql` for arrange and for the assert?
- [ ] What happens to the arranged data after the cycle? Kept: later migrations run on a non-empty database (an `alter column ... not null` fails on nulls, as in production). But the SQL tests that `[SqlDeployFact]` runs after the deployment may expect empty tables. Removed: by a cleanup script, or by running the check in a separate database?
- [ ] Data snapshot: all tables except `dblive`, rows read into C# and compared as multisets by column name (a readable diff), or a hash per table on the server (cheap, says only "table X differs")? Rows up to a size limit, hash above it?
- [ ] Columns to ignore: SQL Server `rowversion` changes on every update.
- [ ] The undo must keep identity values for the snapshots to match. That is the right requirement (foreign keys point to them), but it must be documented.

## Depends on
- `tasks/0096-32-undo-test-schema-compare.md` (the snapshot mechanism)
