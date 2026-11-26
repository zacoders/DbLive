---
type: docs
priority: low
created: 2026-11-23
closed: 2026-11-23
---
# §6.1: "a deployment of a build without the file marks its row `removed`"

## Release note
None. A correction of `docs/llms-full.txt`; DbLive behaves as before.

## Problem
`docs/llms-full.txt` §6.1 (line 386): "A deployment of a build without the file marks its row `removed` and sets `verified_utc`."

Only `CodeDeployer.DeployAsync` marks rows (`src/DbLive/Deployers/Code/CodeDeployer.cs:16-19`, `:30`), and it returns at once when `DeployCode` is false. A `Breaking` run never marks a row, nor does a `Default` run whose code phase failed. §6 (line 331), `docs/deployment/code.md:43` and `docs/deployment/breaking-changes.md:57` say it right.

## Fix
"A deployment with `DeployCode = true` of a build without the file, after its code deployed, marks its row `removed` ..."

## Docs to update
- `docs/llms-full.txt` §6.1

## Done when
- [x] Docs updated

## Result
`docs/llms-full.txt` §6.1: "A deployment with `DeployCode = true` of a build without the file, after its code deployed, marks its row `removed` and sets `verified_utc`", as §6 says of code files. `docs/deployment/code.md` and `docs/deployment/breaking-changes.md` were right; not changed. No code change, no tests.
