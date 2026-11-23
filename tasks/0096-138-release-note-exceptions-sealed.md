---
type: docs
priority: low
created: 2026-11-23
---
# A release note of 2.0 says every exception is sealed; `DbLiveSqlException` is not

## Problem
The release note of `tasks/done/0096-73-public-types-outside-the-api.md` (line 18) says: "The exceptions, `DeployParameters`, `TestRunResult` and `SqlFactAttribute` are sealed." 0096-84 made `DbLiveSqlException` not sealed, so that `ExecutionTimeoutException` derives from it (`src/DbLive/Adapter/DbLiveSqlException.cs:10`, constructors internal), and did not correct the note. 0096-91 did not catch it.

AGENTS.md ("Close", step 2): the notes of a release describe the change from the previous release.

## Fix
In the note of 0096-73: "The exceptions except `DbLiveSqlException` (the base of `ExecutionTimeoutException`, with internal constructors), `DeployParameters`, `TestRunResult` and `SqlFactAttribute` are sealed."

## Done when
- [ ] Note corrected, checked in the preview (`dotnet run .github/scripts/release-notes.cs`)
