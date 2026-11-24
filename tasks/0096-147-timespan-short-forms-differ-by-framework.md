---
type: bug
priority: high
created: 2026-11-24
---
# A time span in the settings parses on .NET 9 and later, and fails on .NET 8

## Problem
`docs/llms-full.txt` §9.1 (line 644) says that the shorter forms of the .NET `TimeSpan` format parse "without an error or a warning": `"60"` is 60 days, `"02:00"` is 2 hours. That holds on .NET 9 and later only. DbLive reads `settings.json` and the item settings files with the `TimeSpan` converter of System.Text.Json, the one of the framework: no DbLive project references the System.Text.Json package. The converter of .NET 8 accepts only `[-][d.]hh:mm:ss[.fffffff]` and rejects every shorter value.

Verified on 2026-11-24 (System.Text.Json of the framework, a record with a `TimeSpan` property):

| Value | .NET 8.0.34 | .NET 10.0.15 |
|---|---|---|
| `"00:00:30"` | 00:00:30 | 00:00:30 |
| `"1.00:00:00"` | 1.00:00:00 | 1.00:00:00 |
| `"02:00"` | `JsonException` | 02:00:00 |
| `"60"` | `JsonException` | 60.00:00:00 |
| `"1:00:00"` | `JsonException` | 01:00:00 |

The review also deployed with the `DbLive.MSSQL` of the branch on both runtimes: on .NET 8 `{"CodeItemTimeout": "02:00"}` fails with `InvalidProjectSettingsException`, an item settings file `{"Timeout": "01:30"}` with `InvalidMigrationItemSettingsException`; on .NET 10 both load.

So one project behaves differently by the runtime of the process: a test project on `net10.0` passes `[SqlDeployFact]`, and a deployer on `net8.0` fails when it loads the project (before it touches the server). DbLive targets `net8.0`, `net9.0` and `net10.0` (§2).

0096-121 documented the short forms after an experiment on .NET 9 or later, and the maintainer decided on 2026-11-23 to only document them. That decision did not know about .NET 8. `DbLive.Tests` targets `net8.0` only and has no test of these forms.

## Where
- `src/DbLive/Common/Settings/SettingsTools.cs:6-12`: `JsonSerializerOptions` without a `TimeSpan` converter of its own
- `src/DbLive/Common/Settings/DbLiveSettings.cs`, `MigrationItemSettings.cs`: the `TimeSpan` properties

## Fix
A `TimeSpan` converter of DbLive in `SettingsTools.JsonSerializerOptions`, the same on every target framework.

- [ ] Which forms does it accept? (a) Every form of the .NET format `c` (`TimeSpan.ParseExact(value, "c", CultureInfo.InvariantCulture)`): .NET 8 behaves as .NET 9 and later, and the docs stay as they are. (b) Only `[-][d.]hh:mm:ss[.fffffff]`, with an error that names the key and the expected form: `"60"` (60 days, not 60 seconds) and `"02:00"` become errors on every framework, which removes the trap that 0096-121 documented. There are no users yet, so (b) breaks nobody; on .NET 8 these values fail already.
- [ ] A negative value (`"-00:00:30"`) is accepted now and fails only when the timeout is used. Reject it in the converter too?

## Docs to update
- `docs/llms-full.txt` §9.1 (the `TimeSpan` format), §9.3 (`Timeout`, line 702), §17 (the row of `InvalidProjectSettingsException`)
- `docs/project/configuration.md:29`

## Done when
- [ ] Fixed: the same values load or fail on `net8.0` and `net10.0`, with unit tests that run on both (the test project targets `net8.0` only: test the converter on its own, or add a target)
- [ ] Docs updated

## Notes
- Found by the code review of 2026-11-24.
