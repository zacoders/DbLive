---
type: feature
priority: medium
created: 2026-10-31
---
# Allow a downgrade only to an explicitly named version

## Problem
`AllowDatabaseDowngrade` is a bool. When it is `true`, DbLive reverts every applied version that the build does not contain, using the stored undo scripts. Undo scripts often drop columns and tables, so data in them is lost. A flag left `true` in a pipeline, or copied from the rollback pipeline, lets any old artifact deployed by mistake revert the database.

The guard against an accidental downgrade with the flag off already exists (`DowngradeNotAllowedException`); this task is about the case where the flag is on.

## Where
- `src/DbLive/DeployParameters.cs:56`
- `src/DbLive/Deployers/Migrations/DowngradeDeployer.cs:32`

## Fix
Let the caller state what it expects to revert to, and fail when the build does not match.
- [ ] Shape: `DowngradeToVersion = 12` (the downgrade runs only if the highest version in the build is 12)? Or the list of versions expected to be undone?
- [ ] Keep `AllowDatabaseDowngrade` as is, mark it obsolete, or make it require the target version (behavior change → release note)?
- [ ] Before running, log the versions to undo and the names of their undo scripts.

## Docs to update
- `docs/llms-full.txt` §5.5, §11.3, §17, §18.8
- `docs/deployment/migrations.md` ("Downgrade"), `docs/deployment/index.md` (`DeployParameters` table), `docs/features/index.md`

## Done when
- [ ] Implemented with unit tests (matching target, wrong target, no downgrade needed)
- [ ] Docs updated
