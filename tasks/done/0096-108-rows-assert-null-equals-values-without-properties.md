---
type: bug
priority: medium
created: 2026-11-23
closed: 2026-11-23
breaking: true
---
# `rows` compares NULL as an empty object and fails on some types

## Release note
**`rows` tells `NULL` and the values without a JSON shape apart.** `rows` compares values by their JSON, and `NULL` was written as `{}`, as was every value of a type without public properties. So `NULL` equaled every PostgreSQL `macaddr`, `macaddr8` and `pg_lsn` value and an empty `hstore`, two different values of these types were equal, and on SQL Server with `Microsoft.SqlServer.Types` every `hierarchyid` equaled every other. A test with a PostgreSQL `inet` value, or with a `geography` value read with `Microsoft.SqlServer.Types`, failed with an error that named neither the column nor the type. Now `NULL` equals only `NULL`, and these types are compared by their text: `"08002B010203"`, `"192.168.0.1"`, `"16/B374D848"`, `"/1/"`, `"POINT (2 1)"`. The netmask of an `inet` value is not compared: select the values as `text` to compare it. A value that `rows` can write neither as JSON nor as text fails the test with `Cannot compare the actual value of the column '<column>' in row N, of the type <type>: ...`. Fix the tests that start failing: they compared nothing before.

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
- `src/DbLive/Deployers/Testing/UnitTestResultChecker.cs:312-349`
- The values come from `src/DbLive.MSSQL/MsSqlDA.cs:508` and `src/DbLive.PostgreSQL/PostgreSqlDA.cs:444` (`reader.GetValue(i)`).

## Fix
Normalize each value before the comparison: `DBNull` becomes `null`; known types without a JSON shape (`PhysicalAddress`, `IPAddress`, `NpgsqlInet`, `NpgsqlCidr`) become their text. The types of SQL Server (`hierarchyid`, `geography`, `sql_variant`) were not checked.

- [x] What to do with a type that has no converter: compare its `ToString()`, or fail the test with "the type X of column Y cannot be compared"? Maintainer, 2026-11-23: both, by a rule of the type instead of a list. `ToString()` when the type overrides it, a failure of the test when it does not, or when writing the value throws.

## Docs to update
- `docs/llms-full.txt` §8.3 (value rules), §17 (the new failure)
- `docs/unit-testing/index.md` (value comparison)

## Done when
- [x] Fixed, with unit tests: `NULL` against a value of every kind, two different values of a type without public properties
- [x] A PostgreSQL integration test with `macaddr` and `inet`
- [x] Docs updated

## Notes
The failure message of `rows` prints the values in another way than they are compared: 0096-114, done together with this task.

More types, verified on 2026-11-23 with the same options and Npgsql 10.0.1, by serializing the .NET values:

| Value | JSON |
|---|---|
| PostgreSQL `pg_lsn` (`NpgsqlLogSequenceNumber`), any value | `{}` |
| PostgreSQL empty `hstore` (empty `Dictionary<string, string?>`) | `{}`, so it equals `NULL` |
| `NpgsqlInet`, `NpgsqlCidr` | throw: their `Address` is an `IPAddress` |
| PostgreSQL `path` (`NpgsqlPath`) | an array of points: an open path equals the closed one with the same points |

- `NpgsqlCidr` is obsolete in Npgsql 10 ("Use .NET IPNetwork instead of NpgsqlCidr to map to PostgreSQL cidr").
- A rule by type, not a list: System.Text.Json writes `{}` for every type whose `JsonTypeInfo` has `Kind == Object` and no properties (`DBNull`, `PhysicalAddress`, `NpgsqlLogSequenceNumber`). The empty `Dictionary` has `Kind == Dictionary`, so such a rule leaves it alone. All of them override `ToString()` with their text (`001122334455`, `16/B374D848`).
- `path` open against closed is a case of its own: not fixed here.

SQL Server types, verified on 2026-11-23 with Microsoft.Data.SqlClient 6.1.4 and Microsoft.SqlServer.Types 160.1000.6, by serializing the .NET values:

| Value | JSON |
|---|---|
| `hierarchyid` (`SqlHierarchyId`) | `{"IsNull":false}` for every value: its only public property is `IsNull` |
| `geography` (`SqlGeography`) | throws "Data is Null. This method or property cannot be called on Null values." |
| `vector` (`SqlVector<float>`) | `{"IsNull":false,"Length":3,"Memory":[1,2,3]}`: correct; it does not override `ToString()` |

The CLR types of SQL Server (`geography`, `geometry`, `hierarchyid`, user CLR types) implement `INullable` and override `ToString()`, which T-SQL `.ToString()` calls. So the rule takes an `INullable` with its own `ToString()` by that text; `SqlVector` keeps its JSON. Without `Microsoft.SqlServer.Types` these values cannot be read at all, as before.

Verified on 2026-11-23 with Npgsql 10.0.1 and PostgreSQL 18: Npgsql reads `inet` as `IPAddress` without the netmask, so `'192.168.0.1/24'::inet` equals `'192.168.0.1/16'::inet`. `cidr` keeps its prefix. Documented as a limitation; the fix is 0096-145.

## Result
- `RowValueText` (`src/DbLive/Deployers/Testing/RowValueText.cs`) writes the text that `rows` compares, with the options that `UnitTestResultChecker` had and a converter factory for the values whose default JSON does not show the value: `DBNull` as `null`; `IPAddress`, an `INullable` with its own `ToString()`, and a type that System.Text.Json writes as an object without properties, by `ToString()` (invariant culture for an `IFormattable`). The rule works inside arrays and records too (`inet[]`).
- A type without properties that does not override `ToString()`, and a value whose writing throws, fail the test: `Cannot compare the <expected|actual> value of the column '<column>' in row N, of the type <type>: <reason>` and `Select the value as text in the test.`
- The core does not reference Npgsql or Microsoft.SqlServer.Types: the rule goes by `JsonTypeInfo`, `INullable` and `IPAddress`.
- Tests: `UnitTestResultCheckerRowsTests` (NULL against 16 kinds of values, equal and different values of `macaddr`, `inet`, `inet[]`, `pg_lsn` and a fake `hierarchyid`, the two failures); `SqlTestResultSetTests.Rows_test_compares_network_addresses_and_pg_lsn` (PostgreSQL: `macaddr`, `macaddr8`, `inet`, `cidr`, `pg_lsn`, `inet[]`, NULL against `macaddr`).
