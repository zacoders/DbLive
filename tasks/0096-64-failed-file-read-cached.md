---
type: bug
priority: low
created: 2026-11-08
---
# A failed read of a script file is cached for the life of the builder

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

- [ ] Fix, or document that a builder whose read failed must be replaced?

## Docs to update
- `docs/llms-full.txt` §3.4, if the behavior or the advice changes

## Done when
- [ ] Decided, and fixed with a unit test (a script source that fails the first read) if changed
