# Instructions for AI agents

DbLive is a .NET library for database lifecycle management: migrations, code deployment, SQL unit tests and rollback, for SQL Server and PostgreSQL. How DbLive behaves: [docs/llms-full.txt](docs/llms-full.txt). Build and CI: [Development.md](Development.md).

Write everything in the repository in English.

## Tasks

Work items (features, bugs, chores) are Markdown files in `tasks/`, one file per task, not GitHub Issues. Format, numbering and the template: [tasks/README.md](tasks/README.md). Read it before you create or close a task.

- **Folders.** Open tasks are in `tasks/`, finished ones in `tasks/done/`: the folder is the status. An idea that is not decided yet goes to `tasks/ideas/`, without a number.
- **Numbers.** A top-level task takes its GitHub issue number: `0096-documentation.md` for #96. Something found while working on a task gets its own file under that task's number, with the next free two-digit number across `tasks/` and `tasks/done/`: `0096-34-<slug>.md`. Do not open a GitHub issue for it.
- **Work.** Tick the checkboxes as you go. Findings, dead ends and decisions go to `## Notes`; mark facts confirmed by an experiment as verified. Write a decision of the maintainer with its date. Open questions are `- [ ] ...?` items, and the maintainer answers them.
- **Same commit.** A task file changes in the same commit as the code it describes. A change in DbLive behavior updates `docs/llms-full.txt` (the reference for AI agents) and the matching page under `docs/` in that commit too; the task lists them under `## Docs to update`.
- **Close.** When everything under `## Done when` is ticked:
  1. Write `## Release note` right after the title. It is required: GitHub Releases are built from it. Write for users of the packages: what changed, and what they must do to upgrade. Write `None.` when they see no change. Preview the notes: `dotnet run .github/scripts/release-notes.cs`.
  2. Set `breaking: true` in the front matter when an upgrade needs a change on the user's side: a renamed file or setting, a changed call, a test that passed and now fails.
  3. Fill `## Result`: what changed, decisions taken, tests.
  4. Set `closed:` and `git mv` the file to `tasks/done/` in the same commit as the final change.

## Commits and pull requests

Do not write `feat:`, `feat!` or `breaking change:` in commit messages: GitVersion takes the version bump from them. The PR title decides the version: start it with `BREAKING CHANGE:` when a task it closes has `breaking: true`, with `feat:` when a `feature` task has a release note, with neither otherwise. The "Release notes" check of the PR fails on a mismatch. See "Releases" in [Development.md](Development.md).
