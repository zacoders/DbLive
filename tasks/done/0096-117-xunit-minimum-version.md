---
type: docs
priority: low
created: 2026-11-23
closed: 2026-11-23
---
# `DbLive.xunit` needs xunit 2.9.2 or later, the docs say 2.x

## Release note
`DbLive.xunit` declares its minimum xunit: `xunit` 2.9.2 or later (v2). It needed 2.9.2 before too, through `Serilog.Sinks.XUnit`; now the package depends on `xunit.extensibility.execution` 2.9.2 directly. With an older `xunit` 2 the restore fails with `NU1107: Version conflict detected for xunit.extensibility.core`: update the `xunit` package. Nothing to change on upgrade.

## Problem
`docs/llms-full.txt` §13 ("`xunit` (2.x)") and `docs/unit-testing/index.md:188` ("switch it to the `xunit` 2.x package").

`DbLive.xunit` gets xunit only through `Serilog.Sinks.XUnit` 3.0.19. Its `net8.0` and `net9.0` dependency groups need `xunit.extensibility.core` 2.9.2 or later (checked in the nuspec on 2026-11-23). `xunit.core` pins the exact versions of the extensibility packages, so a test project on xunit 2.8 or older fails to restore with a version conflict (verified, see Notes). Also noted in `tasks/rejected/0096-58-...` (Notes).

## Fix
Write "xunit 2.9.2 or later (v2)" in the docs.

- [x] Add a direct dependency on `xunit.extensibility.execution` 2.9.2 to `DbLive.xunit.csproj`, so the minimum does not depend on a logging sink? Maintainer, 2026-11-23: yes.

## Docs to update
- `docs/llms-full.txt` §2 (`DbLive.xunit` row), §13, §19
- `docs/unit-testing/index.md:188`
- `src/DbLive.xunit/README.md`

## Done when
- [x] Docs updated
- [x] A restore of a test project on xunit 2.8 tried, and the result written under Notes

## Notes
- **Restore on an old xunit fails (verified 2026-11-23, before the change).** A scratch `net8.0` project with `xunit` 2.4.2 (the oldest 2.x in the local NuGet cache) and a project reference to `DbLive.xunit`: `error NU1107: Version conflict detected for xunit.extensibility.core`, chains `Repro -> DbLive.xunit -> Serilog.Sinks.XUnit 3.0.19 -> xunit.extensibility.core (>= 2.9.2)` and `Repro -> xunit 2.4.2 -> xunit.core 2.4.2 -> xunit.extensibility.core (= 2.4.2)`. xunit 2.8 was not tried: not in the cache, and the conflict is the same for every version below 2.9.2.
- NuGet suggests referencing `xunit.extensibility.core` 2.9.2 directly. That mixes `xunit.core` 2.4.2 with the 2.9.2 extensibility packages (`NU1608`), a combination xunit does not support; the docs tell to update `xunit` instead.
- The direct dependency is not only about the minimum: the code compiles against `XunitTestCase`, `ExecutionErrorTestCase` and `XunitTestCaseDiscoverer` of `xunit.extensibility.execution`, which came only through the logging sink. 2.9.2 is also the first version whose discovery reports an exception of the discoverer as a failing test (`tasks/rejected/0096-58-...`), the behavior §13.2 documents.
- The central version of `xunit.extensibility.execution` is 2.9.2, the minimum, not the latest 2.9.3: the nuspec carries it as the lower bound. The test projects of the repository reference `xunit` 2.9.3 and still resolve 2.9.3 (checked in `project.assets.json`).
- **After the change (verified 2026-11-23).** The packed nuspec has `xunit.extensibility.execution` 2.9.2 in the `net8.0`, `net9.0` and `net10.0` groups. Restore with `xunit` 2.4.2: `NU1608` for `xunit.extensibility.execution` (the direct dependency is nearer and wins, 2.9.2), then the same `NU1107` for `xunit.extensibility.core`, restore fails. Restore with `xunit` 2.9.2: succeeds.

## Result
- `DbLive.xunit.csproj` references `xunit.extensibility.execution` directly; `src/Directory.Packages.props` pins it at 2.9.2, the minimum for users.
- Docs: `xunit` 2.9.2 or later in `docs/llms-full.txt` §2, §13 (with the `NU1107` error and the fix) and §19, in `docs/unit-testing/index.md` and in `src/DbLive.xunit/README.md`.
- Tests: the solution builds in Release with no warnings; the `DbLive.Tests` tests of the xUnit integration (`SqlFact`, `DeploymentTest`) pass; restore on xunit 2.4.2 and 2.9.2 checked as in Notes.
