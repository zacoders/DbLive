# Project Structure

All scripts live under the `Scripts` folder of the SQL project (another name can be set in the `.csproj`, see [Embedding the scripts](index.md#embedding-the-scripts)). The names of the folders inside it can be changed in [`settings.json`](configuration.md); the defaults are shown here.

```
Scripts/
  settings.json                        optional project settings
  Migrations/                          optional
    20260808104925.create-users.sql
    20260808104925.undo.sql
    20260812090000.split-phones.sql
    20260812090000.breaking.sql
    20260812090000.undo.sql
    20260815120000.indexes.sql
    20260815120000.migration.settings.json
  Code/                                optional
    schemas/sales.sql
    dbo/GetUser.sql
    dbo/GetUser.test.sql
    dbo/GetUser.breaking.sql
  Tests/                               optional
    Orders/setup.test.sql
    Orders/select-orders.test.sql
  BeforeDeploy/                        optional
  AfterDeploy/                         optional
```

## Folders

| Folder | Required | Picked up | Runs |
|--------|----------|-----------|------|
| `Migrations` | no | `*.sql` and `*.json`, including subfolders | Once per database, in version order |
| `Code` | no | `*.sql` (except test files, breaking scripts and `setup.test.sql`), including subfolders | When the file is new or changed |
| `Code` (breaking scripts) | no | `breaking.sql`, `b.sql`, `*.breaking.sql`, `*.b.sql`, including subfolders, sorted by relative path | On **every** breaking deployment, before the breaking scripts of migrations |
| `Tests` | no | every `*.sql` except `setup.test.sql`, including subfolders | From xUnit, or with `RunAllTestsAsync()` |
| `BeforeDeploy` | no | `*.sql`, including subfolders, sorted by relative path | On **every** deployment, before migrations |
| `AfterDeploy` | no | `*.sql`, including subfolders, sorted by relative path | On **every** deployment, after breaking changes |

Every folder is optional; a missing folder is skipped. A project without `Migrations` deploys only code and the BeforeDeploy/AfterDeploy scripts; its tests run as usual.

Nothing else may be in `Scripts`. A file outside these folders, other than `settings.json` in the root, stops the deployment with `FilesOutsideProjectFoldersException` before any of your scripts runs, because DbLive would never read it: a `PostDeployment` folder would never run, and a misspelled `setting.json` would leave every setting at its default. Keep other scripts and documents outside `Scripts`. Hidden files and folders, whose names start with a dot (`.DS_Store`, `.gitkeep`), do not count as outside; inside the project folders they are read like any other file.

Folder names and file patterns are matched ignoring case. "Sorted by relative path" compares the path with `/` character by character, ignoring case, so the order is the same on every OS and machine: `01-users.sql`, `02-roles.sql`, `sub/01.sql`. Number the files to control the order.

Files with other extensions (`.txt`, `.md`) inside these folders are ignored, so notes can sit next to scripts. The one exception is a `.json` file in `Migrations` that is not an item settings file: it stops the deployment.

Two files whose paths differ only by case, such as `Code/dbo/GetUser.sql` and `Code/dbo/getuser.sql`, stop the deployment with `ScriptPathsDifferOnlyByCaseException` before any of your scripts runs, whatever their folder or extension. Such files can exist only on a case-sensitive file system (Linux), and DbLive identifies files ignoring case, so they would be one item. Rename or delete one of them.

!!! warning "Ship applied migrations with every build"
    Once a database has applied migrations, every build you deploy to it must contain them. If they are missing, for example because a migration file was deleted or the deployer was built from an older commit, DbLive treats this as a [downgrade](../deployment/migrations.md#downgrade) and the deployment fails with `DowngradeNotAllowedException`.

## Migration files

```
<version>.<type>.<name>.sql
<version>.<name>.sql                 (no type: a migration)
<version>.<item>.settings.json       (settings of the migration, breaking or undo script)
```

**Version.** The first part of the name, a whole number (64-bit). `001`, `1` and `20260808104925` are valid; `001` and `1` are the same version. For teams, use UTC timestamps (`yyyyMMddHHmmss`) so that branches do not collide, and set `"EnforceTimestampVersion": true` to reject anything else.

**Type.** Case-insensitive, with a short form:

| Type | Short | Extension | Meaning |
|------|-------|-----------|---------|
| `migration` | `m` | `.sql` | The forward change. Every version must have one. |
| `undo` | `u` | `.sql` | Reverts this version. Used by downgrades and undo tests. |
| `breaking` | `b` | `.sql` | Destructive part, run only in a breaking deployment. |

**Name.** Free text, for humans only. If the second part of the file name is not a type, the file is a migration and that part is its name: `20260808104925.create-users.sql` is a migration named `create-users`.

**Extension.** `.sql` for scripts, `.json` for settings, in any case: `001.migration.SQL` is a migration.

**Settings.** A `.json` file `<version>.<item>.settings.json` holds the [settings](configuration.md#item-settings) of one script of that version: `migration`, `breaking` or `undo`. Short forms combine: `<version>.m.s.json`, `<version>.b.settings.json`, `<version>.u.s.json`. A settings file needs its script. The old `<version>.settings.json` is rejected with a hint to the new name.

| File | Version | Type | Name |
|------|---------|------|------|
| `001.migration.sql` | 1 | migration | |
| `003.m.orders.sql` | 3 | migration | orders |
| `003.undo.sql` | 3 | undo | |
| `005.breaking.sql` | 5 | breaking | |
| `004.migration.settings.json` | 4 | settings of the migration | |
| `005.b.s.json` | 5 | settings of the breaking script | |
| `20260808104925.create-users.sql` | 20260808104925 | migration | create-users |

Migrations are matched by version and type, not by path. You can move old migrations into a subfolder such as `Migrations/Old/` at any time.

## Code files

Any `*.sql` file under `Code` that is not a test and not a breaking script is a code object: a procedure, function, view, trigger or schema. Organise subfolders as you like (by schema, by object type). A file is executed again whenever its content changes, so it must be safe to run repeatedly: `create or alter` on SQL Server, `create or replace` (or `drop ... if exists` followed by `create`) on PostgreSQL. See [Code Deployment](../deployment/code.md).

`breaking.sql`, `b.sql`, `*.breaking.sql` and `*.b.sql` under `Code` are breaking scripts: they drop old versions of code objects in the breaking phase, on every breaking deployment, so they must be idempotent (`drop ... if exists`). Keep `GetUser.breaking.sql` next to `GetUser.sql`, or one `breaking.sql` per folder. A name that is also a test file name (`test.breaking.sql`, `t.b.sql` with the default patterns) stops the deployment with `BreakingScriptMatchesTestPatternException`. See [Dropping an old procedure version](../deployment/breaking-changes.md#dropping-an-old-procedure-version).

## Test files

- In `Tests`, every `.sql` file except `setup.test.sql` is a test.
- In `Code`, files matching `TestFilePatterns` are tests: `test.*.sql`, `t.*.sql`, `*.test.sql`, `*.t.sql` by default. Keep `GetUser.test.sql` next to `GetUser.sql`.
- `setup.test.sql` runs before each test in the **same** folder (not in subfolders), in `Tests` and in `Code`. It is not a test and is never deployed as code.

See [Unit Testing](../unit-testing/index.md).

## BeforeDeploy and AfterDeploy

These scripts run on every deployment, also when only breaking changes are deployed, so they must be idempotent. The database may still be empty when `BeforeDeploy` runs for the first time. Typical uses: permissions, database options, reference data kept up to date with `merge`.

## Line endings

Line endings do not matter. A Windows checkout (CRLF) and a Linux checkout (LF) of the same file have the same [checksum](../deployment/migrations.md#checksums), so developers on Windows and CI on Linux can deploy to the same database without `.gitattributes`. Scripts run with the line endings they have in the file.
