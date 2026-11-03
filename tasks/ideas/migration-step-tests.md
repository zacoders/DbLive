---
created: 2026-11-02
---
# Tests for each step of a migration version: after the migration, the undo and the breaking script

## Idea
A migration version gets optional tests of three kinds, one kind per script:

| Test | Runs | Checks |
|---|---|---|
| migration test | data → migration → test | the forward change and the data move |
| breaking test | data → migration → breaking → test | what the breaking script does to the data |
| undo test | data → migration → undo → test, and data → migration → breaking → undo → test | the rollback in both states a rollback can find (`docs/llms-full.txt` §0, rule 5) |

A version may have several tests of one kind, one per data case (a person with a null phone, a person with two phones), as a folder holds several SQL tests. The assertions are those of the SQL tests (§8.3): an `assert` result set, or an error.

Each test is isolated: it runs on a database at the previous version, prepares its own data, runs its steps and is rolled back. Neither its data nor its changes reach another test or the next version: otherwise tests and versions depend on each other, which is not acceptable.

```
database at version 4
migration test 1:  begin → data → migration → test → rollback
migration test 2:  begin → data → migration → test → rollback
breaking test:     begin → data → migration → breaking → test → rollback
undo test:         begin → data → migration → undo → test → rollback
                   begin → data → migration → breaking → undo → test → rollback
migration                                  (version 5 for good, on a database without test data)
```

One test that chains every step (migration → test → undo → test → breaking → test) is one test for all cases: a later step runs on whatever the earlier steps left, not on data prepared for it, and a failed step stops the steps after it. Separate tests check each step on its own data.

A test is two files, as everywhere in DbLive now (`setup.test.sql` and the test): the data, run before the migration, and the check, run after the step.

Test data and foreign keys. A test needs rows only in the tables the migration touches, but with foreign keys on it must also fill every table they reference, and a small test grows into an integration test (the same problem for SQL tests, solved there by `dblive.FakeTable` of 0099-01; it does not fit here, because the migration changes the real table). A setting switches off all foreign keys for the migration tests: the data file inserts fake rows with any key values, and the rollback of the test switches the keys back on (DDL is transactional).

Why:
- The deployment test runs migrations on an empty database. A migration that moves data (backfill, column split, table split), an undo that moves it back and a breaking script that cleans up all run on empty tables and pass with any `insert ... select`. Today only "the script does not throw" and "the schema is restored" are checked.
- A step test runs against the schema of its own version, on its own data, so later versions never break it, unlike a SQL test that must follow the current schema. Written once together with the migration, it checks that version from then on.
- The automatic checks (schema compare, data compare of the other idea) need no code. A step test is for what they cannot say: that the forward move is right (migration test), a lossy or partial undo where exact equality is the wrong requirement (undo test), and what a breaking script does to the data (breaking test).
- The prepared data alone catches scripts that fail on real data: an `alter column ... not null` on a column with nulls, a unique index on duplicates.

Prior art: Sqitch has a `verify` script per change, run after deploy. It has no test data and nothing after a revert.

Came up 2026-11-02, after 0096-32 and 0096-32-02.

## Decided
- 2026-11-02: the test data never reaches another test or the next version; each test is rolled back.
- 2026-11-02: migration tests run in a mode of their own, not in a production deployment.
- 2026-11-02: separate tests for the migration, the breaking script and the undo, each with its own data, and several of one kind per version; not one test that chains all steps.
- 2026-11-02: a test is two files, the data and the check, as `setup.test.sql` and a SQL test are now.

## Relation to `undo-test-data-round-trip.md`
One feature seen from two sides. An assert after a step on an empty database checks little that the schema compare and the SQL tests do not already check; its value comes from data. That idea gives the data and the automatic data compare (it fits the undo test: data → snapshot → migration → undo → compare), this one the asserts after each step. Merge them when taken into work.

## Open questions
- [ ] Where it runs: inside the deployment test (`[SqlDeployFact]`, next to the undo cycle), or a run of its own, as the SQL tests have (`RunAllMigrationTestsAsync`, each test as its own xUnit case as with `[SqlFact]`)? A run of its own deploys the versions one by one on its own database, because the tests of version N need the database at version N-1; the tests of one version can run in any order, versions in version order.
- [ ] When the data is prepared: before the migration, in the old schema (tests the data move, the main point), or after it, in the new schema (tests only the new schema, close to a SQL test with `setup.test.sql`)?
- [ ] One file split by a marker (data above, check below), as an option next to the two files?
- [ ] Foreign keys setting: in `settings.json` for all migration tests, with an override per test (a test of a foreign key that the migration adds needs them on)? Only foreign keys, or also check constraints and triggers?
- [ ] Foreign keys stay off for the whole test, migration included: switching them back on before the migration checks the fake rows and fails. Then cascade actions do not run, and a migration that adds a foreign key checks the existing rows and fails on fake ones: such a test inserts consistent rows for that key, or turns the setting off. The undo test takes its schema snapshot after the keys are off, because the snapshot compares disabled foreign keys.
- [ ] How on each engine (not verified). SQL Server: `alter table ... nocheck constraint all` (foreign keys and check constraints), transactional. PostgreSQL: `set session_replication_role = replica` needs superuser (fine in a Docker test database) and also switches off ordinary triggers, including the sync triggers of an expand/contract migration; `alter table ... disable trigger all` needs superuser too; deferred constraints need deferrable keys, and `alter table` on a table with pending deferred checks fails ("cannot ALTER TABLE ... because it has pending trigger events"), which is what a migration does. Dropping the foreign keys inside the test transaction needs only the table owner and the rollback restores them, but a migration that drops or changes such a key by name fails.
- [ ] The undo test in two states: one test file run twice (after the migration, and after the migration and the breaking script), or two kinds of tests?
- [ ] How to roll back. One transaction around each test is the simplest: DDL is transactional on SQL Server and PostgreSQL. DbLive now opens a transaction per item; in a test the items must run in the outer one. It does not work for an item with `TransactionWrapLevel: None` (PostgreSQL `create index concurrently` fails inside a transaction, SQL Server `alter database`): skip the tests of such a version with a message, or run them in a throwaway database (PostgreSQL `create database ... template`, SQL Server database snapshot or backup/restore)?
- [ ] File names. Several tests of each kind per version suggest a folder per version (`MigrationTests/005/migration.null-phone.sql`, `MigrationTests/005/undo.sql`). Names next to the migration collide with the grammar (§5.1): `005.migration.test.sql` is today a second migration of version 5 (`DuplicateMigrationItemException`); `005.test.sql` and `005.arrange.sql` are migrations named `test` / `arrange` and may exist in user projects, so new type tokens would be a breaking change of the grammar. Two files per test must be paired by name; or a folder per data case, its data used by every kind of test in it (`MigrationTests/005/null-phone/data.sql`, `migration.sql`, `undo.sql`, `breaking.sql`), as `setup.test.sql` serves every test of its folder?
- [ ] Not checksummed and not stored in `dblive.migration`: a test may be edited after its migration was applied. Production deployments never read them; the check of 0096-33 must accept the new names.
- [ ] Does an undo test turn off the automatic data compare of its version (the user takes control), or do both run?
- [ ] Failure message names the test and the step: `Migration test: MigrationTests/005/migration.null-phone.sql failed after Migrations/005.migration.sql: Expected 3 row(s), but 0 received.` A failed test stops the run (the next versions need this one applied), or the version is still applied and the next versions are tested?

## Depends on
- `tasks/ideas/undo-test-data-round-trip.md` (test data, data snapshot)
- `tasks/done/0096-32-02-deploy-fact-two-cases.md` (the undo cycle)
- `tasks/0099-01-test-helpers.md` (schema changes inside the rolled-back test transaction, schema locks with tests in parallel: verified there)
