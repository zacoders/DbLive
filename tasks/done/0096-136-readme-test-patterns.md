---
type: docs
priority: low
created: 2026-11-23
closed: 2026-11-23
---
# The root README lists two of the four default test patterns

## Release note
None. A correction of the root `README.md`; DbLive behaves as before.

## Problem
`README.md:166`: "in `Code`, files matching `*.test.sql` or `t.*.sql` (patterns configurable)". The defaults are `test.*.sql`, `t.*.sql`, `*.test.sql`, `*.t.sql` (`docs/llms-full.txt` §4 and §9.2). A code object named `x.t.sql` or `test.x.sql` becomes a test and is never deployed, and the README does not warn of it.

## Fix
List all four patterns.

## Docs to update
- `README.md`

## Done when
- [x] README fixed

## Result
`README.md`, the naming examples: the four default patterns `test.*.sql`, `t.*.sql`, `*.test.sql`, `*.t.sql`, and that a file matching them is a test and is not deployed as code. `docs/project/configuration.md`, `docs/project/structure.md` and `docs/unit-testing/index.md` list all four already. No code change, no tests.
