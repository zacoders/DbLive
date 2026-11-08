---
type: chore
priority: low
created: 2026-11-08
---
# The default PostgreSQL image is `postgres:latest`

## Problem
`PostgreSqlContainerFixtureBuilder.DefaultImage` is `postgres:latest`; the SQL Server builder takes `mcr.microsoft.com/mssql/server:2025-latest`, a fixed major version. The template (`dotnet new dblive --provider postgresql`) uses the default, and the step "Test packages" of CI tests the template on it (the fixtures of `package-tests.cs` pass `postgres:17`). The schema snapshot of the deployment test reads `pg_catalog` columns (`attgenerated`, `prokind`, ...). A new major version of PostgreSQL that changes the catalog or the behavior can make the users' tests and CI fail with no change in the code, and a run cannot be repeated on the same image.

The maintainer decided on 2026-11-03 (0096-30-02, Notes): `postgres:latest`, not a pinned major version. Nothing in the code changed since: the snapshot query is from 0096-32 (2026-11-01) and the template from 0096-30 (closed 2026-11-03). The code review of 2026-11-08 (finding 5) raised it again; this task asks whether to keep that decision.

## Where
- `src/DbLive.Testcontainers.PostgreSQL/PostgreSqlContainerFixtureBuilder.cs:16`
- `src/DbLive.PostgreSQL.Tests/PostgreSqlFixture.cs:10`
- `src/DbLive.PostgreSQL/PostgreSqlSchemaSnapshot.cs`

## Fix
- [ ] Keep `postgres:latest` (decision of 2026-11-03), or pin a major version (`postgres:18`) and raise it with the releases?

## Docs to update
If pinned:
- `docs/llms-full.txt` §11.6, §13.1
- `docs/unit-testing/index.md`, `src/DbLive.Testcontainers.PostgreSQL/README.md`

## Done when
- [ ] Decided, and done if changed
