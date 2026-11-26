---
type: bug
priority: low
created: 2026-11-23
closed: 2026-11-23
---
# `[SqlFact]`: the explanation of an invalid `TestFixture` is incomplete and its example does not compile

## Release note
`[SqlFact]` with a `TestFixture` type that is abstract or has no public parameterless constructor, `typeof(DbLiveTestFixture)` itself among them, now gives one failing test that names the type and the reason, `Invalid type specified for TestFixture in SqlFact attribute: MyTests.MyDbTestFixture has no public parameterless constructor.`, with an example of a fixture that compiles. Before, xUnit reported "Exception during discovery" with `MissingMethodException`. Nothing to change on upgrade.

## Problem
`docs/llms-full.txt` §13.2 (line 942): "An invalid `TestFixture` type produces a failing test case that explains the fix."

- `src/DbLive.xunit/SqlTest/SqlFactDiscoverer.cs:24` checks only `typeof(DbLiveTestFixture).IsAssignableFrom(fixtureType)`. An abstract type, `typeof(DbLiveTestFixture)` itself, or a type without a public parameterless constructor reaches `Activator.CreateInstance` (`:61`), which throws `MissingMethodException`; xUnit reports "Exception during discovery", not the explanation.
- The example in the explanation (`:38`), `class MyTestFixture : DbLiveTestFixture { ... }`, does not compile: `DbLiveTestFixture` has only the constructor `(IDbLiveFixtureBuilder fixtureBuilder, bool dropDatabaseOnComplete)`. It also does not show that the fixture must be public and have a public parameterless constructor (`SqlFactAttribute` doc comment, §13.2 line 922).

## Fix
Also check `!fixtureType.IsAbstract` and a public parameterless constructor. Show a compiling example:

```csharp
public class MyTestFixture()
    : DbLiveTestFixture(new MsSqlContainerFixtureBuilder(typeof(MyDbMarker).Assembly), dropDatabaseOnComplete: true);
```

## Docs to update
- `docs/llms-full.txt` §13.2
- `docs/unit-testing/index.md` (section "2. SQL tests: `[SqlFact]`")

## Done when
- [x] Fixed, with tests of the discoverer: a type that does not derive, an abstract type, a type without a parameterless constructor

## Notes
- Verified: with the old discoverer the abstract fixture, `typeof(DbLiveTestFixture)`, a fixture with a constructor parameter and one with an `internal` parameterless constructor all throw `MissingMethodException` from `Activator.CreateInstance`; a type that does not derive got the old message.
- The message names the first failed check: does not derive, then abstract, then no public parameterless constructor. `GetConstructor(Type.EmptyTypes)` finds only public instance constructors, the ones `Activator.CreateInstance(Type)` takes.
- An open generic type (`typeof(MyFixture<>)`) still reaches `Activator.CreateInstance` and "Exception during discovery". Not checked: no test class can use such a fixture, `IClassFixture<MyFixture<>>` does not compile.
- The example of the message shows the fixture together with the test class (`IClassFixture<MyTestFixture>`, the `[SqlFact]` method), as §13.2; the arrow between the class and the attribute is gone, the class name in both places says the same.
- The discoverer tests passed `Substitute.For<ITestFrameworkDiscoveryOptions>()`: its `""` for a string option fails `MethodDisplayOrDefault()`, which only the error test case calls. The substitute returns `null` now, the defaults of xUnit.

## Result
- `SqlFactDiscoverer`: a `TestFixture` type that does not derive from `DbLiveTestFixture`, is abstract or has no public parameterless constructor gives an `ExecutionErrorTestCase` with `Invalid type specified for TestFixture in SqlFact attribute: <full type name> <reason>.`, the requirements and an example that compiles (the fixture with `MsSqlContainerFixtureBuilder` and the test class).
- Tests in `SqlFactDiscovererTests`: `typeof(string)`, an abstract fixture, `typeof(DbLiveTestFixture)`, a fixture with a constructor parameter, a fixture with an `internal` parameterless constructor; each gives one `ExecutionErrorTestCase` with the type and the reason, and the example. All 583 tests of `DbLive.Tests` pass; the Release build of `DbLive.xunit` has no warnings.
- Docs: `docs/llms-full.txt` §13.2 (the three checks and the message), `docs/unit-testing/index.md` (what an invalid fixture gives). No release note of an earlier task names the check.
