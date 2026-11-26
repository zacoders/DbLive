---
type: bug
priority: high
created: 2026-11-24
closed: 2026-11-24
breaking: true
---
# A time span in the settings parses on .NET 9 and later, and fails on .NET 8

## Release note
**A time span in the settings is `"hh:mm:ss"` or `"d.hh:mm:ss"`, the same on every runtime.** DbLive read the time spans of `settings.json` and of the item settings files with the converter of the .NET runtime: on .NET 9 and later `"60"` was 60 days and `"02:00"` 2 hours, on .NET 8 both failed the load of the project. So a project could pass its tests on .NET 10 and fail its deployment on .NET 8. Now a shorter form (`"60"`, `"02:00"`, `"1.02:03"`) and a negative value fail on every runtime with `InvalidProjectSettingsException` or `InvalidMigrationItemSettingsException`, before anything runs, and the message names the key: `The value of 'CodeItemTimeout' must be a time span "hh:mm:ss" or "d.hh:mm:ss", such as "00:00:30" or "1.00:00:00". Path: $.CodeItemTimeout.` A fraction of a second (`"00:00:00.5"`) still works.

To upgrade: write such a value in full, `"02:00:00"` for 2 hours, `"60.00:00:00"` for 60 days.

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

- [x] Which forms does it accept? (a) Every form of the .NET format `c` (`TimeSpan.ParseExact(value, "c", CultureInfo.InvariantCulture)`): .NET 8 behaves as .NET 9 and later, and the docs stay as they are. (b) Only `[-][d.]hh:mm:ss[.fffffff]`, with an error that names the key and the expected form: `"60"` (60 days, not 60 seconds) and `"02:00"` become errors on every framework, which removes the trap that 0096-121 documented. There are no users yet, so (b) breaks nobody; on .NET 8 these values fail already. Maintainer, 2026-11-24: (b); a value whose meaning is unclear is not needed.
- [x] A negative value (`"-00:00:30"`) is accepted now and fails only when the timeout is used. Reject it in the converter too? Maintainer, 2026-11-24: yes, a negative timeout makes no sense.

## Docs to update
- `docs/llms-full.txt` §9.1 (the `TimeSpan` format, the errors of DbLive), §9.3 (`Timeout`, line 702), §5 and §17 (the rows of `InvalidProjectSettingsException` and `InvalidMigrationItemSettingsException`)
- `docs/project/configuration.md:29`, the errors of the item settings
- The XML doc of `InvalidMigrationItemSettingsException`

## Done when
- [x] Fixed: the same values load or fail on `net8.0` and `net10.0`, with unit tests that run on both (the test project targets `net8.0` only: test the converter on its own, or add a target)
- [x] Docs updated

## Notes
- Found by the code review of 2026-11-24.
- Verified again on 2026-11-24 (.NET 8.0.34 and 10.0.15, System.Text.Json of the framework): the table above, and `"1.02:03"` (`JsonException` on .NET 8, 1.02:03:00 on .NET 10); `"-00:00:30"` loads on both.
- Before the fix, a negative timeout failed only where it was used, possibly in the middle of a deployment: `TransactionScope` throws `ArgumentOutOfRangeException`, `ScriptTimeout` throws `ExecutionTimeoutException` before the first command (read in the code).
- The rule of the converter: the format `c` with both colons. `TimeSpan.TryParseExact(value, "c", CultureInfo.InvariantCulture)` checks the ranges (hours up to 23, minutes and seconds up to 59, at most 7 digits of a fraction, at most `TimeSpan.MaxValue`); two colons exclude its short forms `d`, `hh:mm` and `d.hh:mm`. Verified on 2026-11-24 for 19 values: `TryParseExact` gives the same result on .NET 8.0.34 and 10.0.15. It also takes `" 00:00:30 "`, `"1:0:0"` and `"0001:00:00"`: their meaning is clear, so the converter keeps them. `"-00:00:00"` is zero, no limit, and is accepted.
- A regular expression for the shape was not needed: `TryParseExact` and the count of colons say the same.
- The key in the message: System.Text.Json sets `JsonException.Path` when an exception without a path leaves a converter, and does not change its message. The exception of the converter builds its message from `Path`, so it reads as the errors of `null` of 0096-119: `The value of '<key>' ... Path: $.<key>.`.
- `null`: a `TimeSpan` key of `settings.json` gets it in the converter, `cannot be null` (before: the error of System.Text.Json, also an error). The `TimeSpan?` `Timeout` of the item settings never passes `null` to the converter (System.Text.Json sets the property to `null` itself); 0096-163 makes `null` an error there.
- `Write` throws `NotSupportedException`: DbLive only reads the settings.

## Result
- `TimeSpanSettingConverter` (`src/DbLive/Common/Settings/TimeSpanSettingConverter.cs`), registered in `SettingsTools.JsonSerializerOptions`: every `TimeSpan` of `settings.json` and of the item settings files is `"hh:mm:ss"` or `"d.hh:mm:ss"` with an optional fraction, not negative, on every target framework. Another form, a JSON number, a negative value and `null` fail with a `JsonException` that names the key; the readers wrap it in `InvalidProjectSettingsException` or `InvalidMigrationItemSettingsException` as before. Decisions of the maintainer of 2026-11-24: the strict form (b), and no negative values. `breaking: true`: on .NET 9 and later `"02:00"` and `"60"` loaded in 1.5.
- Tests: `TimeSpanSettingConverterTests` (the long forms, the short forms and other wrong values, negative values, `null` of a key of `settings.json`, the message of an item settings file), and a case `"CodeItemTimeout": "02:00"` in `SettingsTests.LoadSettings_InvalidFile_ErrorNamesFile`. Without the converter 17 of them fail on .NET 8 (verified, with the test of `null` in the item settings that 0096-163 replaced). The net8.0 test assembly also ran on .NET 10.0.15 (`DOTNET_ROLL_FORWARD=LatestMajor`): all pass with the converter; without it `"60"` loads there ("No exception was thrown", verified), which shows that the run was on .NET 10. All 665 tests of `DbLive.Tests` pass; `DbLive` builds without warnings on net8.0, net9.0 and net10.0.
- Docs: `docs/llms-full.txt` §9.1, §9.3, §5, §17 and `docs/project/configuration.md` describe the strict format, without the short forms that 0096-121 documented.
