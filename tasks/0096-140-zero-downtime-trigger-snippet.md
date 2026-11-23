---
type: docs
priority: low
created: 2026-11-23
---
# `zero-downtime.md`: the trigger of step 1 is not valid T-SQL

## Problem
`docs/deployment/zero-downtime.md:41-43`:

```sql
as
begin
    -- keep dbo.PersonPhones in sync with dbo.Person.PhoneNumber
end
```

A T-SQL `BEGIN ... END` block must hold at least one statement: with only a comment SQL Server reports "Incorrect syntax near 'end'". It is a placeholder, but a reader who copies it gets an error. The demo has the real trigger (`src/Demo/DemoMSSQL/Scripts/Migrations/007.migration.sql`).

The maintainer writes this page by hand.

## Fix
- [ ] Put the body of the demo trigger there, or `set nocount on;` before the comment?

## Docs to update
- `docs/deployment/zero-downtime.md`

## Done when
- [ ] Page updated by the maintainer or with the maintainer's agreement
