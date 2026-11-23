---
type: docs
priority: low
created: 2026-11-23
---
# `CodeFolder: ""` is described as a way past the check of files outside the project folders

## Problem
`docs/llms-full.txt` §4 (line 208): "With `CodeFolder: ""` every file is in the Code folder, so the check never fails." It reads like a safe setting.

`IsInFolder(path, "")` is always true (`src/DbLive/Project/DbLiveProject.cs:403-404`), so every `*.sql` file of the project becomes a code file:
- migrations run a second time as code;
- tests in `Tests/` that do not match `TestFilePatterns` are deployed as code, outside a rolled-back transaction;
- BeforeDeploy and AfterDeploy scripts run as code too.

0096-33 noted this ("everything is code"); the docs do not say it. An agent that meets `FilesOutsideProjectFoldersException` can take the sentence as the fix.

## Fix
Replace the sentence: "`CodeFolder: ""` makes every `.sql` file of the project a code file, also migrations, tests and BeforeDeploy/AfterDeploy scripts: do not use it."

- [ ] Also reject an empty folder name in `settings.json` when the project is loaded?

## Docs to update
- `docs/llms-full.txt` §4
- `docs/project/configuration.md` (folder keys), if it mentions an empty folder

## Done when
- [ ] Docs updated
