---
type: docs
priority: low
created: 2026-11-24
closed: 2026-11-25
---
# PostgreSQL: a value of a type that the test creates cannot be read

## Release note
None.

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

- [x] Docs only, or reject by the decision of 0096-165 ("no fix and no docs")?
  - Docs only. Decision of the maintainer, 2026-11-25.

## Docs to update
- `docs/llms-full.txt` §8.3, §17
- `docs/unit-testing/index.md` (the values that cannot be read)

## Done when
- [x] Docs updated, `mkdocs build --strict` passes

## Notes
- Found by the code review of 2026-11-24 (the second one of that day).
- Only an enum was run. A composite type cannot be read anyway (§8.3).
- The reproduction of the review still holds: `PostgreSqlDA.cs` and the test runner have not changed since (last change `080d072`, 2026-11-24 19:23, before the review; the task came at 23:33). Npgsql is still 10.0.1.
- Verified 2026-11-25, a temporary test class in `DbLive.PostgreSQL.Tests` (removed), `postgres:18`, Npgsql 10.0.1, the types created after the deployment:
  - Created in the test, fail with `Cannot read the value of the column 'v' in row 1 of result set 1, of the type -.-: Reading as 'System.Object' is not supported for fields having DataTypeName '-.-'`, then `Select the value as text in the test.`: an enum, an array of the enum, a range type (`create type ... as range (subtype = float8)`), a composite type (named `-.-` here, not `public.t_pair`), a domain over an enum that the test created.
  - Created in `setup.test.sql`: an enum and a range type fail the same way.
  - Pass: a domain over `int` created in the test (the row description has the base type, as 0096-165 found for a domain of a migration); the enum selected as text (`'happy'::t_mood4::text`); the enum used in the test but not selected (a table column of it in `where`).
  - So the docs can say "a type that the test or its `setup.test.sql` creates (an enum, a range type, a composite type, an array of them, a domain over them)": not a domain over a built-in type.
- The decision of 0096-165 (the maintainer, 2026-11-24): "A type created by `setup.test.sql` or by the test itself stays as it is, no fix and no docs." This task, filed by the review later that day, proposes the docs. What changed since: 0096-168 (closed after 0096-165) added the lists of values a test cannot read, which name `-.-` only as the row of a table, so the type `-.-` of a type created in the test now points to a wrong cause in the docs. The error itself already says what to do ("Select the value as text in the test.").
- Code was weighed in 0096-165: a `ReloadTypesAsync` on the connection of the test, after `setup.test.sql`, reads a type of `setup.test.sql` (verified there), not one created by the test itself; a reload before every test was ruled out by a measurement (4.4–12.8 ms per reload against 2.7 ms of a trivial test). A type created by the test cannot be read by any reload: the row description comes in the same command as the `create type`.

## Result
Docs only, no code changed.
- `docs/llms-full.txt` §8.3, the values Npgsql cannot read: a value of a type that the test or its `setup.test.sql` creates (an enum, a range type, a composite type, an array of one, a domain over one), of the type `-.-`, and why; a domain over a built-in type is read; create the type in a migration (§11.1).
- `docs/llms-full.txt` §17, row `Test: Cannot read the value ...`: the same cause, and the migration as a second way out.
- `docs/unit-testing/index.md` (the values that cannot be read): the same, shorter.

Decision of the maintainer, 2026-11-25: docs only, the case stays unhandled in the code (as decided in 0096-165).

Tests: none, no code changed. `mkdocs build --strict` passes.
