---
type: chore
priority: high
created: 2026-10-31
closed: 2026-11-02
---
# Release notes, version numbers and a protected `main`

## Release note
**Release notes and a versioning policy.** Every release now has notes in [GitHub Releases](https://github.com/zacoders/DbLive/releases): breaking changes with the upgrade steps first, then features, fixes and other changes. The [versioning policy](https://zacoders.github.io/DbLive/versioning/) says what the public API is: breaking changes in it come only in a major version. Public types the documentation does not describe (deployers, provider data access, DI classes) may change in any version. All DbLive packages are released together with one version; use the same version of all of them.

## Problem
Every change that reaches `main` publishes all four packages and tags a version (`cicd.yml`, GitVersion `Mainline`). On 2026-06-28 six versions were released: four PR merges (#89 → 1.2.1, #90 → 1.3.0, #92 → 1.4.0, #94 → 1.5.0) and fixes pushed to `main` directly (1.3.1, 1.3.3). Most were small fixes, but there are no release notes, so an outside reader cannot see that: an outside review (2026-10-31) read the history as "several releases a day, the API may still change". The repository has no GitHub Releases, and `main` has no protection and no rulesets (verified 2026-11-02).

The version number comes from commit messages: `feat:` → minor, `feat!` or `breaking change:` → major, anything else → patch. GitHub puts the PR title into the body of the merge commit, and the regexes in `GitVersion.yml` match anywhere in the message, so the PR title already decides the version: #90, #92 and #94 became minor versions this way (verified). In `Mainline` mode GitVersion 5 also reads the commits of the merged branch and takes the largest bump (verified with GitVersion 5.12, see Notes), so a `feat:` or `breaking change:` in any commit of the branch raises the version too. The 45 commits of branch `96-documentation` (2026-11-02) contain none of these words (verified), so merging it as is gives 1.5.1, although it already has six breaking changes (0096-04, 0096-07, 0096-13, 0096-15, 0096-17, 0096-33).

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
- Public types not described in the docs (deployers, provider data access, DI classes) are outside the versioning policy and may change in any version (`docs/versioning.md`).
- Close the task in the branch of #96, before PR #98 is merged, so its note goes into 2.0.0. Closing it after the merge would take a PR of its own, and that PR would release 2.0.1 with this note only. "Every release gets a GitHub Release" and "Next release 2.0.0" are checked after the merge; a failure gets a task of its own.

## Where
- `.github/workflows/cicd.yml` (steps "Publish packages", "Create git tag", "Push git tag")
- `.github/GitVersion.yml` (`mode: Mainline`, bump by commit message). The `dev` branch exists on the remote, but CI does not run on it, so its config is unused.
- `.github/workflows/version-simple.yml`: disabled (triggered by a tag that never exists).
- `tasks/README.md` (task format, workflow step "Close")
- No `AGENTS.md`, `CLAUDE.md` or PR template in the repository.

## Plan

### `main`
- [x] Protect `main` with a GitHub ruleset: changes only through a PR, small fixes too; required status checks: `Build > Test > Publish` and the release notes check (below); required approvals 0, because the only maintainer cannot approve their own PR; no bypass. The tag pushed by CI is not a branch, so the ruleset does not block it. Written as `.github/rulesets/main.json`, with merge commits only, no force push and no deletion.
- [x] The maintainer applies the ruleset on GitHub (Development.md, "Protected `main`"), before #96 is merged. Applied 2026-11-02 (see Notes).
- [x] The maintainer applies the changed ruleset (`require_extra_approval_for_unattributed_changes: false`, see Notes) before #96 is merged: `gh api -X PUT repos/zacoders/DbLive/rulesets/26353008 --input .github/rulesets/main.json`. Applied 2026-11-02.
- [x] The PR title decides the version: `BREAKING CHANGE:` for major, `feat:` for minor, anything else patch. Commit messages in the branch count too, so they do not use these words. Add this to the PR template (0096 D6) and `Development.md`.

### Release notes
- [x] Task format in `tasks/README.md`:
  - `## Release note` right after the title, required in a closed task: text for users of the packages, or `None.` when they see no change;
  - `breaking: true` in the front matter when users must change something to upgrade (rename a file, change a call, a test that passed now fails);
  - workflow step "Close": write `## Release note` and set `breaking` before moving the file to `done/`.
- [x] Agent instructions: add `AGENTS.md` and a `CLAUDE.md` that imports it (`@AGENTS.md`). Keep it short: work items follow `tasks/README.md`; a behavior change updates `docs/llms-full.txt` and the page under `docs/` in the same commit; closing a task needs `## Release note` (`None.` allowed) and `breaking: true` when it applies. The PR check catches what the instructions miss.
- [x] Bring the 25 tasks closed in #96 to the format (list of 2026-11-02):
  - section present, moved right after the title: 0096-15, 0096-17, 0096-18, 0096-33 (15, 17 and 33 breaking);
  - note inside `## Result`, move it into the section: 0096-04, 0096-06, 0096-07, 0096-08, 0096-09, 0096-13; mark 04, 07 and 13 as breaking;
  - no note, write one or `None.`: 0096-01, 0096-02, 0096-03, 0096-10, 0096-11, 0096-12, 0096-14, 0096-16, 0096-31, 0096-31-01, 0096-31-02, 0096-31-02-01, 0096-32, 0096-32-01, 0096-32-02; breaking: 11, 31, 31-01, 31-02-01, 32, 32-02 (see Notes); `None.`: 02, 10, 14, 16, 32-01.
- [x] Script `.github/scripts/release-notes.cs` (`dotnet run`, .NET 10 SDK is already in CI):
  - tasks closed since the previous tag: `git diff --no-renames --diff-filter=A <prev>..HEAD -- tasks/done/`, previous tag from `git describe --tags --abbrev=0 HEAD^`;
  - groups: Breaking changes (`breaking: true`), Features, Fixes, Other (`docs`, `chore`, `story`);
  - an item is the `## Release note` text with the issue link from the file name (`0096-…` → #96); `None.` leaves the task out; a task without the section is an error;
  - no task closed in the range: one item, the PR title from the merge commit;
  - a compare link `<prev>...<new>` at the end;
  - runs locally to preview the notes.
- [x] `cicd.yml` on `main`: build the notes before "Publish packages", so an error stops the release before anything is published; after "Push git tag", `gh release create <SemVer> --title <SemVer> --notes-file <file>` (`GH_TOKEN: ${{ github.token }}`; `contents: write` is already granted).
- [x] PR check, a workflow of its own (`pull_request` with types `opened`, `synchronize`, `reopened`, `edited`, so a changed title re-runs it without rebuilding everything): `.github/workflows/release-notes.yml`, job "Release notes".
  - writes the notes to the job summary;
  - fails when a closed task has no `## Release note`;
  - fails when the PR title and the commits give another bump than the tasks need: `breaking: true` needs `BREAKING CHANGE:`, a `feature` task with a note needs `feat:` (the same regexes as `GitVersion.yml`). The plan said "a smaller bump"; see Notes for "another".

### Versions
- [x] Next release (#96, PR #98): 2.0.0, the PR title starts with `BREAKING CHANGE:` (breaking: 0096-04, 0096-07, 0096-11, 0096-13, 0096-15, 0096-17, 0096-31, 0096-31-01, 0096-31-02-01, 0096-32, 0096-32-02, 0096-33): the title is set (Notes, 2026-11-02); the release itself moved to 0096-49.
- [x] Versioning policy: what is public API (`DbLiveBuilder`, `DeployParameters`, `IDbLive`, `settings.json` keys, file naming, assert semantics, the `dblive` schema); breaking changes only in a major version. `docs/versioning.md`.

### Cleanup
- [x] Remove `version-simple.yml` and the `dev` section of `GitVersion.yml`.
- [x] Write the release process into `Development.md`.

## Docs to update
- `Development.md` ("Library versioning", release process)
- `tasks/README.md` (format: `## Release note`, `breaking`; step "Close")
- `AGENTS.md`, `CLAUDE.md` (new)
- `.github/pull_request_template.md` (new, shared with 0096 D6)
- `README.md` (link to GitHub Releases and the versioning policy, see 0096-20)
- `docs/llms.txt` (link to GitHub Releases)
- `docs/versioning.md` (new), `mkdocs.yml` (nav), `docs/llms-full.txt` §2 (versions)

## Done when
- [x] `main` protected
- [x] The agent instructions require `## Release note`, and the PR check fails a closed task without it
- [x] Every release gets a GitHub Release with notes, starting with 2.0.0: checked on 2.0.0 in 0096-49
- [x] Versioning policy published

## Notes
2026-11-02, implementation:
- The breaking list grew from 6 to 12 tasks after reading each one against the definition ("an upgrade needs a change on the user's side") and the public API of tag 1.5.0 (`IProjectPath`, `ProjectPath`, `IFileSystem`, `UndoTestMode`, `DeployFixture` and `SetProject(assembly, useVsProjectPath)` were public there): 0096-11 (a test with a misspelled or differently cased `assert` column now checks or fails), 0096-31 (new `.csproj` block, `useVsProjectPath` removed), 0096-31-01 (`IProjectPath`, `IFileSystem` removed), 0096-31-02-01 (two paths that differ only by case fail the deployment), 0096-32 (the deployment test fails on an undo that does not restore the schema), 0096-32-02 (`DeployFixture` renamed, the test method has no parameters).
- The notes describe the change against 1.5.0, not against an intermediate state of the branch: 0096-02 and 0096-10 (replaced by 0096-31) and 0096-32-01 (resolved by 0096-32-02) are `None.` with the reason; the `[SqlDeployFact]` bullet of 0096-15 describes the final deployment test; the 0096-06 note no longer says that a deployment runs tests (0096-17). The "Breaking:" and "Fixed:" prefixes of the moved notes are gone: the group heading says it.
- `None.` may be followed by the reason; the script checks the prefix. `breaking: true` with `None.` is an error.
- Script: compares the previous tag with the working tree, not with HEAD, so a local preview sees a task moved to `done/` and not committed yet; in CI they are the same. `git describe --match "[0-9]*.[0-9]*.[0-9]*"`, so another kind of tag never becomes the previous release. Tasks ordered by number, a parent before its subtasks. The three patterns are read from `GitVersion.yml`, so there is one copy. The PR check also writes the version to the summary (`1.5.0 + major` → `2.0.0`).
- The bump check is "equal", not "at least" (suggested by the agent when reviewing this task, 2026-11-02; the maintainer then asked to implement the task): a bump larger than the tasks need comes from a stray keyword, usually in a commit of the branch, and releases a major or a minor version for nothing. A PR that closes no task is not checked.
- Verified with GitVersion.Tool 5.12.0 on a local clone, merge commits made like GitHub's: #96 merged under the title `BREAKING CHANGE: ...` gives 2.0.0, under a plain title 1.5.1; a branch with a `feat:` commit merged under the plain title "Fix some bug" gives 2.1.0, so the commits of the merged branch count. The script gave the same versions, failed the last case naming the commit, and a merge that closes no task got the PR title as its note.
- `cicd.yml`: the notes are built right after GitVersion, before the restore, so an error stops the run at once, still before "Publish packages". `gh release create --verify-tag` fails instead of creating a new tag when the tag push did not happen.
- The ruleset is a file to import, not applied: the agent session has no `gh` and no token, and changing the repository settings is the maintainer's step. Added to the plan: `deletion` and `non_fast_forward`; `allowed_merge_methods: ["merge"]`, because a rebase merge puts every commit on `main` as a direct commit, and GitVersion increments the version for each one. `integration_id` 15368 is GitHub Actions, so only a workflow can report the two checks. "Require branches to be up to date" is off: one maintainer, rarely two PRs at once.
- The PR template points to `Development.md` instead of spelling out the prefixes: a repository setting can put the PR description into the merge commit, and the words would raise the version.
- The "Release notes" job has no `if` that skips it on a description edit: a skipped job counts as passed for a required check, so it would turn a failed check green.
- Not verified yet: the two workflows on GitHub. They run for the first time on the PR of #96 and on its merge.
- 2026-11-02, PR #98 (head 67e02bb): "Release notes" and "Build > Test > Publish" passed on GitHub; the PR title is `BREAKING CHANGE: 96 documentation and new features` (verified). The script on a copy of the branch with `## Release note` removed from 0096-03 exited with 1 and named the file; on the branch as is it exited with 0 and printed "Merging this pull request releases **2.0.0**" (verified). The release on `main` (tag, packages, GitHub Release) is not verified yet: it runs on the merge of #98.
- [x] `docs/versioning.md` says that public types not described in the docs (deployers, provider data access, DI classes) may change in any version. Agreed? Yes (maintainer, 2026-11-02).
- 2026-11-02: the maintainer imported the ruleset (id 26353008). Verified: `GET repos/zacoders/DbLive/rules/branches/main` returns `deletion`, `non_fast_forward`, `pull_request` (0 approvals, merge only) and the two required checks. GitHub added `require_extra_approval_for_unattributed_changes: true` to the `pull_request` rule, which is not in the file.
- 2026-11-02, `require_extra_approval_for_unattributed_changes`: GitHub does not document it. Reports from other repositories (September to November 2026, e.g. just-tavrn/.github#23, rjmurillo/ai-agents#6144) say that it is on when a ruleset leaves it out, and that it asks for one more reviewer for commits with a `Co-Authored-By` trailer: PRs stayed `BLOCKED` with 0 required approvals and green checks. Here the only maintainer cannot approve their own PR and the ruleset has no bypass, so such a PR could not be merged at all. Of the 51 commits of #98, 9 have `Co-Authored-By: Claude` (verified). PR #98 is not blocked now: the public API (no token) returns `mergeable_state: clean` with both checks green and 0 reviews (verified), but that state may depend on the viewer. Decision of the maintainer, 2026-11-02: turn it off. `main.json` sets it to `false`. The maintainer applied the file the same day; `GET repos/zacoders/DbLive/rules/branches/main` returns `false` and otherwise the same rules, so the file and GitHub match (verified).

## Result
- Releases: `.github/scripts/release-notes.cs` builds the notes from the `## Release note` sections of the tasks closed since the previous tag (Breaking changes, Features, Fixes, Other; the PR title when no task is closed; a compare link). `cicd.yml` builds them right after GitVersion, so a task without a note stops the run before anything is published, and runs `gh release create --verify-tag` after pushing the tag. The "Release notes" workflow checks every PR: notes in the job summary, a closed task without the section fails, and so does a bump of the PR title and the commits that differs from the one the tasks need.
- `main`: ruleset `.github/rulesets/main.json` (PR only, merge commits only, the two checks required, 0 approvals, no extra approval for unattributed changes, no bypass, no force push, no deletion), applied on GitHub as ruleset 26353008.
- Task format: `## Release note` (or `None.`) required in a closed task, `breaking: true`; the 25 tasks closed in #96 brought to it, 12 of them breaking. `AGENTS.md` and `CLAUDE.md` tell the agent to write the note when it closes a task; `.github/pull_request_template.md`.
- Versioning policy `docs/versioning.md`; links in `README.md`, `docs/llms.txt`, `docs/llms-full.txt` §2, `mkdocs.yml`. The release process in `Development.md` ("Releases", "Protected `main`"). `version-simple.yml` and the `dev` section of `GitVersion.yml` removed.
- Tests: GitVersion.Tool 5.12.0 and the script on a local clone (2.0.0 under `BREAKING CHANGE:`, 1.5.1 under a plain title, a `feat:` commit in the branch caught); the PR check on a copy of the branch without one note failed; both checks passed on PR #98 on GitHub. Not tested yet: the release on `main` (merge of #98).
- Commits: 67e02bb and the commit that closes this task.
