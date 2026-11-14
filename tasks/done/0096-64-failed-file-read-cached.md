---
type: bug
priority: low
created: 2026-11-08
closed: 2026-11-08
---
# A failed read of a script file is cached for the life of the builder

## Release note
A deployment that failed because a script file or `settings.json` could not be read (for example, a file of `SetScriptsFolder` locked for a moment by an antivirus or an editor) can now be repeated from the same builder: the file is read again. Before, the same error came back until a new builder was created. Files read successfully are still read once per builder. Nothing to change on upgrade.

## Problem
`DbLiveProject` caches the read of each file as a `Task` (`_fileData.GetOrAdd`), and `SettingsAccessor` caches the read of `settings.json` (`_settingsTask`). A failed read is cached too: a temporary I/O error stays until the builder is gone. The list of files is not cached when its checks fail, so it is read again on the next deployment; the files themselves are not.

Example: `SetScriptsFolder` on Windows, a file is locked for a moment by an editor or an antivirus, and `DeployAsync` fails with `IOException`. A second `DeployAsync` from the same builder gets the same failed `Task`, although the file can be read now. Only a new builder helps.

Low impact: the docs already say to create a new builder for each build to deploy (§3.4), and `SetProject` reads embedded resources, which do not fail this way. It matters for an application that keeps one builder and retries a deployment.

Found by the code review of 2026-11-08 (finding 7).

## Where
- `src/DbLive/Project/DbLiveProject.cs:276` (`ReadFileDataAsync`)
- `src/DbLive/Project/ProjectSettingsReader.cs:12-19` (`SettingsAccessor.GetProjectSettingsAsync`)

## Fix
Proposal: drop a faulted `Task` from the cache, so the next call reads again (`TryRemove` of the same entry when the task fails; the same for `_settingsTask`).

- [x] Fix, or document that a builder whose read failed must be replaced?
  Maintainer, 2026-11-08: fix, drop a failed read from the cache.

## Docs to update
- `docs/llms-full.txt` §3.4
- `docs/project/index.md` (the paragraph on reading the folder once)

## Done when
- [x] Decided, and fixed with a unit test (a script source that fails the first read) if changed

## Notes
- Verified: the bug is real. Both caches keep the `Task`, and `LoadSettingsAsync` is `async`, so even an exception of `GetFiles()` ends up in the cached `Task`.
- One builder for several deployments is a supported case, not an operator error: the `[SqlDeployFact]` fixture deploys three times from one builder (§13.3).
- Options weighed: drop the failed `Task` (chosen; the same policy as `_files`, whose `??=` assigns nothing when the checks throw); only document that the builder must be replaced (leaves two cache policies in one class); retry the read inside `FolderScriptSource` (would hide the lock even on the first deployment, but needs a retry count and a delay for a rare case).
- No other cache of a `Task` in the library: `InternalDbLiveProject` reads its scripts without one.
- The `dblive` tables do not change: `MinCompatibleVersion` stays.

## Result
- `DbLiveProject.ReadFileDataAsync` awaits the cached read; on an exception it removes the entry with `TryRemove(KeyValuePair)`, which removes only this `Task`, so a later successful read that is already in the cache stays.
- `SettingsAccessor.GetProjectSettingsAsync` does the same under `_settingsLock`: it clears `_settingsTask` only if it still holds the failed `Task`. A broken `settings.json` (invalid JSON) is read again on the next call too, so a fixed file is picked up.
- Test helper: `InMemoryScriptSource.FailNextRead(path, exception)` and `ReadCount(path)`.
- Tests: `CodeItemsTests.GetCodeItems_FailedRead_ReadAgain` (the first call throws, the second returns both files, the file read successfully is read once, the failed one twice) and `SettingsTests.LoadSettings_FailedRead_ReadAgain`. Both fail without the fix. All 454 tests of `DbLive.Tests` pass.
- Docs: `docs/llms-full.txt` §3.4; `docs/project/index.md`.
