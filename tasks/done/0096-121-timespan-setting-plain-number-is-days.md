---
type: docs
priority: low
created: 2026-11-23
closed: 2026-11-23
---
# A time span in the settings written as a plain number means days

## Release note
None. A correction of `docs/llms-full.txt` and `docs/project/configuration.md`; DbLive behaves as before.

## Problem
`docs/llms-full.txt` §9.1 (line 632) and `docs/project/configuration.md:29` list only `"hh:mm:ss"` and `"d.hh:mm:ss"`. The `TimeSpan` converter of System.Text.Json (format `c`) also accepts `"02:00"` (2 hours) and `"5"` (5 days). Verified by experiment on 2026-11-23: `"MigrationTimeout": "5"` gives `5.00:00:00`, `"02:00"` gives `02:00:00`. A JSON number (`60`) is rejected.

So `"CodeItemTimeout": "60"`, written by someone who means seconds, is 60 days, without an error. The same holds for the `Timeout` of item settings.

## Fix
- [x] Only document it ("a plain number is days: write 30 seconds as `"00:00:30"`"), or reject a time span without `:` with a converter of our own (`InvalidProjectSettingsException`, `InvalidMigrationItemSettingsException`)? Maintainer, 2026-11-23: only document it.

## Docs to update
- `docs/llms-full.txt` §9.1, §9.3, §17 (row `InvalidProjectSettingsException`)
- `docs/project/configuration.md`

## Done when
- [x] Docs updated, or the converter added with unit tests

## Notes
- The mistake needs a value that leaves the documented format and is a number in quotes: the number without quotes, the more likely mistake, already fails with `InvalidProjectSettingsException`. The effect is a timeout that is in practice off; nothing is applied wrong.
- The same parsing is the .NET convention: `TimeSpan.Parse` and the configuration binder of ASP.NET Core also read `"5"` as 5 days.
- Rejected options: a strict converter guards against an operator error, and rejecting `"5"`, which worked, would be a breaking change; a JSON number read as seconds would make `"60"` and `60` mean different things; a warning on a large timeout needs an arbitrary threshold.

## Result
- Docs only, the maintainer's decision of 2026-11-23: DbLive still accepts every form of the .NET `TimeSpan` format `c`.
- `docs/llms-full.txt` §9.1: the shorter forms parse without an error, a plain number is days (`"60"` is 60 days, 30 seconds is `"00:00:30"`), `"02:00"` is 2 hours, a JSON number is an error. §9.3: the `Timeout` row points to §9.1. §17: the row `InvalidProjectSettingsException` no longer calls every `TimeSpan` in another format an error.
- `docs/project/configuration.md`: the same in "File format"; the `Timeout` row of "Item settings" links to it.
- No code changed, no tests.
