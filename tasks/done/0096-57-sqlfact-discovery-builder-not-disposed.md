---
type: chore
priority: low
created: 2026-11-07
closed: 2026-11-08
breaking: true
---
# The discovery of `[SqlFact]` does not dispose its builder

## Release note
**`DbLiveTestFixture.GetProject()` is removed, and the fixtures dispose the builder they get from the fixture builder.**

To upgrade:

- `GetProject()` has no replacement: reading the project is not part of the public API.
- `DbLiveTestFixture` and `DeploymentTestFixture` dispose the `DbLiveBuilder` from `IDbLiveFixtureBuilder.GetBuilderAsync()` at the end, after the database is dropped. A custom fixture builder must return a new builder from every call: one that returns a shared builder fails the next fixture with `ObjectDisposedException`. The ready-made fixture builders already return a new one.

## Problem
`DbLiveTestFixture.GetProject()` creates a `DbLiveBuilder` (`IDisposable`, it owns a `ServiceProvider`), returns the project from it and never disposes it. `SqlFactDiscoverer` calls it once for every method with `[SqlFact]` during discovery, reads the tests and drops the project.

The cost is small: nothing roots the provider after discovery (its logger is `Logger.None`, no timers or connections), so the GC collects it with the cached text of the scripts. It is an `IDisposable` that is not disposed, and a public method that returns a project whose owner is gone.

Found by the code review of 2026-11-07 (finding 11).

## Where
- `src/DbLive.xunit/SqlTest/DbLiveTestFixture.cs:20` (`GetProject`)
- `src/DbLive.xunit/SqlTest/SqlFactDiscoverer.cs:60`, its only caller
- `src/DbLive.xunit/SqlTest/DbLiveTestFixture.cs:46` and `src/DbLive.xunit/Deploy/DeploymentTestFixture.cs:23` (`DisposeAsync`), found while working on it

## Fix
Proposal: replace the public `GetProject()` with an internal method that creates the builder in `using`, reads the tests and disposes the builder, for example `internal IReadOnlyCollection<TestItem> GetTests()`. `SqlFactDiscoverer` calls it. `GetProject()` is not in the docs; it is public, so removing it goes into the release note of 2.0.

- [x] Remove the public `GetProject()` (as proposed), keep it and dispose the builder in the discoverer, or leave it as it is? Maintainer, 2026-11-08: remove it as proposed, dispose the builders of both fixtures too, together with 0096-58; `breaking: true`.

## Docs to update
- `docs/llms-full.txt` §11.6 and §13.1 (the fixture disposes the builder; `GetBuilderAsync` returns a new one on every call)
- `docs/unit-testing/index.md` (section "1. A fixture builder")

## Done when
- [x] Decided, and done with a unit test if changed

## Notes
- Verified: no class in `src/` except `DbLiveBuilder` itself implements `IDisposable` or `IAsyncDisposable` (test projects aside), so disposing the `ServiceProvider` released nothing in practice. The change is hygiene, not a leak fix.
- Found: `DbLiveTestFixture.DisposeAsync` and `DeploymentTestFixture.DisposeAsync` did not dispose their `_builder` either. Every `GetBuilderAsync()` call of the ready-made fixture builders returns a new builder, so the fixture owns it. Keeping the builder of `GetProject()` alone disposed would leave the code inconsistent.
- Keeping `GetProject()` public and disposing the builder in the discoverer is not possible without changing the method: the builder is created inside it and never returned.
- The dispose inside `GetTestsAsync()` has no test of its own: the builder is created inside the method and cannot be observed. Discovery itself is covered by `SqlFactDiscovererTests`.

- 2026-11-14: `DbLiveBuilder.CreateProject()`, which the release note named as the replacement of `GetProject()`, is internal now (0096-73), in the same release. The release note says that there is no replacement.
## Result
- `DbLiveTestFixture.GetProject()` (public) is replaced by `internal Task<IReadOnlyCollection<TestItem>> GetTestsAsync()`, which creates the builder in `using`; `SqlFactDiscoverer` calls it.
- `DbLiveTestFixture.DisposeAsync` and `DeploymentTestFixture.DisposeAsync` dispose the builder in `finally`, after the drop of the database, also when the drop fails.
- The XML doc of `IDbLiveFixtureBuilder.GetBuilderAsync`, `docs/llms-full.txt` §11.6 and §13.1, `docs/unit-testing/index.md`: the fixture disposes the builder, so `GetBuilderAsync` returns a new one on every call.
- The discoverer does not catch an error of the project: xUnit reports it as a failing test (verified in 0096-58). A comment in `SqlFactDiscoverer` and §13.2 of `docs/llms-full.txt` say so.
- Tests: `DbLiveTestFixtureTests` (new: the drop and the dispose; a failing drop still disposes), `DeploymentTestFixtureTests.DisposeAsync_DropsTheDatabase_AndDisposesTheBuilder`, `SqlFactDiscovererTests.Discover_ProjectError_Throws`. `DbLive.Tests`: 445 passed.
