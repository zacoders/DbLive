---
type: docs
priority: low
created: 2026-11-23
---
# The root README lists two of the four default test patterns

## Problem
`README.md:166`: "in `Code`, files matching `*.test.sql` or `t.*.sql` (patterns configurable)". The defaults are `test.*.sql`, `t.*.sql`, `*.test.sql`, `*.t.sql` (`docs/llms-full.txt` §4 and §9.2). A code object named `x.t.sql` or `test.x.sql` becomes a test and is never deployed, and the README does not warn of it.

## Fix
List all four patterns.

## Docs to update
- `README.md`

## Done when
- [ ] README fixed
