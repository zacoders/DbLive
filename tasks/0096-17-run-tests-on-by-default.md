---
type: bug
priority: high
created: 2026-10-31
---
# SQL tests run during deployment by default

## Problem
The docs say not to run SQL tests against production, but the defaults run them. To deploy safely, a user has to opt out; the first example a reader sees turns tests on.

Verified by reading the code:
- `DeployParameters.RunTests` defaults to `true`, and the `DeployParameters.Default` preset sets it to `true`.
- The quick start in `README.md` and `docs/gettingstarted.md` sets `RunTests = true` with the comment "tests are not recommended to run on prod databases".
- Tests run at the end of `DeployAsync`, after the deployment is committed, 10 in parallel (`NumberOfThreadsForTestsRun`), with `Serializable` isolation (`TestsTransactionIsolationLevel`). On a busy database they take range locks on live tables, and a failure does not undo the release; it only reports it.

An outside review of the project (2026-10-31) named this as the first place where it is easy to get hurt in production.

## Where
- `src/DbLive/DeployParameters.cs:12` (`Default` preset), `:39` (property default)
- `src/Demo/DemoMSSQL/Program.cs:19`, `src/Demo/Demo.PostgreSQL.Chinook/Program.cs:19`
- Not affected: `DbLiveTestFixture` sets `RunTests = false` and `DeploymentTestFixture` sets `RunTests = true` explicitly (`src/DbLive.xunit`).

## Fix
`RunTests` defaults to `false`, in the property and in `DeployParameters.Default`. Tests at the end of a deployment become an explicit choice for local and test databases; CI runs them through xUnit against a disposable database.

- [ ] A preset with tests (`DeployParameters.DefaultWithTests`?), or just `DeployParameters.Default with { RunTests = true }` in the docs?
- [ ] Keep `DeployParameters.BreakingAndTests` as it is?
- [ ] Keep `Serializable` as the default `TestsTransactionIsolationLevel`? With tests off by default it matters only where tests are turned on.
- [ ] Demos: keep `RunTests = true` explicitly (they deploy to a local database), with a comment.

This is a behavior change: deployments that relied on the default stop running tests. Release note required (0096-19).

## Docs to update
- `docs/llms-full.txt` §0 rule 12, §3.3, §8.2, §11.3, §12, §18.10, §18.11
- `README.md` (quick start, "Deployment flow")
- `docs/gettingstarted.md`, `docs/deployment/index.md` (`DeployParameters` table, presets, tip "Tests and production"), `docs/unit-testing/index.md` (intro)
- `docs/deployment/zero-downtime.md:84` is written by the maintainer: `with { RunTests = false }` becomes redundant but stays correct; ask before changing.

## Done when
- [ ] Default changed, with unit tests for the property and the presets
- [ ] Demos and docs updated
- [ ] Release note written
