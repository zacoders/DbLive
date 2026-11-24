---
type: docs
priority: low
created: 2026-11-24
---
# PostgreSQL: a value of a type that the test creates cannot be read

## Problem
A PostgreSQL test that creates a type and selects a value of it fails, also when `setup.test.sql` creates the type:

```sql
create type mood_t as enum ('happy', 'sad');
select 'happy'::mood_t as v;
select 'rows' as assert;
select 'happy'::mood_t as v;
```

fails with `Cannot read the value of the column 'v' in row 1 of result set 1, of the type -.-: Reading as 'System.Object' is not supported for fields having DataTypeName '-.-'`. Verified by the review of 2026-11-24 on `postgres:18` (Npgsql 10.0.1), with the enum created in the test and in `setup.test.sql`.

Npgsql loads the types of the database once for a connection string; DbLive reloads them after a deployment (0096-165), so a type that a migration creates is read. A type created inside the transaction of the test is not committed, and no reload of another connection could see it. Nothing to fix in the code.

The docs explain the type `-.-` only as the row of a table:
- `docs/llms-full.txt` §8.3 (line 559): the list of values Npgsql cannot read: "the row of a table (`select u from users u`, of the type `-.-`: ...)".
- `docs/llms-full.txt` §17 (line 1134), row `Test: Cannot read the value ...`, PostgreSQL causes.
- `docs/unit-testing/index.md:90`, the same list.

## Fix
Docs only: add to the three lists "a type (an enum, a range type) that the test or its `setup.test.sql` creates, of the type `-.-`: create the type in a migration, or select the value as text (`v::text`)".

## Docs to update
- `docs/llms-full.txt` §8.3, §17
- `docs/unit-testing/index.md` (the values that cannot be read)

## Done when
- [ ] Docs updated, `mkdocs build --strict` passes

## Notes
- Found by the code review of 2026-11-24 (the second one of that day).
- Only an enum was run. A composite type cannot be read anyway (§8.3).
