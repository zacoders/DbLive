---
created: 2026-11-14
---
# SQL tests and the deployment test without xUnit v2

## Idea
`DbLive.xunit` supports xUnit v2 only. With another test framework (xUnit v3, NUnit, MSTest) the docs offer one recipe: a fixture builder and `RunAllTestsAsync` in one test of that framework (Section 8.2 of `docs/llms-full.txt`, `docs/unit-testing/index.md` "Running all tests without xUnit"). Compared with xUnit v2, such a user does not get:

- every SQL test as its own test case, as `[SqlFact]` gives: one test runs them all, and a failure is one `UnitTestsFailedException` with the list of failed files;
- the deployment test with the schema check: `DeploymentTestFixture.DeployAsync` takes xUnit's `ITestOutputHelper`, and the schema snapshot and its comparison are internal (0096-80);
- a test database that DbLive drops: with 0096-81 `DropDbAsync` is internal. With a container builder there is nothing to drop; on their own server the user drops a database with a fixed name with their own SQL before the deployment (0096-81);
- the log in the test output: `LogToXUnitOutput` is for xUnit v2 (`LogTo(Serilog.ILogger)` works with any framework).

Two directions, which can go together:

- A core that does not depend on a test framework: an object that owns a test database (gets a builder from an `IDbLiveFixtureBuilder`, deploys, gives the tester, drops the database on `DisposeAsync`), and the deployment test as a method that writes its output to an `Action<string>`, as `RunTestAsync` does. `DbLive.xunit` becomes a thin adapter over it.
- A package per framework, each with every SQL test as its own test case: `DbLive.xunit.v3` (xUnit v3 has another extensibility API than v2, so `[SqlFact]` and its discoverer are written again; not checked in detail), `DbLive.NUnit` (`TestCaseSource`), `DbLive.MSTest` (`DynamicData`). The data-driven attributes give a test case per file, but likely not "go to source" of the `.sql` file.

Open questions:

- [ ] Do users ask for it, and for which frameworks? xUnit v3 is the current major version of xUnit, and a new project of a user is likely on it.
- [ ] The core: public types of `DbLive`, or a package of its own?
- [ ] The deployment test without xUnit: keep the schema check internal (the result is `SchemaChangedException` with the differences in its message), or make the snapshot public?

Came up 2026-11-14, in 0096-81: with `DropDbAsync` internal, who drops a test database with a random name without xUnit. A fixture without xUnit for that alone would add a public type in place of `IDbLiveDA` and cover only a part of what such a user does not get.
