---
type: bug
priority: high
created: 2026-10-31
---
# SQL tests run during deployment by default

## Problem
The docs say not to run SQL tests against production, but the defaults run them. To deploy safely, a user has to opt out; the first example a reader sees turns tests on.

Verified by reading the code:
- `DeployParameters.RunTests` defaults to `true`, and the `DeployParameters.Default` preset sets it to `true` (next to a `todo` saying it should not).
- The quick start in `README.md` and `docs/gettingstarted.md` sets `RunTests = true` with the comment "tests are not recommended to run on prod databases".
- Tests run at the end of `DeployAsync`, after the deployment transaction is committed (`DbLiveDeployer.cs:57`), 10 in parallel (`NumberOfThreadsForTestsRun`), with `Serializable` isolation (`TestsTransactionIsolationLevel`). On a busy database they take range locks on live tables, and a failure does not undo the release; it only reports it.

The flag itself misleads: as a deploy parameter it reads as a step of the deployment that can stop it, but the tests are a separate operation attached to the end of `DeployAsync`. Running the tests alone is not possible either: `DeployAsync` with every flag off still validates checksums, saves migration items and runs the BeforeDeploy and AfterDeploy scripts.

An outside review of the project (2026-10-31) named this as the first place where it is easy to get hurt in production.

## Decision
2026-11-02, maintainer: the library has no users yet, so the API may break. Instead of flipping the default, take the tests out of the deployment: `DeployAsync` only deploys, and the tests run through a call of their own. A production deployer does not make that call, so there is no flag to forget.

- The call is `IDbLiveTester.RunAllTestsAsync()`, next to `RunTestAsync`; the deployer only deploys. `DbLiveVisualStudioTester` becomes `DbLiveTester`, as it no longer serves only Visual Studio.
- A failed test throws `UnitTestsFailedException`, as now: a console deployer exits with a non-zero code without extra code, and a forgotten check cannot hide a failure.
- Before the tests run, the same checks as in `DeployAsync`: ProjectId and transaction settings (`TestsTransactionIsolationLevel` for the provider). A database without the DbLive tables fails with a clear message, not with an error from saving to `dblive.unit_test_run`.
- The ProjectId check before the tests only compares. `ProjectIdValidator` binds a database that has no ProjectId yet (`ProjectIdValidator.cs:33`); before the tests, when the settings have a ProjectId, a database without one fails instead ("the database was not deployed by this project"). After a normal `DeployAsync` the ProjectId is already bound, so this stops only tests run against a foreign database. Without a ProjectId in the settings there is nothing to check, as in `DeployAsync`.
- `Serializable` stays the default `TestsTransactionIsolationLevel`: tests now run only where someone calls them.

Rejected:
- `RunTests` defaults to `false`: smaller, but keeps a flag that presents the tests as part of the deployment.
- `required bool RunTests`: forces a choice at every call, same objection.
- Only safer test settings by default (`ReadCommitted`, fewer threads): tests still run against production.

## Where
- `src/DbLive/DeployParameters.cs`: `RunTests` (:39), the presets (:12, :21), `BreakingAndTests` (:24)
- `src/DbLive/Deployers/DbLiveDeployer.cs:8, :57`: the `IUnitTestsRunner` dependency and the tests call
- `src/DbLive/Deployers/Testing/UnitTestsRunner.cs`, `IUnitTestsRunner.cs`: take `DeployParameters` only for the flag
- `src/DbLive/Testing/IDbLiveTester.cs`, `DbLiveVisualStudioTester.cs`, `src/DbLive/DbLiveBuilder.cs:67` (`CreateTester`), `src/DbLive/Bootstrapper.cs:22, :37`
- `src/DbLive.xunit/Deploy/DeploymentTestFixture.cs:58, :65`, `src/DbLive.xunit/SqlTest/DbLiveTestFixture.cs:43`
- Demos: `src/Demo/DemoMSSQL/Program.cs:19`, `src/Demo/Demo.PostgreSQL.Chinook/Program.cs:19`, `LocalDeployTests.cs` in `DemoMSSQL.Tests`, `Demo.PostgreSQL.Chinook.Tests` and `AdventureWorks.Tests`
- Tests: `src/DbLive.MSSQL.Tests/DeployAndUndoTests.cs:43, :57, :70`, `src/DbLive.Tests/Deployers/DbLiveDeployerTests.cs:54`, `src/DbLive.Tests/Deployers/Testing/UnitTestsRunnerTests.cs`, `src/DbLive.Tests/Deploy/DeploymentTestFixtureTests.cs:26, :29`
- Not affected: `demo/SqlServerConsole` (old solution, last changed in #45, already calls a `Deploy` method that no longer exists).

## Fix
- [ ] Remove `RunTests` from `DeployParameters`, and the `BreakingAndTests` preset. `Default` and `Breaking` stay.
- [ ] `DbLiveDeployer` no longer runs tests. `IUnitTestsRunner.RunAllTestsAsync()` loses its `DeployParameters` argument.
- [ ] `IDbLiveTester.RunAllTestsAsync()` runs all SQL tests the way `DeployAsync` does now: in parallel, results saved to `dblive.unit_test_run`, `UnitTestsFailedException` if any test failed. Rename `DbLiveVisualStudioTester` to `DbLiveTester`.
- [ ] Before the tests: the ProjectId check (compare only, a missing ProjectId fails), the transaction settings check, and a clear error when the DbLive tables are missing.
- [ ] `DeploymentTestFixture`: tests after the release deployment and after the first breaking deployment, as now. `DbLiveTestFixture`: drop the flag.
- [ ] `DeployAndUndoTests` and the demo `LocalDeployTests`: an explicit tests call after each deployment that runs tests now.
- [ ] Demo `Program.cs`: run the tests after the deployment (they deploy to a local database), with a comment that a production deployer does not.
- [ ] Along the way: `UnitTestsRunner` logs under the context of `UnitTestItemRunner` (`UnitTestsRunner.cs:13`).

## Docs to update
- `docs/llms-full.txt` §0 rule 12, §3.3, §8.2, §9.2 (`NumberOfThreadsForTestsRun`), §11.2, §11.3, §11.5, §12, §13.3, §18.8, §18.10, §18.11
- `README.md` (quick start; "Deployment flow": step 7 and the `DeployParameters` line)
- `docs/gettingstarted.md` (quick start)
- `docs/deployment/index.md` (deployer example, deployment steps, `DeployParameters` table, presets, tip "Tests and production")
- `docs/deployment/migrations.md:89`, `docs/deployment/breaking-changes.md:75, :92`
- `docs/unit-testing/index.md` (intro, execution, `[SqlDeployFact]` steps at :240)
- `docs/project/configuration.md:70` (`NumberOfThreadsForTestsRun`)
- `docs/deployment/zero-downtime.md:84` is written by the maintainer: `with { RunTests = false }` no longer compiles; confirm the new line with the maintainer.

## Done when
- [ ] `RunTests` and `BreakingAndTests` removed; tests run only through the new call, with unit tests for it
- [ ] xUnit fixtures, demos and integration tests updated; `DeploymentTestFixture` still runs the tests after both deployments
- [ ] Docs updated
- [ ] Release note in `## Result`: breaking API change (0096-19)
