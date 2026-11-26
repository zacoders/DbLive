---
type: docs
priority: low
created: 2026-11-23
closed: 2026-11-23
---
# A release note of 2.0 says every exception is sealed; `DbLiveSqlException` is not

## Release note
None. A correction of the release note of 0096-73.

## Problem
The release note of `tasks/done/0096-73-public-types-outside-the-api.md` (line 18) says: "The exceptions, `DeployParameters`, `TestRunResult` and `SqlFactAttribute` are sealed." 0096-84 made `DbLiveSqlException` not sealed, so that `ExecutionTimeoutException` derives from it (`src/DbLive/Adapter/DbLiveSqlException.cs:10`, constructors internal), and did not correct the note. 0096-91 did not catch it.

AGENTS.md ("Close", step 2): the notes of a release describe the change from the previous release.

## Fix
In the note of 0096-73: "The exceptions except `DbLiveSqlException` (the base of `ExecutionTimeoutException`, with internal constructors), `DeployParameters`, `TestRunResult` and `SqlFactAttribute` are sealed."

## Done when
- [x] Note corrected, checked in the preview (`dotnet run .github/scripts/release-notes.cs`)

## Notes
- `DbLiveSqlException` is the only public class of `src` (outside the tests) that derives from `Exception` and is not sealed; `DbLiveTestFixture` is the other public class that is not sealed, and the note does not name it (checked with grep on 2026-11-23). The release note of 0096-84 says nothing about `sealed`.

## Result
The release note of `tasks/done/0096-73-public-types-outside-the-api.md`: "The exceptions except `DbLiveSqlException` (the base of `ExecutionTimeoutException`, with internal constructors), `DeployParameters`, `TestRunResult` and `SqlFactAttribute` are sealed." Checked in the preview of `dotnet run .github/scripts/release-notes.cs`. No code change, no tests.
