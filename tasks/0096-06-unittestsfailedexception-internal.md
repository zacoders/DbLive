---
type: bug
priority: low
created: 2026-10-26
---
# `UnitTestsFailedException` is internal

## Problem
Test failures can't be caught by type, only with `catch (Exception)`. A deployer can't tell "deploy succeeded, tests failed" from "deploy failed".

## Where
`src/DbLive/Deployers/Testing/UnitTestsFailedException.cs:6`.

## Fix
Make the class `public`. Optionally add `FailedCount`, `TotalCount` and the list of failed files.

## Docs to update
- `docs/llms-full.txt` §8.2, §17 (remove "internal type")
- `docs/unit-testing/index.md` (section "Isolation and parallelism": name the type)

## Done when
- [ ] Class is public
- [ ] Docs updated
