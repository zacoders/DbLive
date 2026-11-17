# For AI Agents

Coding assistants (Claude Code, Copilot, Cursor and others) increasingly write the migrations, procedures and tests in a project. DbLive publishes its documentation in a form they can load in one go.

| File | URL | Purpose |
|------|-----|---------|
| `llms.txt` | [zacoders.github.io/DbLive/llms.txt](https://zacoders.github.io/DbLive/llms.txt) | Short index in the [llms.txt](https://llmstxt.org) format: what DbLive is and where everything is. |
| `llms-full.txt` | [zacoders.github.io/DbLive/llms-full.txt](https://zacoders.github.io/DbLive/llms-full.txt) | The complete reference in one file: setup, file naming grammar, every setting, transaction behaviour, assertion semantics, exceptions with fixes, and recipes for common tasks. |

`llms-full.txt` is written from the source code and states exact behaviour, including limitations and pitfalls that the narrative pages only mention briefly. It starts with a list of hard rules (for example: never edit an applied migration, code files must be re-runnable, no `GO` in SQL Server test files).

## Using it in your project

Point your assistant at the reference from the instructions file it reads automatically (`AGENTS.md`, `CLAUDE.md`, `.github/copilot-instructions.md`, `.cursor/rules`):

```markdown
## Database (DbLive)

The database is managed with DbLive (https://github.com/zacoders/DbLive).
SQL lives in `src/MyDb/Scripts` (Migrations, Code, Tests).
Before changing anything there, read https://zacoders.github.io/DbLive/llms-full.txt
and follow its hard rules. Deploy locally with the `MyDb.Deploy` console app;
run SQL tests with `dotnet test src/MyDb.Tests`.
```

For offline use, or to pin the documentation to the DbLive version you use, copy `llms-full.txt` into the repository (for example `docs/dblive.md`) and reference that path instead.
