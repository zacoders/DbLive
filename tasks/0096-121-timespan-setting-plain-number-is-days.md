---
type: docs
priority: low
created: 2026-11-23
---
# A time span in the settings written as a plain number means days

## Problem
`docs/llms-full.txt` §9.1 (line 632) and `docs/project/configuration.md:29` list only `"hh:mm:ss"` and `"d.hh:mm:ss"`. The `TimeSpan` converter of System.Text.Json (format `c`) also accepts `"02:00"` (2 hours) and `"5"` (5 days). Verified by experiment on 2026-11-23: `"MigrationTimeout": "5"` gives `5.00:00:00`, `"02:00"` gives `02:00:00`. A JSON number (`60`) is rejected.

So `"CodeItemTimeout": "60"`, written by someone who means seconds, is 60 days, without an error. The same holds for the `Timeout` of item settings.

## Fix
- [ ] Only document it ("a plain number is days: write 30 seconds as `"00:00:30"`"), or reject a time span without `:` with a converter of our own (`InvalidProjectSettingsException`, `InvalidMigrationItemSettingsException`)?

## Docs to update
- `docs/llms-full.txt` §9.1, §9.3
- `docs/project/configuration.md`

## Done when
- [ ] Docs updated, or the converter added with unit tests
