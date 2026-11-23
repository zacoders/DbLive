---
type: docs
priority: low
created: 2026-11-23
---
# `database-as-service.md` says DbLive enforces the breaking phase after the normal deployment

## Problem
`docs/database-as-service.md:123`, under "DbLive enforces": "Breaking only when `DeployBreaking = true`, only after the normal deployment of the same build, and a breaking script never before its migration."

The guard checks only code files (`docs/llms-full.txt` §5.5; §17, row `CodeNotDeployedException`: "Its absence does not prove that `Default` succeeded"). A `Default` run that failed before the code phase, or did not run, passes it when the build added or changed no code file. 0096-67 decided not to guard against this operator mistake, so the code stays; only the claim is too strong.

The maintainer writes this page by hand, so the wording is the maintainer's.

## Fix
- [ ] How should the page put it: for example, move "only after the normal deployment of the same build" to "The team must ensure", and keep "DbLive checks that every code file of the build is deployed" under "DbLive enforces"?

## Docs to update
- `docs/database-as-service.md`

## Done when
- [ ] Page updated by the maintainer or with the maintainer's wording
