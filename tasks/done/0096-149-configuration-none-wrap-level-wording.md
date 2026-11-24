---
type: docs
priority: medium
created: 2026-11-24
closed: 2026-11-24
---
# `docs/project/configuration.md`: `TransactionWrapLevel: None` is "no transactions"

## Release note
None. A correction of `docs/project/configuration.md`; DbLive behaves as before.

## Problem
`docs/project/configuration.md:72` (table "Transactions"): "`None`: no transactions." and `:109`: "**`None`**: no transactions, except for a script whose item settings file sets `"TransactionWrapLevel": "Migration"`."

With `None` every code file still runs in a transaction of its own, and so do the self-deployment and each SQL test. `docs/llms-full.txt` §10 (row `None`): "No transactions, except self-deployment, tests and the transaction of each code file (Section 6)"; §6: "Each code file runs in a transaction of its own ... with every `TransactionWrapLevel` but `Deployment`". `docs/deployment/code.md:89` says it right. In the code neither provider reads `TransactionWrapLevel` for a code file: `MsSqlDA.ExecuteCodeItemAsync` (`src/DbLive.MSSQL/MsSqlDA.cs:29-38`) and `PostgreSqlDA.ExecuteCodeItemAsync` run it in a transaction of its own whenever there is no ambient one.

Probably left over when code files got their own transaction: the page was updated in the `TransactionIsolationLevel` row only.

## Docs to update
- `docs/project/configuration.md:72` and `:109`, as §10 says

## Done when
- [x] Docs updated

## Notes
- Found by the code review of 2026-11-24.

## Result
Docs only, no change in behavior.
- `docs/project/configuration.md`, table "Transactions": `None` runs migration and breaking scripts without a transaction; each code file (link to "Transactions and timeouts" of `docs/deployment/code.md`) and each SQL test still run in a transaction of their own.
- `docs/project/configuration.md`, "Choosing a transaction level": `None` is about migration and breaking scripts, with the item settings exception as before; each code file still runs in a transaction of its own, as with `Migration`.
- The XML doc of the internal `TransactionWrapLevel.None` (`src/DbLive/Common/TransactionLevel.cs`) said "No transaction will be used": now the same as the docs.
- `mkdocs build --strict` passes.
