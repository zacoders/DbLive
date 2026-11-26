---
type: docs
priority: high
created: 2026-11-09
closed: 2026-11-13
---
# The release notes of 2.0 describe undo scripts and names that 2.0 no longer has

## Release note
None. The release notes of 0096-13, 0096-15, 0096-30, 0096-32, 0096-32-02, 0096-37, 0096-45 and 0096-46 now describe the change from 1.5.0 to 2.0.

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
- `tasks/done/0096-15-drop-old-procedure-version.md` (found while checking the preview)
- `tasks/done/0096-30-dotnet-new-template.md`
- `tasks/done/0096-32-undo-test-schema-compare.md`
- `tasks/done/0096-32-02-deploy-fact-two-cases.md`
- `tasks/done/0096-37-postgresql-isolation-level-without-transaction.md`
- `tasks/done/0096-45-code-review-small-fixes.md` (found while checking the preview)
- `tasks/done/0096-46-remove-undo.md`

## Fix
Write each note as the change from 1.5.0 to 2.0: no undo scripts and no downgrade except their removal in 0096-46, no name that is in neither 1.5.0 nor 2.0. Keep what a user of 1.5.0 must still do, for example remove `DowngradeTimeout` from `settings.json` (it is in 1.5.0).

The release note of 0096-15 also promises more of the breaking guard than it checks: that is 0096-67.

## Done when
- [x] The notes above rewritten
- [x] The whole preview read against 1.5.0: undo and the downgrade appear only in the note of 0096-46, and every name in the notes exists in 1.5.0 or in 2.0

## Notes
- 2026-11-13, everything a user of 1.5.0 must do because undo is removed is in the note of 0096-46: `DowngradeTimeout` moved there from 0096-13 (a `settings.json` that still has it fails with `JsonException`, `docs/llms-full.txt` §17), and the removal of `UndoTestMode` from 0096-32-02.
- The note of 0096-46 no longer names `<version>.undo.settings.json`, `<version>.u.s.json` and `MigrationItemType.UndoSettings`: they came with 0096-13 in this release, so a user of 1.5.0 has neither the files nor the value. Verified with `git grep` on `1.5.0`: in 1.5.0 a `<version>.settings.json` applied to the migration script only, so the note of 0096-13 now says to rename it to `<version>.migration.settings.json`.
- The example of 0096-32 was `missing column dbo.Person.PhoneNumber`, the failure of an undo that does not add a column back. It is now `missing index dbo.Person.IX_Person_Name`, as in `docs/llms-full.txt` §13.3. In 2.0 the check covers the second breaking deployment only, in which the breaking scripts of the `Code` folder and BeforeDeploy/AfterDeploy run again (`DeploymentTestFixture`): the note names them, since in 1.5.0 the deployment test ran BeforeDeploy/AfterDeploy once.
- Found while checking the preview, not in the list of the code review:
  - 0096-15: "a file that comes back with a rollback is executed again" became "a file that a later release brings back (`git revert`) is executed again", the wording 0096-46 chose for the `removed` status.
  - 0096-45: `DeploymentTestFixture.Deployer` is in neither release: 1.5.0 has `DeployFixture.Deployer`, 2.0 has no `Deployer`. The note now names `DeployFixture.Deployer`.
- The note of 0096-65 keeps "an undo script left in `Migrations`": it names the undo script as a mistake that 2.0 rejects before it changes anything, which is what an upgrade from 1.5.0 with undo scripts meets; it does not describe undo as a feature.
- Check of the names (verified): every name in backticks in the preview was looked up with `git grep -w` in `1.5.0` and in `HEAD` (outside `tasks/`), and `Type.Member` names in the file that declares the type. Not found in either: only names outside DbLive (`NU1902`, `NU1903`, `default_transaction_isolation`, `.gitattributes`) and example file names (`getUser.sql`).

## Result
The release notes of 0096-13, 0096-30, 0096-32, 0096-32-02 and 0096-37 no longer mention undo scripts or the downgrade; the note of 0096-46 lists everything the removal of undo asks of a user of 1.5.0 (`DowngradeTimeout` and `UndoTestMode` included), without the names that came and went inside this release (`undo.settings.json`, `UndoSettings`, `SchemaCheck.Undo`, `ReleaseDeploysAndRollsBack`). The example of 0096-32-02 names the test `ReleaseDeploys`. Also fixed: the rollback wording of 0096-15 and the name `DeployFixture.Deployer` in 0096-45. Checked in the preview (`dotnet run .github/scripts/release-notes.cs`): undo and the downgrade appear only in the notes of 0096-46 and 0096-65 (see Notes), and every name exists in 1.5.0 or in 2.0.
