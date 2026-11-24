---
type: chore
priority: low
created: 2026-11-24
---
# Small code fixes from the review of 2026-11-24

## Problem
Each small; none changes what a correct project does.

1. **Two paths that differ only by case can pass the case check.** `DbLiveProject.ReadFiles` (`src/DbLive/Project/DbLiveProject.cs:331-340`) compares paths with `OrdinalIgnoreCase`; the stored identity is `ToLowerInvariant()` (`StringExtensions.ToStoredPath`, `CodeRows`). They disagree for a few letters: Kelvin sign `K` (U+212A) and `k`, Ohm sign (U+2126) and `ω`, Angstrom sign (U+212B) and `å`, `ẞ` (U+1E9E) and `ß`. `Code/STRAẞE.sql` and `Code/straße.sql` pass the load (verified by the review), and share one row of `dblive.code` (from the code): one of them runs on every deployment, and the breaking guard fails. `docs/llms-full.txt` §4 (line 218) promises such pairs fail. Fix: key the set by `ToStoredPath()` with an ordinal comparer.
2. **`EnforceTimestampVersion` checks the number, not the file name.** `MigrationVersionValidator` (`src/DbLive/Project/MigrationVersionValidator.cs:10-13`) formats the parsed `long` again, so leading zeros vanish: `020260808104925.migration.sql` (15 digits) passes, and the message shows `1` for `001...`. §5.1 (line 281): "14 digits". Verified by the review.
3. **Column names of `rows` are compared with `StringComparison.InvariantCulture`** (`src/DbLive/Deployers/Testing/UnitTestResultChecker.cs:274`), a linguistic comparison (it ignores zero-width characters); `rows-with-schema` compares ordinal. §8.3 (line 541): "same column names (case-sensitive)". Use `Ordinal`.
4. **The message of an unknown assert** lists `row-count` instead of `row-count=N` (`src/DbLive/Deployers/Testing/AssertTypeExtensions.cs:31`, `SupportedAssertTypes()`).
5. **`SetProject(null)`** throws `NullReferenceException` (`src/DbLive/DbLiveBuilderExtensions.cs:68-73`), its XML doc says "Cannot be null"; `LogTo` uses `ArgumentNullException.ThrowIfNull`. The same for `SetDbConnection(null)` and `SetScriptsFolder(null)`.
6. **Unreachable exceptions**: `InvalidMigrationItemTypeException` (`src/DbLive/Project/MigrationFileNameParser.cs:70`) and `UnknownMigrationItemTypeException` (`:93`): only `.sql` and `.json` files reach the parser. Remove them, or keep them as internal checks.
7. **Typos and a bare `Exception`**: "Unknown ProjectFolded." (`src/DbLive/Project/DbLiveProject.cs:264`, `src/DbLive/Deployers/Folder/FolderDeployer.cs:50`), "successfully completed.." (`FolderDeployer.cs:35`), `throw new Exception($"Database '{databaseToDrop}' does not exists.")` (`src/DbLive.MSSQL/MsSqlDA.cs:744`).

## Docs to update
- None, unless 1 or 2 change a message.

## Done when
- [ ] 1–7 fixed or decided, with unit tests for 1 and 2

## Notes
- Found by the code review of 2026-11-24.
