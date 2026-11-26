---
type: chore
priority: medium
created: 2026-11-17
closed: 2026-11-17
---
# Task numbers above 99

## Release note
None. Only the script that builds the release notes of this repository.

## Problem
The tasks found during #96 reached 0096-99. The next number did not fit: `ClosedTask.Id` of the release notes script matched two digits per part (`^\d{4}(-\d{2})*`), so it read `0096-100-...` as `0096-10`. The tasks were also sorted by that string, which puts `0096-100` before `0096-11` even with the right number. AGENTS.md and tasks/README.md asked for a "two-digit number".

Found while creating the tasks of the second code review of 2026-11-17.

## Where
- `.github/scripts/release-notes/ClosedTask.cs:10` (`Id`), `:31` (the sort)
- `AGENTS.md` ("Numbers"), `tasks/README.md` ("File name and number")

## Done when
- [x] `Id` reads a part of two digits or more, and the tasks are sorted by number
- [x] AGENTS.md and tasks/README.md say what comes after 99

## Result
- `Id`: `^\d{4}(-\d{2,})*`. The sort pads each part of the number to four digits, so 0096-99 comes before 0096-100. Checked with a scratch script on sample names: `0096-documentation`, `0096-04`, `0096-04-01`, `0096-10`, `0096-11`, `0096-99`, `0096-100`, `0096-101`, `0099-02` come in this order, each with the right number. The preview of the notes of the branch is the same as before the change.
- Numbers stay two digits up to 99, so no file is renamed. `ls` lists `0096-100` between `0096-10` and `0096-11`; tasks/README.md says so.
