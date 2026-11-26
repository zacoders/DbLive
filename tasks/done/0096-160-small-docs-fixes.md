---
type: docs
priority: low
created: 2026-11-24
closed: 2026-11-24
---
# Small docs fixes from the review of 2026-11-24

## Release note
None. Corrections of `docs/llms-full.txt`, of the docs pages, of the XML doc of `DbLiveSqlException.SqlError` and of a release note of this release; DbLive behaves as before.

## Problem
Statements of the docs that the code does not match, each small.

1. **`null` in an item settings file.** `docs/llms-full.txt` §9.3 (line 696): "Same JSON rules as `settings.json`", and §9.1 (line 646): "`null` is an error for every key but `ProjectId`". `MigrationItemSettings` (`src/DbLive/Common/Settings/MigrationItemSettings.cs:14-21`) has only nullable properties, so `{"Timeout": null, "TransactionWrapLevel": null}` loads and means "the key is missing": the project value. 0096-119 left it so on purpose ("nothing to check"), the docs were not updated. Verified by the review.
2. **`SqlError` of a lost connection on SQL Server.** §11.2 (line 793) and the XML doc of `DbLiveSqlException.SqlError` (`src/DbLive/Adapter/DbLiveSqlException.cs:13-16`): "Empty when the error does not come from the server, such as a timeout or a lost connection". On SQL Server a transport error or a killed session is a `SqlException` with errors (`Msg 10054, Level 20, ...`), and `MsSqlDA.ExecuteBatchesAsync` (`src/DbLive.MSSQL/MsSqlDA.cs:309-312`) writes it into `SqlError` as any other error. Holds for PostgreSQL only. From the code, not run.
3. **Undo files "with any extension".** §5.1 (line 252): a file whose second part is `undo` or `u`, "with any extension", fails with `UndoNotSupportedException`. Only `.sql` and `.json` files of `Migrations` are parsed (`src/DbLive/Project/DbLiveProject.cs:128-134`), so `005.undo.txt` is ignored like any `.txt`. "With the extension `.sql` or `.json`".
4. **The warning of `Snapshot`.** On SQL Server `TransactionIsolationLevel: Snapshot` in `settings.json` logs a warning (`src/DbLive/Common/TransactionSettingsValidator.cs:21-29`), also before `RunAllTestsAsync`. The release note of 0096-109 names it; §9.2 (row `TransactionIsolationLevel`) does not.
5. **Containers without the resource reaper.** §11.5 (line 855): "the Testcontainers resource reaper removes it when the test process ends". With `TESTCONTAINERS_RYUK_DISABLED=true`, set on some CI runners, nothing removes the containers of the container fixture builders. One sentence. Not run.
6. **The release note of 0096-37** (`tasks/done/0096-37-postgresql-isolation-level-without-transaction.md`): "a script that runs without a transaction now runs at the configured isolation level. This covers code files, ...". Since then a code file on PostgreSQL runs between `begin` and `commit` of its own (release note of the transaction check of scripts). Code files still run at the configured level; the note must not call them scripts without a transaction (AGENTS.md: a later change corrects the notes of the release).

## Docs to update
- `docs/llms-full.txt` §5.1, §9.2, §9.3, §11.2, §11.5; the XML doc of `DbLiveSqlException.SqlError`
- `docs/project/configuration.md` (item settings: `null`) where it repeats them
- `tasks/done/0096-37-postgresql-isolation-level-without-transaction.md` (`## Release note`)

## Done when
- [x] 1–6 fixed

## Notes
- Found by the code review of 2026-11-24.
- 1: nothing to do. 0096-163, closed the same day after the review, made `null` an error in the item settings files too (maintainer's decision of 2026-11-24), and updated §9.1, §9.3 and `docs/project/configuration.md`: the docs and the code agree.
- 2: confirmed from the code: `MsSqlDA.ExecuteBatchesAsync` passes every `SqlException` but a command timeout to `GetSqlServerError`, which writes all its errors. Not run. No error number in the docs: the number of a transport error depends on the OS.
- 4: `TransactionSettingsValidator.ValidateAsync` runs in `ProjectLoader` (every deployment) and in `DbLiveTester.RunAllTestsAsync`, not in `RunTestAsync`.
- 5: the READMEs of `DbLive.Testcontainers.MSSQL` and `DbLive.Testcontainers.PostgreSQL` and `docs/unit-testing/index.md` say "Testcontainers removes it when the test run ends" too: they got the same condition.

## Result
Docs only, no change in behavior.
1. Already done by 0096-163.
2. `docs/llms-full.txt` §11.2 and the XML doc of `DbLiveSqlException.SqlError`: empty for a timeout, and on PostgreSQL for a lost connection; on SQL Server a lost connection or a killed session is a `SqlException` like any other error, and `SqlError` holds its errors.
3. §5.1: an undo file "with the extension `.sql` or `.json`"; other extensions are not read (`005.undo.txt` is ignored).
4. §9.2, row `TransactionIsolationLevel`, and the same row of `docs/project/configuration.md`: on SQL Server `Snapshot` in `settings.json` logs a warning, on every deployment and before `RunAllTestsAsync`.
5. §11.5: with `TESTCONTAINERS_RYUK_DISABLED=true` nothing removes the container. The same condition in `docs/unit-testing/index.md` and in the READMEs of both Testcontainers packages.
6. The release note of 0096-37: code files are no longer called scripts without a transaction; they run in a transaction of their own, at the configured level too.

`mkdocs build --strict` passes.
