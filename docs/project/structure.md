# Project Structure

All scripts live under the `Scripts` folder of the SQL project. Folder names can be changed in [`settings.json`](configuration.md); the defaults are shown here.

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
    20260815120000.settings.json
  Code/                                optional
    schemas/sales.sql
    dbo/GetUser.sql
    dbo/GetUser.test.sql
  Tests/                               optional
    Orders/init.sql
    Orders/select-orders.test.sql
  BeforeDeploy/                        optional
  AfterDeploy/                         optional
```

## Folders

| Folder | Required | Picked up | Runs |
|--------|----------|-----------|------|
| `Migrations` | no | `*.sql` and `*.json`, including subfolders | Once per database, in version order |
| `Code` | no | `*.sql` (except test files), including subfolders | When the file is new or changed |
| `Tests` | no | every `*.sql` except `init.sql`, including subfolders | At the end of a deployment, or from xUnit |
| `BeforeDeploy` | no | `*.sql`, including subfolders, alphabetical by path | On **every** deployment, before migrations |
| `AfterDeploy` | no | `*.sql`, including subfolders, alphabetical by path | On **every** deployment, after breaking changes |

Every folder is optional; a missing folder is skipped. A project without `Migrations` deploys only code, tests and the BeforeDeploy/AfterDeploy scripts.

Files with other extensions (`.txt`, `.md`) are ignored, so notes can sit next to scripts. The one exception is a `.json` file in `Migrations` that is not a migration settings file: it stops the deployment.

!!! warning "Ship applied migrations with every build"
    Once a database has applied migrations, every build you deploy to it must contain them. If they are missing, for example because the `Migrations` folder was not copied to the output, DbLive treats this as a [downgrade](../deployment/migrations.md#downgrade) and the deployment fails with `DowngradeNotAllowedException`.

## Migration files

```
<version>.<type>.<name>.sql
<version>.<name>.sql                 (no type: a migration)
<version>.settings.json
```

**Version.** The first part of the name, a whole number (64-bit). `001`, `1` and `20260808104925` are valid; `001` and `1` are the same version. For teams, use UTC timestamps (`yyyyMMddHHmmss`) so that branches do not collide, and set `"EnforceTimestampVersion": true` to reject anything else.

**Type.** Case-insensitive, with a short form:

| Type | Short | Extension | Meaning |
|------|-------|-----------|---------|
| `migration` | `m` | `.sql` | The forward change. Every version must have one. |
| `undo` | `u` | `.sql` | Reverts this version. Used by downgrades and undo tests. |
| `breaking` | `b` | `.sql` | Destructive part, run only in a breaking deployment. |
| `settings` | `s` | `.json` | [Per-migration settings](configuration.md#per-migration-settings). |

**Name.** Free text, for humans only. If the second part of the file name is not a type, the file is a migration and that part is its name: `20260808104925.create-users.sql` is a migration named `create-users`.

| File | Version | Type | Name |
|------|---------|------|------|
| `001.migration.sql` | 1 | migration | |
| `003.m.orders.sql` | 3 | migration | orders |
| `003.undo.sql` | 3 | undo | |
| `005.breaking.sql` | 5 | breaking | |
| `004.settings.json` | 4 | settings | |
| `20260808104925.create-users.sql` | 20260808104925 | migration | create-users |

Migrations are matched by version and type, not by path. You can move old migrations into a subfolder such as `Migrations/Old/` at any time.

## Code files

Any `*.sql` file under `Code` that is not a test is a code object: a procedure, function, view, trigger or schema. Organise subfolders as you like (by schema, by object type). A file is executed again whenever its content changes, so it must be safe to run repeatedly: `create or alter` on SQL Server, `create or replace` (or `drop ... if exists` followed by `create`) on PostgreSQL. See [Code Deployment](../deployment/code.md).

## Test files

- In `Tests`, every `.sql` file except `init.sql` is a test.
- In `Code`, files matching `TestFilePatterns` are tests: `test.*.sql`, `t.*.sql`, `*.test.sql`, `*.t.sql` by default. Keep `GetUser.test.sql` next to `GetUser.sql`.
- `init.sql` runs before each test in the **same** folder (not in subfolders). Put `init.sql` in `Tests` folders only: inside `Code` it would also be deployed as a code object.

See [Unit Testing](../unit-testing/index.md).

## BeforeDeploy and AfterDeploy

These scripts run on every deployment, also when only breaking changes are deployed, so they must be idempotent. The database may still be empty when `BeforeDeploy` runs for the first time. Typical uses: permissions, database options, reference data kept up to date with `merge`.

## Line endings

DbLive checksums applied migrations byte for byte. If Windows machines check out `.sql` files with CRLF and CI uses LF, the same migration has two different checksums. Pin line endings in `.gitattributes`:

```
*.sql text eol=lf
```
