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

## Scripts
`.github/scripts/` holds the scripts of CI, .NET file-based apps run with `dotnet run <script>.cs` (.NET 10 SDK). A script file is the outline: it reads the arguments and calls the steps in order. The steps are in the folder of the same name, one file each, included with `#:include`: `release-notes.cs` and `release-notes/`, `package-tests.cs` and `package-tests/`, `public-api.cs` and `public-api/` (`release-notes.cs` includes `public-api/ApiFile.cs` too). The comment at the top of a script lists its arguments and its files.

## The public API
The six packages with a C# API (`DbLive`, `DbLive.MSSQL`, `DbLive.PostgreSQL`, `DbLive.xunit`, `DbLive.Testcontainers.MSSQL`, `DbLive.Testcontainers.PostgreSQL`) list their public API in `PublicAPI.Shipped.txt` and `PublicAPI.Unshipped.txt` next to the `.csproj`: one line per public symbol, with its nullable annotations. `Microsoft.CodeAnalysis.PublicApiAnalyzers` compares the files with the code, and `TreatWarningsAsErrors` makes a difference a build error. So a change of the public API does not build until the files say it, and the files show it in the pull request.

- `PublicAPI.Shipped.txt` is the public API on `main`: every merge is a release, so it is the API of the latest release. `PublicAPI.Unshipped.txt` is a draft of the branch, empty on `main`.
- **RS0016**, a public symbol that is not in the files: add its line to `PublicAPI.Unshipped.txt` with the code fix of RS0016 in the IDE, or for the whole project with `dotnet format analyzers <project> --diagnostics RS0016 --severity info`.
- **RS0017**, a symbol of the files that is gone: delete its line from the file it is in. A changed signature is a removed symbol and an added one.
- **Before the merge**, `dotnet run .github/scripts/public-api.cs` moves the lines of every `PublicAPI.Unshipped.txt` to the `PublicAPI.Shipped.txt` next to it, sorted as the code fix sorts them (a `*REMOVED*` line deletes its symbol). The diff of `PublicAPI.Shipped.txt` in the pull request is then the change of the public API: an added line is new API, a deleted or changed line a breaking change.

The "Release notes" check (`release-notes.cs`, see [Release notes](#release-notes)) fails when a `PublicAPI.Unshipped.txt` has lines, and when a line of a `PublicAPI.Shipped.txt` of the previous release is gone and the version is not major: start the PR title with `BREAKING CHANGE:`. It does not see one breaking change: a member added to an interface that users implement (`IDbLiveFixtureBuilder`) is an added line, and breaks their implementations.

One pair of files serves all target frameworks: no code of `src` depends on the target framework.

A type is public only when the docs describe it (Section 11 of `docs/llms-full.txt`, the exceptions of Section 17, what `docs/versioning.md` covers) or a described type has it in a signature. Everything else is internal, also a class that the dependency injection creates: the class is internal, its constructor public. The same for members: a public type has public only what a user calls or reads. A type that DbLive creates and the user only receives (an exception, a result) has internal constructors and setters, and a public class that the user does not derive from is sealed. The public types are in the namespaces `DbLive`, `DbLive.Testing`, `DbLive.MSSQL`, `DbLive.PostgreSQL` and `DbLive.xunit`, wherever their files are. The provider packages, `DbLive.xunit` and the test projects see the internals through `InternalsVisibleTo`.

## Releases
Every merge to `main` is a release. CI (`.github/workflows/cicd.yml`) builds and tests the solution, publishes the seven packages (`DbLive`, `DbLive.MSSQL`, `DbLive.PostgreSQL`, `DbLive.xunit`, `DbLive.Testcontainers.MSSQL`, `DbLive.Testcontainers.PostgreSQL`, and the `dotnet new` template `DbLive.Templates`) to nuget.org, tags the merge commit with the version and creates a GitHub Release with the release notes. Work happens in branches and reaches `main` through a pull request, merged with a merge commit. Small fix releases are fine; the versioning policy for users is [docs/versioning.md](docs/versioning.md).

### Version
[GitVersion](https://gitversion.net/) 5 in [Mainline](https://gitversion.net/docs/reference/modes/mainline) mode (`.github/GitVersion.yml`) takes the bump from the messages of the merge commit and of every commit of the merged branch. The largest bump wins:

| A message contains | Bump |
| --- | --- |
| `BREAKING CHANGE:` or `feat!` | major |
| `feat:` | minor |
| neither | patch |

GitHub writes the PR title into the merge commit, so **the PR title decides the version**: start it with `BREAKING CHANGE:` when a closed task has `breaking: true`, with `feat:` when a `feature` task has a release note, and with neither otherwise. A symbol removed from a `PublicAPI.Shipped.txt` needs `BREAKING CHANGE:` (see [The public API](#the-public-api)). Commit messages must not contain these words, or they raise the version too (verified with GitVersion 5.12: a `feat:` commit in a branch merged under a plain title gives a minor version). A rebase merge would put every commit on `main` as a release of its own, which is why the ruleset allows merge commits only.

### Release notes
GitHub Releases are the changelog. `.github/scripts/release-notes.cs` builds the notes from the `## Release note` sections of the task files added to `tasks/done/` since the previous tag, grouped into Breaking changes (`breaking: true`), Features (`type: feature`), Fixes (`type: bug`) and Other. A note that starts with `None.` leaves its task out. A release with no such note (a small fix without a task) gets the PR title as its note.

Preview the notes of the current branch (.NET 10 SDK; tasks moved to `done/` but not committed yet are included):

```sh
dotnet run .github/scripts/release-notes.cs
dotnet run .github/scripts/release-notes.cs -- --check --title "<PR title>"
```

- **On a pull request**, the "Release notes" workflow (`.github/workflows/release-notes.yml`) runs the script with `--check` and writes the version and the notes to the job summary. It fails when a closed task has no `## Release note`, or when the bump of the PR title and the commits differs from the one the tasks need: major for a `breaking: true` task, minor for a `feature` task with a note, patch otherwise. A PR that closes no task is not checked against the tasks: its title decides. It also fails when `MinCompatibleVersion` changed and is not the version of the release (see [Compatibility of the `dblive` tables](#compatibility-of-the-dblive-tables)), when a `PublicAPI.Unshipped.txt` has lines, and when a public symbol was removed and the version is not major (see [The public API](#the-public-api)), also in a PR that closes no task. The workflow runs again when the title is edited.
- **On `main`**, CI builds the notes right after GitVersion, so a task without a note, a `MinCompatibleVersion` that is not the version of the release, or a public API file that fails the checks above, stops the release before anything is published, and creates the GitHub Release after pushing the tag.

### Compatibility of the `dblive` tables
An older DbLive meets a database that a newer DbLive has deployed when an older build is deployed again: by mistake, or as the rollback of a release that upgraded DbLive. A newer DbLive can store in the `dblive` tables what an older one cannot read or understands differently, so the database keeps the oldest DbLive version that can deploy it, `dblive.version.min_dblive_lib_version`. The self-deployer checks it before any change, and so does `RunAllTestsAsync`: an older DbLive fails with `DatabaseRequiresNewerDbLiveException`. Every deployment writes the `MinCompatibleVersion` of its DbLive there (`src/DbLive/SelfDeployer/DbLiveCompatibility.cs`) and never lowers the stored value. The requirement is compared with the constant, not with the assembly version: that is `0.0.0-placeholder` in a local build and a prerelease on a branch.

Raise `MinCompatibleVersion` to the version of the release in the change after which an older DbLive misreads or miswrites the `dblive` tables:

- a new value of a stored column, or a new meaning of a value or a column: a status, an item type, the format of `relative_path`;
- a dropped or renamed column or table, or converted data that an older DbLive reads differently;
- a new `NOT NULL` column without a default: an older DbLive inserts rows without it.

Do not raise it for a change that the older code does not see: a nullable column, a column with a default, a new table or index.

The change can come with a self-migration or from the C# code alone. `DbLiveTablesSnapshotTests` (`src/DbLive.Tests/SelfDeployer`) fails on a new or changed self-migration of either provider and on a new value of an enum stored in the `dblive` tables; decide, then raise the constant or only update the snapshot. A change of behavior in C# alone is not caught: check a change of `MsSqlDA`, `PostgreSqlDA` and of what they write against the list above.

A change that raises it:

1. Sets `MinCompatibleVersion` to the version of the release; the PR check shows it ("Merging this pull request releases ...").
2. Says in the release note of its task that DbLive versions before this one can no longer deploy a database this version has deployed, and that a rollback of the release is a rollback of the application that keeps the database. The task is not `breaking: true`: an upgrade needs no change on the user's side.
3. Updates the snapshot of `DbLiveTablesSnapshotTests`.

`release-notes.cs` checks the version: when a commit since the previous release changed `MinCompatibleVersion`, it must be the version of the release. In the PR check the title decides the version, so the check runs again when the title is edited. On `main` it runs before anything is published, and also catches two pull requests that raised the constant at the same time: they merge without a conflict, and the second one is released with a later version. Then set it to the version of the next release in a new pull request.

The guard runs in the older DbLive, so a DbLive without it is never protected: DbLive before 2.0.0 does not check the requirement.

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
