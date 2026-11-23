---
type: chore
priority: low
created: 2026-11-23
---
# `DbLiveTestingMSSQL.cs` is commented out as a whole

## Problem
`src/DbLive.MSSQL/DbLiveTestingMSSQL.cs` has no line of code: every line is a comment (`//public class DbLiveTestingMSSQL : DbLiveTesting ...`). The type it refers to does not exist any more. It is the only such file in the library projects.

## Fix
Delete the file.

## Done when
- [ ] File deleted, the solution builds
