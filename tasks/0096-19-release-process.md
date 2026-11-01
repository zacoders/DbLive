---
type: chore
priority: high
created: 2026-10-31
---
# Release notes, version numbers and a protected `main`

## Problem
Every change that reaches `main` publishes all four packages and tags a version (`cicd.yml`, GitVersion `Mainline`). On 2026-06-28 six versions were released: four PR merges (#89 → 1.2.1, #90 → 1.3.0, #92 → 1.4.0, #94 → 1.5.0) and fixes pushed to `main` directly (1.3.1, 1.3.3). Most were small fixes, but there are no release notes, so an outside reader cannot see that: an outside review (2026-10-31) read the history as "several releases a day, the API may still change".

The version number comes from commit messages: `feat:` → minor, `feat!` or `breaking change:` → major, anything else → patch. The 24 commits of branch `96-documentation` contain none of these words (verified), so merging it as is gives 1.5.1, although it already has three breaking changes (0096-04, 0096-07, 0096-13).

## Decision
2026-10-31, maintainer:
- Keep "merge to `main` = release" with GitVersion; no tag-based releases. Work happens in branches and is merged to `main` less often, in larger batches. Small fix releases are fine.
- GitHub Releases are the changelog, generated from the tasks closed since the previous release. No `CHANGELOG.md`: CI would have to commit to a protected `main`. Not GitHub's auto-generated notes: a release is usually one PR, so they would be one line. Not tools that read commit messages (git-cliff, release-please): the commits here are not written for that ("fixes, #96", "new docs").

## Where
- `.github/workflows/cicd.yml` (steps "Publish packages", "Create git tag", "Push git tag")
- `.github/GitVersion.yml` (`mode: Mainline`, bump by commit message). The `dev` branch exists on the remote, but CI does not run on it, so its config is unused.
- `.github/workflows/version-simple.yml`: disabled (triggered by a tag that never exists).
- `tasks/README.md` (task format)

## Plan

### `main`
- [ ] Protect `main` (GitHub ruleset): changes only through a PR with CI passing, small fixes too.
- [ ] The PR title goes into the merge commit message and decides the version: `BREAKING CHANGE:` for major, `feat:` for minor. Add this to the PR template (0096 D6) and `Development.md`.

### Release notes
- [ ] Task format: a `## Release note` section (text for users; left out when users see no change) and `breaking: true` in the front matter. Update the template in `tasks/README.md`. Move the notes of 0096-04, 0096-06, 0096-07 and 0096-13 from `## Result` into the new section; mark 0096-04, 0096-07 and 0096-13 as breaking.
- [ ] Script `.github/scripts/release-notes.cs` (`dotnet run`, .NET 10 SDK is already in CI):
  - tasks closed since the previous tag: `git diff --no-renames --diff-filter=A <prev>..HEAD -- tasks/done/`, previous tag from `git describe --tags --abbrev=0 HEAD^`;
  - groups: Breaking changes (`breaking: true`), Features, Fixes, Other (`docs`, `chore`, `story`);
  - an item is the `## Release note` text, or the task title when there is none, with the issue link from the file name (`0096-…` → #96);
  - a compare link `<prev>...<new>` at the end;
  - runs locally to preview the notes.
- [ ] `cicd.yml`: after "Push git tag", `gh release create <SemVer> --title <SemVer> --notes-file <file>` (`GH_TOKEN: ${{ github.token }}`; `contents: write` is already granted).
- [ ] No task closed in the range (a fix without a task): list the titles of the merged PRs instead?
- [ ] Optional, can come later: on a PR, write the notes to the job summary, and fail when a task has `breaking: true` but the PR title has no `BREAKING CHANGE:`.

### Versions
- [ ] Next release (#96): 2.0.0 (three breaking changes already, plus 0096-15 and 0096-17).
- [ ] Versioning policy: what is public API (`DbLiveBuilder`, `DeployParameters`, `IDbLive`, `settings.json` keys, file naming, assert semantics, the `dblive` schema); breaking changes only in a major version.

### Cleanup
- [ ] Remove `version-simple.yml`. The `dev` config: remove, or run CI on `dev` for preview packages?
- [ ] Write the release process into `Development.md`.

## Docs to update
- `Development.md` ("Library versioning", release process)
- `tasks/README.md` (format: `## Release note`, `breaking`)
- `README.md` (link to GitHub Releases and the versioning policy, see 0096-20)
- `docs/llms.txt` (link to GitHub Releases)

## Done when
- [ ] `main` protected
- [ ] Every release gets a GitHub Release with notes, starting with the next one
- [ ] Versioning policy published
