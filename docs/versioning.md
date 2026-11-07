# Versioning

DbLive follows [Semantic Versioning](https://semver.org/): `MAJOR.MINOR.PATCH`. All DbLive packages (`DbLive`, `DbLive.MSSQL`, `DbLive.PostgreSQL`, `DbLive.xunit`, `DbLive.Testcontainers.MSSQL`, `DbLive.Testcontainers.PostgreSQL`, and the template `DbLive.Templates`) are released together with one version; use the same version of all of them. A project created by the template references the packages of the template's version.

| Version | What it contains | What you do to upgrade |
| --- | --- | --- |
| Major (`2.0.0`) | Breaking changes, and anything else | Read "Breaking changes" in the release notes and follow the upgrade steps there |
| Minor (`2.1.0`) | New features and fixes, nothing breaking | Nothing |
| Patch (`2.0.1`) | Fixes, docs, dependency updates | Nothing |

A **breaking change** is anything that needs a change on your side when you upgrade: a renamed file, folder or setting, a changed call, a deployment or a test that passed and now fails. Breaking changes come only in a major version. A fix that makes a wrong result fail counts too: for example, a SQL test that passed only because its check was ignored.

## Release notes

Every release has notes in [GitHub Releases](https://github.com/zacoders/DbLive/releases), starting with 2.0.0: breaking changes with the upgrade steps first, then features, fixes and other changes. Every merge to `main` is a release, so a small fix gets its own patch version.

## What the policy covers

- **The C# API described in the documentation**, listed in Section 11 of the [reference for AI agents](https://zacoders.github.io/DbLive/llms-full.txt): `DbLiveBuilder` and its extension methods, `IDbLive`, `DeployParameters`, `IDbLiveTester`, `IScriptSource`, the listed members of `IDbLiveDA`, and the exceptions DbLive throws to the caller (Section 17).
- **Testing**: `[SqlFact]`, `[SqlDeployFact]`, `DbLiveTestFixture`, `DeploymentTestFixture` (`DbLive.xunit`) and what the tests check; `IDbLiveFixtureBuilder` and the fixture builders `MsSqlFixtureBuilder`, `PostgreSqlFixtureBuilder`, `MsSqlContainerFixtureBuilder`, `PostgreSqlContainerFixtureBuilder` (Section 11.6 of the reference).
- **The project layout**: the folders, the file naming rules, the `settings.json` keys and the item settings files ([Project structure](project/structure.md), [Configuration](project/configuration.md)).
- **SQL test semantics**: the `assert` result set and its values, `setup.test.sql`, the transaction a test runs in ([SQL unit testing](unit-testing/index.md)).
- **The `dblive` schema**, DbLive's own tables in your database. DbLive creates and upgrades it during a deployment, without manual steps; a new version may add columns and tables and convert the stored data. Removing or renaming what you can query is a breaking change. A release after which older DbLive versions can no longer deploy the database says so in its release notes (see [The DbLive version a database requires](deployment/index.md#the-dblive-version-a-database-requires)). It is not a breaking change: the upgrade needs nothing on your side, but the previous build of your database can no longer be deployed after it, so roll back the application and keep the database.
- **Supported .NET versions and database engines**: dropping one is a breaking change.

The template `DbLive.Templates` (`dotnet new dblive`) creates new projects and does not change existing ones, so a change in what it creates (file names, example scripts) is not a breaking change. A removed or renamed option of the template is.

Types that are public but not described in the documentation (the deployers, the provider data access classes, the classes behind the dependency injection) are public so that the provider packages and `DbLive.xunit` can use them. They may change in any version.
