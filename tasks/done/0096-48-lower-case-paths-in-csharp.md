---
type: chore
priority: low
created: 2026-11-07
closed: 2026-11-07
---
# Lower-case relative paths in C#, not with the database's lower()

## Release note
None. The paths in the `dblive` tables are stored in lower case as before; DbLive lower-cases them itself instead of the database.

## Problem
Since 0096-31-02 every query that writes or looks up `relative_path` lower-cases it in SQL: `lower(@relative_path)`. A comparison with `lower()` on the side of a list of paths can not use an index: in 0096-44 `MarkMissingCodeAsRemovedAsync` compares `lower(p.value) = c.relative_path`, so the primary key of `dblive.tvp_unique_string_list` is not used, and the plan depends on the row estimate of the table-valued parameter.

The result of `lower()` also depends on the database: PostgreSQL with the `C` locale lower-cases only ASCII letters, so there `Отчёт.sql` and `отчёт.sql` are two items, while DbLive finds the files ignoring case.

0096-31-02 did not lower-case in C# because `ToLowerInvariant()` may differ from the database's `lower()` for non-ASCII names. That matters only when both are mixed: when C# lower-cases every path that DbLive writes and looks up, the stored value and the compared value come from the same function.

## Where
- `src/DbLive.MSSQL/MsSqlDA.cs`, `src/DbLive.PostgreSQL/PostgreSqlDA.cs`: every `lower(@relative_path)` and the lists of paths
- `src/DbLive/Deployers/Code/CodeRows.cs`: the lookup of a row by path

## Fix
- [x] One helper lower-cases a relative path for the `dblive` tables (`ToLowerInvariant()`); both providers pass its result and compare without `lower()`.
- [x] SQL Server: the table-valued parameter holds the lower-cased paths, compared with `p.value = c.relative_path`. PostgreSQL: the `text[]` parameter likewise.
- [x] `CodeRows` looks a row up by the lower-cased path, ordinal.
- [x] The self-migrations that lower-cased the existing rows (`09.relative-path-lower-case.sql`, `06.relative-path-lower-case.sql`) stay as they are.

## Docs to update
- `docs/llms-full.txt` §15 (how paths are lower-cased, how to find the row of a file)

## Done when
- [x] No `lower(` on a path in `MsSqlDA` and `PostgreSqlDA`
- [x] Existing tests pass

## Notes
- 2026-11-07, the maintainer: lower-case in C# everywhere and store what C# lower-cased. `MinCompatibleVersion` is not raised: v2.0 is not released yet.
- For Cyrillic and Latin letters with diacritics, `ToLowerInvariant()` gives what SQL Server's and PostgreSQL's `lower()` give (verified: the tests with `R&D Отчёт.sql` store `r&d отчёт.sql` on both providers, as before). They can differ for PostgreSQL with the `C` locale (its `lower()` lower-cases only ASCII letters) and for letters newer than the Unicode tables of a SQL Server collation.
- The self-migrations that lower-cased the rows of a database deployed by DbLive 1.x use the database's `lower()`: SQL can not call `ToLowerInvariant()`. On PostgreSQL with the `C` locale, a row whose path has non-ASCII capital letters keeps them, so after the upgrade the file is not found by its lower-cased path: a code file runs once more and its old row is marked removed. Not worth a conversion in C#.
- PostgreSQL: the lists stay `in` / `not in ( select p from unnest(@paths) as p )`. `<> all(@paths)` would compare each row with the whole array; the subquery gets a hashed subplan.
- `CodeRows` keys the rows ordinal: the stored paths are unique by the primary key of `dblive.code`, and the lookup uses the same lower-cased path the row was saved with.
- 2026-11-07, considered with the maintainer: a case-insensitive collation on the `relative_path` columns instead of lower-casing, which would keep the real casing in the `dblive` tables. Not chosen:
  - PostgreSQL has no case-insensitive deterministic collation: strings equal by the locale are then compared byte by byte. `citext` is an extension (`create extension`) and folds with `lower()` by the `LC_CTYPE` of the database, so the `C` locale problem stays. A nondeterministic ICU collation (`und-u-ks-level2`) needs PostgreSQL 12+ built with ICU, `LIKE` on the column fails before PostgreSQL 18, and an ICU upgrade in the OS changes the collation version and needs a `REINDEX`.
  - SQL Server: works, but the collation is needed on the columns of four tables and on `dblive.tvp_unique_string_list` (otherwise a join fails with "Cannot resolve the collation conflict" in a database with another collation), and an existing database needs its primary keys dropped and created again and the table type recreated.
  - Each provider would have its own rule for which paths are equal (ICU, a Windows collation, `OrdinalIgnoreCase` in C#); `COLLATE NOCASE` of SQLite, a planned provider, folds only ASCII letters. `ToLowerInvariant()` is one rule in one place, and the database compares with `=`.

## Result
- `StringExtensions.ToStoredPath()` (`ToLowerInvariant()`): every path that `MsSqlDA` and `PostgreSqlDA` write or look up goes through it; no `lower()` on a path in their queries.
- SQL Server: the table-valued parameter holds stored paths and is compared with `p.value = c.relative_path`, so the primary keys of `dblive.tvp_unique_string_list` and `dblive.code` can be used. PostgreSQL: the `text[]` parameters hold stored paths.
- `CodeRows` looks a row up by `ToStoredPath()`, ordinal.
- `docs/llms-full.txt` §15: DbLive lower-cases the paths itself; the conversion of older databases uses the database's `lower()`.
- Unit tests, SQL Server and PostgreSQL integration tests pass.
