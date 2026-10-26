---
type: chore
priority: high
created: 2026-10-26
closed: 2026-10-26
---
# Restore fails on vulnerable NuGet packages

## Problem
After the advisories of October 2026, restore fails with `NU1902` / `NU1903` (warnings are errors in `Directory.Build.props`):
- `System.Security.Cryptography.Pkcs` 8.0.1 (net8.0) and 9.0.11 (net9.0, net10.0): GHSA-2fmv-rv2c-59x7, GHSA-846f-f764-76mx. Transitive dependency of `Microsoft.Data.SqlClient`, in `DbLive.MSSQL` and `DbLive.Tests.Common`. Patched: 8.0.2, 9.0.21, 10.0.13.
- `SSH.NET` 2025.1.0: GHSA-q939-rpr3-3284. Transitive dependency of `Testcontainers` 4.10.0, test projects only. Patched: 2026.0.0.

A no-op restore skips the audit, so a machine with an existing restore may not see the error until `dotnet restore --force` or a clean CI build.

## Fix
- `SSH.NET`: Testcontainers 4.14+ references SSH.NET 2026.0.0, so updating Testcontainers is enough.
- `Pkcs`: updating the parent does not help. Every `Microsoft.Data.SqlClient` release up to 7.1.1 (2026-09-29) still references 8.0.1 for net8.0, so the package is referenced directly with a patched version.

## Result
- `Directory.Packages.props`: `System.Security.Cryptography.Pkcs` 10.0.14 (one version for all target frameworks; it supports net8.0+, like the `Microsoft.Extensions.*` 10.x packages already used); `Testcontainers.MsSql` and `Testcontainers.PostgreSql` 4.10.0 → 4.16.0.
- `DbLive.MSSQL.csproj` and `DbLive.Tests.Common.csproj`: direct `PackageReference` to `System.Security.Cryptography.Pkcs`, with a comment. Because `DbLive.MSSQL` references it directly, the NuGet package now declares the patched version as a dependency, so library users get it too.
- Verified: `dotnet restore --force` and `dotnet list package --vulnerable --include-transitive` are clean, the solution builds with no warnings, all tests pass (unit, provider and demo tests in Docker; the test projects target net8.0 only).
- Remove the `Pkcs` reference once `Microsoft.Data.SqlClient` references a patched version.
