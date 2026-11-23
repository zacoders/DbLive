---
type: bug
priority: low
created: 2026-11-23
---
# `[SqlFact]`: the explanation of an invalid `TestFixture` is incomplete and its example does not compile

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

## Done when
- [ ] Fixed, with tests of the discoverer: a type that does not derive, an abstract type, a type without a parameterless constructor
