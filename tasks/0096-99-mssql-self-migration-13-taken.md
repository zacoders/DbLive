---
type: chore
priority: low
created: 2026-11-17
---
# SQL Server: the self-migration number 13 is taken by builds of this branch

## Problem
The SQL Server self-migration `13.tvp-unique-string-list.sql` was added on 2026-11-07 (cc0305e) and deleted on 2026-11-14 (086ccf0). It was never on `main` and never released, and the last self-migration of SQL Server is 12 now. A database that a build of the branch deployed between these dates has `dblive.version.version = 13`.

The self-deployer runs only the self-migrations above the stored version (`m.Version > appliedSelfMigrationVersion`). When the next SQL Server self-migration takes the number 13, it does not run on such a database: the self-deployer logs "a newer DbLive deployed the database" (`LogNewerSelfMigrations`) and goes on, and the code that needs the new column or table fails later with an error that does not point to the cause. Today such a database already logs that message on every deployment.

Only databases of development are affected: no package was published from the branch, and the integration tests create new databases. From the code and the git history, not reproduced.

Found by the second code review of 2026-11-17 (finding 9).

## Where
- `src/DbLive.MSSQL/SQL/` (the last file is `12.min-dblive-lib-version.sql`)
- `src/DbLive/SelfDeployer/DbLiveSelfDeployer.cs:55` (the filter), `:78-93` (`LogNewerSelfMigrations`)

## Fix
- [ ] Which way?
  1. Number the next SQL Server self-migration 14, and say so where the next one will be written: a line in Development.md ("Compatibility of the `dblive` tables"), or a comment next to the snapshot of the self-migrations in `DbLiveTablesSnapshotTests`.
  2. Drop and recreate the databases of development that the branch deployed, and keep 13 free.

## Done when
- [ ] The decision of the maintainer is under `## Notes` with its date, and written where the next self-migration is added (option 1), or the databases are recreated (option 2)
