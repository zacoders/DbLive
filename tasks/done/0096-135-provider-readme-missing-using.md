---
type: docs
priority: low
created: 2026-11-23
closed: 2026-11-23
---
# The snippets in the READMEs of `DbLive.MSSQL` and `DbLive.PostgreSQL` do not compile

## Release note
None. A correction of the READMEs of `DbLive.MSSQL` and `DbLive.PostgreSQL` on NuGet; DbLive behaves as before.

## Problem
`src/DbLive.MSSQL/README.md:6-13` and `src/DbLive.PostgreSQL/README.md:6-13` (the pages of the packages on NuGet) have `using DbLive;` and `using DbLive.<provider>;` and call `Assembly.GetExecutingAssembly()`. `System.Reflection` is not an implicit using, so the pasted snippet fails with CS0103. The template's `Program.cs`, `docs/gettingstarted.md`, `docs/deployment/index.md:15` and the root `README.md` have `using System.Reflection;`.

## Fix
Add `using System.Reflection;` to both snippets.

## Docs to update
- `src/DbLive.MSSQL/README.md`, `src/DbLive.PostgreSQL/README.md`

## Done when
- [x] Both READMEs fixed

## Notes
- The snippets of `src/DbLive/README.md`, `src/DbLive.xunit/README.md` and the fixture of the root `README.md` (section of the SQL tests) call `Assembly` too, but they have no `using` at all: fragments, not a file to paste. Not changed. The READMEs of the Testcontainers packages have `using System.Reflection;`.

## Result
`src/DbLive.MSSQL/README.md` and `src/DbLive.PostgreSQL/README.md`: `using System.Reflection;` after the DbLive usings, as in the template's `Program.cs`. No code change, no tests.
