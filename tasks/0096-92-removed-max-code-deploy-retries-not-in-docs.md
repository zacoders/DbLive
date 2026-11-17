---
type: docs
priority: medium
created: 2026-11-17
---
# The removal of `MaxCodeDeployRetries` is not in the docs

## Problem
0096-77 removed the setting `MaxCodeDeployRetries` (`breaking: true`). DbLive 1.5 has it (verified: `git grep MaxCodeDeployRetries 1.5.0 -- src` finds it in `DbLiveSettings.cs:37`), so a `settings.json` with `"MaxCodeDeployRetries": 5` fails after the upgrade with `JsonException: The JSON property 'MaxCodeDeployRetries' could not be mapped ...`.

The name is nowhere under `docs/` (verified with a search). The table of exceptions in `docs/llms-full.txt` has a row for the removed `DowngradeTimeout` only, so a user or an agent who looks the error up finds nothing. The rule of AGENTS.md was not kept: a change in DbLive behavior updates `docs/llms-full.txt` and the matching page under `docs/` in the same commit.

Found by the code review of 2026-11-17 (finding 6).

## Where
- `docs/llms-full.txt:1079` (§17, the row of `DowngradeTimeout`), `:626` (§9.1, unknown properties are errors)
- `docs/project/configuration.md` ("File format": unknown properties are rejected)

## Fix
Make the row of §17 cover both removed keys: `MaxCodeDeployRetries` (code files are retried in rounds, there is no limit to configure) and `DowngradeTimeout`; the fix is to delete the key. Say the same on the configuration page.

- [ ] One row for "a key removed in 2.0" that lists both, or a row per key? An agent searches the table by the text of the error, so the names of both keys must be in it either way.

0096-90 may change the exception and its message; when it is done first, write the row with the new text.

## Docs to update
- `docs/llms-full.txt` §17, §9.1
- `docs/project/configuration.md` ("File format")

## Done when
- [ ] A search for `MaxCodeDeployRetries` in `docs/` finds the cause and the fix
- [ ] The configuration page names the keys removed in 2.0
