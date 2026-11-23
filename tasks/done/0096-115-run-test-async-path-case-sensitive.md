---
type: bug
priority: low
created: 2026-11-23
closed: 2026-11-23
---
# `RunTestAsync` finds a test by its path with case

## Release note
`IDbLiveTester.RunTestAsync` finds the test by its relative path ignoring case and takes `\` as `/` (`tests\orders\select.sql` runs `Tests/Orders/select.sql`), as the documentation says. A path that is not a test of the project throws `KeyNotFoundException` with the message `Test '<path>' not found in the project.` and the form of the path to pass. `[SqlFact]` is not affected. Nothing to change on upgrade.

## Problem
`docs/llms-full.txt` §8.1 (line 421): "A test is identified by its relative path, ignoring case". `DbLiveTester` builds its dictionary with the default comparer (`src/DbLive/Testing/DbLiveTester.cs:49-53`), and `RunTestAsync` indexes it (`:71`). A path in another case or with `\` throws `KeyNotFoundException` ("The given key '...' was not present in the dictionary").

Only a direct caller of `RunTestAsync` meets it: `[SqlFact]` passes the exact path.

## Fix
`StringComparer.OrdinalIgnoreCase`, `\` replaced with `/`, and a clear exception for a path that is not a test of the project (it names the path).

## Docs to update
- `docs/llms-full.txt` §11.4, if the exception for a missing test is documented

## Done when
- [x] Fixed, with a unit test: another case, `\`, a path that is not a test

## Notes
- The exception stays `KeyNotFoundException`, as `IDbLiveTester` documented it: a caller that catches it keeps working, and §17 already lists a .NET exception with a DbLive message (`DirectoryNotFoundException: Scripts folder '<path>' not found.`). A new DbLive exception type would add public API for a case only a direct caller meets.
- The message names the path as given (with `\`, if the caller passed it) and shows the expected form, because the likely mistakes are a path relative to another folder (`Scripts/Tests/...`, an absolute path) or a code file.
- The dictionary does not need a check for two paths that differ only by case: the project load rejects them first (`ScriptPathsDifferOnlyByCaseException`).

## Result
- `DbLiveTester` keys the tests by relative path with `StringComparer.OrdinalIgnoreCase`; `RunTestAsync` replaces `\` with `/` in the path and, when the project has no such test, throws `KeyNotFoundException`: "Test '<path>' not found in the project. Pass the path of the test file relative to the scripts folder, for example 'Tests/Orders/select-order.test.sql'." The XML doc of `IDbLiveTester.RunTestAsync` says so.
- Unit tests (`DbLiveTesterTests`): the test is found by its path in another case, in upper case, with `\` and with mixed separators, among tests with the same file name in other folders; a missing file, a path with the scripts folder, a code file and a missing path with `\` throw with the message, and no test runs.
- Docs: `docs/llms-full.txt` §11.4 (how `RunTestAsync` finds the test) and §17 (row `KeyNotFoundException`). `docs/unit-testing/index.md` shows `RunTestAsync` only inside `[SqlFact]`, which passes the exact path: no change.
