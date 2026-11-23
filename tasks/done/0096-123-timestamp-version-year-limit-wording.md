---
type: docs
priority: low
created: 2026-11-23
closed: 2026-11-23
---
# `configuration.md` gets the year limit of `EnforceTimestampVersion` wrong

## Release note
None. A correction of `docs/project/configuration.md`; DbLive behaves as before.

## Problem
`docs/project/configuration.md:47`: "year from 2000 up to next month". The code checks only the year: `int maxYear = timeProvider.UtcNow().AddMonths(1).Year;` (`src/DbLive/Project/MigrationVersionValidator.cs:35`). On 2026-11-23 any version up to `20261231235959` passes; in December the next year passes too. `docs/llms-full.txt` §5.1 (line 272) is right: "year from 2000 to next month's year".

## Fix
"the year is from 2000 to the year of next month".

## Docs to update
- `docs/project/configuration.md:47`

## Done when
- [x] Docs updated

## Notes
The "14 digits" are checked on the parsed `long` (`MigrationVersionValidator.cs:10`), so `020260808104925.migration.sql` (15 characters, a leading zero) passes. Harmless: `001` and `1` are the same version anyway.

## Result
`docs/project/configuration.md`, the row of `EnforceTimestampVersion`: "the year is from 2000 to the year of next month", as the code checks and as `docs/llms-full.txt` §5.1 says. No other page states the limit. No code change, no tests.
