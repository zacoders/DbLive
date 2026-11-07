---
type: chore
priority: low
created: 2026-11-07
---
# The discovery of `[SqlFact]` does not dispose its builder

## Problem
`DbLiveTestFixture.GetProject()` creates a `DbLiveBuilder` (`IDisposable`, it owns a `ServiceProvider`), returns the project from it and never disposes it. `SqlFactDiscoverer` calls it once for every method with `[SqlFact]` during discovery, reads the tests and drops the project.

The cost is small: nothing roots the provider after discovery (its logger is `Logger.None`, no timers or connections), so the GC collects it with the cached text of the scripts. It is an `IDisposable` that is not disposed, and a public method that returns a project whose owner is gone.

Found by the code review of 2026-11-07 (finding 11).

## Where
- `src/DbLive.xunit/SqlTest/DbLiveTestFixture.cs:20` (`GetProject`)
- `src/DbLive.xunit/SqlTest/SqlFactDiscoverer.cs:60`, its only caller

## Fix
Proposal: replace the public `GetProject()` with an internal method that creates the builder in `using`, reads the tests and disposes the builder, for example `internal IReadOnlyCollection<TestItem> GetTests()`. `SqlFactDiscoverer` calls it. `GetProject()` is not in the docs; it is public, so removing it goes into the release note of 2.0.

- [ ] Remove the public `GetProject()` (as proposed), keep it and dispose the builder in the discoverer, or leave it as it is?

## Done when
- [ ] Decided, and done with a unit test if changed
