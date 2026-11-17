---
type: docs
priority: high
created: 2026-11-17
---
# Release notes of 2.0 contradict later tasks of the same release

## Problem
The GitHub Release of 2.0.0 is built from the `## Release note` sections of the tasks closed since 1.5.0. Some notes describe a state that a later task of the same release changed, so the release contradicts itself (seen in the output of `dotnet run .github/scripts/release-notes.cs -- --check`, 172 lines):

- 0096-68: "A file that fails on every attempt fails the deployment after `MaxCodeDeployRetries` attempts". 0096-77 removed `MaxCodeDeployRetries`: a group stops when a round deploys no file.
- 0096-83: "`CodeItemTimeout` stays the timeout of each batch". 0096-84 made it the timeout of the whole file.
- 0096-35, 0096-40 and 0096-63 name the namespaces their exceptions had then: `DatabaseNotFoundException` in `DbLive.Adapter`, `DatabaseRequiresNewerDbLiveException` in `DbLive.SelfDeployer`, `FolderDeploymentException` in `DbLive.Deployers.Folder`. 0096-73 moved all three to `DbLive` (`PublicAPI.Shipped.txt`), and its own note says "The exceptions are in `DbLive` now". A `using` or a `catch` written from the older notes does not compile (CS0234). Found by the second code review of 2026-11-17 (finding 4); the other notes that name a namespace (0096-30-02, 0096-31-01) are right.

Only these terms were searched for; the generated notes were not read from start to end. 0096-66 fixed the same kind of problem for undo scripts.

Found by the code review of 2026-11-17 (finding 5).

## Where
- `tasks/done/0096-68-transaction-check-hides-code-file-error.md:10`
- `tasks/done/0096-83-code-file-partly-applied-on-sql-server.md:11`
- `tasks/done/0096-35-missing-database-error.md:10`, `tasks/done/0096-40-older-dblive-on-newer-database.md:10`, `tasks/done/0096-63-folder-script-error-without-file-name.md:11`

## Fix
Correct these notes so that they describe the change from 1.5.0 to 2.0. Then read the whole preview once and correct every other statement that a later task changed.

0096-88 and 0096-90 may change the notes of 0096-83 and 0096-77. Do the full read after them, or repeat it.

## Done when
- [ ] The notes of 0096-35, 0096-40, 0096-63, 0096-68 and 0096-83 corrected
- [ ] The whole output of `dotnet run .github/scripts/release-notes.cs` read; other contradictions corrected and listed under `## Notes`
- [ ] `dotnet run .github/scripts/release-notes.cs -- --check` passes
