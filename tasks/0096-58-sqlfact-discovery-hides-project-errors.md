---
type: bug
priority: medium
created: 2026-11-08
---
# The discovery of `[SqlFact]` hides the errors of the project

## Problem
`SqlFactDiscoverer` turns an invalid `TestFixture` type into an `ExecutionErrorTestCase`, a failing test that explains the fix. An exception while it reads the project does not get that treatment: `Activator.CreateInstance` of the fixture, `GetProject()` (`ScriptsNotEmbeddedException`) and `GetTestsAsync()` (`FilesOutsideProjectFoldersException`, `ScriptPathsDifferOnlyByCaseException`, `BreakingScriptMatchesTestPatternException`, an invalid `settings.json`). xUnit v2 catches an exception of a discoverer in `TestFrameworkDiscoverer.FindTestsForTypeAndWrapExceptions` and reports it only as a diagnostic message, "Exception during discovery", which `dotnet test` and Test Explorer do not show by default (from the xUnit v2 source, not verified by an experiment).

Then the class has no SQL tests. `dotnet test` prints "No test is available" and exits with 0 when the test project has nothing else. The checks of the project files are meant to be loud (§4 of `docs/llms-full.txt`), and here the error is lost.

Example: a `README.md` in `Scripts/` (`FilesOutsideProjectFoldersException`), or a SQL project without `<DbLiveScriptsFolder>` (`ScriptsNotEmbeddedException`). Both are mistakes of the developer, but the documented flow catches them only through this error. A project made from the template has a `[SqlDeployFact]` test, which deploys the project at run time and fails with the same exception; a project with only `[SqlFact]` tests stays green with no SQL test run.

Found by the code review of 2026-11-08 (finding 1).

## Where
- `src/DbLive.xunit/SqlTest/SqlFactDiscoverer.cs:59-67`: the creation of the fixture and the read of the tests.
- `src/DbLive.xunit/SqlTest/DbLiveTestFixture.cs:20` (`GetProject`).

## Fix
Proposal: read the tests before the first `yield` (an iterator cannot `yield` inside a `try` with a `catch`), catch any exception, and return one `ExecutionErrorTestCase` whose message names the fixture type and has the exception text. Test Explorer and `dotnet test` then show one failing test with the error.

Same code as 0096-57 (the builder of `GetProject()` is not disposed): do both together.

- [ ] Not reproduced (see Notes): reject the task? The behavior is documented, and the discoverer has a test that it does not catch the error.
- [ ] A project that discovered no tests and passed now has a failing test: `breaking: true`, or a bug fix with a release note only? Moot if rejected.

## Docs to update
- `docs/llms-full.txt` §13.2 (an error of the project gives a failing test case, like an invalid `TestFixture` type)
- `docs/unit-testing/index.md`

## Done when
- [ ] Reproduced: a `[SqlFact]` class over a project with a file outside the project folders discovers no tests and `dotnet test` exits with 0
- [ ] Fixed, with a unit test of the discoverer
- [ ] Docs updated

## Notes
- **Not reproduced (verified 2026-11-08).** A scratch xUnit test project that is its own SQL project (`Scripts/Tests/example.sql` and `Scripts/README.md`), with a `DbLiveTestFixture` and one `[SqlFact]` method, referencing `DbLive.xunit` of this repository: `dotnet test` shows one failing test, `Repro58.SqlTests.Sql`, with "System.InvalidOperationException : Exception during discovery:" followed by the `FilesOutsideProjectFoldersException` with its message and stack trace, and exits with 1. Same result with xunit 2.9.3 and 2.9.2, before and after the change of 0096-57. In these versions `XunitTestFrameworkDiscoverer.FindTestsForType` catches the exception of `FindTestsForMethod` and reports it as a failing test case; the premise above (a diagnostic message only) does not hold for them.
- Verified: `DbLive.xunit` depends on `Serilog.Sinks.XUnit` 3.0.19, which requires `xunit.extensibility.execution` 2.9.2 or later, so every user of `DbLive.xunit` gets a discovery that reports the error.
- Not checked in Visual Studio Test Explorer; it runs the same discovery through `xunit.runner.visualstudio`.
- The proposed fix would change only the first line of the message (the fixture type instead of "Exception during discovery").
- The verified behavior is written in `docs/llms-full.txt` §13.2 and `docs/unit-testing/index.md`, and `SqlFactDiscovererTests.Discover_ProjectError_Throws` checks that the discoverer does not catch the error, in the commit of 0096-57.
