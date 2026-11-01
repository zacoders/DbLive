---
type: feature
priority: medium
created: 2026-11-01
closed: 2026-11-01
---
# Deployment test: one case that follows the production order instead of six

## Problem
`[SqlDeployFact]` runs 6 cases (`deployBreaking` × `UndoTestMode`): 6 databases, 9 deployments, 6 runs of the SQL tests. The undo cases existed because an undo was checked only indirectly, by the SQL tests on the final state. Since 0096-32 the schema is compared right after the undo and after the second breaking run, so most cases repeat each other:

| Case | Checks | Still needed |
|---|---|---|
| `breaking=false, None` | migrations and code on an empty database, SQL tests before breaking | yes |
| `breaking=true, None` | SQL tests after breaking, the second breaking deployment | yes |
| `false, MigrationUndoMigration` | undo without breaking; final state as in `false, None` | only the undo check |
| `false, MigrationBreakingUndoMigration` | undo after breaking, breaking twice; final state as in `false, None` | only the checks |
| `true, MigrationUndoMigration` | the same undo plus breaking at the end; final state as in `true, None` | no |
| `true, MigrationBreakingUndoMigration` | the same, breaking a third time | no |

After an undo that restores the schema (checked), the migration runs again from the same schema, so the final schema equals the case without undo; only data may differ, and data is not compared. A deployment of everything at once (`DeployBreaking = true` in one run) is already done by the `[SqlFact]` fixture for every test class (`src/DbLive.xunit/SqlTest/DbLiveTestFixture.cs:38`).

The cases with `breaking=true` also deploy everything at once, which production does not do: production deploys `Default`, then `Breaking`.

## Where
- `src/DbLive.xunit/Deploy/DeployFixture.cs`, `SqlDeployFactDiscoverer.cs`, `SqlDeployXunitTestCase.cs`
- `src/DbLive/Common/UndoTestMode.cs` (public), `src/DbLive/Deployers/Migrations/MigrationVersionDeployer.cs`
- `src/Demo/*/DeploymentTests.cs` (3 demo projects)

## Fix
The test deploys like production, three deployments to one fresh database:
1. `Default`: migrations, code, SQL tests. The state "release deployed, breaking not yet", while old application instances still run.
2. `BreakingAndTests`: the breaking phase, then the SQL tests after breaking.
3. `Breaking` again: the schema must not change (`SchemaCheck.BreakingSecondDeployment`).

The two undo modes become one cycle per version with an undo script, because an undo must work in both states:
```
snapshot S0
migration → undo → schema == S0                                          (rollback before breaking, the usual one)
migration → breaking → S1 → breaking → schema == S1 → undo → schema == S0 (rollback after breaking)
migration
```

| | Now | After |
|---|---|---|
| Databases | 6 | 1 |
| Deployments | 9 | 3 |
| Runs of the SQL tests | 6 | 2 |

- [x] Two cases or one? One, with the undo cycle always on (decided 2026-11-01, see Notes).
- [x] Public API: `Deploy(bool deployBreaking, UndoTestMode undoTestMode)` becomes `ReleaseDeploysAndRollsBack()`, `DeployFixture` becomes `DeploymentTestFixture` with `DeployAsync(output)`. `UndoTestMode` becomes internal (`None`, `UndoCycle`). Users change the signature and the call in their test class.
- [x] Fix 0096-32-01 (all cases have one display name) in the same change: with one case it is gone.

## Docs to update
- `docs/llms-full.txt` §5.3, §13.3 (cases, sequence, example), §0 if a rule mentions the cases
- `docs/unit-testing/index.md` ("Deployment tests": the table of undo modes), `docs/deployment/migrations.md` ("Undo scripts"), `docs/deployment/breaking-changes.md` ("In tests")
- `src/DbLive.xunit/README.md`, `docs/features/index.md` ("six ways")

## Done when
- [x] `[SqlDeployFact]` deploys `Default`, `BreakingAndTests`, `Breaking`, with unit tests for the fixture and the version deployer
- [x] The demo projects use the new signature and pass on both engines
- [x] Docs updated

## Notes
Found while finishing 0096-32 (2026-11-01).

2026-11-01, one case instead of two. The first implementation kept two cases: the clean release path (`None`) and the undo cycle. The clean case was meant to give a "the release deploys" signal when an undo fails, and to run the SQL tests on data the undo cycles did not touch. Both are what the `[SqlFact]` fixture already gives: it deploys every migration once (in one run with the breaking phase) and runs every SQL test on that data, and `[SqlFact]` is the main feature, so almost every project has it. What only the clean case had: the SQL tests in the state before breaking on untouched data; marginal. The undo cycle costs nothing in a project without undo scripts (it deploys every version once), so it needs no switch. With one case and no parameter, `[SqlDeployFact]` is a plain `FactAttribute`; the discoverer and the test case class are gone, and with them the naming bug of 0096-32-01.

2026-11-01, implementation:
- A version with an undo script but without a breaking script runs only the first part of the cycle (migration → undo → migration); the second part would repeat it.
- Both undo checks run in one test, so the message of the undo after the breaking script names it: `Undo test: <undo> does not restore the schema after the breaking script <breaking>. ...`. The check stays `SchemaCheck.Undo`.
- The integration test "an undo that does not restore a column, an index and a constraint" could no longer fail with all three: the column is dropped by the breaking script, so it is still there after the first undo. Split into `Undo_that_does_not_restore_an_index_and_a_constraint_fails` (first undo) and `Undo_that_does_not_restore_a_column_dropped_by_the_breaking_script_fails` (second undo), on both engines. The failure example in the docs had the same problem and was split the same way.
- `docs/deployment/breaking-changes.md` "In tests": text followed the closing code fence on the same line, so the fence did not close; fixed.

## Result
- `UndoTestMode` is internal, values `None` (production) and `UndoCycle`. `MigrationVersionDeployer` with `UndoCycle` runs, per version with an undo script: migration → undo (schema == S0), then for a version with a breaking script migration → breaking → breaking (schema after the second run == after the first) → undo (schema == S0), then the migration.
- `DeploymentTestFixture.DeployAsync(output)` (was `DeployFixture`; renamed to the term of the docs and the messages, "deployment test", next to `DbLiveTestFixture`) deploys three times: the release (`DeployBreaking = false`, SQL tests, the undo cycle), `DeployParameters.BreakingAndTests`, `DeployParameters.Breaking` with the schema compared around it.
- `[SqlDeployFact]` is a plain `FactAttribute`; `SqlDeployFactDiscoverer` and `SqlDeployXunitTestCase` are deleted. A test is `[SqlDeployFact] public Task ReleaseDeploysAndRollsBack() => _fixture.DeployAsync(_output);`; the demos and the docs name it so (`Deploy` did not say that breaking and undo are checked) and describe the three deployments in its XML comment.
- Tests: `DeploymentTestFixtureTests`, `MigrationVersionDeployerTests`, `MigrationsDeployerTests` (unit, 367 pass); `SchemaCheckTests` and `ItemTransactionSettingsTests` on SQL Server and PostgreSQL. Demo projects `DemoMSSQL.Tests`, `AdventureWorks.Tests`, `Demo.PostgreSQL.Chinook.Tests` pass. With a `DemoMSSQL` `007.undo.sql` that does not add `PhoneNumber` back, the deployment test fails with `missing column dbo.Person.PhoneNumber` after the breaking script (checked with the two-case version; the cycle did not change since).
- Docs: `llms-full.txt` §0 (rule 5), §5.3, §10, §13.3; `unit-testing/index.md`, `deployment/migrations.md`, `deployment/breaking-changes.md`, `features/index.md`, `DbLive.xunit/README.md`.
