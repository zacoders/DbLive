---
created: 2026-11-27
---
# A rules file for coding agents in the `dotnet new dblive` template

## Idea
The template creates the SQL project with the deployer, the scripts and the test project (`src/DbLive.Templates/templates/dblive`), but no instructions for coding agents. `docs/ai-agents.md` gives a section to paste into `AGENTS.md` or `CLAUDE.md`, and only a person who read that page adds it. An agent in a project made from the template does not know that `docs/llms-full.txt` exists, or the rules: it edits an applied migration, writes a code file that is not re-runnable, puts `GO` into a test.

The template creates a short file, one page:
- what is where: `MyDb/Scripts/Migrations`, `Code`, `Tests`, `settings.json`; the deployer; the test project;
- the commands: `dotnet test` (Docker must run), `dotnet run` in the SQL project to deploy to a local database;
- the hard rules of every day from Section 0, one line each: never edit or delete an applied migration, code files are re-runnable, migrations expand only and drops go to breaking scripts, no `GO` in SQL Server tests, `assert` is the first column, follow the way the project uses DbLive (rule 14: the template's `GetUser` has no `_vN`, so no versioned names until the project chooses them);
- a link to `llms-full.txt` for the rest.

Came up 2026-11-27, from a review of DbLive by an agent that was asked what would make it choose DbLive (chat with the maintainer).

## Open questions
- [ ] Which file: `AGENTS.md` (Codex, Copilot, Cursor and others read it), `CLAUDE.md` (Claude Code; this repository imports `AGENTS.md` into it with `@AGENTS.md`), or both? Not verified per tool.
- [ ] Where: the template's output folder is usually inside a repository that has its own `AGENTS.md` at the root. Does each tool read a file in a subfolder? A template option to leave the file out? Adding an option is not breaking; removing it later is (`docs/versioning.md`).
- [ ] The link: the published docs follow `main`, so a project on an older DbLive reads the rules of the newer one. Link to the `llms-full.txt` of the template's version on GitHub (the tag), or copy it into the project?
- [ ] How the file stays in sync with Section 0: is it the core of `llms-full-core-and-reference.md`, the same text in both places, or generated from one source? Does a check in CI compare them?
- [ ] Does the package test of the template (`.github/scripts/package-tests/Template.cs`) check that the file is created?

## Docs to update
- `docs/ai-agents.md`: a project created by the template has the file; the section to paste stays for other projects
- `docs/gettingstarted.md`, `docs/llms-full.txt` §18.1, if they list what the template creates

## See also
- `tasks/ideas/llms-full-core-and-reference.md`: the core of the reference, of which this file could be the shortest form
- `tasks/ideas/simulated-users-from-docs.md`: a way to measure whether an agent with this file does the scenarios as well as one that read the whole reference
