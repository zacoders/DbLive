---
type: docs
priority: low
created: 2026-11-23
closed: 2026-11-23
---
# The XML docs of `DeployParameters` promise more of the breaking guard than it checks

## Release note
None. A correction of the XML docs of `DeployParameters.Breaking` and `DeployParameters.DeployBreaking`; DbLive behaves as before.

## Problem
- `src/DbLive/DeployParameters.cs:27-28`, the preset `Breaking`: "Deploy `Default` of the same build first: otherwise the breaking phase fails with `CodeNotDeployedException`." The guard checks only code files (`BreakingChangesDeployer.cs:46-66`): a `Default` run that failed before the code phase, or did not run, passes it when the build added or changed no code file. `docs/llms-full.txt` §5.5, the row `CodeNotDeployedException` of §17 ("Its absence does not prove that `Default` succeeded") and `docs/deployment/breaking-changes.md` say so; 0096-67 corrected them, but not this XML doc, which IntelliSense shows.
- `DeployParameters.cs:52`, `DeployBreaking`: "the breaking scripts of the migrations not applied yet". Read literally, these are the scripts of migrations that are not applied, which never run (`BreakingChangesDeployer.cs:79-86`).

## Fix
- `Breaking`: "... otherwise the breaking phase fails with `CodeNotDeployedException` when a code file of the build is not deployed with its current content."
- `DeployBreaking`: "the breaking scripts, not applied yet, of the applied migrations".

## Done when
- [x] XML docs fixed

## Result
`src/DbLive/DeployParameters.cs`: the XML doc of `Breaking` says that the breaking phase fails with `CodeNotDeployedException` when a code file of the build is not deployed with its current content, and that only code files are checked, so a failed or skipped `Default` of a build that added or changed no code file passes. The XML doc of `DeployBreaking` names "the breaking scripts, not applied yet, of the applied migrations". `src/DbLive` builds without warnings. No behavior change, no tests.
