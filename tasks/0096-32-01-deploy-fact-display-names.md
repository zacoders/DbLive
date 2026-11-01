---
type: bug
priority: low
created: 2026-11-01
---
# `[SqlDeployFact]` cases are all named `(breaking=False, undo=None)`

## Problem
`dotnet test` lists the six cases of a `[SqlDeployFact]` test with one display name, `DeploymentTests.Deploy (breaking=False, undo=None)`, although they run with different arguments (verified on `DemoMSSQL.Tests` 2026-11-01: with a broken undo script four cases failed with `SchemaChangedException` and two passed, all six under that name). A failed case cannot be told from the others in the test output or in Test Explorer.

## Where
`src/DbLive.xunit/Deploy/SqlDeployXunitTestCase.cs`: `DeployBreaking` and `UndoTestMode` are private properties set only by the constructor used at discovery. xUnit serializes a test case between discovery and execution; the deserialization constructor leaves them at their defaults, and `GetDisplayName` reads them. `TestMethodArguments` are serialized by `XunitTestCase`, so the cases still run with the right arguments.

## Fix
Build the display name from `TestMethodArguments`, or override `Serialize`/`Deserialize` to keep the two values.

## Done when
- [ ] The six cases have distinct names in `dotnet test` and Test Explorer, with a unit test
