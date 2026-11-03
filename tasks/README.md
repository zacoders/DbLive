# Tasks

Work items (features, bugs, stories, chores) live here as Markdown files, one file per task. Agents and people read and edit tasks like any other file, and a task changes in the same commit as the code it describes.

- `tasks/` holds open tasks.
- `tasks/done/` holds finished tasks. The folder is the status; there is no status field.
- `tasks/ideas/` holds ideas not decided yet (see [Ideas](#ideas)).
- `tasks/rejected/` holds tasks and ideas the maintainer decided not to do, with the reason (see [Rejected](#rejected)).

## File name and number

A top-level task takes the number of its GitHub issue, padded to four digits so files sort: `0096-documentation.md` for #96.

A task found while working on another one, or split out of it, takes the parent's number plus its own two-digit number: `0096-04-rows-check-incomplete.md` is the fourth task found during #96. The next one takes the largest such number in `tasks/`, `tasks/done/` and `tasks/rejected/` plus one. Nesting goes deeper the same way: `0096-04-01-...`. Numbers never collide with GitHub issues, because only GitHub hands out top-level numbers.

The branch for a task is its file name without `.md`: `0096-04-rows-check-incomplete`.

## Format

| Front matter | Values | Required |
| --- | --- | --- |
| `type` | `feature`, `bug`, `story`, `chore`, `docs` | yes |
| `priority` | `high`, `medium`, `low` | yes |
| `created` | `YYYY-MM-DD` | yes |
| `closed` | `YYYY-MM-DD`, set when the file moves to `done/` | in `done/` |
| `rejected` | `YYYY-MM-DD`, set when the file moves to `rejected/` | in `rejected/` |
| `breaking` | `true` when an upgrade needs a change on the user's side: a renamed file or setting, a changed call, a test that passed and now fails | when it applies |

Template (a feature or story may use `Goal` / `Plan` instead of `Problem` / `Where` / `Fix`; leave out sections that would be empty, except `## Release note` in a closed task):

```markdown
---
type: bug
priority: medium
created: 2026-10-26
---
# Short title of the problem or goal

## Release note
Written when closing, for users of the packages: what changed, and what they must do to upgrade. `None.` when they see no change.

## Problem
What is wrong or missing, and why it matters. Mark facts confirmed by an experiment as verified.

## Where
`src/DbLive/Project/DbLiveProject.cs:85` and other places involved.

## Fix
Planned approach. Open questions as `- [ ] ...?` items.

## Docs to update
- `docs/llms-full.txt` §N
- `docs/<page>.md` (section "...")

## Done when
- [ ] Fixed, with a unit test
- [ ] Docs updated

## Notes
Decisions, dead ends and findings, added while working.

## Result
Filled when closing: what changed, decisions taken, commits.
```

GitHub Releases are built from the `## Release note` sections of the tasks closed since the previous release ("Releases" in [Development.md](../Development.md)), so the section is required in every closed task; `None.` is the way to say there is nothing to tell, optionally followed by why (`None. Demo project only.`). The PR check fails a closed task without the section. Preview the notes with `dotnet run .github/scripts/release-notes.cs`.

## Workflow

1. **Create.** One task per file. Something found while working on another task gets its own file, numbered under that task. Steps of a single task stay a checklist inside its file.
2. **Work.** Tick checkboxes and add notes under `## Notes` as you go.
3. **Close.** When everything under "Done when" is ticked, write `## Release note` (and `breaking: true` when it applies), fill `## Result`, set `closed:`, and `git mv` the file to `done/` in the same commit as the final change.
4. **Docs.** A change in DbLive behavior updates `docs/llms-full.txt` (the reference for AI agents) and the matching page under `docs/` in the same commit.
5. **Reject.** A task the maintainer decides not to do moves to `rejected/` (see [Rejected](#rejected)).

Useful queries:

```sh
ls tasks                              # open tasks
grep -l "type: bug" tasks/*.md        # open bugs
grep -l "priority: high" tasks/*.md   # open high-priority tasks
ls tasks/0096-* tasks/done/0096-*     # #96 and everything found during it
ls tasks/rejected                     # what was decided not to do, and why
```

## Ideas

An idea is something worth thinking about later, not decided and not planned. It has no number: `ideas/undo-test-data-round-trip.md`. Front matter holds only `created`; the body says what the idea is, where it came from, and the open questions.

When an idea is taken into work, `git mv` it to `tasks/` under a task number, add `type` and `priority`, and rewrite it in the task format. A rejected idea moves to `rejected/` like a task.

## Rejected

`tasks/rejected/` keeps the tasks and ideas the maintainer decided not to do, so the reason stays next to the proposal and the same proposal is not made again. Before you propose a task or an idea, look there; propose a rejected one again only when something changed since, and say what.

Only the maintainer rejects. The file is not deleted:

1. Write `## Rejected` right after the title: the maintainer's decision with its date, and why.
2. Add `rejected: YYYY-MM-DD` to the front matter.
3. `git mv` the file to `rejected/`. It keeps its name, so its number stays taken. Commit message: `tasks/rejected/<file>: rejected`.

A rejected task has no `## Release note`: release notes are built from `done/` only. When a rejected task is taken up again, `git mv` it back to `tasks/`, drop `rejected:`, and keep the `## Rejected` text under `## Notes`.

## GitHub Issues

GitHub Issues hand out the numbers for top-level tasks, and external users report problems there. When an issue is taken into work, create `NNNN-slug.md` with its number; the details live in the file. Close the issue when the file moves to `done/`.
