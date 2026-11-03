# Development

## Tasks
Features, bugs and other work items are Markdown files in [tasks/](tasks/), one file per task; finished ones move to `tasks/done/`, rejected ones to `tasks/rejected/`. See [tasks/README.md](tasks/README.md) for the format and workflow.

## Documentation
The user documentation in `docs/` is published with [Material for MkDocs](https://squidfunk.github.io/mkdocs-material/) to https://zacoders.github.io/DbLive/ by `.github/workflows/docs.yml` on every push to `main`. The site navigation is the `nav:` of `mkdocs.yml`: add a new page there. `docs/llms.txt` and `docs/llms-full.txt` are published as they are; `docs/llms-full.txt` is the reference for AI agents and the source of facts for the pages.

Build locally (Python 3; `requirements.txt` keeps MkDocs below 2.0):

```sh
pip install -r requirements.txt
mkdocs serve            # http://127.0.0.1:8000, rebuilds on save
mkdocs build --strict   # what CI runs
```

The workflow builds with `--strict`, also on pull requests that change the docs, so a broken link or anchor fails the check.

## Releases
Every merge to `main` is a release. CI (`.github/workflows/cicd.yml`) builds and tests the solution, publishes the seven packages (`DbLive`, `DbLive.MSSQL`, `DbLive.PostgreSQL`, `DbLive.xunit`, `DbLive.Testcontainers.MSSQL`, `DbLive.Testcontainers.PostgreSQL`, and the `dotnet new` template `DbLive.Templates`) to nuget.org, tags the merge commit with the version and creates a GitHub Release with the release notes. Work happens in branches and reaches `main` through a pull request, merged with a merge commit. Small fix releases are fine; the versioning policy for users is [docs/versioning.md](docs/versioning.md).

### Version
[GitVersion](https://gitversion.net/) 5 in [Mainline](https://gitversion.net/docs/reference/modes/mainline) mode (`.github/GitVersion.yml`) takes the bump from the messages of the merge commit and of every commit of the merged branch. The largest bump wins:

| A message contains | Bump |
| --- | --- |
| `BREAKING CHANGE:` or `feat!` | major |
| `feat:` | minor |
| neither | patch |

GitHub writes the PR title into the merge commit, so **the PR title decides the version**: start it with `BREAKING CHANGE:` when a closed task has `breaking: true`, with `feat:` when a `feature` task has a release note, and with neither otherwise. Commit messages must not contain these words, or they raise the version too (verified with GitVersion 5.12: a `feat:` commit in a branch merged under a plain title gives a minor version). A rebase merge would put every commit on `main` as a release of its own, which is why the ruleset allows merge commits only.

### Release notes
GitHub Releases are the changelog. `.github/scripts/release-notes.cs` builds the notes from the `## Release note` sections of the task files added to `tasks/done/` since the previous tag, grouped into Breaking changes (`breaking: true`), Features (`type: feature`), Fixes (`type: bug`) and Other. A note that starts with `None.` leaves its task out. A release with no such note (a small fix without a task) gets the PR title as its note.

Preview the notes of the current branch (.NET 10 SDK; tasks moved to `done/` but not committed yet are included):

```sh
dotnet run .github/scripts/release-notes.cs
dotnet run .github/scripts/release-notes.cs -- --check --title "<PR title>"
```

- **On a pull request**, the "Release notes" workflow (`.github/workflows/release-notes.yml`) runs the script with `--check` and writes the version and the notes to the job summary. It fails when a closed task has no `## Release note`, or when the bump of the PR title and the commits differs from the one the tasks need: major for a `breaking: true` task, minor for a `feature` task with a note, patch otherwise. A PR that closes no task is not checked: its title decides. The workflow runs again when the title is edited.
- **On `main`**, CI builds the notes right after GitVersion, so a task without a note stops the release before anything is published, and creates the GitHub Release after pushing the tag.

### Protected `main`
`main` takes changes only through a pull request, with the checks "Build > Test > Publish" and "Release notes" passed and merged with a merge commit; no force push, no deletion, no bypass. Required approvals are 0, because the only maintainer cannot approve their own PR; for the same reason the extra approval for unattributed changes is off (GitHub turns it on when the ruleset does not set it). The tag pushed by CI is not a branch, so the ruleset does not block it.

The ruleset is kept in `.github/rulesets/main.json`. To apply it, or a changed version of it: Settings → Rules → Rulesets → New ruleset → Import a ruleset (delete the old one first), or

```sh
gh api -X POST repos/zacoders/DbLive/rulesets --input .github/rulesets/main.json
gh api -X PUT repos/zacoders/DbLive/rulesets/<id> --input .github/rulesets/main.json   # update; ids: gh api repos/zacoders/DbLive/rulesets
```

### Assembly version and symbols
CI builds the solution once in Release with `-p:Version=<NuGetVersionV2>`, runs the tests on that build and packs it with `--no-build`. The assemblies get AssemblyVersion and FileVersion `Major.Minor.Patch.0` and the informational version `<version>+<commit sha>`. Every package gets a symbols package (`.snupkg`, with Source Link to GitHub), pushed to nuget.org together with the `.nupkg`. `src/Directory.Build.props` turns on the symbols package and, on GitHub Actions only, `ContinuousIntegrationBuild`. Local builds keep the placeholder version `0.0.0-placeholder` from the `.csproj` files.

The GitVersion action also exports its outputs as environment variables without a prefix (`informationalVersion`, `sha`, ...), and MSBuild reads environment variables as properties, ignoring case. So CI passes `InformationalVersion` explicitly; a property that the SDK reads and that is not passed on the command line or set in a project file may come from GitVersion.
