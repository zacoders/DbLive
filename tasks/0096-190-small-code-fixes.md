---
type: chore
priority: low
created: 2026-11-26
---
# Small code fixes from the review of 2026-11-26

## Problem
1. **SQL Server: the first `kill` that fails stops the other kills and the wait.** `KillSessionsAsync` (`src/DbLive.MSSQL/MsSqlDA.cs:878-914`, new in 0096-183) runs the query, the loop of `kill` and the wait in one `try` with `catch (SqlException)`. Msg 6106 (the session ended between the query and its `kill`), which the comment of the catch names, is what happens to the session of a pooled connection closed just before the drop. The sessions after it are not killed, the wait is skipped, and `single_user with rollback immediate` ends them in about 3 seconds, the cost the method exists to avoid. Read in the code, not run.
2. **`row-count=N` takes a negative number.** `AssertTypeExtensions.TryParseAssertInfo` (`src/DbLive/Deployers/Testing/AssertTypeExtensions.cs:50-53`) uses `int.TryParse(parts[1], out int rowCount)`, `NumberStyles.Integer` with the current culture: `row-count=-1` and `row-count=+3` are assertions. A negative count is never met, so the test fails with `Expected -1 row(s), but 0 received.` instead of `Please use row-count=[Number] to assert row count. ...`. The typo guard (`UnitTestResultChecker.cs:140`) uses the same parser.
3. **A very large `UnitTestItemTimeout` crashes the test run.** `UnitTestItemRunner.RunTestAsync` computes `projectSettings.UnitTestItemTimeout * 2` before its `try` (`src/DbLive/Deployers/Testing/UnitTestItemRunner.cs:34-38`), and `TimeSpanSettingConverter` takes any time span up to `TimeSpan.MaxValue`. Above about 5,337,599 days (`"10675199.00:00:00"`, meant as no limit) every test throws `OverflowException`, and `RunAllTestsAsync` fails with it from `Parallel.ForEachAsync` instead of `UnitTestsFailedException`. `"00:00:00"` is the way to say no limit (§9.1).

## Fix
1. Catch `SqlException` 6106 around each `kill` and go on; keep the outer catch for the errors of permissions (6102, 300).
2. `int.TryParse(parts[1], NumberStyles.AllowLeadingWhite | NumberStyles.AllowTrailingWhite, CultureInfo.InvariantCulture, out int rowCount)`: digits only. Not `NumberStyles.None`: spaces around `=` are documented (§8.3 line 556: `Row-Count = 3` works).
3. - [ ] Cap twice the timeout at `TimeSpan.MaxValue`, or reject at the read of `settings.json` a time span too large to double?

## Docs to update
- `docs/llms-full.txt` §8.3 (line 556: `row-count=-1` fails like `row-count=abc`) for 2
- `docs/llms-full.txt` §9.2 (row `UnitTestItemTimeout`) for 3, if a large value is rejected

## Done when
- [ ] 1–3 fixed, with unit tests where behavior changes
- [ ] Docs updated

## Notes
- Found by the code review of 2026-11-26 (the branch as release 2.0).
