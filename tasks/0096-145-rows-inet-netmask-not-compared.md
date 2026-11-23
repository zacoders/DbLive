---
type: bug
priority: low
created: 2026-11-23
---
# `rows` does not compare the netmask of a PostgreSQL `inet` value

## Problem
Npgsql reads `inet` with `GetValue` as `IPAddress`, which has no netmask. So in `rows` `'192.168.0.1/24'::inet` equals `'192.168.0.1/16'::inet`, and the test passes when it should fail. Verified on 2026-11-23 with Npgsql 10.0.1 and PostgreSQL 18: `other-netmask.sql` in `SqlTestResultSetTests.Rows_test_compares_network_addresses_and_pg_lsn` (PostgreSQL) passes. `cidr` keeps its prefix: `other-cidr.sql` fails as it should.

Found in 0096-108, which documented it as a limitation: select the values as `text` to compare the netmask.

## Where
- `src/DbLive.PostgreSQL/PostgreSqlDA.cs`, `ReadResultAsync`: `reader.GetValue(i)`

## Fix
Read an `inet` column with `reader.GetFieldValue<NpgsqlInet>(i)`: `NpgsqlInet` has the properties `Address` and `Netmask`, so `RowValueText` would write it as a JSON object with both. Or store its text, `NpgsqlInet.ToString()`. Not tried.

- [ ] `inet[]` too? Npgsql reads it as `IPAddress[]`.
- [ ] Is the case worth the code, with `select i::text` as the workaround?

## Docs to update
- `docs/llms-full.txt` §8.3 (the netmask of `inet`)
- `docs/unit-testing/index.md` ("How values are compared")

## Done when
- [ ] Fixed: `other-netmask.sql` fails
- [ ] Docs updated
