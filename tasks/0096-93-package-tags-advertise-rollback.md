---
type: chore
priority: low
created: 2026-11-17
---
# Package tags and AGENTS.md still say that DbLive does rollback

## Problem
2.0 removes undo scripts and the downgrade (0096-46): "DbLive does not roll back a database". Two places still say it does:

- `PackageTags` of four packages contain `rollback`, so a search for "rollback" on nuget.org leads to a package without it.
- `AGENTS.md` line 3 describes DbLive as a library for "migrations, code deployment, SQL unit tests and rollback", which agents read first.

The README and `docs/index.md` have a "Rollback" row that describes the new behavior (roll back the application, keep the database). They are right and stay.

Found by the code review of 2026-11-17 (finding 7).

## Where
- `src/DbLive/DbLive.csproj:22`
- `src/DbLive.MSSQL/DbLive.MSSQL.csproj:22`
- `src/DbLive.PostgreSQL/DbLive.PostgreSQL.csproj:23`
- `src/DbLive.xunit/DbLive.xunit.csproj:22`
- `AGENTS.md:3`

## Fix
Remove `rollback` from the four `PackageTags`. In `AGENTS.md`: "migrations, code deployment and SQL unit tests".

## Done when
- [ ] No `rollback` in `PackageTags`
- [ ] `AGENTS.md` describes DbLive without rollback
