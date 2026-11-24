---
type: chore
priority: low
created: 2026-11-24
closed: 2026-11-24
---
# Small code fixes from the review of 2026-11-24

## Release note
Small corrections of checks, messages and arguments. Nothing to change on upgrade, unless a project has one of the rare names below.

- With `EnforceTimestampVersion`, the version of a migration file is checked as written: a leading zero (`020260808104925.migration.sql`) fails with `InvalidMigrationVersionException`, and the message shows the version as written (`Bad version 001.`, not `Bad version 1.`). Remove the leading zeros: the version, a number, stays the same.
- The assert `rows` compares column names character by character, as `rows-with-schema` does: names that differ only by an invisible character (a zero-width space, a soft hyphen) or by the Unicode form of an accented letter no longer match. Before, the result depended on the globalization mode of the machine.
- An unknown assert value fails with a list that names `row-count=N` instead of `row-count`.
- `SetProject(null)`, `SetScriptsFolder(null)` and `SetDbConnection(null)` throw `ArgumentNullException` for their parameter. Before, the first threw `NullReferenceException`, the second named the parameter `path`, and the third accepted `null`.

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
- [x] `docs/llms-full.txt` §4 (what "only by case" means), §5.1 (14 digits as written), §8.3 (`rows`: column names ordinal)
- [x] `docs/project/configuration.md` (`EnforceTimestampVersion`), `docs/unit-testing/index.md` (`rows`)

## Done when
- [x] 1–7 fixed or decided, with unit tests for 1 and 2

## Notes
- Found by the code review of 2026-11-24.
- Maintainer's decision of 2026-11-24: fix all seven in code; 1 with both comparisons (a pair fails when `OrdinalIgnoreCase` or the stored path finds it equal); 6: remove both exceptions; 7 also covers the other bare `Exception`s (`AssertTypeExtensions`, `UnitTestResultChecker`, `PostgreSqlDA.DropDbAsync`); no task for `straße`/`strasse` on SQL Server (below).
- Line numbers checked against the code on 2026-11-24: they match, except `MsSqlDA.cs` (7: line 761, not 744).
- 1, verified 2026-11-24 (.NET 10.0.15, ICU): over the BMP, 9 pairs of letters are equal after `ToLowerInvariant()` and different for `OrdinalIgnoreCase`, so the case check misses them: `K`/`k` and the Kelvin sign U+212A, `Å`/`å` and the Angstrom sign U+212B, `ß` and `ẞ` U+1E9E, `Θ`/`θ` and `ϴ` U+03F4, `Ω`/`ω` and the Ohm sign U+2126.
- 1, verified 2026-11-24 (SQL Server 2025, temporary test in `DbLive.MSSQL.Tests`): `Code/STRAẞE.sql` and `Code/straße.sql` (both exist on NTFS) pass the load and share the row `code/straße.sql`. Every deployment runs one of them, in turn (`upper,lower`, then `upper`, then `lower`), and `DeployParameters.Breaking` fails with `CodeNotDeployedException: ... 1 code file(s) are not deployed with their current content: Code/STRAẞE.sql`.
- 1, the fix of the task makes the check weaker. 44 pairs go the other way: equal for `OrdinalIgnoreCase`, different after `ToLowerInvariant()` (`σ`/`ς`, `µ`/`μ`, `β`/`ϐ`, `в`/`ᲀ`, ...). Today the check rejects them; keyed only by `ToStoredPath()` it would let them pass. On SQL Server `relative_path` compares by the database collation, and `code/σ.sql` = `code/ς.sql` under `SQL_Latin1_General_CP1_CI_AS` (the default of the container), `Latin1_General_100_CI_AS`, `..._CI_AS_SC_UTF8`, `Greek_100_CI_AS`, `Cyrillic_General_100_CI_AS`; not under `_CS_AS` or `BIN2` (verified 2026-11-24, SQL Server 2025). Such a pair would share one row, as in the bug. `µ`/`μ`, `β`/`ϐ`, `в`/`ᲀ`, `ω`/Ohm are different under every collation tried. So the check must reject a pair when either comparison finds it equal: keep the `OrdinalIgnoreCase` set and add a set of `ToStoredPath()` with `StringComparer.Ordinal`.
- 1, out of scope, the same class as the accent-insensitive collations of 0096-70 and 0096-142 ("such files already run on every deployment"): on SQL Server `code/straße.sql` = `code/strasse.sql` under every collation tried, `_CS_AS` included, and so are a precomposed and a decomposed `é`; only `BIN2` keeps them apart (verified 2026-11-24). No check in .NET can follow every collation; DbLive accepts this today. A zero-width space is not ignored by any collation tried.
- 2, verified 2026-11-24 (temporary unit test with the real `MigrationFileNameParser` and `MigrationVersionValidator`): `020260808104925.migration.sql` and `00000000000000020260808104925.migration.sql` pass `EnforceTimestampVersion`; `001.migration.sql` fails with "Bad version 1. Version prefix must be exactly 14 characters long."
- 3, verified 2026-11-24 (.NET 10.0.15): `InvariantCulture` finds `ab` equal to `a` U+200B `b` (zero-width space) and to `a` U+00AD `b` (soft hyphen), `a` equal to `a` U+0000, and `caf` U+00E9 equal to `cafe` U+0301 (precomposed and decomposed `é`); `Ordinal` finds them different. With `DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1` (common in containers) `InvariantCulture` finds them all different: the result of `rows` depends on the machine. `rows-with-schema` compares `SqlColumn` records, whose `ColumnName` is compared ordinal.
- 4, verified 2026-11-24: `row-cnt` gives "Unknown assert type row-cnt. Supported values: rows, rows-with-schema, has-rows, row-count, empty, single-row". A value that contains `row-count` never reaches this message: it gets "Please use row-count=[Number] ...". Both are a bare `Exception`.
- 5, verified 2026-11-24: `SetProject(null)` throws `NullReferenceException`; `SetScriptsFolder(null)` throws `ArgumentNullException` for the parameter `path` (from `Path.GetFullPath`), not `scriptsPath`; `SetDbConnection(null)` throws nothing and keeps `null`; `LogTo(null)` throws `ArgumentNullException` for `logger`. All four parameters are non-nullable in `PublicAPI.Shipped.txt`, so a caller with nullable reference types gets a warning already.
- 6, from the code: `DbLiveProject.GetMigrationsAsync` passes only `*.sql` and `*.json` files to the parser, and `InternalDbLiveProject` only the embedded resources, which the provider projects embed from `SQL\**\*.sql`. Neither exception is reachable; `MigrationFileNameParserTests` (lines 184, 247) call the parser directly with a `.txt` file. Neither is named in `docs/`.
- 7, from the code: `DropDbAsync(skipIfNotExists: false)` is called only by tests; the deployment with `RecreateDatabase`, `DeploymentTest` and `DbLiveTestFixture` use the default `true`. The same bare `Exception` on PostgreSQL (`PostgreSqlDA.cs:667`, without the typo). Other bare `Exception`s, not in this task: `AssertTypeExtensions.cs:28,31`, `UnitTestResultChecker.cs:80,101,119,146`.
- Release notes of this release checked for the names this task touches: the note of the case check ("Two scripts whose paths differ only by case ... `ScriptPathsDifferOnlyByCaseException`") already describes the behavior after this fix, the case check did not exist in 1.5; the note that names `InvalidMigrationItemTypeException` and `UnknownMigrationItemTypeException` describes 1.5 and stays right. No note to correct.
- `breaking` is not set: a timestamp version with a leading zero and column names with invisible characters are the only things that fail now, and neither is a realistic project.

## Result
1. `DbLiveProject.ReadFiles` rejects a pair of paths when `OrdinalIgnoreCase` or the stored path (`ToStoredPath()`, ordinal) finds it equal: the first set as before, a dictionary of stored paths added. Test: `PathsDifferingOnlyByCaseTests.GetCodeGroups_throws_when_paths_are_equal_for_one_of_the_comparisons` (ẞ/ß, Kelvin sign/k, Angstrom sign/Å, Ohm sign/ω, ϴ/θ, and σ/ς that the first set already caught).
2. `IMigrationVersionValidator.Validate(relativePath)` checks the version part as written, from `MigrationFileNameParser.GetVersionPart`, which the parser uses too. Tests: `MigrationVersionValidatorTests.Validate_Throws_For_Leading_Zeros_And_Names_The_Version_As_Written`, `MigrationFileNameParserTests.Version_Part_Keeps_Leading_Zeros`.
3. `rows` compares column names with `StringComparison.Ordinal`. Test: `UnitTestResultCheckerRowsTests.Rows_Column_Names_Are_Compared_Ordinal`.
4. The list of an unknown assert names `row-count=N`. Test: `UnitTestResultCheckerAssertColumnTests.AssertValue_Unknown_Throws_With_The_Supported_Values`.
5. `SetProject`, `SetScriptsFolder` and `SetDbConnection` call `ArgumentNullException.ThrowIfNull`; the XML docs of the four builder methods name `ArgumentNullException`. Tests in `DbLiveBuilderExtensionsTests`.
6. `InvalidMigrationItemTypeException` and `UnknownMigrationItemTypeException` are removed, with their tests and the empty namespace `DbLive.Project.Exceptions`: after the `.json` branch the parser returns the `.sql` item, a migration when the second part is no type.
7. Typos fixed. The bare `Exception`s of the assert checks are the internal `InvalidAssertException`, with the same messages; an `AssertType` without its branch in `UnitTestResultChecker` is an `InternalException`; `DropDbAsync(skipIfNotExists: false)` throws `InvalidOperationException` ("does not exist") on both providers.

Unit tests: 701 passed (`DbLive.Tests`).
