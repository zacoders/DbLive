---
type: chore
priority: low
created: 2026-11-17
closed: 2026-11-22
---
# Package tags and AGENTS.md still say that DbLive does rollback

## Release note
None. The NuGet tags of DbLive, DbLive.MSSQL and DbLive.PostgreSQL say `zero-downtime expand-contract` instead of `rollback`; DbLive.xunit drops `rollback`.

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
Replace `rollback` with tags of what DbLive does instead: the expand/contract releases that let the application be rolled back while the database stays.

- DbLive, DbLive.MSSQL, DbLive.PostgreSQL: `rollback` → `zero-downtime expand-contract`.
- DbLive.xunit: remove `rollback`. It runs SQL tests and does not deploy releases, so the new tags do not apply.
- `AGENTS.md`: "migrations, code deployment, SQL unit tests and zero-downtime releases", the wording of `docs/index.md`.

## Done when
- [x] No `rollback` in `PackageTags`
- [x] The three deployment packages are tagged `zero-downtime expand-contract`
- [x] `AGENTS.md` describes DbLive without rollback

## Notes
- The second meaning of "rollback" in DbLive, the transaction of a SQL test that is always rolled back (README, "Test isolation and rollback"), does not justify the tag on DbLive.xunit: next to `deployment migration` a reader takes it for migration rollback.
- Maintainer's decision of 2026-11-22: replace the tag with `zero-downtime expand-contract` (option 2), do not only remove it.
- The GitHub repository has no topics (verified 2026-11-22 through the GitHub API). Its description, set in the repository settings outside the code, still says "Versioned SQL migrations, code deployment, SQL unit tests, and safe rollback in one workflow". It is right for 1.5, which has undo scripts; change it when 2.0 is released, for example to "Versioned SQL migrations, code deployment, SQL unit tests and zero-downtime releases in one workflow".

## Result
`rollback` is replaced with `zero-downtime expand-contract` in `PackageTags` of DbLive, DbLive.MSSQL and DbLive.PostgreSQL, and removed from DbLive.xunit. `AGENTS.md` line 3 says "SQL unit tests and zero-downtime releases" instead of "SQL unit tests and rollback", as `docs/index.md` does. No code or behavior change, so no tests and no docs under `docs/`.
