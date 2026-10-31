---
type: bug
priority: high
created: 2026-10-31
---
# Published DLLs have version 0.0.0.0, and there are no symbols

## Problem
Verified on `DbLive` 1.5.0 from the local NuGet cache (`lib/net8.0/DbLive.dll`):
- AssemblyVersion `0.0.0.0`, FileVersion `0.0.0.0`.
- ProductVersion `1.5.0+Branch.main.Sha.<sha>.<sha>`: the commit hash appears twice.
- No symbols package (`.snupkg`) is published.

Package inspectors (NuGet Package Explorer, the "package health" checks) show this at once, and it looks careless to anyone evaluating the library.

Cause: every `.csproj` has `<Version>0.0.0-placeholder</Version>`. CI builds with `-p:Version=...` and `ContinuousIntegrationBuild=true` (Debug), but `dotnet pack` builds again in Release with only `-p:PackageVersion=...`, so the assemblies get the placeholder version and no CI build flags.

## Where
- `.github/workflows/cicd.yml:58` (build), `:64-73` (pack)
- `<Version>` in `src/DbLive/DbLive.csproj`, `src/DbLive.MSSQL/DbLive.MSSQL.csproj`, `src/DbLive.PostgreSQL/DbLive.PostgreSQL.csproj`, `src/DbLive.xunit/DbLive.xunit.csproj`

## Fix
- Build once in Release with `-p:Version=<SemVer> -p:ContinuousIntegrationBuild=true`, then test and pack with `--no-build -p:PackageVersion=<NuGetVersion>`. Or pass `-p:Version` and `-p:ContinuousIntegrationBuild=true` to pack.
- Add `IncludeSymbols=true` and `SymbolPackageFormat=snupkg` to the packable projects. `dotnet nuget push` pushes the `.snupkg` next to each `.nupkg`.
- [ ] Duplicate hash in ProductVersion: GitVersion already puts the hash into the informational version and Source Link appends it again. Set `IncludeSourceRevisionInInformationalVersion=false`?
- [ ] AssemblyVersion: full version, or `<major>.0.0.0` to avoid binding churn? For .NET 8+ either works; pick one and note it.

## Done when
- [ ] A locally packed package has the right AssemblyVersion, FileVersion and ProductVersion
- [ ] NuGet Package Explorer shows Source Link, deterministic build and compiler flags as valid
- [ ] The next release on nuget.org has a symbols package
