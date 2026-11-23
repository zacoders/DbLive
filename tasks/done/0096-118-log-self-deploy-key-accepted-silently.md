---
type: bug
priority: low
created: 2026-11-23
closed: 2026-11-23
breaking: true
---
# `settings.json` accepts the key `LogSelfDeploy` and ignores it

## Release note
**`LogSelfDeploy` in `settings.json` is an error.** The key was never documented, and DbLive ignored its value: the self-deployment logged nothing either way. A `settings.json` with `LogSelfDeploy` now fails with `InvalidProjectSettingsException`, like any other unknown key.

To upgrade: remove `LogSelfDeploy` from `settings.json`.

## Problem
`docs/llms-full.txt` §9.1 (line 635): "Unknown properties are errors". §9.2 does not list `LogSelfDeploy`.

`DbLiveSettings` declares `public bool LogSelfDeploy { get; internal set; }` (`src/DbLive/Common/Settings/DbLiveSettings.cs:37`). System.Text.Json takes a property with a non-public setter as a known read-only member, so `JsonUnmappedMemberHandling.Disallow` does not reject the key, and the value is skipped. Verified on 2026-11-23 with the options of `SettingsTools`: `{"LogSelfDeploy": true}` gives no error and `false`; `{"Foo": true}` gives "could not be mapped". `[JsonIgnore]` would not help: an ignored property is accepted silently too (verified).

Only unit tests set the property, so `DbLiveSelfDeployer` (`:27`, `:70`) never logs "Starting self deploy." and "Self deploy completed." in a real deployment.

## Fix
Make the whole property `internal` (`DbLiveSettings` is internal, so nothing public changes; the tests use `InternalsVisibleTo`). The key then fails as unknown. Or remove the property and log the two lines at `Debug`.

## Done when
- [x] Fixed, with a unit test: `{"LogSelfDeploy": true}` fails with `InvalidProjectSettingsException`

## Notes
- DbLive 1.5.0 had the same `{ get; internal set; }`, so the key did nothing there either: a `settings.json` that has it deployed before and fails now, hence `breaking: true`.

## Result
- `DbLiveSettings.LogSelfDeploy` is `internal` as a whole, the first option of the fix. System.Text.Json maps only public properties, so the key fails as unknown: `The JSON property 'LogSelfDeploy' could not be mapped to any .NET member ...`. The tests of `DbLiveSelfDeployer` set it as before.
- Test: a case of `SettingsTests.LoadSettings_InvalidFile_ErrorNamesFile` with `{ "LogSelfDeploy": true }`. It failed before the change ("No exception was thrown", verified) and passes after; all 558 tests of `DbLive.Tests` pass.
- Docs: none. §9.2 never listed the key, and "Unknown properties are errors" in §9.1 now holds for it too.
