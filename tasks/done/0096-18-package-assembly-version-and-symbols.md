---
type: bug
priority: high
created: 2026-10-31
---
# Published DLLs have version 0.0.0.0, and there are no symbols

## Release note
The DbLive assemblies now carry the package version (AssemblyVersion and FileVersion were `0.0.0.0`), and every package has a symbols package on nuget.org with Source Link. To step into DbLive code, enable the NuGet.org symbol server in the debugger.

## Problem
Verified on all four packages 1.5.0 from the local NuGet cache, every DLL (net8.0, net9.0, net10.0):
- AssemblyVersion `0.0.0.0`, FileVersion `0.0.0.0`.
- ProductVersion `1.5.0+Branch.main.Sha.<sha>.<sha>`: the commit hash appears twice.
- No PDBs in the package and no symbols package (`.snupkg`).

Package inspectors (NuGet Package Explorer, the "package health" checks) show this at once, and it looks careless to anyone evaluating the library. Nothing breaks at run time: DbLive never reads its own assembly version, and all its assemblies have the same `0.0.0.0`. Without symbols, users cannot step into DbLive code.

Cause: every `.csproj` has `<Version>0.0.0-placeholder</Version>`. CI builds with `-p:Version=...` and `ContinuousIntegrationBuild=true` (Debug), but `dotnet pack` builds again in Release with only `-p:PackageVersion=...`, so the assemblies get the placeholder version and no CI build flags.

The ProductVersion comes from the environment, not from `Version`: the GitVersion action (`gittools/actions@v0`) also exports its outputs as environment variables without a prefix (`informationalVersion`, `sha`, ...). MSBuild reads environment variables as properties, and property names ignore case, so `InformationalVersion` became GitVersion's `1.5.0+Branch.main.Sha.<sha>`, and Source Link appended `.<sha>` again. Verified locally: a pack with an `informationalVersion` environment variable gives the same doubled hash; without it, `<version>+<sha>`.

## Where
- `.github/workflows/cicd.yml`, steps "Build", "Test" and "Create … package"
- `<Version>` in `src/DbLive/DbLive.csproj`, `src/DbLive.MSSQL/DbLive.MSSQL.csproj`, `src/DbLive.PostgreSQL/DbLive.PostgreSQL.csproj`, `src/DbLive.xunit/DbLive.xunit.csproj`
- `src/Directory.Build.props`

## Fix
- [x] Build once in Release with `-p:Version=<NuGetVersionV2>`, test and pack that build with `--no-build`. The tests now run on the assemblies that go into the packages. Pack still needs `-p:Version`: it sets the package version and the version of the `DbLive` dependency in the provider and xunit packages.
- [x] One GitVersion value for everything, `NuGetVersionV2` (was `FullSemVer` for the build and `NuGetVersionV2` for the package). On `main` it equals `SemVer`, which names the git tag.
- [x] `IncludeSymbols=true` and `SymbolPackageFormat=snupkg` once in `src/Directory.Build.props`, not in the four `.csproj` files. `dotnet nuget push` pushes the `.snupkg` next to each `.nupkg`; the push step is unchanged.
- [x] `ContinuousIntegrationBuild=true` in `src/Directory.Build.props` when `GITHUB_ACTIONS` is `true`, instead of a command-line flag, so build and pack cannot disagree. Not locally: paths in the PDBs would become `/_/...`.
- [x] Duplicate hash in ProductVersion: the build passes `-p:InformationalVersion=<NuGetVersionV2>`, a global property, which wins over the environment variable; Source Link appends `+<sha>` once. Rejected: `IncludeSourceRevisionInInformationalVersion=false`, which keeps the version dependent on the leaked variable and drops the hash from local builds.
- [x] AssemblyVersion: the SDK default, derived from `Version` (`1.6.0` → `1.6.0.0`). The assemblies are not strong-named and target .NET 8+ only, where the loader accepts the referenced version or a higher one, and NuGet already resolves dependencies to at least the referenced version. `<major>.0.0.0` only matters for binding redirects of strong-named assemblies on .NET Framework.

## Docs to update
- `Development.md` ("Library versioning": how CI versions the assemblies, symbols, the GitVersion environment variables)

## Done when
- [x] A locally packed package has the right AssemblyVersion, FileVersion and ProductVersion
- [ ] NuGet Package Explorer shows Source Link, deterministic build and compiler flags as valid
- [ ] The next release on nuget.org has a symbols package

## Notes
- Local run of the new CI steps, 2026-11-02: `GITHUB_ACTIONS=true` and an `informationalVersion=1.6.0+Branch.main.Sha.111…` environment variable (as GitVersion leaves it), `dotnet build src/DbLive.sln -c Release -p:Version=1.6.0 -p:InformationalVersion=1.6.0`, then `dotnet pack --no-build -p:Version=1.6.0` for the four packages. All 12 DLLs: AssemblyVersion and FileVersion `1.6.0.0`, ProductVersion `1.6.0+f00f8fd…` (one hash). Each package has a `.snupkg` with the PDBs of the three frameworks. The PDBs hold the Source Link map `https://raw.githubusercontent.com/zacoders/DbLive/<sha>/*`, paths `/_/...` and no local paths, and the compiler options (`compiler-version`, `language-version`, `optimization=release`). The provider and xunit packages depend on `DbLive 1.6.0`. Tests on that Release build, as the new "Test" step runs them (`--no-build -c Release --filter "Category!=LocalOnly"`, SQL Server and PostgreSQL in Docker): 486 passed, 0 failed, the same count as on the Debug build in 0096-17.
- NuGet Package Explorer itself was not run: its command-line validator (`dotnet-validate`) was not allowed in the agent session. Check with NuGet Package Explorer on the next package, or on nuget.org after the release.
- Alternative not taken: `DebugType=embedded` (PDB inside the DLL). Debugging works without enabling the nuget.org symbol server, but the DLLs grow. The `.snupkg` is the standard way for nuget.org.
