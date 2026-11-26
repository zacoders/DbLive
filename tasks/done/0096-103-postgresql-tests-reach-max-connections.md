---
type: chore
priority: low
created: 2026-11-22
closed: 2026-11-22
---
# The PostgreSQL tests reach `max_connections` of their container

## Release note
None. The integration tests of the repository only.

## Problem
A run of `DbLive.PostgreSQL.Tests` opens as many connections as the container allows. Verified on 2026-11-22: polled `pg_stat_activity` during a full run (78 tests), the peak was 100 client backends, and `max_connections` is 100, the default of `postgres:18`. Most tests deploy a database of their own (a random name), Npgsql keeps a pool per connection string, and the pools are not cleared until the end of the run, so their idle connections add up.

So one more test that deploys a database of its own can fail another test with `53300: sorry, too many clients already`. Found in 0096-97: its first two tests did, and failed `TransactionTimeoutTests.Setup_and_test_that_each_fit_into_UnitTestItemTimeout_pass_together` on every run; without them all tests passed. The number of tests that run at once depends on the machine, so a run in CI may fail where a local one passes, or the other way round.

## Where
- `src/DbLive.PostgreSQL.Tests/PostgreSqlFixture.cs` (the container)

## Fix
- [x] Raise `max_connections` of the container (`.WithCommand("-c", "max_connections=300")`), or clear the pool of a test database when its test ends? The first is one line; the second keeps the run as small as a real server allows. Maintainer, 2026-11-22: neither, close it with 0096-104. The base class of 0096-104 drops the database of each test, and the drop ends its sessions (`pg_terminate_backend`), also the idle connections of its pools; `max_connections` stays at 100.

## Done when
- [x] A full run stays well below `max_connections`, checked the same way

## Notes
Verified on 2026-11-22, after 0096-104: a poll of `pg_stat_activity` inside the container every 50 ms during a full run (78 tests) saw at most 44 client backends, 43 and 44 in two runs. A poll with `docker exec` from the host, about every 0.6 s, saw at most 44 too.

## Result
Closed by 0096-104, without a change of its own: the tests drop their databases, and a run of `DbLive.PostgreSQL.Tests` peaks at 44 client backends of 100. `PostgreSqlFixture` is unchanged.
