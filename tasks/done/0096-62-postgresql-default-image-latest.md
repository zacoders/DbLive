---
type: chore
priority: low
created: 2026-11-08
closed: 2026-11-08
---
# The default PostgreSQL image is `postgres:latest`

## Release note
None. The fixture builders of 0096-30-02 are not released yet; its release note names `postgres:18`.

## Problem
`PostgreSqlContainerFixtureBuilder.DefaultImage` is `postgres:latest`; the SQL Server builder takes `mcr.microsoft.com/mssql/server:2025-latest`, a fixed major version. The template (`dotnet new dblive --provider postgresql`) uses the default, and the step "Test packages" of CI tests the template on it (the fixtures of `package-tests.cs` pass `postgres:17`). The schema snapshot of the deployment test reads `pg_catalog` columns (`attgenerated`, `prokind`, ...). A new major version of PostgreSQL that changes the catalog or the behavior can make the users' tests and CI fail with no change in the code, and a run cannot be repeated on the same image.

The maintainer decided on 2026-11-03 (0096-30-02, Notes): `postgres:latest`, not a pinned major version. Nothing in the code changed since: the snapshot query is from 0096-32 (2026-11-01) and the template from 0096-30 (closed 2026-11-03). The code review of 2026-11-08 (finding 5) raised it again; this task asks whether to keep that decision.

## Where
- `src/DbLive.Testcontainers.PostgreSQL/PostgreSqlContainerFixtureBuilder.cs:16`
- `src/DbLive.PostgreSQL.Tests/PostgreSqlFixture.cs:10`
- `src/DbLive.PostgreSQL/PostgreSqlSchemaSnapshot.cs`

## Fix
- [x] Keep `postgres:latest` (decision of 2026-11-03), or pin a major version (`postgres:18`) and raise it with the releases? Pin `postgres:18` (maintainer, 2026-11-08).

## Docs to update
- `docs/llms-full.txt` §11.6, §13.1
- `docs/unit-testing/index.md`, `src/DbLive.Testcontainers.PostgreSQL/README.md`

## Done when
- [x] Decided, and done if changed

## Notes
- 2026-11-08, maintainer: `postgres:18` everywhere, not `postgres:latest`. This replaces the decision of 2026-11-03 (0096-30-02).
- Verified on 2026-11-08 with `docker manifest inspect`: Docker Hub has no `postgres:19`, and `postgres:latest` and `postgres:18` have the same digest. The default image is the same server as before the change.
- The docs that show how to pass another image (`new PostgreSqlContainerFixtureBuilder(project, "postgres:17")`) keep `postgres:17`: the example needs an image other than the default.
- 0096-30-02 is not released, so its release note now names `postgres:18`, and this task has no note of its own (as 0096-43 did with the note of 0096-31).

## Result
- `PostgreSqlContainerFixtureBuilder.DefaultImage` is `postgres:18`, with a comment that a DbLive release raises it. The template takes the default.
- `PostgreSqlFixture` of `DbLive.PostgreSQL.Tests` and the fixture project of `.github/scripts/package-tests.cs` use `postgres:18` (the latter used `postgres:17`).
- Docs: `docs/llms-full.txt` §11.6 and §13.1, `docs/unit-testing/index.md`, `src/DbLive.Testcontainers.PostgreSQL/README.md`; the release note of 0096-30-02.
- Tests: `DbLive.PostgreSQL.Tests` (45) and `Demo.PostgreSQL.Chinook.Tests` (6, the default image) pass on `postgres:18`.
