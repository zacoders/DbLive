# Development

## Tasks
Features, bugs and other work items are Markdown files in [tasks/](tasks/), one file per task; finished ones move to `tasks/done/`. See [tasks/README.md](tasks/README.md) for the format and workflow.

## Library versioning
[GitVersion](https://gitversion.net/) used for versioning. Please check [Mainline Development](https://gitversion.net/docs/reference/modes/mainline) using GitVersion.

CI (`.github/workflows/cicd.yml`) builds the solution once in Release with `-p:Version=<NuGetVersionV2>`, runs the tests on that build and packs it with `--no-build`. The assemblies get AssemblyVersion and FileVersion `Major.Minor.Patch.0` and the informational version `<version>+<commit sha>`. Every package gets a symbols package (`.snupkg`, with Source Link to GitHub), pushed to nuget.org together with the `.nupkg`. `src/Directory.Build.props` turns on the symbols package and, on GitHub Actions only, `ContinuousIntegrationBuild`. Local builds keep the placeholder version `0.0.0-placeholder` from the `.csproj` files.

The GitVersion action also exports its outputs as environment variables without a prefix (`informationalVersion`, `sha`, ...), and MSBuild reads environment variables as properties, ignoring case. So CI passes `InformationalVersion` explicitly; a property that the SDK reads and that is not passed on the command line or set in a project file may come from GitVersion.



