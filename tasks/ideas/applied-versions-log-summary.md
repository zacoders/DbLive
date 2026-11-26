---
created: 2026-11-26
---
# A summary of the applied migrations in the log instead of the list

## Idea
Every deployment logs all applied migration versions in one line (`src/DbLive/Deployers/Migrations/MigrationsDeployer.cs:38`):

```
[INF] Applied migration versions in target database: 20250101120000, 20250102093000, ...
```

The line grows with the project. A project with 2 000 migrations writes about 30 KB of versions on every deployment, which is hard to read in a console or a CI log. The list does not help much either: the applied versions are in `dblive.migration` (`docs/llms-full.txt` Section 15, the query "applied versions and their state"), and every migration the deployment applies logs its own `Deploying Migrations/...sql` line.

The line came with the switch to timestamp versions (#87). Before, a single "current migration version" was logged; with versions applied out of order there is no single one, so the whole set was logged.

Log a summary that does not grow with the project:

```
[INF] Applied migrations in target database: 1243, the latest 20260808104925.
[INF] Migrations to apply: 2.
```

- The count and the latest version say where the database is.
- The migrations to apply as a count, not a list: on an empty database of an old project all 1 243 are pending, and the list would be the same problem.
- `none` stays for a database without applied migrations; `No migrations to apply.` stays for nothing to apply.
- Structured properties (`{AppliedCount}`, `{LatestAppliedVersion}`, `{ToApplyCount}`) instead of one joined string, so a log store (Seq and others) can filter on them.
- A pending version lower than the latest applied one (a branch merged later with an older timestamp) is applied as now (`docs/llms-full.txt` Section 5.2, "There is no out of order check"). Its `Deploying ...` line shows it; no extra message.

The same problem in an exception: `DatabaseAheadOfProjectException` (`src/DbLive/Deployers/Migrations/DatabaseAheadOfProjectException.cs:12`) puts every missing version into its message. When a folder of old migrations is deleted from the project, the message holds all of them. The message could list the first 20 and `and N more`, while `Versions` keeps all of them.

What it touches:

- `MigrationsDeployer.GetMigrationsToApplyAsync` and `DeployAsync`; `MigrationsDeployerTests`.
- `DatabaseAheadOfProjectException`, if the message is shortened; `docs/llms-full.txt` (the table of errors quotes the message) and `docs/deployment/migrations.md`.
- The sample log in `docs/gettingstarted.md` ("Run App"). `docs/llms-full.txt` does not quote the log line.
- Release note: the deployment log shows a summary of the applied migrations instead of the list. Not breaking, unless a user parses the line.

Came up 2026-11-26: the maintainer noticed that the line lists every version.

## Open questions
- [ ] Shorten the message of `DatabaseAheadOfProjectException` in the same task? It is reached only after an operator mistake (deleted migration files), but the fix is only the length of the message, not a guard.
- [ ] The full list at Debug level? It costs nothing when Debug is off, but `dblive.migration` already has it.
- [ ] The latest applied version only, or also the earliest (`1243, from 20200101000000 to 20260808104925`)?
