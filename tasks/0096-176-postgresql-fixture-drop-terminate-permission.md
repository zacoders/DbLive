---
type: bug
priority: low
created: 2026-11-25
---
# PostgreSQL: the drop of a test database fails on a session that its login may not end

## Problem
`docs/llms-full.txt` §13.2 (line 956): "The drop of the fixtures (`DbLiveTestFixture`, `DeploymentTest`) ends the sessions still open in the database, also the idle connections of the pool." On PostgreSQL the drop runs `select pg_terminate_backend(pid) from pg_stat_activity where datname = @name;` and catches no error. A login that is not a superuser (the admin login of Azure Database for PostgreSQL and Amazon RDS is not one) may not end a process of a superuser: `pg_terminate_backend` raises `42501: permission denied to terminate process. Only roles with the SUPERUSER attribute may terminate processes of roles with the SUPERUSER attribute.`, the whole statement fails, and so does the cleanup of `DbLiveTestFixture` or `DeploymentTest`, and with it the test. Verified by the review of 2026-11-25 on PostgreSQL 18: a database owner that is not a superuser, against an idle session of `postgres` in the database.

Which session can be there: an autovacuum worker that analyzes the new tables of the test database. A process without a role counts as a superuser one for `pg_terminate_backend` (PostgreSQL 18 added the role `pg_signal_autovacuum_worker` for it). Inferred, not reproduced: rare and flaky, on a managed server with `PostgreSqlFixtureBuilder`. `drop database` ends autovacuum workers in the database itself.

SQL Server falls back when the login may not kill sessions (`MsSqlDA.cs:837`, `single_user with rollback immediate`).

## Where
`src/DbLive.PostgreSQL/PostgreSqlDA.cs:774-777` (`DropDbAsync`).

## Fix
End only client sessions (`backend_type = 'client backend'`), which leaves autovacuum to `drop database`, or end the sessions one by one and ignore `42501`; then `drop database` fails with its own error if a session is still there. A test on PostgreSQL with a login that is not a superuser and a session of `postgres` in the database.

## Done when
- [ ] Fixed, with a test

## Notes
- Found by the code review of 2026-11-25.
- A superuser session in a test database with a random name otherwise comes only from a person or a tool connected to it.
