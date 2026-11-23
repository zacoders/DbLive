---
type: docs
priority: low
created: 2026-11-23
---
# `DbLive.xunit` needs xunit 2.9.2 or later, the docs say 2.x

## Problem
`docs/llms-full.txt` §13 (line 879: "`xunit` (2.x)") and `docs/unit-testing/index.md:188` ("switch it to the `xunit` 2.x package").

`DbLive.xunit` gets xunit only through `Serilog.Sinks.XUnit` 3.0.19. Its `net8.0` and `net9.0` dependency groups need `xunit.extensibility.core` 2.9.2 or later (checked in the nuspec on 2026-11-23). `xunit.core` pins the exact versions of the extensibility packages, so a test project on xunit 2.8 or older should fail to restore with a version conflict. Not restored. Also noted in `tasks/rejected/0096-58-...` (Notes).

## Fix
Write "xunit 2.9.2 or later (v2)" in the docs.

- [ ] Add a direct dependency on `xunit.extensibility.execution` 2.9.2 to `DbLive.xunit.csproj`, so the minimum does not depend on a logging sink?

## Docs to update
- `docs/llms-full.txt` §2 (`DbLive.xunit` row), §13
- `docs/unit-testing/index.md:188`
- `src/DbLive.xunit/README.md`

## Done when
- [ ] Docs updated
- [ ] A restore of a test project on xunit 2.8 tried, and the result written under Notes
