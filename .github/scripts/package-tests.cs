#!/usr/bin/env dotnet
// Builds projects that reference the packed DbLive packages from a local feed, the way a user project does,
// and checks what the packages add to them: the DbLive package embeds the scripts folder without a block in the
// .csproj (task 0096-30-01), only the folder that the .csproj names in DbLiveScriptsFolder (task 0096-43), and the
// fixture builders compile with their packages (task 0096-30-02).
// Then installs the dotnet new template from its package, creates a solution for each provider, and runs its tests
// against the packages of the same build (task 0096-30). The tests start SQL Server and PostgreSQL in Docker, so
// Docker must be running.
//
//   dotnet pack src/DbLive/DbLive.csproj -o <folder>              (and the other packages, see cicd.yml)
//   dotnet run .github/scripts/package-tests.cs -- --packages <folder>
//
// --packages <folder>   the folder with the .nupkg files of one build
//
// Fails (exit code 1) when a check fails, and keeps the projects for inspection.
//
// This file lists the checks. How they work is in package-tests/:
//   Workspace.cs     the temporary folder of the projects, isolated from the repository and from the user's caches
//   TestProject.cs   a project: its files, its build, and what the build embeds
//   Template.cs      the dotnet new template: install, create a solution, run its tests
//   Report.cs, Dotnet.cs

// Reads the built assemblies by reflection, which the AOT analyzers of a file-based app warn about.
#:property PublishAot=false

#:include package-tests/Workspace.cs
#:include package-tests/TestProject.cs
#:include package-tests/Template.cs
#:include package-tests/Report.cs
#:include package-tests/Dotnet.cs

string packages = args is ["--packages", var folder]
	? Path.GetFullPath(folder)
	: throw new ArgumentException("Usage: dotnet run .github/scripts/package-tests.cs -- --packages <folder>");

var workspace = Workspace.Create(packages);
Console.WriteLine($"DbLive {workspace.Version} from {packages}, projects in {workspace.Root}");

// Layout A: scripts and deployer in one project, DbLive comes through the provider package. The .csproj names the
// folder of the scripts.
workspace.Project("Provider", packages: ["DbLive.PostgreSQL"], properties: "<DbLiveScriptsFolder>Scripts</DbLiveScriptsFolder>")
	.Write("Scripts/settings.json", """{ "ProjectId": "package-tests" }""")
	.Write("Scripts/Migrations/001.migration.sql", "create table t1 ( id int );")
	.Write("Scripts/Code/proc.fr.sql", "select 1;") // a culture name: without WithCulture="false" in a satellite assembly
	.Write("Scripts/Code/sub/f.sql", "select 2;")
	.ExpectScripts("Scripts", ["settings.json", "Migrations/001.migration.sql", "Code/proc.fr.sql", "Code/sub/f.sql"]);

// Layout B: a project with only scripts references DbLive, in a folder with another name.
// A Scripts folder next to it is not embedded.
workspace.Project("CustomFolder", packages: ["DbLive"], properties: "<DbLiveScriptsFolder>Db</DbLiveScriptsFolder>")
	.Write("Db/Migrations/001.migration.sql", "create table t1 ( id int );")
	.Write("Scripts/other.sql", "select 1;")
	.ExpectScripts("Db", ["Migrations/001.migration.sql"]);

// Layout B: the deployer references the SQL project and the provider, and names no folder: it gets nothing.
workspace.Project("Deployer", packages: ["DbLive.PostgreSQL"], items: """<ProjectReference Include="../CustomFolder/CustomFolder.csproj" />""")
	.ExpectNoScripts();

// A project with DbLive scripts in Scripts/ that does not name the folder: nothing is embedded (task 0096-43).
workspace.Project("NoFolderProperty", packages: ["DbLive"])
	.Write("Scripts/settings.json", """{ "ProjectId": "package-tests" }""")
	.Write("Scripts/Migrations/001.migration.sql", "create table t1 ( id int );")
	.ExpectNoScripts();

// A project that gets DbLive through a ProjectReference and has a Scripts folder of something else (task 0096-43):
// nothing is embedded as DbLive scripts, and the project embeds its own files without an error.
workspace.Project("OtherScripts", packages: [], items: """
	<ProjectReference Include="../CustomFolder/CustomFolder.csproj" />
	<EmbeddedResource Include="Scripts\**\*" />
	""")
	.Write("Scripts/jquery.js", "// not DbLive")
	.Write("Scripts/Migrations/001.sql", "insert into t1 values ( 1 );")
	.ExpectNoScripts();

// The block that the .csproj needed before, next to the property: the build fails with a message that says to
// remove the block.
workspace.Project("OldBlock", packages: ["DbLive"], properties: "<DbLiveScriptsFolder>Scripts</DbLiveScriptsFolder>",
	items: """<EmbeddedResource Include="Scripts\**\*" LogicalName="DbLive/%(RecursiveDir)%(Filename)%(Extension)" WithCulture="false" />""")
	.Write("Scripts/Migrations/001.migration.sql", "create table t1 ( id int );")
	.ExpectBuildError("Remove the DbLive block from OldBlock.csproj");

// A test project with the fixture builders (task 0096-30-02): the Testcontainers packages bring the provider and
// Testcontainers, and a fixture is one line. The project names no scripts folder, so nothing is embedded.
workspace.Project("Fixtures", packages: ["DbLive.xunit", "DbLive.Testcontainers.MSSQL", "DbLive.Testcontainers.PostgreSQL"])
	.Write("Fixtures.cs", """
		using DbLive.MSSQL;
		using DbLive.PostgreSQL;
		using DbLive.xunit;
		using System.Reflection;

		public class MsSqlContainerFixture() : DbLiveTestFixture(new MsSqlContainerFixtureBuilder(Assembly.Load("MyDb")), dropDatabaseOnComplete: true);
		public class PostgreSqlContainerFixture() : DbLiveTestFixture(new PostgreSqlContainerFixtureBuilder(Assembly.Load("MyDb"), "postgres:18"), dropDatabaseOnComplete: true);
		public class MsSqlFixture() : DbLiveTestFixture(new MsSqlFixtureBuilder(Assembly.Load("MyDb"), "Server=localhost;Database=master;"), dropDatabaseOnComplete: true);
		public class PostgreSqlFixture() : DbLiveTestFixture(new PostgreSqlFixtureBuilder(Assembly.Load("MyDb"), "Host=localhost;Username=postgres"), dropDatabaseOnComplete: true);
		""")
	.ExpectNoScripts();

// The dotnet new template (task 0096-30), a solution for each provider. The first one takes the default provider,
// SQL Server.
if (Template.Install(workspace))
{
	Template.Test(workspace, "mssql", providerOption: null, provider: "MSSQL",
		sqlTests: ["Code/dbo.GetUser.test.sql", "Tests/user-name-is-required.sql"]);
	Template.Test(workspace, "postgresql", providerOption: "postgresql", provider: "PostgreSQL",
		sqlTests: ["Code/get_user.test.sql", "Tests/user-name-is-required.sql"]);
}

if (Report.Failed)
{
	Report.PrintFailures(workspace.Root);
	return 1;
}
workspace.Delete();
Console.WriteLine("All checks passed.");
return 0;
