---
type: bug
priority: low
created: 2026-11-23
closed: 2026-11-23
---
# `rows` does not compare the netmask of a PostgreSQL `inet` value

## Release note
None. The previous release failed on an `inet` value in `rows`; the note on `NULL` and the values without a JSON shape says how `rows` compares `inet` now.

## Problem
Npgsql reads `inet` with `GetValue` as `IPAddress`, which has no netmask. So in `rows` `'192.168.0.1/24'::inet` equals `'192.168.0.1/16'::inet`, and the test passes when it should fail. Verified on 2026-11-23 with Npgsql 10.0.1 and PostgreSQL 18: `other-netmask.sql` in `SqlTestResultSetTests.Rows_test_compares_network_addresses_and_pg_lsn` (PostgreSQL) passes. `cidr` keeps its prefix: `other-cidr.sql` fails as it should.

Found in 0096-108, which documented it as a limitation: select the values as `text` to compare the netmask.

## Where
- `src/DbLive.PostgreSQL/PostgreSqlDA.cs`, `ReadResultAsync`: `reader.GetValue(i)`

## Fix
Read an `inet` column with `reader.GetFieldValue<NpgsqlInet>(i)`: `NpgsqlInet` has the properties `Address` and `Netmask`, so `RowValueText` would write it as a JSON object with both. Or store its text, `NpgsqlInet.ToString()`.

- [x] `inet[]` too? Npgsql reads it as `IPAddress[]`. Yes, the same way (maintainer, 2026-11-23).
- [x] Is the case worth the code, with `select i::text` as the workaround? Yes, as the text of `NpgsqlInet` (maintainer, 2026-11-23).

## Docs to update
- `docs/llms-full.txt` §8.3 (the netmask of `inet`)
- `docs/unit-testing/index.md` ("How values are compared")

## Done when
- [x] Fixed: `other-netmask.sql` fails
- [x] Docs updated

## Notes
- Verified on 2026-11-23 with Npgsql 10.0.1: `NpgsqlInet` has the properties `IPAddress Address` and `byte Netmask`; its `ToString()` is `192.168.0.1/24`, and without the netmask for a single host: `192.168.0.1` for `/32`, `::1` for `/128`. By the PostgreSQL documentation, that is the text PostgreSQL shows for an `inet` value; `inet::text` writes `/32` too (not verified).
- The text, not the JSON object: the object would change the text of every `inet` value, also of a single host, so `'192.168.0.1'::inet` would no longer equal the string `'192.168.0.1'` and every failure message with `inet` would change. With the text only a value with a netmask changes. Decided by the maintainer on 2026-11-23, without `breaking`: only the tests that compared `inet` values differing by the netmask start to fail, and they compared nothing before.
- `NpgsqlCidr` is obsolete in Npgsql 10 ("Use .NET IPNetwork instead of NpgsqlCidr to map to PostgreSQL cidr"); `cidr` is not touched.

## Result
- `PostgreSqlDA.ReadValue` reads the value with `GetValue` and, when it is an `IPAddress` or an `IPAddress[]`, reads the column again as `NpgsqlInet` or `NpgsqlInet?[]` and stores its text: a string, or an array of strings with `null` for a NULL element. A multidimensional `inet[]` stays as Npgsql reads it. Only `inet` comes as `IPAddress` from Npgsql.
- `RowValueText` keeps writing an `IPAddress` by its `ToString()`; its comment says that PostgreSQL `inet` no longer comes as one.
- The note of 0096-108, closed in the same release, no longer names the netmask as a limitation and shows `"192.168.0.1/24"`.
- Tests: `SqlTestResultSetTests.Rows_test_compares_network_addresses_and_pg_lsn` (PostgreSQL): `other-netmask.sql` fails with `Expected values: "192.168.0.1/16"`, `Actual values: "192.168.0.1/24"`; `other-netmask-in-array.sql` fails; `same.sql` has a NULL `inet` and an `inet[]` with a netmask and a NULL element.
- Docs: `docs/llms-full.txt` §8.3, `docs/unit-testing/index.md` ("How values are compared").
