---
type: bug
priority: low
created: 2026-10-26
---
# `init.sql` in `Code` is deployed as a code object

## Problem
Code deployment excludes only files matching `TestFilePatterns`. An `init.sql` next to tests in `Code` runs as code on every change and leaves test data in the database.

## Where
`src/DbLive/Project/DbLiveProject.cs:34` (exclusion list in `GetCodeGroupsAsync`).

## Fix
Exclude `init.sql` when collecting code files; unit test in `CodeItemsTests`.

## Docs to update
- `docs/llms-full.txt` §4 (paragraph about `init.sql` in `Code`)
- `docs/project/structure.md` (section "Test files")
- `docs/unit-testing/index.md` (section "Shared setup with `init.sql`")

## Done when
- [ ] `init.sql` excluded, with a unit test
- [ ] Docs updated
