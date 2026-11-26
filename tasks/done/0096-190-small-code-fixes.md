---
type: chore
priority: low
created: 2026-11-26
closed: 2026-11-26
breaking: true
---
# Small code fixes from the review of 2026-11-26

## Release note
**`row-count=N` takes digits only.** A sign before the number is an error now: `row-count=+3` passed as `row-count=3` and now fails with `Please use row-count=[Number] to assert row count. Example: row-count=10.`, and `row-count=-1` fails with that message instead of `Expected -1 row(s), but 0 received.` Spaces around `=` and around the number are still ignored (`Row-Count = 3`).

To upgrade: write `row-count=3` instead of `row-count=+3`.

## Problem
1. **SQL Server: the first `kill` that fails stops the other kills and the wait.** `KillSessionsAsync` (`src/DbLive.MSSQL/MsSqlDA.cs:878-914`, new in 0096-183) runs the query, the loop of `kill` and the wait in one `try` with `catch (SqlException)`. Msg 6106 (the session ended between the query and its `kill`), which the comment of the catch names, is what happens to the session of a pooled connection closed just before the drop. The sessions after it are not killed, the wait is skipped, and `single_user with rollback immediate` ends them in about 3 seconds, the cost the method exists to avoid. Read in the code, not run.
2. **`row-count=N` takes a negative number.** `AssertTypeExtensions.TryParseAssertInfo` (`src/DbLive/Deployers/Testing/AssertTypeExtensions.cs:50-53`) uses `int.TryParse(parts[1], out int rowCount)`, `NumberStyles.Integer` with the current culture: `row-count=-1` and `row-count=+3` are assertions. A negative count is never met, so the test fails with `Expected -1 row(s), but 0 received.` instead of `Please use row-count=[Number] to assert row count. ...`. The typo guard (`UnitTestResultChecker.cs:140`) uses the same parser.
3. **A very large `UnitTestItemTimeout` crashes the test run.** `UnitTestItemRunner.RunTestAsync` computes `projectSettings.UnitTestItemTimeout * 2` before its `try` (`src/DbLive/Deployers/Testing/UnitTestItemRunner.cs:34-38`), and `TimeSpanSettingConverter` takes any time span up to `TimeSpan.MaxValue`. Above about 5,337,599 days (`"10675199.00:00:00"`, meant as no limit) every test throws `OverflowException`, and `RunAllTestsAsync` fails with it from `Parallel.ForEachAsync` instead of `UnitTestsFailedException`. `"00:00:00"` is the way to say no limit (§9.1).

## Fix
1. Catch `SqlException` 6106 around each `kill` and go on; keep the outer catch for the errors of permissions (6102, 300).
2. `int.TryParse(parts[1], NumberStyles.AllowLeadingWhite | NumberStyles.AllowTrailingWhite, CultureInfo.InvariantCulture, out int rowCount)`: digits only. Not `NumberStyles.None`: spaces around `=` are documented (§8.3 line 556: `Row-Count = 3` works).
3. - [x] Cap twice the timeout at `TimeSpan.MaxValue`, or reject at the read of `settings.json` a time span too large to double? Cap (2026-11-26; the maintainer left the choice to the agent with "if the fixes are simple, just fix"). The cap is local to the one place that doubles; a rejection would be a limit of one key that no other key has, and an error for a value that means "no limit" and works for every other timeout.

## Docs to update
- [x] `docs/llms-full.txt` §8.3 (line 556: `row-count=-1` fails like `row-count=abc`) for 2
- [x] `docs/unit-testing/index.md` ("Assertions", the row of `row-count=N`) for 2
- `docs/llms-full.txt` §9.2 (row `UnitTestItemTimeout`) for 3: not needed, no value is rejected.

## Done when
- [x] 1–3 fixed, with unit tests where behavior changes
- [x] Docs updated

## Notes
- Found by the code review of 2026-11-26 (the branch as release 2.0).
- 1: `kill` of a session that ended is Msg 6106, `Process ID 90 is not an active process ID.` (verified on the SQL Server container of `DbLive.MSSQL.Tests`: the `@@spid` of an unpooled connection, closed, then killed from another one). The race itself (a session that ends between the query and its `kill`) cannot be made to happen in a test; `MsSqlDADropDbTests` pass.
- 2: 1.5.0 parsed the number with `Convert.ToInt32` (current culture, a sign allowed), so `row-count=+3` passed there too: `breaking: true` for a project that writes it.
- 2: the typo guard (`ThrowIfLooksLikeMisnamedAssert`) uses the same parser, so `select 'row-count=-1' as asstert` is no longer caught as a misnamed assert, the same as `row-count=abc` before.
- 3: `TimeSpan * 2` goes through `double` (`TimeSpan.op_Multiply(TimeSpan, double)`, verified: `OverflowException` from `IntervalFromDoubleTicks`), which rounds near `long.MaxValue` ticks; the doubling is in integer ticks instead.
- 1 and 3 refine behavior new in this release (0096-183 and the doubled timeout of the test transaction): no release note of their own. The note of 0096-183 (the drop about 3 seconds faster) holds better now; no other note of the release names `row-count` parsing in a way this changes (searched the preview for "row-count", "Please use", "UnitTestItemTimeout", "kill").

## Result
- `MsSqlDA.KillSessionsAsync`: each `kill` catches Msg 6106 (`SessionNotActiveErrorNumber`) and goes on to the next session and the wait; the outer catch stays for the errors of permissions.
- `AssertTypeExtensions.TryParseAssertInfo`: the count is parsed with `NumberStyles.AllowLeadingWhite | NumberStyles.AllowTrailingWhite` and the invariant culture, digits only.
- `UnitTestItemRunner.Twice`: twice `UnitTestItemTimeout` in integer ticks, capped at `TimeSpan.MaxValue`; `00:00:00` stays no limit.
- Tests: `AssertValue_RowCountWithoutNumber_Throws` with `row-count=-1` and `row-count=+3`; `RunTest_TimeoutTooLargeToDouble_TransactionGetsTheLargestTimeout` (failed before the fix with `OverflowException`).
