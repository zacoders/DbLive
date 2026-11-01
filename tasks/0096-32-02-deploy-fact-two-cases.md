---
type: feature
priority: medium
created: 2026-11-01
---
# Deployment test: two cases that follow the production order instead of six

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

After an undo that restores the schema (checked), the migration runs again from the same schema, so the final schema equals the case without undo; only data may differ, and data is not compared. A deployment of everything at once (`DeployBreaking = true` in one run) is already done by the `[SqlFact]` fixture for every test class (`src/DbLive.xunit/SqlTest/DbLiveTestFixture.cs:34`).

The cases with `breaking=true` also deploy everything at once, which production does not do: production deploys `Default`, then `Breaking`.

## Where
- `src/DbLive.xunit/Deploy/DeployFixture.cs`, `SqlDeployFactDiscoverer.cs`, `SqlDeployXunitTestCase.cs`
- `src/DbLive/Common/UndoTestMode.cs` (public), `src/DbLive/Deployers/Migrations/MigrationVersionDeployer.cs`
- `src/Demo/*/DeploymentTests.cs` (3 demo projects)

## Fix
Each case deploys like production, three deployments to one fresh database:
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

Two cases: the clean release path, and the same path with the undo cycle. One case (with undo) would cover the schema too, but a failed undo hides everything after it (code, tests, breaking); the clean case gives a separate "the release deploys" signal and runs the tests on data the undo cycles did not touch.

| | Now | After |
|---|---|---|
| Databases | 6 | 2 |
| Deployments | 9 | 6 |
| Runs of the SQL tests | 6 | 4 |

- [ ] Two cases or one? Two recommended (above).
- [ ] Public API: `Deploy(bool deployBreaking, UndoTestMode undoTestMode)` becomes `Deploy(UndoTestMode undoTestMode)` (or a `bool`); `UndoTestMode` keeps two values (names to choose). Users change one line in their test class.
- [ ] Fix 0096-32-01 (all cases have one display name) in the same change: with one argument the name is built from `TestMethodArguments`.

## Docs to update
- `docs/llms-full.txt` §5.3, §13.3 (cases, sequence, example), §0 if a rule mentions the cases
- `docs/unit-testing/index.md` ("Deployment tests": the table of undo modes), `docs/deployment/migrations.md` ("Undo scripts"), `docs/deployment/breaking-changes.md` ("In tests")
- `src/DbLive.xunit/README.md`, `docs/features/index.md` ("six ways")

## Done when
- [ ] `[SqlDeployFact]` produces the agreed cases, each deploying `Default`, `BreakingAndTests`, `Breaking`, with unit tests for the fixture and the version deployer
- [ ] The demo projects use the new signature and pass on both engines
- [ ] Docs updated

## Notes
Found while finishing 0096-32 (2026-11-01).
