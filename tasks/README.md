# Tasks

Work items (features, bugs, stories, chores) live here as Markdown files, one file per task. Agents and people read and edit tasks like any other file, and a task changes in the same commit as the code it describes.

- `tasks/` holds open tasks.
- `tasks/done/` holds finished tasks. The folder is the status; there is no status field.
- `tasks/ideas/` holds ideas not decided yet (see [Ideas](#ideas)).

## File name and number

A top-level task takes the number of its GitHub issue, padded to four digits so files sort: `0096-documentation.md` for #96.

A task found while working on another one, or split out of it, takes the parent's number plus its own two-digit number: `0096-04-rows-check-incomplete.md` is the fourth task found during #96. The next one takes the largest such number in `tasks/` and `tasks/done/` plus one. Nesting goes deeper the same way: `0096-04-01-...`. Numbers never collide with GitHub issues, because only GitHub hands out top-level numbers.

The branch for a task is its file name without `.md`: `0096-04-rows-check-incomplete`.

## Format

| Front matter | Values | Required |
| --- | --- | --- |
| `type` | `feature`, `bug`, `story`, `chore`, `docs` | yes |
| `priority` | `high`, `medium`, `low` | yes |
| `created` | `YYYY-MM-DD` | yes |
| `closed` | `YYYY-MM-DD`, set when the file moves to `done/` | in `done/` |
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

GitHub Releases are built from the `## Release note` sections of the tasks closed since the previous release (0096-19), so the section is required in every closed task; `None.` is the way to say there is nothing to tell.

## Workflow

1. **Create.** One task per file. Something found while working on another task gets its own file, numbered under that task. Steps of a single task stay a checklist inside its file.
2. **Work.** Tick checkboxes and add notes under `## Notes` as you go.
3. **Close.** When everything under "Done when" is ticked, write `## Release note` (and `breaking: true` when it applies), fill `## Result`, set `closed:`, and `git mv` the file to `done/` in the same commit as the final change.
4. **Docs.** A change in DbLive behavior updates `docs/llms-full.txt` (the reference for AI agents) and the matching page under `docs/` in the same commit.

Useful queries:

```sh
ls tasks                              # open tasks
grep -l "type: bug" tasks/*.md        # open bugs
grep -l "priority: high" tasks/*.md   # open high-priority tasks
ls tasks/0096-* tasks/done/0096-*     # #96 and everything found during it
```

## Ideas

An idea is something worth thinking about later, not decided and not planned. It has no number: `ideas/undo-test-data-round-trip.md`. Front matter holds only `created`; the body says what the idea is, where it came from, and the open questions.

When an idea is taken into work, `git mv` it to `tasks/` under a task number, add `type` and `priority`, and rewrite it in the task format. A rejected idea is deleted; write the reason in the commit message.

## GitHub Issues

GitHub Issues hand out the numbers for top-level tasks, and external users report problems there. When an issue is taken into work, create `NNNN-slug.md` with its number; the details live in the file. Close the issue when the file moves to `done/`.
