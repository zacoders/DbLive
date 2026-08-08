# ProjectId

`ProjectId` is an optional setting in `settings.json` that binds a DbLive project to a target database. It reduces the risk of deploying to the wrong environment by blocking deployment when the configured value does not match the value stored in the database.

## Format

- Unicode string, maximum **128** characters
- Case insensitive
- Example: `"ProjectId": "my-app-prod"`

Choose a value that uniquely identifies your project and environment, for example `billing-api-staging` or `inventory-prod`.

## Configuration

Add `ProjectId` to `settings.json` next to your scripts:

```json
{
  "ProjectId": "my-app-prod",
  "TransactionWrapLevel": "migration"
}
```

## Behavior

### ProjectId not set

If `ProjectId` is absent, null, or whitespace, validation is skipped entirely. Existing projects continue to work without changes.

### First deploy with ProjectId set

When `ProjectId` is configured and `dblive.version.project_id` is null in the target database, DbLive persists the settings value before any other project deploy steps.

### Subsequent deploys

On every deploy, DbLive compares the settings value with `dblive.version.project_id`. If they differ, deployment is blocked with a `ProjectIdMismatchException`.

## Storage

The value is stored in the `project_id` column on the singleton `dblive.version` table. The column is added automatically during DbLive self-deploy (internal migration). Values are normalized to lowercase on write; comparison is case insensitive.

## See also

- [Philosophy — Zero-Downtime Database Delivery](philosophy.md)
- [Issue #71](https://github.com/zacoders/DbLive/issues/71)
