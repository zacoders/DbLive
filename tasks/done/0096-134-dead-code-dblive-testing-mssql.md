---
type: chore
priority: low
created: 2026-11-23
closed: 2026-11-23
---
# `DbLiveTestingMSSQL.cs` is commented out as a whole

## Release note
None. A source file of `DbLive.MSSQL` that held only comments is deleted; the package does not change.

## Problem
`src/DbLive.MSSQL/DbLiveTestingMSSQL.cs` has no line of code: every line is a comment (`//public class DbLiveTestingMSSQL : DbLiveTesting ...`). The type it refers to does not exist any more. It is the only such file in the library projects.

## Fix
Delete the file.

## Done when
- [x] File deleted, the solution builds

## Result
- `src/DbLive.MSSQL/DbLiveTestingMSSQL.cs` deleted. Nothing referred to it: `MyDbLiveTestingMSSQLFixture` of the demos is another type.
- `dotnet build src/DbLive.sln -c Release`: no errors, no warnings.
