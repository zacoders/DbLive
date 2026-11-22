---
type: chore
priority: low
created: 2026-11-22
---
# The PostgreSQL tests reach `max_connections` of their container

## Problem
A run of `DbLive.PostgreSQL.Tests` opens as many connections as the container allows. Verified on 2026-11-22: polled `pg_stat_activity` during a full run (78 tests), the peak was 100 client backends, and `max_connections` is 100, the default of `postgres:18`. Most tests deploy a database of their own (a random name), Npgsql keeps a pool per connection string, and the pools are not cleared until the end of the run, so their idle connections add up.

So one more test that deploys a database of its own can fail another test with `53300: sorry, too many clients already`. Found in 0096-97: its first two tests did, and failed `TransactionTimeoutTests.Setup_and_test_that_each_fit_into_UnitTestItemTimeout_pass_together` on every run; without them all tests passed. The number of tests that run at once depends on the machine, so a run in CI may fail where a local one passes, or the other way round.

## Where
- `src/DbLive.PostgreSQL.Tests/PostgreSqlFixture.cs` (the container)

## Fix
- [ ] Raise `max_connections` of the container (`.WithCommand("-c", "max_connections=300")`), or clear the pool of a test database when its test ends? The first is one line; the second keeps the run as small as a real server allows.

## Done when
- [ ] A full run stays well below `max_connections`, checked the same way
