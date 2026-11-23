---
type: bug
priority: low
created: 2026-11-23
---
# `RunTestAsync` finds a test by its path with case

## Problem
`docs/llms-full.txt` §8.1 (line 421): "A test is identified by its relative path, ignoring case". `DbLiveTester` builds its dictionary with the default comparer (`src/DbLive/Testing/DbLiveTester.cs:49-53`), and `RunTestAsync` indexes it (`:71`). A path in another case or with `\` throws `KeyNotFoundException` ("The given key '...' was not present in the dictionary").

Only a direct caller of `RunTestAsync` meets it: `[SqlFact]` passes the exact path.

## Fix
`StringComparer.OrdinalIgnoreCase`, `\` replaced with `/`, and a clear exception for a path that is not a test of the project (it names the path).

## Docs to update
- `docs/llms-full.txt` §11.4, if the exception for a missing test is documented

## Done when
- [ ] Fixed, with a unit test: another case, `\`, a path that is not a test
