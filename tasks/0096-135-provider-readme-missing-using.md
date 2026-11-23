---
type: docs
priority: low
created: 2026-11-23
---
# The snippets in the READMEs of `DbLive.MSSQL` and `DbLive.PostgreSQL` do not compile

## Problem
`src/DbLive.MSSQL/README.md:6-13` and `src/DbLive.PostgreSQL/README.md:6-13` (the pages of the packages on NuGet) have `using DbLive;` and `using DbLive.<provider>;` and call `Assembly.GetExecutingAssembly()`. `System.Reflection` is not an implicit using, so the pasted snippet fails with CS0103. The template's `Program.cs`, `docs/gettingstarted.md`, `docs/deployment/index.md:15` and the root `README.md` have `using System.Reflection;`.

## Fix
Add `using System.Reflection;` to both snippets.

## Docs to update
- `src/DbLive.MSSQL/README.md`, `src/DbLive.PostgreSQL/README.md`

## Done when
- [ ] Both READMEs fixed
