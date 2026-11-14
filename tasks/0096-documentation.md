---
type: docs
priority: high
created: 2026-06-28
---
# Documentation

Write the user documentation (`docs/`, published with mkdocs) and the reference for AI agents (`docs/llms.txt`, `docs/llms-full.txt`, `docs/ai-agents.md`). AI agents are the primary readers: `docs/llms-full.txt` is the source of facts, and the pages under `docs/` tell the same in narrative form.

Branch: `96-documentation`.

The maintainer writes `docs/database-as-service.md` and `docs/deployment/zero-downtime.md` by hand: fix links there, but ask before rewriting text.

## Remaining work

`mkdocs build --strict` passes since D1–D4.

### D1. Broken links in `docs/philosophy.md`
- [x] `philosophy.md` merged into `database-as-service.md` (see Notes); its links now point to site pages and absolute GitHub links.

### D2. Broken links in `docs/deployment/zero-downtime.md`
- [x] `../../README.md#deployment-flow` → `deployment/index.md`.
- [x] Links to `../../src/...` → absolute GitHub links.

### D3. Empty page `docs/database-as-service.md`
- [x] Filled from `philosophy.md`; `philosophy.md` removed and all links updated.

### D4. No `nav:` in `mkdocs.yml`
- [x] Added: Home, Getting Started, Database as a Service, Project, Deployment, Unit Testing, Features, Examples, For AI agents.

### D5. Review the new documentation
The new pages are committed (fa118f6).
- [x] Proofread `docs/llms-full.txt` first: it is the source of facts for agents. Checked against the source code on 2026-11-03 (see Notes).
- [x] Review the pages under `docs/` and `README.md` against `docs/llms-full.txt` (2026-11-03, see Notes).
- [ ] After merging to `main`, check that the links from `llms.txt` to `raw.githubusercontent.com/.../main/` work.

### D6. Keep the docs from going stale
- [x] After D1 and D2, run `mkdocs build --strict` in `.github/workflows/docs.yml`, so a broken link fails the build. Also on pull requests that change the docs (build only, no deploy); `pip install -r requirements.txt` (MkDocs below 2.0); `mkdocs.yml` reports a missing anchor and a page missing from `nav:` as warnings, so `--strict` fails on them (verified: a link to a missing anchor aborts the build). Local build in `Development.md` ("Documentation").
- [x] Add a PR template (none exists yet) with the item "behavior changed → `docs/llms-full.txt` and the page under `docs/` updated". `.github/pull_request_template.md`, added with 0096-19.

The site is also published from `96-documentation` until the merge: the maintainer decided on 2026-11-13 that this is fine, nothing links to the site before 2.0 (`rejected/0096-71-docs-published-from-branch.md`).

## Found during this task

Bugs found while studying the code for the docs are tasks `0096-01` to `0096-16` (`ls tasks/0096-* tasks/done/0096-*`). The docs describe current behavior, so each fix updates them in the same commit.

Tasks `0096-36` to `0096-38` are code bugs found by the documentation review of 2026-11-03 (Notes).

Tasks `0096-40` to `0096-45` come from the code review of 2026-11-05 (docs against code, bugs): a rollback to a build with DbLive 1.5.0 after the upgrade (40, reduced on 2026-11-06 to a guard against an older DbLive), the breaking guard after a rollback (41), stale undo settings (42, rejected on 2026-11-06), the transitive embedding of `Scripts` (43), repeated reads of the project (44), small fixes (45). On 2026-11-06 the maintainer decided to remove undo scripts and the downgrade before 2.0 (`0096-46-remove-undo.md`). The review also found that the site is published from this branch (0096-71, rejected on 2026-11-13: nothing links to the site before 2.0).

Tasks `0096-51` to `0096-57` come from the code review of 2026-11-07 (the state of the branch): the error row of a failed code file hid its SQL error (51), ProjectId checked after the self-deployment (52), the second run of migration breaking scripts removed (53), code compared with the current hash only (54), the version of a migration with a sign or spaces (55), small fixes (56), the builder of the `[SqlFact]` discovery (57, open).

Tasks `0096-58` to `0096-64` come from the code review of 2026-11-08 (the library code of the branch), open unless marked done: the `[SqlFact]` discovery hides the errors of the project (58), a failed migration can lose its cause (59), a long error text fails the write of the error row on SQL Server (60, not verified), a code file retried after its error rolled back the SQL Server transaction (61, not verified), the default image `postgres:latest` (62, done: `postgres:18` since 2026-11-08), the error of a BeforeDeploy/AfterDeploy script without the file name (63), a failed read of a file cached for the life of the builder (64).

Tasks `0096-65` to `0096-72` come from the code review of 2026-11-09 (what to fix before the release): the self-deployment changes the `dblive` tables before the project is checked (65), release notes that describe undo scripts (66), the breaking guard after a `Default` run that failed before the code phase (67, needs an operator mistake), an exception of the transaction check hides the error of a code file on SQL Server (68, not verified), the `EXECUTE` permission on the table type of self-migration 13 (69, not verified), its primary key on accent-insensitive collations (70, not verified), the site published from this branch (71, rejected on 2026-11-13), the isolation level of an item settings file checked only when its script runs (72).

Tasks `0096-74` to `0096-79` come from the code review of 2026-11-12 (the library code of the branch, with experiments on SQL Server 2022 and PostgreSQL 18; the code did not change since the review of 2026-11-09): a script that is not UTF-8 is deployed with its non-ASCII characters replaced (74, verified), two deployments at the same time apply a migration twice (75, verified, needs two deployments at once), a SQL test whose code rolls back the transaction passes and leaves data behind on SQL Server (76, verified), a code file uses up its retries while its dependency is still being deployed (77, verified), `DbLive.MSSQL` changes the Dapper settings of the process and PostgreSQL deployments then fail (78, verified), small fixes of the result set comparison in SQL tests (79, verified). The same review deployed the demo databases of 1.5.0 (SQL Server and PostgreSQL, after `Default` and after `Default` and `Breaking`) with the code and the demo scripts of the branch (for DemoMSSQL with `004.migration.sql`, `007.migration.sql` and `007.breaking.sql` of 1.5.0: the branch changed these applied migrations, and the deployment stops on them with `MigrationChecksumMismatchException`, as it should): the upgrade, the SQL tests and the breaking phase passed. Also passed: a database with a binary and with a Turkish collation, a project without code files.

Tasks `0096-17` to `0096-30` come from an outside review of the project (2026-10-31): safe defaults, release process, how the project looks to someone evaluating it, and gaps compared with tSQLt. They do not block this task. The testing ones (0096-23, 0096-24, 0096-28, 0096-29) moved to #99 on 2026-11-03: `0099-unit-testing-improvements.md`. The comparison page with tSQLt (0096-21) followed on 2026-11-08 as 0099-05.

## Done when
- [ ] D1–D6 done
- [x] `mkdocs build --strict` passes
- [ ] Branch merged to `main`

## Notes

2026-10-31, docs structure agreed with the maintainer:
- "Database as a service" is the core idea of DbLive and its philosophy, so there is one page for it: `docs/database-as-service.md`. `philosophy.md` was merged into it and removed.
- The model is optional. The page has "Ways to use DbLive": classic (migrations and code), zero downtime, database as a service. SQL tests, rollback, checksums and ProjectId are extras available in every way. DbLive encourages zero downtime; transactions per migration and scripts without a transaction are part of that.
- The interface: procedures and functions are recommended; views and direct table access are possible but make seamless releases harder.
- A procedure is a module with a history: one file, changed in place. A contract change renames the object to the next version in the same file, and the old version is dropped in a breaking script; an implementation change keeps the name.
- Moving data in the background is an example of how a migration can expand the schema (`examples/table-split.md`), not a DbLive feature.
- One home per fact: the idea, the model and the release cycle diagram live in `database-as-service.md`; steps, CI/CD and the checklist in `deployment/zero-downtime.md`; presets in `deployment/index.md`; transactions in `project/configuration.md`; the 007 walkthrough in `examples/table-split.md` (aligned with the demo: `Person`/`PersonPhones`). Other pages link.
- `docs/llms-full.txt`: rule 14 (follow the way the project uses DbLive; contract vs implementation change), ways table in Section 1, AfterDeploy is not for breaking changes (Section 7).
- Open: where to drop an old procedure version when the release has no migration → task 0096-15.

2026-11-03, unit testing pages, decision of the maintainer:
- The test project setup (commands, packages, fixture, `[SqlFact]` class) lives only in `unit-testing/index.md`. The tutorial `stored-proc-and-unit-test.md` links to it and keeps what is its own: the procedure, the tests, the deploy output, a passing run and a deliberately failing one.
- `[SqlDeployFact]` moved to its own page, `unit-testing/deployment-tests.md`; `index.md` keeps a short paragraph and a link.
- One example table across the docs: `dbo.Users (UserId, Name)`. Getting Started and the tutorial used `Username`.
- Verified 2026-11-03 against packages packed from this branch and a local SQL Server: Getting Started, every tutorial step and the anatomy example from `index.md`. The deploy logs and the failure message in the docs are copied from that run. `dotnet new xunit` (SDK 10.0.402) creates an xUnit v2 project.

2026-11-03, review of the whole documentation:
- `docs/llms-full.txt` was checked against the source code, section by section (about 500 claims). Fixed there, and on the matching pages:
  - Migrations: the extension of a migration file must be lower case (bug 0096-36); a `.sql`/`.json` file in `Migrations` without a version number (`config.json`, a macOS `._001.migration.sql`) fails with `InvalidMigrationVersionException`; hidden files count only for the check of files outside the project folders, inside the folders they are read.
  - The checks of the project files (files outside the project folders, case, file names) run at the first read of the project, the checksum validation (§12 step 7): after self-deployment and ProjectId binding, before any script of the project. "Before anything is deployed" became "before any script of the project runs".
  - `settings.json` is read once per builder; the log starts with `Starting deployment.`; a leftover `ProjectDir` item does not fail the build; parameter names of `SetDbConnection`, `SetScriptsFolder`, `RunTestAsync`.
  - `MigrationDeploymentException` has no inner exception for a SQL error (the message has it); `TransactionAbortedException` vs the command timeout, which `None` does not remove; a `ProjectId` over 128 characters → `ProjectIdMismatchException`; `Snapshot` in `TestsTransactionIsolationLevel` fails too.
  - PostgreSQL: `raise exception` needs a `do` block in a test; a multi-statement file is one implicit transaction (code files and Code breaking scripts are atomic); with `Deployment` a failed code file aborts the transaction, so retries cannot help (verified on 2026-11-14 with a temporary test: a view or a `language sql` function that uses an object of a later file fails the deployment at once, nothing is committed; a `plpgsql` function does not fail, its body is resolved at run time; with `None` the same project deploys through a retry; changed by 0096-68: a code file runs under a savepoint and is retried); the isolation level of a command without a transaction is ignored (bug 0096-37).
  - With project `Deployment`, an item's isolation level is not used and its `Timeout` is only the command timeout; `None` at project level still gives an item with `Migration` its own transaction.
  - Tests: date comparison (a date equals its ISO string, not `'2026-01-01'`; `timestamptz` only a `timestamptz`), the `row-count` message without a number, `dblive.unit_test_run` is written only by `RunAllTestsAsync`, `verified_utc` of Code breaking rows.
  - Template: `dotnet new dblive -n MyDb` creates the folder `MyDb/`; on PostgreSQL the database name is the name in lower case. The deployment test's first deployment also creates the database.
- Found while checking, verified on a local SQL Server: `CommandBehavior.KeyInfo` adds hidden key columns to the result sets of a SQL test (bug 0096-38).
- Not verified: whether the session state of `setup.test.sql` (temporary tables) is visible to the test on PostgreSQL, where Npgsql may reuse the enlisted connection; the docs say it is not, and advise permanent tables either way. The type text of `timestamptz` in `rows-with-schema`.
- Pages: Features said SQL tests run "at deployment time"; README called BeforeDeploy/AfterDeploy "once-before/once-after" scripts, had a CI step "deploy database to Docker" before `dotnet test`, said `Tests` files need a pattern, and mixed `GetUser2`/`GetUser`; the separate-projects table missed `DbLive.Testcontainers.MSSQL`; the demo `settings.json` comments said tests run at the end of the deployment. Getting Started and README deploy to a database named `MyDb`.
- `database-as-service.md` (maintainer's page): the DemoMSSQL example linked `005.breaking.sql`, the old way; the link now points to `Code/dbo/stored-procedures/GetUser.breaking.sql`, the text is unchanged otherwise.
- [ ] `deployment/code.md` and `deployment/zero-downtime.md` show the versioned procedure on `dbo.Users (Id, UserName, Deleted, Active)`, while the other pages use `dbo.Users (UserId, Name)`. Align them (`zero-downtime.md` is the maintainer's page)?

2026-11-07, long-running migrations on PostgreSQL:
- Verified on PostgreSQL 18.0 with Npgsql 10.0.1, deployed through DbLive (a temporary test, not kept): with `TransactionWrapLevel: None`, several `update`s in one file commit in one transaction; a `do` block (or a `call`) with `commit` after each batch commits each batch, but only when it is alone in its file (comments are fine), otherwise `2D000: invalid transaction termination`; two `create index concurrently` in one file fail with `25001` and leave the first index invalid, and `if not exists` skips that index on the next deployment. The full table is in `tasks/ideas/postgresql-split-script-into-statements.md`.
- Decision of the maintainer: an index left invalid by a failed concurrent build is PostgreSQL behavior and the developer's to handle; DbLive documents it and does not check index validity. Keep `create index concurrently if not exists` as the re-runnable pattern, and show the code that drops the index if it is invalid.
- Verified the same way: `drop index concurrently` in a `do` block fails (`25001: ... cannot be executed from a function`); a `do` block that drops the invalid index and `create index concurrently` in one file fail with `25001`, and the drop is rolled back; the `do` block in a BeforeDeploy script works: after the failure the next deployment drops the invalid index and the migration builds it again, a third deployment changes nothing.
- Docs: `deployment/migrations.md` ("Long-running migrations": why a script without a transaction keeps the database online), `project/configuration.md` ("Item settings": batches in a `do` block; the invalid index and the BeforeDeploy script that drops it), `examples/table-split.md`, `docs/llms-full.txt` §5.3, §16, §18.7.
- Idea: split a PostgreSQL script into statements and run them one by one, as DbUp does: `tasks/ideas/postgresql-split-script-into-statements.md`.
