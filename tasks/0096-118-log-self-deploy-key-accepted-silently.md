---
type: bug
priority: low
created: 2026-11-23
---
# `settings.json` accepts the key `LogSelfDeploy` and ignores it

## Problem
`docs/llms-full.txt` §9.1 (line 628): "Unknown properties are errors". §9.2 does not list `LogSelfDeploy`.

`DbLiveSettings` declares `public bool LogSelfDeploy { get; internal set; }` (`src/DbLive/Common/Settings/DbLiveSettings.cs:37`). System.Text.Json takes a property with a non-public setter as a known read-only member, so `JsonUnmappedMemberHandling.Disallow` does not reject the key, and the value is skipped. Verified on 2026-11-23 with the options of `SettingsTools`: `{"LogSelfDeploy": true}` gives no error and `false`; `{"Foo": true}` gives "could not be mapped". `[JsonIgnore]` would not help: an ignored property is accepted silently too (verified).

Only unit tests set the property, so `DbLiveSelfDeployer` (`:27`, `:70`) never logs "Starting self deploy." and "Self deploy completed." in a real deployment.

## Fix
Make the whole property `internal` (`DbLiveSettings` is internal, so nothing public changes; the tests use `InternalsVisibleTo`). The key then fails as unknown. Or remove the property and log the two lines at `Debug`.

## Done when
- [ ] Fixed, with a unit test: `{"LogSelfDeploy": true}` fails with `InvalidProjectSettingsException`
