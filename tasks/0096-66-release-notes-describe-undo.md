---
type: docs
priority: high
created: 2026-11-09
---
# The release notes of 2.0 describe undo scripts and names that 2.0 no longer has

## Problem
The GitHub Release of 2.0 is built from the `## Release note` sections of the tasks closed since 1.5.0. Several notes were written before 0096-46 removed undo scripts and the downgrade, and still describe them, so the release contradicts itself. A user who follows the note of 0096-13 creates `<version>.undo.settings.json`, on which the deployment fails with `UndoNotSupportedException`.

Notes that describe undo (checked in the output of `dotnet run .github/scripts/release-notes.cs`):

- 0096-13: lists `<version>.undo.settings.json` among the new files; "Breaking scripts and undo scripts no longer share one transaction ... so a downgrade or a breaking deployment can stop between versions".
- 0096-30: the template has "a migration with its undo script". It has only `MIGRATION_VERSION.users.sql`.
- 0096-32: "`[SqlDeployFact]` checks that the undo of a migration restores the schema"; "Before, an undo that ran without errors but did not restore the schema passed. Fix the undo and breaking scripts that start failing."
- 0096-32-02: "every migration with an undo script also goes through its undo"; the example names the test `ReleaseDeploysAndRollsBack`, which 0096-46 renamed to `ReleaseDeploys`.
- 0096-37: "migration, breaking and undo scripts".

Notes that describe a change made and changed again inside this release, which a user of 1.5.0 never saw (verified with `git grep` on the tag `1.5.0`: `SchemaCheck`, `ReleaseDeploysAndRollsBack` and `undo.settings` are not there):

- 0096-46: "`SchemaCheck.Undo` is gone" (`SchemaCheck` came with 0096-32), and "The deployment test of the template is renamed `ReleaseDeploysAndRollsBack` → `ReleaseDeploys`" (the template came with 0096-30).
- 0096-32-02: "`UndoTestMode` is no longer public". It was public in 1.5.0 and is now removed.

Found by the code review of 2026-11-09 (finding 2).

## Where
`## Release note` of:
- `tasks/done/0096-13-item-transaction-settings.md`
- `tasks/done/0096-30-dotnet-new-template.md`
- `tasks/done/0096-32-undo-test-schema-compare.md`
- `tasks/done/0096-32-02-deploy-fact-two-cases.md`
- `tasks/done/0096-37-postgresql-isolation-level-without-transaction.md`
- `tasks/done/0096-46-remove-undo.md`

## Fix
Write each note as the change from 1.5.0 to 2.0: no undo scripts and no downgrade except their removal in 0096-46, no name that is in neither 1.5.0 nor 2.0. Keep what a user of 1.5.0 must still do, for example remove `DowngradeTimeout` from `settings.json` (it is in 1.5.0).

The release note of 0096-15 also promises more of the breaking guard than it checks: that is 0096-67.

## Done when
- [ ] The notes above rewritten
- [ ] The whole preview read against 1.5.0: undo and the downgrade appear only in the note of 0096-46, and every name in the notes exists in 1.5.0 or in 2.0
