---
type: docs
priority: low
created: 2026-11-23
closed: 2026-11-23
---
# `database-as-service.md` says DbLive enforces the breaking phase after the normal deployment

## Release note
None. A correction of the docs: "Database as a Service" and "Breaking Changes" no longer say that DbLive enforces the breaking phase only after the normal deployment of the same build. DbLive behaves as before.

## Problem
`docs/database-as-service.md:123`, under "DbLive enforces": "Breaking only when `DeployBreaking = true`, only after the normal deployment of the same build, and a breaking script never before its migration."

The guard checks only code files (`docs/llms-full.txt` §5.5; §17, row `CodeNotDeployedException`: "Its absence does not prove that `Default` succeeded"). A `Default` run that failed before the code phase, or did not run, passes it when the build added or changed no code file. 0096-67 decided not to guard against this operator mistake, so the code stays; only the claim is too strong.

The same claim is in `docs/deployment/breaking-changes.md`: the bold lead of "When breaking scripts run" ("Only after the normal deployment of the same build", qualified by the text after it) and "What DbLive cannot check" ("DbLive guarantees the order (..., and only after the normal deployment of the build)", not qualified).

The maintainer writes `database-as-service.md` by hand, so the wording is the maintainer's.

## Fix
- [x] How should the page put it: for example, move "only after the normal deployment of the same build" to "The team must ensure", and keep "DbLive checks that every code file of the build is deployed" under "DbLive enforces"?
  - Decided by the maintainer on 2026-11-23: "DbLive enforces" says what the guard checks ("only when every code file of the build is deployed with its current content"); "The team must ensure" gets "The breaking deployment starts only after the `Default` deployment of the same build succeeded, and never for a release that was rolled back or replaced." `breaking-changes.md` says the same.

## Docs to update
- `docs/database-as-service.md`
- `docs/deployment/breaking-changes.md`

## Done when
- [x] Page updated by the maintainer or with the maintainer's wording

## Notes
2026-11-23, a stronger guard considered again and dropped by the maintainer: a fingerprint of the build (a hash of every deployable file of the project) in a nullable column of `dblive.version`, cleared at the start of a run that deploys migrations or code, written at the end of a successful run that deploys both, compared by the breaking phase. It would close the cases of 0096-41 and 0096-67 except a rollback of the application alone, which no check of the database can see. The maintainer's decision: too much new code and risk of bugs before 2.0.0, for what is an operator mistake (as decided on 0096-41 and 0096-67).

## Result
Docs only, no change in behavior.
- `docs/database-as-service.md`, "DbLive enforces": breaking only when every code file of the build is deployed with its current content (was: only after the normal deployment of the same build). "The team must ensure": the breaking deployment starts only after the `Default` deployment of the same build succeeded, and never for a release that was rolled back or replaced.
- `docs/deployment/breaking-changes.md`: the bold lead of the guard in "When breaking scripts run" names what is checked; "What DbLive cannot check" no longer says that DbLive guarantees the breaking phase after the normal deployment, and lists the same team rule.
