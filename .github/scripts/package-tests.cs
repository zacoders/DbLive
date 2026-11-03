// Builds projects that reference the packed DbLive packages from a local feed, the way a user project does,
// and checks what the packages add to them: the DbLive package embeds the scripts folder without a block in the
// .csproj (task 0096-30-01), and the fixture builders compile with their packages (task 0096-30-02).
//
//   dotnet pack src/DbLive/DbLive.csproj -o <folder>              (and the other packages, see cicd.yml)
//   dotnet run .github/scripts/package-tests.cs -- --packages <folder>
//
// --packages <folder>   the folder with the .nupkg files of one build
//
// The projects are created in a temporary folder, outside the repository, with their own NuGet cache, so a package
// rebuilt with the same version is not taken from the global cache. Fails (exit code 1) when a check fails, and
// keeps the folder for inspection.

// Reads the built assemblies by reflection, which the AOT analyzers of a file-based app warn about.
#:property PublishAot=false

using System.Diagnostics;
using System.Reflection;
using System.Runtime.Loader;
using System.Text.RegularExpressions;

string? packages = null;
for (int i = 0; i < args.Length; i++)
{
	string Value() => i + 1 < args.Length ? args[++i] : throw new ArgumentException($"{args[i]} needs a value.");
	switch (args[i])
	{
		case "--packages": packages = Path.GetFullPath(Value()); break;
		default: throw new ArgumentException($"Unknown argument '{args[i]}'.");
	}
}
if (packages is null)
	throw new ArgumentException("--packages <folder> is required.");

// DbLive.<version>.nupkg; DbLive.MSSQL.<version>.nupkg does not match, a version starts with a digit.
string[] versions = [.. Directory.GetFiles(packages, "DbLive.*.nupkg")
	.Select(path => Regex.Match(Path.GetFileName(path), @"^DbLive\.(\d.*)\.nupkg$"))
	.Where(m => m.Success)
	.Select(m => m.Groups[1].Value)];
if (versions.Length != 1)
	throw new InvalidOperationException($"Expected one DbLive package in {packages}, found {versions.Length}.");
string version = versions[0];

string root = Directory.CreateTempSubdirectory("dblive-package-tests-").FullName;
Console.WriteLine($"DbLive {version} from {packages}, projects in {root}");

// Empty files stop MSBuild from looking for these files in the folders above.
foreach (string name in new[] { "Directory.Build.props", "Directory.Build.targets", "Directory.Packages.props" })
	Write(name, "<Project />");
Write("nuget.config", $"""
	<?xml version="1.0" encoding="utf-8"?>
	<configuration>
	  <config>
	    <add key="globalPackagesFolder" value="{Path.Combine(root, ".packages")}" />
	  </config>
	  <packageSources>
	    <clear />
	    <add key="local" value="{packages}" />
	    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
	  </packageSources>
	  <packageSourceMapping>
	    <packageSource key="local">
	      <package pattern="DbLive" />
	      <package pattern="DbLive.*" />
	    </packageSource>
	    <packageSource key="nuget.org">
	      <package pattern="*" />
	    </packageSource>
	  </packageSourceMapping>
	</configuration>
	""");

var failures = new List<string>();

// Layout A: scripts and deployer in one project, DbLive comes through the provider package.
Project("Provider", """<PackageReference Include="DbLive.PostgreSQL" Version="$(DbLiveVersion)" />""");
Write("Provider/Scripts/settings.json", """{ "ProjectId": "package-tests" }""");
Write("Provider/Scripts/Migrations/001.migration.sql", "create table t1 ( id int );");
Write("Provider/Scripts/Code/proc.fr.sql", "select 1;"); // a culture name: without WithCulture="false" in a satellite assembly
Write("Provider/Scripts/Code/sub/f.sql", "select 2;");
Check("Provider", builds: true, resources: ["settings.json", "Migrations/001.migration.sql", "Code/proc.fr.sql", "Code/sub/f.sql"], scriptsFolder: "Scripts");

// Layout B: a project with only scripts references DbLive, in a folder named in the .csproj.
// A Scripts folder next to it is not embedded.
Project("CustomFolder", """<PackageReference Include="DbLive" Version="$(DbLiveVersion)" />""", "<DbLiveScriptsFolder>Db</DbLiveScriptsFolder>");
Write("CustomFolder/Db/Migrations/001.migration.sql", "create table t1 ( id int );");
Write("CustomFolder/Scripts/other.sql", "select 1;");
Check("CustomFolder", builds: true, resources: ["Migrations/001.migration.sql"], scriptsFolder: "Db");

// Layout B: the deployer references the SQL project and the provider, and has no scripts folder: it gets nothing.
Project("Deployer", """
	<PackageReference Include="DbLive.PostgreSQL" Version="$(DbLiveVersion)" />
	    <ProjectReference Include="../CustomFolder/CustomFolder.csproj" />
	""");
Check("Deployer", builds: true, resources: [], scriptsFolder: null);

// Embedding turned off: the Scripts folder holds other files.
Project("EmbedOff", """<PackageReference Include="DbLive" Version="$(DbLiveVersion)" />""", "<DbLiveEmbedScripts>false</DbLiveEmbedScripts>");
Write("EmbedOff/Scripts/other.sql", "select 1;");
Check("EmbedOff", builds: true, resources: [], scriptsFolder: null);

// The block that the .csproj needed before: the build fails with a message that says to remove it.
Project("OldBlock", """
	<PackageReference Include="DbLive" Version="$(DbLiveVersion)" />
	    <EmbeddedResource Include="Scripts\**\*" LogicalName="DbLive/%(RecursiveDir)%(Filename)%(Extension)" WithCulture="false" />
	""");
Write("OldBlock/Scripts/Migrations/001.migration.sql", "create table t1 ( id int );");
Check("OldBlock", builds: false, resources: [], scriptsFolder: null, error: "Remove the DbLive block from OldBlock.csproj");

// A test project with the fixture builders (task 0096-30-02): the Testcontainers packages bring the provider and
// Testcontainers, and a fixture is one line. The project has no Scripts folder, so nothing is embedded.
Project("Fixtures", """
	<PackageReference Include="DbLive.xunit" Version="$(DbLiveVersion)" />
	    <PackageReference Include="DbLive.Testcontainers.MSSQL" Version="$(DbLiveVersion)" />
	    <PackageReference Include="DbLive.Testcontainers.PostgreSQL" Version="$(DbLiveVersion)" />
	""");
Write("Fixtures/Fixtures.cs", """
	using DbLive.MSSQL;
	using DbLive.PostgreSQL;
	using DbLive.xunit.SqlTest;
	using System.Reflection;

	public class MsSqlContainerFixture() : DbLiveTestFixture(new MsSqlContainerFixtureBuilder(Assembly.Load("MyDb")), dropDatabaseOnComplete: true);
	public class PostgreSqlContainerFixture() : DbLiveTestFixture(new PostgreSqlContainerFixtureBuilder(Assembly.Load("MyDb"), "postgres:17"), dropDatabaseOnComplete: true);
	public class MsSqlFixture() : DbLiveTestFixture(new MsSqlFixtureBuilder(Assembly.Load("MyDb"), "Server=localhost;Database=master;"), dropDatabaseOnComplete: true);
	public class PostgreSqlFixture() : DbLiveTestFixture(new PostgreSqlFixtureBuilder(Assembly.Load("MyDb"), "Host=localhost;Username=postgres"), dropDatabaseOnComplete: true);
	""");
Check("Fixtures", builds: true, resources: [], scriptsFolder: null);

if (failures.Count > 0)
{
	Console.Error.WriteLine();
	Console.Error.WriteLine($"{failures.Count} check(s) failed, projects kept in {root}:");
	foreach (string failure in failures)
		Console.Error.WriteLine($"  {failure}");
	return 1;
}

Directory.Delete(root, recursive: true);
Console.WriteLine("All checks passed.");
return 0;


void Write(string relativePath, string content)
{
	string path = Path.Combine(root, relativePath);
	Directory.CreateDirectory(Path.GetDirectoryName(path)!);
	File.WriteAllText(path, content);
}

void Project(string name, string items, string properties = "") => Write($"{name}/{name}.csproj", $"""
	<Project Sdk="Microsoft.NET.Sdk">
	  <PropertyGroup>
	    <TargetFramework>net10.0</TargetFramework>
	    <DbLiveVersion>{version}</DbLiveVersion>
	    {properties}
	  </PropertyGroup>
	  <ItemGroup>
	    {items}
	  </ItemGroup>
	</Project>
	""");

// resources: the expected script paths, without the DbLive/ prefix. scriptsFolder: the expected DbLiveScriptsFolder
// metadata, which comes with the ProjectDir metadata; null: neither.
void Check(string name, bool builds, string[] resources, string? scriptsFolder, string? error = null)
{
	string projectDir = Path.Combine(root, name);
	string outputDir = Path.Combine(projectDir, "bin", "Debug", "net10.0");
	Console.WriteLine();
	Console.WriteLine($"== {name}");

	(int exitCode, string output) = Run("dotnet", "build", projectDir, "-nologo", "-v:q");

	void Fail(string message)
	{
		failures.Add($"{name}: {message}");
		Console.WriteLine($"   FAIL: {message}");
	}

	if (!builds)
	{
		if (exitCode == 0)
			Fail("the build passed, expected it to fail.");
		else if (error is not null && !output.Contains(error, StringComparison.Ordinal))
			Fail($"the build failed without '{error}':{Environment.NewLine}{output}");
		else
			Console.WriteLine($"   ok: the build failed with '{error}'");
		return;
	}
	if (exitCode != 0)
	{
		Fail($"the build failed (exit code {exitCode}):{Environment.NewLine}{output}");
		return;
	}

	// Loaded from bytes in a context of its own: the file is not locked, and the metadata is read without
	// creating the attributes.
	var context = new AssemblyLoadContext(name, isCollectible: true);
	try
	{
		Assembly assembly = context.LoadFromStream(new MemoryStream(File.ReadAllBytes(Path.Combine(outputDir, $"{name}.dll"))));

		string[] actual = [.. assembly.GetManifestResourceNames()
			.Where(r => r.StartsWith("DbLive/", StringComparison.Ordinal))
			.Select(r => r["DbLive/".Length..].Replace('\\', '/'))
			.Order(StringComparer.Ordinal)];
		string[] expected = [.. resources.Order(StringComparer.Ordinal)];
		if (!actual.SequenceEqual(expected))
			Fail($"resources DbLive/[{string.Join(", ", actual)}], expected DbLive/[{string.Join(", ", expected)}].");
		else
			Console.WriteLine($"   ok: {actual.Length} script(s) embedded as DbLive/...");

		var metadata = assembly.GetCustomAttributesData()
			.Where(a => a.AttributeType.FullName == "System.Reflection.AssemblyMetadataAttribute")
			.ToDictionary(a => (string)a.ConstructorArguments[0].Value!, a => (string?)a.ConstructorArguments[1].Value);
		metadata.TryGetValue("ProjectDir", out string? actualProjectDir);
		metadata.TryGetValue("DbLiveScriptsFolder", out string? actualScriptsFolder);
		string? expectedProjectDir = scriptsFolder is null ? null : projectDir + Path.DirectorySeparatorChar;
		if (!string.Equals(actualProjectDir, expectedProjectDir, StringComparison.OrdinalIgnoreCase)
			|| actualScriptsFolder != scriptsFolder)
			Fail($"metadata ProjectDir '{actualProjectDir}', DbLiveScriptsFolder '{actualScriptsFolder}'; expected '{expectedProjectDir}', '{scriptsFolder}'.");
		else
			Console.WriteLine(scriptsFolder is null ? "   ok: no ProjectDir metadata" : "   ok: ProjectDir and DbLiveScriptsFolder metadata");
	}
	finally
	{
		context.Unload();
	}

	// name.fr.sql must stay in the assembly, not go to a satellite assembly in fr/.
	if (Directory.Exists(Path.Combine(outputDir, "fr")))
		Fail("a satellite assembly folder fr/ was created.");
}

static (int ExitCode, string Output) Run(string fileName, params string[] arguments)
{
	var info = new ProcessStartInfo(fileName) { RedirectStandardOutput = true, RedirectStandardError = true };
	foreach (string argument in arguments)
		info.ArgumentList.Add(argument);

	using var process = Process.Start(info)!;
	Task<string> error = process.StandardError.ReadToEndAsync();
	string output = process.StandardOutput.ReadToEnd();
	process.WaitForExit();
	return (process.ExitCode, output + error.Result);
}
