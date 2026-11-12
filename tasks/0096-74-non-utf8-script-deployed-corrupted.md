---
type: bug
priority: medium
created: 2026-11-12
---
# A script that is not UTF-8 is deployed with its non-ASCII characters replaced

## Problem
DbLive reads every script as UTF-8 (UTF-16 and UTF-32 only with a BOM). A file in another encoding, such as Windows-1252 or Windows-1251 without a BOM, is not rejected: every byte that is not valid UTF-8 becomes `U+FFFD`, and the script is deployed like that. The deployment succeeds, and nothing is logged.

Comments only look wrong. String literals change the data and the code: the text of a lookup row, a default, a message of `throw`, a `like` pattern.

Verified on SQL Server 2022 (a temporary harness, not kept): `Migrations/001.status.sql` saved in Windows-1252 with

```sql
insert into dbo.Status (Id, Name) values (1, N'Résumé reçu');
```

deploys without an error, and `dbo.Status.Name` holds `R�sum� re�u` (`0xFDFF` in place of each letter). The migration is `applied`, so the wrong rows stay until a new migration repairs them.

It matters for a project brought to DbLive from existing scripts (§18.2): SQL files written in SSMS or in an old editor are often in the ANSI code page of the machine. The AdventureWorks demo had such a file (`documentaion.sql`, Latin-1, `Résumé`); it was converted by hand in 0096-16, and DbLive got no check.

Found by the code review of 2026-11-12 (finding 1).

## Where
- `src/DbLive/Project/FolderScriptSource.cs:17` (`File.ReadAllTextAsync`)
- `src/DbLive/Project/AssemblyScriptSource.cs:57` (`StreamReader` with `Encoding.UTF8`)
- `src/DbLive/Project/ScriptSourceExtensions.cs` (`ReadFileDataAsync`, the one place both go through)

## Fix
Decode with an encoding that throws on invalid bytes (`new UTF8Encoding(false, throwOnInvalidBytes: true)`; BOM detection stays), and turn the `DecoderFallbackException` into an exception of the project that names the file and says what to do: save the file as UTF-8.

The files are read when the project is read, so the deployment stops before any script of the project runs, like the other checks of the project files. `settings.json` and the item settings files go the same way.

The hash of a valid file does not change: the decoded text is the same.

- [ ] Fail (proposed), or only warn? A warning keeps the deployments of today working, and keeps deploying wrong text.

When closing: `breaking: true`. A project with such a file deploys today and fails after the fix.

## Docs to update
- `docs/llms-full.txt` §4 (discovery rules: the encoding of the scripts), §17 (the exception), §18.2 (brownfield: convert the scripts to UTF-8)
- `docs/project/structure.md`

## Done when
- [ ] A script with bytes that are not valid UTF-8 stops the deployment before any script runs, with the file name in the message; unit tests for the folder and for the embedded scripts
- [ ] UTF-8 with and without a BOM and UTF-16 with a BOM read as before, with the same hashes
- [ ] Docs updated
