---
created: 2026-11-26
---
# Split `llms-full.txt` into a core for every task and a reference of the details

## Idea
`docs/llms-full.txt` has 228 KB, about 84 000 tokens (dfe4558: the Read tool of Claude Code counted 37 000 tokens for lines 1–649 and 46 000 for the rest, and refuses a read of more than 25 000 tokens without a flag, so an agent reads the file in several parts). `llms.txt`, `README.md` and `docs/ai-agents.md` tell an agent to read it first. That takes about 40% of a context of 200 000 tokens before the agent has read a file of the project. An agent with a smaller context, or one that reads only the first part, misses rules.

Most of the file is needed rarely: the exact behavior of edge cases, such as arrays with NULL elements in Npgsql, the Kelvin sign in paths, PgBouncer, In-Memory OLTP, the subtransaction cache of PostgreSQL. The tasks of every day (a migration, a procedure and its test, a breaking script, a release) need Section 0, the file names, the folders, the settings, the assert values, the order of the deployment and the recipes.

Sizes of the sections (dfe4558, KB):

| Section | KB |
|---|---|
| 8. SQL tests | 29 |
| 17. Exceptions: cause and fix | 28 |
| 6. Code objects | 17 |
| 11. C# API | 17 |
| 18. Recipes | 16 |
| 10. Transactions and timeouts | 14 |
| 5. Migrations | 14 |
| 0. Hard rules | 12 |
| 9. `settings.json` | 12 |
| 13. xUnit integration | 11 |
| the other 10 sections | under 10 each, 57 together |

Proposal:
- A core of 300–400 lines that an agent reads in full: Section 0, the file name grammar, the folders, the settings table, the assert values, the order of `DeployAsync`, the recipes. Each statement links to the section of the reference with the details.
- The reference: the rest, by section, for an agent to search (an exception name, a setting, an error message) and read in parts. The table of exceptions (Section 17) is the natural entry: an agent comes with a message.
- Hard rules of one or two lines, each with a link to its section. The longest now, rules 22, 21, 10, 16 and 14, have 800 to 1200 characters each.
- One table instead of the prose of §6, §6.1, §7 and §10 about how each script runs: the kind of script (migration, breaking script of a migration, code file, code file with In-Memory OLTP DDL, breaking script of the Code folder, BeforeDeploy and AfterDeploy, `setup.test.sql`, test) × `TransactionWrapLevel` → its own transaction or none, isolation level, timeout setting, retried or not, row in the `dblive` tables.

Came up 2026-11-26, in a review of the docs by an agent that played a new user (chat with the maintainer).

## Open questions
- [ ] Two files (the core in `llms.txt` or a new file, the reference in `llms-full.txt`), or one file with the core first and a line that says where the reference begins? One file keeps "the single source of truth for agents" of its header; two files let an agent load only the core.
- [ ] How the core and the reference stay consistent: the core states rules and links, the reference states behavior, and each fact lives in one of them only?
- [ ] Does an agent with the core alone do the scenarios of `simulated-users-from-docs.md` as well as with the whole file?

## See also
- `tasks/ideas/simulated-users-from-docs.md`: agents that work from the published docs, a way to measure the split
- `docs/ai-agents.md`: what an agent is told to read
