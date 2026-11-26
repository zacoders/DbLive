---
created: 2026-11-26
---
# Support for .NET 11

## Idea
.NET 11 was released on 2026-11-10: an STS release with 24 months of support, until November 2028, about when .NET 10 LTS ends. The DbLive packages target `net8.0;net9.0;net10.0`. An application on .NET 11 can use them already: NuGet takes the `net10.0` assets. Support means a `net11.0` target in the packages and the tests running on .NET 11, so that CI finds a difference of the runtime before a user does (as with the time spans of 0096-147, which the .NET 9 runtime read differently from .NET 8).

What it touches:

- `<TargetFrameworks>` of the packages: `DbLive`, `DbLive.MSSQL`, `DbLive.PostgreSQL`, `DbLive.xunit`, `DbLive.Testcontainers.MSSQL`, `DbLive.Testcontainers.PostgreSQL`, `DbLive.Console`.
- CI: `dotnet-version: 10.0.x` in `.github/workflows/cicd.yml` and `release-notes.yml`. Building `net11.0` needs the .NET 11 SDK; the maintainer's machine has SDK 9 and 10 only.
- The test projects (`DbLive.Tests`, `DbLive.MSSQL.Tests`, `DbLive.PostgreSQL.Tests`, `DbLive.Tests.Common`) and the demos target `net8.0` only, so the tests do not run the `net9.0` and `net10.0` builds of the packages either.
- `src/Directory.Packages.props`: `Microsoft.Extensions.*`, `System.IO.Hashing`, `System.Security.Cryptography.Pkcs` are on 10.0.x. A new major version of them may drop `net8.0`.
- Docs: `docs/llms-full.txt` Section 2 ("Target frameworks"), Section 3.3 (the file-based app of .NET 10 sets `PublishAot=true` by default, verified with the .NET 10.0.404 SDK; check the .NET 11 SDK).

The template (`DbLive.Templates`, `MyDb`, `MyDb.Tests`) stays on `net10.0`, so `docs/llms-full.txt` Section 18.1 and `docs/gettingstarted.md` "With the template" do not change.

Open questions:

- [x] Drop `net8.0` and `net9.0`? The support of both ended on 2026-11-10. A user on .NET 8 or 9 would stay on the last DbLive version that targets it; for such a user the upgrade is breaking. Maintainer, 2026-11-26: keep both for now.
- [ ] Run the tests on every target framework (multi-targeted test projects), or on the lowest and the highest only? The database tests take most of the CI time.
- [x] The template: stay on `net10.0` (LTS) or move to `net11.0`? Maintainer, 2026-11-26: the template stays on `net10.0`.
- [ ] Now, or when a user asks? DbLive has no users besides the maintainer yet.

Came up 2026-11-26: the maintainer asked to add the idea.
