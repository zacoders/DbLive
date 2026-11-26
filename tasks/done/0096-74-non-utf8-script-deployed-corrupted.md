---
type: bug
priority: medium
created: 2026-11-12
closed: 2026-11-15
breaking: true
---
# A script that is not UTF-8 is deployed with its non-ASCII characters replaced

## Release note
**A file of the project that is not UTF-8 now stops the deployment with `FileNotUtf8Exception`**, which names the file, before any script runs. Before, DbLive read such a file (a script saved in Windows-1252 or Windows-1251 without a BOM, often by an old editor) as UTF-8 and deployed each of its non-ASCII characters as `�`, in comments and in string literals alike: `N'Résumé'` was stored as `N'R�sum�'`. A test file fails when the tests are read. UTF-8 with or without a BOM, and UTF-16 or UTF-32 with a BOM, are read as before, with the same checksums.

To upgrade, when a deployment fails with `FileNotUtf8Exception`:
- A code file, a BeforeDeploy or AfterDeploy script, a test, a settings file, or a migration not applied yet: save it as UTF-8. A code file then has a new checksum and is deployed again, with the right text.
- A migration or breaking script that is already applied: converting it changes its text (`�` becomes `é`), so the next deployment fails with `MigrationChecksumMismatchException`. Either save it as UTF-8 with the text DbLive read, which keeps the checksum (`pwsh -c '$p = "Scripts/Migrations/001.status.sql"; [IO.File]::WriteAllText($p, [IO.File]::ReadAllText($p))'`), or convert it properly and deploy once with `RepairMigrationChecksums = true`. Either way, fix the rows the migration wrote with a new migration.

See "File encoding" in the docs (Project structure).

## Problem
DbLive reads every script as UTF-8 (UTF-16 and UTF-32 only with a BOM). A file in another encoding, such as Windows-1252 or Windows-1251 without a BOM, is not rejected: every byte that is not valid UTF-8 becomes `U+FFFD`, and the script is deployed like that. The deployment succeeds, and nothing is logged.

Comments only look wrong. String literals change the data and the code: the text of a lookup row, a default, a message of `throw`, a `like` pattern.

Verified on SQL Server 2022 (a temporary harness, not kept): `Migrations/001.status.sql` saved in Windows-1252 with

```sql
insert into dbo.Status (Id, Name) values (1, N'Résumé reçu');
```

deploys without an error, and `dbo.Status.Name` holds `R�sum� re�u` (`0xFDFF` in place of each letter). The migration is `applied`, so the wrong rows stay until a new migration repairs them.

It matters for a project brought to DbLive from existing scripts (§18.2): SQL files written in an old editor or taken from samples are often in the ANSI code page of the machine. The AdventureWorks demo had such a file (`documentaion.sql`, Latin-1, `Résumé`); it was converted by hand in 0096-16, and DbLive got no check. SSMS saves UTF-16 LE with a BOM (according to Redgate), which DbLive reads correctly.

Found by the code review of 2026-11-12 (finding 1).

## Where
- `src/DbLive/Project/FolderScriptSource.cs:17` (`File.ReadAllTextAsync`)
- `src/DbLive/Project/AssemblyScriptSource.cs:57` (`StreamReader` with `Encoding.UTF8`)
- `src/DbLive/Project/ProjectSettingsReader.cs:46` (`SettingsAccessor` reads `settings.json` with `IScriptSource.ReadAsync` directly, not through `ScriptSourceExtensions.ReadFileDataAsync`; so the check belongs in `ReadAsync` of both sources)

## Fix
Decode with an encoding that throws on invalid bytes, and keep the BOM detection of `StreamReader`:

```csharp
static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
```

- `FolderScriptSource.ReadAsync`: `File.ReadAllTextAsync(path, StrictUtf8)` (the same `StreamReader` with BOM detection inside).
- `AssemblyScriptSource.ReadAsync`: `StrictUtf8` in place of `Encoding.UTF8` in the existing `StreamReader`.
- Both turn the `DecoderFallbackException` into a new public exception of the project (like the others in `Project/Exceptions`: internal constructor, `FileName` property) that names the file and says what to do: save the file as UTF-8.

A file without a BOM is decoded as strict UTF-8, which rejects Windows-1252 and Windows-1251 (verified). With a UTF-16 or UTF-32 BOM, `StreamReader` switches to that encoding as today. The hash of a valid file does not change: the decoded text is the same.

When it fails: the files `ProjectLoader` reads (migrations with their settings files, code, breaking scripts of the Code folder, BeforeDeploy, AfterDeploy) and `settings.json` stop the deployment before any script runs, like the other checks of the project files. Test files are read when the tests run (`GetTestsAsync`), so a test file fails then. The scripts of the self-deployment (`InternalDbLiveProject`) go through `AssemblyScriptSource` too; they are UTF-8.

- [x] Fail (proposed), or only warn? A warning keeps the deployments of today working, and keeps deploying wrong text. Fail (maintainer, 2026-11-15).

When closing: `breaking: true`. A project with such a file deploys today and fails after the fix. The release note gives the upgrade path:
- A code file, a BeforeDeploy or AfterDeploy script, a test, a settings file, or a migration not applied yet: save it as UTF-8. A code file then has a new hash and is deployed again, with the right text.
- An applied migration or breaking script: converting it changes its text (`�` becomes `é`), so the next deployment fails with `MigrationChecksumMismatchException`. Either save the file as UTF-8 with the text DbLive read (the `�` characters stay, the hash stays), or convert it properly and deploy once with `RepairMigrationChecksums = true`. In both cases a new migration repairs the data.

## Docs to update
- `docs/llms-full.txt` §4 (discovery rules: the encoding of the scripts), §17 (the exception), §18.2 (brownfield: convert the scripts to UTF-8; the upgrade path of an applied migration)
- `docs/project/structure.md`

## Done when
- [x] A script or settings file with bytes that are not valid UTF-8 fails with the new exception, with the file name in the message: the deployment stops before any script runs (a test file: when the tests are read); unit tests for the folder and for the embedded scripts
- [x] UTF-8 with and without a BOM and UTF-16 with a BOM read as before, with the same hashes
- [x] Docs updated

## Notes
- 2026-11-15, verified with a scratch .NET 10 app (not kept): `File.ReadAllText` turns Windows-1252 `N'Résumé reçu'` into `N'R�sum� re�u'` and Windows-1251 `-- Привет, мир` into `-- ������, ���`, without an exception. `StreamReader` with `StrictUtf8` and BOM detection throws `DecoderFallbackException` on both.
- Known limitations, accepted: both need an unusual file, and no code is added for them (verified 2026-11-15):
  - A UTF-8 BOM followed by ANSI bytes passes with `U+FFFD`: after a UTF-8 BOM, `StreamReader` replaces the given encoding with `Encoding.UTF8`, which does not throw.
  - UTF-16 without a BOM decodes as valid UTF-8 when the text is ASCII or Cyrillic (the bytes `00` and `04` are valid UTF-8), and the text holds `\0` characters; Latin letters with diacritics fail.
- Dead end, 2026-11-15: own BOM detection with strict UTF-16 and UTF-32 decoders, plus a check for `\0`. A prototype gave the same text as today for UTF-8 with and without a BOM and UTF-16 and UTF-32 LE/BE with a BOM, and caught both limitations above. Decision of the maintainer, 2026-11-15: not done, it repeats `StreamReader` for rare files; use `StreamReader` with `StrictUtf8`.
- Other tools (2026-11-15, from their sources and docs). None rejects invalid bytes; each defaults to UTF-8, has a setting for the encoding, and replaces invalid bytes silently:
  - DbUp: `Encoding` option, default `DbUpDefaults.DefaultEncoding = Encoding.UTF8`; `SqlScript.FromStream` uses `new StreamReader(stream, encoding, true)`, the same as DbLive today.
  - Flyway: `encoding` (default UTF-8, also per script) and `detectEncoding` (default off; UTF-8, ISO-8859-1, UTF-16). Issue 183 (2012): checksums depended on the platform encoding and failed across machines. Changing the encoding of an applied migration needs `flyway repair`.
  - Liquibase: `--file-encoding` (default UTF-8), `encoding` of `sqlFile`. Its docs warn that changing the encoding changes checksums; issue #1527: checksums differed after a move of Jenkins from Windows to Linux.
  - The C# compiler (Roslyn, `EncodedStringText.Create`) tries strict UTF-8, then falls back to the ANSI code page of the build machine, then 1252, then Latin1, without a warning. Verified on a machine with ANSI code page 1252: a Windows-1251 C# file with `"Привет"` builds and prints `Ïðèâåò`; with `<CodePage>1251</CodePage>` it prints `Привет`.
- Alternatives not taken (2026-11-15):
  - A warning only: deployments go on writing wrong text.
  - A setting for the encoding (what DbUp, Flyway and Liquibase do): without it set, text is still replaced silently. It can be added later without a breaking change if users ask.
  - Detecting the encoding, or using the ANSI code page of the machine: the same file gives other text and another hash on another machine (the issues of Flyway and Liquibase above; the C# compiler shows the same). A compiler can guess because a fix is a rebuild; a migration runs once and its data stays.
- 2026-11-15, the exception is `FileNotUtf8Exception` with `RelativePath`, not `FileName` as the Fix says: the value is the relative path, and most exceptions of the project files name it so (`UndoNotSupportedException`, `BreakingScriptMatchesTestPatternException`, `MigrationSettingsWithoutScriptException`). "File", not "Script": `settings.json` and the item settings files fail with it too.
- Verified 2026-11-15 with PowerShell 7.7: a Windows-1252 file saved again with `[IO.File]::WriteAllText($p, [IO.File]::ReadAllText($p))` holds `EF BF BD` in place of each byte that is not UTF-8, and the strict reader reads the same text that `File.ReadAllText` with `Encoding.UTF8` (DbLive 1.x) read from the original, so the hash of an applied migration stays. The docs give this command for the upgrade path.

## Result
- `FolderScriptSource.ReadAsync` and `AssemblyScriptSource.ReadAsync` decode with `ScriptEncoding.StrictUtf8` (new: UTF-8 that throws on invalid bytes), keeping the BOM detection of `StreamReader`. A `DecoderFallbackException` becomes `FileNotUtf8Exception` (new, public, `RelativePath`): the message names the file and where it was read from (the folder or the dll), says to save the file as UTF-8, and points to "File encoding" in the docs for an applied migration. `SettingsAccessor`, `DbLiveProject` and the self-deployment (`InternalScriptSource`) read through these two sources, so `settings.json`, every project file and DbLive's own scripts are checked; nothing else changed.
- Decisions: fail, not warn (maintainer, 2026-11-15); `StreamReader` with strict UTF-8, no own BOM detection (maintainer, 2026-11-15); the two known limitations under Notes stay.
- Public API: `DbLive.FileNotUtf8Exception` and its `RelativePath` in `PublicAPI.Unshipped.txt`.
- Tests: `ScriptSourceTests.NotUtf8File_Throws` (Windows-1252, Windows-1251, and invalid bytes after 20 000 ASCII characters; the folder and the embedded scripts; the message and the inner `DecoderFallbackException`); `AssemblyScriptSource_ReadsTheSameContentAsFolderScriptSource` now also covers UTF-16 BE and UTF-32 with a BOM, and its text of about 20 KB has characters of 2, 3 and 4 bytes split between the buffers of `StreamReader`: the same text and hash as before. `ProjectLoaderTests` on a real folder: a Windows-1252 `settings.json`, migration, migration settings file, code file, breaking script of the Code folder, BeforeDeploy or AfterDeploy script fails the load with `FileNotUtf8Exception` (the same files in UTF-8 load); a test file (`Tests/t1.sql`, `Tests/setup.test.sql`, `Code/GetUser.test.sql`) does not fail the load and fails `GetTestsAsync`. The new tests fail with the old readers (verified). `DbLive.Tests` 498 pass; the MSSQL, PostgreSQL and demo test projects pass; the solution builds without warnings; every `.sql` and `.json` file of the repository is UTF-8.
- Docs: `docs/llms-full.txt` §4 (encoding), §12 step 1, §17, §18.2 (convert the scripts to UTF-8; the upgrade path); `docs/project/structure.md` ("File encoding").
