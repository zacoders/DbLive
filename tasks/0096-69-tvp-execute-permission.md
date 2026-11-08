---
type: bug
priority: medium
created: 2026-11-09
---
# The table type of self-migration 13 may need EXECUTE for the deployment login (SQL Server)

## Problem
Since 0096-44, `MarkCodeAsVerifiedAsync` and `MarkMissingCodeAsRemovedAsync` pass the paths as a table-valued parameter of the type `dblive.tvp_unique_string_list` (self-migration 13). To use a user-defined table type as a parameter, a login needs the `EXECUTE` permission on the type. The owner of the schema `dblive` and the members of `db_owner` have it. `db_ddladmin`, `db_datareader` and `db_datawriter` do not grant it.

Scenario (not verified): an administrator created the schema `dblive` (owned by `dbo`), and the CI login has `db_ddladmin`, `db_datareader` and `db_datawriter`. Self-migration 13 runs under the CI login, and the type belongs to the owner of the schema. Every deployment then fails in the code phase with "The EXECUTE permission was denied on the object 'tvp_unique_string_list'". DbLive 1.5 deployed with this login.

If it is real, it fails every deployment for such a login, so it blocks the release. Most deployment logins are `db_owner`, which is not affected.

The docs do not say which permissions the deployment login needs.

Found by the code review of 2026-11-09 (finding 5).

## Where
- `src/DbLive.MSSQL/SQL/13.tvp-unique-string-list.sql`
- `src/DbLive.MSSQL/MsSqlDA.cs:505-546` (`ToPathList`, `MarkCodeAsVerifiedAsync`, `MarkMissingCodeAsRemovedAsync`)

## Fix
- [ ] Reproduce first, on SQL Server with a login that has `db_ddladmin`, `db_datareader` and `db_datawriter` and does not own `dblive`.
- [ ] If it is real, which fix?
  - `grant execute on type::dblive.tvp_unique_string_list to public` in the self-migration. The type is only a shape, so the grant exposes nothing. The login that runs the self-migration must be allowed to grant it.
  - No user-defined type: the paths as one `nvarchar(max)` JSON parameter read with `openjson` (compatibility level 130, SQL Server 2016), or a temporary table.
  - Document the permissions the deployment login needs.

2.0 is not released, so self-migration 13 can still be changed in place. Only databases deployed from this branch already have the type. 0096-70 changes the same type: decide both together.

## Docs to update
- `docs/llms-full.txt` §15 (`dblive` schema) or §2, if the permissions of the deployment login are documented
- `docs/deployment/index.md`, the same

## Done when
- [ ] Reproduced or ruled out, with the permissions of the login written in Notes
- [ ] If real: fixed, with an integration test under a login that is not `db_owner`
