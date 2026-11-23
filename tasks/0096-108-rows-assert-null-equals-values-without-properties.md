---
type: bug
priority: medium
created: 2026-11-23
---
# `rows` compares NULL as an empty object and fails on some types

## Problem
`UnitTestResultChecker.CompareRows` compares two values by their `System.Text.Json` text. It serializes the raw value of the data reader with the default converters. Verified on 2026-11-23 with the options of `ValueJsonOptions`, Npgsql 10.0.1 and PostgreSQL 18:

| Value | JSON |
|---|---|
| `NULL` (`DBNull.Value`) | `{}` |
| PostgreSQL `macaddr` (`PhysicalAddress`), any value | `{}` |
| PostgreSQL `inet` (`IPAddress`) | throws `SocketException`: "The attempted operation is not supported for the type of object referenced." |

So in a `rows` test on PostgreSQL:
- `NULL` equals any MAC address, and two different MAC addresses are equal. The test passes when it should fail.
- A test with an `inet` column fails with the `SocketException` message instead of a comparison.

Any value of a CLR type without public properties serializes as `{}` and equals `NULL`. §8.3 says "`NULL` equals `NULL`", which implies that it equals nothing else.

## Where
- `src/DbLive/Deployers/Testing/UnitTestResultChecker.cs:312-333`
- The values come from `src/DbLive.MSSQL/MsSqlDA.cs:524` and `src/DbLive.PostgreSQL/PostgreSqlDA.cs:438` (`reader.GetValue(i)`).

## Fix
Normalize each value before the comparison: `DBNull` becomes `null`; known types without a JSON shape (`PhysicalAddress`, `IPAddress`, `NpgsqlInet`, `NpgsqlCidr`) become their text. The types of SQL Server (`hierarchyid`, `geography`, `sql_variant`) were not checked.

- [ ] What to do with a type that has no converter: compare its `ToString()`, or fail the test with "the type X of column Y cannot be compared"?

## Docs to update
- `docs/llms-full.txt` §8.3 (value rules), if a type gets a rule of its own
- `docs/unit-testing/index.md` (value comparison)

## Done when
- [ ] Fixed, with unit tests: `NULL` against a value of every kind, two different values of a type without public properties
- [ ] A PostgreSQL integration test with `macaddr` and `inet`
- [ ] Docs updated

## Notes
The failure message of `rows` prints the values in another way than they are compared: 0096-114.
