---
type: bug
priority: medium
created: 2026-10-26
closed: 2026-10-31
---
# Checksums depend on line endings and path separators

## Release note
**Checksums no longer depend on line endings.** A Windows checkout (CRLF) and a Linux checkout (LF) of the same migration have the same checksum, so one database can be deployed from both without `MigrationChecksumMismatchException`, and `.gitattributes` is no longer required. Checksums stored by earlier versions keep matching; nothing to repair. Relative paths in the `dblive` tables now use `/` on every OS; rows written on Windows are converted on the first deployment, so switching the deploying OS no longer redeploys all code. SQL test names in Test Explorer show `/` (`Tests/GetUser.test.sql`).

## Problem
The hash is computed over the raw file text. On Windows with `core.autocrlf=true` files have CRLF, on a Linux CI they have LF, so an applied migration fails with `MigrationChecksumMismatchException`. The repository has no `.gitattributes`. Likewise `relative_path` is stored with the OS separator, so switching OS redeploys all code once.

Verified: in this repository 133 `.sql` files are LF in the index and CRLF in the Windows working tree (`core.autocrlf=true` from the Git for Windows system config); CI runs on `ubuntu-latest`. `File.ReadAllTextAsync` keeps `\r\n`, the hash is XxHash64 of the raw text, and `MigrationChecksumMode` is `Strict` by default.

It breaks only when one database is deployed from checkouts with different line endings: a shared dev database deployed by Windows developers and Linux CI, build agents moved from Windows to Linux, Windows and WSL/Docker on one local database. Also an editor that changes the line endings of an applied migration: `git status` is clean, the deployment fails. Code items only redeploy.

`relative_path` matters only for `dblive.code` (primary key, `FindCodeItemAsync`): switching from Windows to Linux redeploys all code once and leaves the `\` rows forever. In `dblive.migration` it is informational (the key is version and item type). Linux already stores `/`.

## Where
`src/DbLive/Common/Hasher.cs:8`, `src/DbLive/Common/FileSystem.cs:62`.

## Fix
- Option A: normalize `\r\n` to `\n` before hashing. Hashes of already applied CRLF migrations change, so a transition is needed (accept both hashes).
- Option B: keep the code, add `.gitattributes` (`*.sql text eol=lf`) and recommend it in the project template.

In both cases store `relative_path` with `/`.

- [x] Choose A or B. **A.** B is the current state of the docs and does not help existing databases, delivery without git (zip artifacts, Docker `COPY` from a Windows checkout) or an editor that changes line endings.

Transition: "accept both hashes" (raw and normalized) is not enough. A Linux checkout gives LF, so its raw and normalized hashes are the same, while a database deployed from Windows stores the hash of the CRLF text. A stored hash matches when it is the hash of the LF text (new), of the CRLF text or of the raw text (mixed line endings), all computed from the current file. Old stored hashes are not rewritten.

- Normalize only for the hash: the executed SQL and the stored undo content stay as in the file (CRLF inside a multi-line string literal is data).
- Only `\r\n` → `\n`. Not `string.ReplaceLineEndings`: it also replaces `\r`, U+0085, U+2028, U+2029.
- Compare by the new rule wherever a stored hash is compared: `MigrationChecksumValidator`, `CodeItemDeployer`, `MigrationsSaver`. No mass code redeploy after the upgrade.
- `relative_path` with `/`: `Path.DirectorySeparatorChar` → `/` (on Linux `\` is a file name character). Self-deploy script (MSSQL `08`, PostgreSQL `05`) rewrites `\` to `/` in `dblive.migration`, `dblive.code`, `dblive.unit_test_run`, `dblive.folder_items`; when a database has both rows for one file (deployed from both OSes), the latest row is kept. No code redeploy, no orphan rows.

## Docs to update
- `docs/llms-full.txt` §5.4 (Line endings paragraph), §18.1 (step 5), §19
- `docs/project/structure.md` (section "Line endings")
- `docs/deployment/migrations.md` (section "Checksums")

## Done when
- [x] Chosen option implemented, with unit tests
- [x] `relative_path` stored with `/`
- [x] Docs updated

## Notes
- Old stored hashes stay as they are: no automatic rewrite during validation (no extra writes, nothing to remove later). The legacy hashes are computed only when the new hash does not match.
- The self-deploy script treats `\` as a separator also in rows written on Linux, where it is a file name character. Such a file would be redeployed once; not worth handling.
- `.gitattributes` for this repository was not added: no longer needed for DbLive, and it changes the working tree of every checkout. A separate decision.
- `DemoMSSQL.Tests.LocalDeployTests` (`LocalOnly`, persistent local database `DbLive_DemoMSSQL`) fails with a mismatch on `007.migration.sql`. Not caused by this task: the stored hash `-5948481248845642669` is the hash of the CRLF text of `007` before commit `dc78014` changed its trigger, which also confirms that older DbLive stored the hash of the raw CRLF text. The local database has the old trigger: drop it and redeploy.

## Result
- `HashExtensions.ComputeFileHash`: XxHash64 of the text with `\r\n` → `\n`. `FileData.MatchesHash(storedHash)`: the new hash, or a legacy hash (raw CRLF text, raw text). Used by `MigrationChecksumValidator`, `CodeItemDeployer`, `MigrationsSaver`. Executed SQL and stored undo content are unchanged.
- `StringExtensions.GetRelativePath` returns `/` on every OS. `SqlFactDiscoverer` passes `Path.GetFullPath` of the test file to the IDE. SQL test names show `/`.
- Self-deploy scripts MSSQL `08.relative-path-separator.sql`, PostgreSQL `05.relative-path-separator.sql`: `\` → `/` in `dblive.migration`, `dblive.code`, `dblive.unit_test_run`, `dblive.folder_items`; for duplicates the latest row is kept (code: latest of `applied_utc`/`verified_utc`/`created_utc`; tests: `run_utc`; folder items: `completed_utc`, per `folder_type`).
- Tests:
  - `FileDataTests`: CRLF/LF same hash, content kept, only CRLF normalized (`\r`, U+2028, U+0085 are changes), legacy hashes of CRLF/LF/mixed text match, other content does not.
  - `StringExtensionsTests.GetRelativePath_Uses_Forward_Slash`; `MigrationChecksumValidatorTests.ValidateAsync_strict_passes_when_only_line_endings_differ` (both directions); `CodeItemDeployerTests.DeployCodeItem_Hash_Of_Other_Line_Endings_Is_A_Match`; `MigrationsSaverTests.Save_skips_not_applied_item_when_only_line_endings_differ`. With the old hash and path code these 8 checks fail.
  - Integration `CrossPlatformDeployTests` (MSSQL and PostgreSQL): a database deployed by older DbLive from Windows (CRLF hashes, `\` paths) deploys from LF files in `Strict` mode, the code file is found by the new path and not redeployed; duplicates from Windows and Linux keep the latest row in all three keyed tables.
  - `DbLive.Tests` 286, `DbLive.MSSQL.Tests` 22, `DbLive.PostgreSQL.Tests` 11, `Demo.PostgreSQL.Chinook.Tests` 11, `AdventureWorks.Tests` 10, `DemoMSSQL.Tests` 17 of 18 (the `LocalOnly` test, see Notes).
- Docs: `docs/llms-full.txt` §0 (rule 1), §5.4, §15 (paragraph on `relative_path`), §17 (`MigrationChecksumMismatchException` row), §18.1 (step 5 removed), §19 (limitation removed); `docs/project/structure.md` ("Line endings"); `docs/deployment/migrations.md` ("Checksums").
