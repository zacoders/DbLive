---
type: chore
priority: high
created: 2026-10-31
---
# Release notes, version numbers and a protected `main`

## Problem
Every change that reaches `main` publishes all four packages and tags a version (`cicd.yml`, GitVersion `Mainline`). On 2026-06-28 six versions were released: four PR merges (#89 → 1.2.1, #90 → 1.3.0, #92 → 1.4.0, #94 → 1.5.0) and fixes pushed to `main` directly (1.3.1, 1.3.3). Most were small fixes, but there are no release notes, so an outside reader cannot see that: an outside review (2026-10-31) read the history as "several releases a day, the API may still change". The repository has no GitHub Releases, and `main` has no protection and no rulesets (verified 2026-11-02).

The version number comes from commit messages: `feat:` → minor, `feat!` or `breaking change:` → major, anything else → patch. GitHub puts the PR title into the body of the merge commit, and the regexes in `GitVersion.yml` match anywhere in the message, so the PR title already decides the version: #90, #92 and #94 became minor versions this way (verified). In `Mainline` mode GitVersion 5 also reads the commits of the merged branch and takes the largest bump (from its source, not verified here), so a `feat:` or `breaking change:` in any commit of the branch raises the version too. The 45 commits of branch `96-documentation` (2026-11-02) contain none of these words (verified), so merging it as is gives 1.5.1, although it already has six breaking changes (0096-04, 0096-07, 0096-13, 0096-15, 0096-17, 0096-33).

The code and the tasks are written by an AI agent, and nothing tells it to write a release note: the repository has no `AGENTS.md` or `CLAUDE.md`, and the format in `tasks/README.md` has no such section. 0096-15, 0096-17, 0096-18 and 0096-33 have `## Release note` only because the agent read this plan; 0096-04, 0096-06, 0096-07, 0096-08, 0096-09 and 0096-13 have the note inside `## Result`; the other 15 tasks closed in #96 have none.

## Decision
2026-10-31, maintainer:
- Keep "merge to `main` = release" with GitVersion; no tag-based releases. Work happens in branches and is merged to `main` less often, in larger batches. Small fix releases are fine.
- GitHub Releases are the changelog, generated from the tasks closed since the previous release. No `CHANGELOG.md`: CI would have to commit to a protected `main`. Not GitHub's auto-generated notes: a release is usually one PR, so they would be one line. Not tools that read commit messages (git-cliff, release-please): the commits here are not written for that ("fixes, #96", "new docs").

2026-11-02, maintainer:
- The agent writes `## Release note` when it closes a task, while the context is fresh. It must be told so in the agent instructions, and a PR check fails a closed task without the section. `None.` is the note when users see no change, so a missing section always means "forgotten".
- The notes are previewed on every PR, not only published after the merge.
- A release without a closed task (a small fix) gets the PR title as its note.
- `version-simple.yml` and the `dev` config go. Preview packages, if ever needed, are a task of their own.

## Where
- `.github/workflows/cicd.yml` (steps "Publish packages", "Create git tag", "Push git tag")
- `.github/GitVersion.yml` (`mode: Mainline`, bump by commit message). The `dev` branch exists on the remote, but CI does not run on it, so its config is unused.
- `.github/workflows/version-simple.yml`: disabled (triggered by a tag that never exists).
- `tasks/README.md` (task format, workflow step "Close")
- No `AGENTS.md`, `CLAUDE.md` or PR template in the repository.

## Plan

### `main`
- [ ] Protect `main` with a GitHub ruleset: changes only through a PR, small fixes too; required status checks: `Build > Test > Publish` and the release notes check (below); required approvals 0, because the only maintainer cannot approve their own PR; no bypass. The tag pushed by CI is not a branch, so the ruleset does not block it.
- [ ] The PR title decides the version: `BREAKING CHANGE:` for major, `feat:` for minor, anything else patch. Commit messages in the branch count too, so they do not use these words. Add this to the PR template (0096 D6) and `Development.md`.

### Release notes
- [x] Task format in `tasks/README.md`:
  - `## Release note` right after the title, required in a closed task: text for users of the packages, or `None.` when they see no change;
  - `breaking: true` in the front matter when users must change something to upgrade (rename a file, change a call, a test that passed now fails);
  - workflow step "Close": write `## Release note` and set `breaking` before moving the file to `done/`.
- [x] Agent instructions: add `AGENTS.md` and a `CLAUDE.md` that imports it (`@AGENTS.md`). Keep it short: work items follow `tasks/README.md`; a behavior change updates `docs/llms-full.txt` and the page under `docs/` in the same commit; closing a task needs `## Release note` (`None.` allowed) and `breaking: true` when it applies. The PR check catches what the instructions miss.
- [ ] Bring the 25 tasks closed in #96 to the format (list of 2026-11-02):
  - section present: 0096-15, 0096-17, 0096-18, 0096-33 (15, 17 and 33 breaking);
  - note inside `## Result`, move it into the section: 0096-04, 0096-06, 0096-07, 0096-08, 0096-09, 0096-13; mark 04, 07 and 13 as breaking;
  - no note, write one or `None.`: 0096-01, 0096-02, 0096-03, 0096-10, 0096-11, 0096-12, 0096-14, 0096-16, 0096-31, 0096-31-01, 0096-31-02, 0096-31-02-01, 0096-32, 0096-32-01, 0096-32-02.
- [ ] Script `.github/scripts/release-notes.cs` (`dotnet run`, .NET 10 SDK is already in CI):
  - tasks closed since the previous tag: `git diff --no-renames --diff-filter=A <prev>..HEAD -- tasks/done/`, previous tag from `git describe --tags --abbrev=0 HEAD^`;
  - groups: Breaking changes (`breaking: true`), Features, Fixes, Other (`docs`, `chore`, `story`);
  - an item is the `## Release note` text with the issue link from the file name (`0096-…` → #96); `None.` leaves the task out; a task without the section is an error;
  - no task closed in the range: one item, the PR title from the merge commit;
  - a compare link `<prev>...<new>` at the end;
  - runs locally to preview the notes.
- [ ] `cicd.yml` on `main`: build the notes before "Publish packages", so an error stops the release before anything is published; after "Push git tag", `gh release create <SemVer> --title <SemVer> --notes-file <file>` (`GH_TOKEN: ${{ github.token }}`; `contents: write` is already granted).
- [ ] PR check, a workflow of its own (`pull_request` with types `opened`, `synchronize`, `reopened`, `edited`, so a changed title re-runs it without rebuilding everything):
  - writes the notes to the job summary;
  - fails when a closed task has no `## Release note`;
  - fails when the PR title gives a smaller bump than the tasks: `breaking: true` needs `BREAKING CHANGE:`, a `feature` task with a note needs at least `feat:` (the same regexes as `GitVersion.yml`).

### Versions
- [ ] Next release (#96): 2.0.0, the PR title starts with `BREAKING CHANGE:` (breaking: 0096-04, 0096-07, 0096-13, 0096-15, 0096-17, 0096-33).
- [ ] Versioning policy: what is public API (`DbLiveBuilder`, `DeployParameters`, `IDbLive`, `settings.json` keys, file naming, assert semantics, the `dblive` schema); breaking changes only in a major version.

### Cleanup
- [ ] Remove `version-simple.yml` and the `dev` section of `GitVersion.yml`.
- [ ] Write the release process into `Development.md`.

## Docs to update
- `Development.md` ("Library versioning", release process)
- `tasks/README.md` (format: `## Release note`, `breaking`; step "Close")
- `AGENTS.md`, `CLAUDE.md` (new)
- `.github/pull_request_template.md` (new, shared with 0096 D6)
- `README.md` (link to GitHub Releases and the versioning policy, see 0096-20)
- `docs/llms.txt` (link to GitHub Releases)

## Done when
- [ ] `main` protected
- [ ] The agent instructions require `## Release note`, and the PR check fails a closed task without it
- [ ] Every release gets a GitHub Release with notes, starting with 2.0.0
- [ ] Versioning policy published
