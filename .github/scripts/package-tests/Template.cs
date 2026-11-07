using System.Text.RegularExpressions;
using System.Xml.Linq;

/// <summary>
/// The dotnet new template (task 0096-30): installed from its package into the template cache of the workspace, it
/// creates a solution on the DbLive packages of the same build, and the tests of the solution pass.
/// </summary>
static class Template
{
	// A name with a dot and a hyphen: the paths and Assembly.Load take it as it is, the namespaces take Contoso.Billing_Db.
	const string ProjectName = "Contoso.Billing-Db";
	const string TestNamespace = "Contoso.Billing_Db.Tests";

	public static bool Install(Workspace workspace)
	{
		Report.Section("Template");
		string package = Path.Combine(workspace.Packages, $"DbLive.Templates.{workspace.Version}.nupkg");
		var (exitCode, output) = Dotnet.Run("new", "install", package, "--debug:custom-hive", workspace.TemplateHive);

		// dotnet new install exits with 0 also when no template of the package loads, with "No templates were found in
		// the package" (task 0096-39). So the template is looked up by its short name too, and the install output tells why.
		bool installed = exitCode == 0
			&& Dotnet.Run("new", "list", "dblive", "--debug:custom-hive", workspace.TemplateHive).ExitCode == 0;
		if (installed)
			Report.Ok($"installed {Path.GetFileName(package)}");
		else
			Report.Fail($"dotnet new install {package} did not install the template dblive (exit code {exitCode}):{Environment.NewLine}{output}");
		return installed;
	}

	/// <summary>
	/// Creates a solution from the template into Template-&lt;name&gt; and runs its tests.
	/// </summary>
	/// <param name="providerOption">The --provider value, null for the default.</param>
	/// <param name="provider">The suffix of the provider packages: MSSQL, PostgreSQL.</param>
	/// <param name="sqlTests">The SQL test files of the template, each one a test case named after its path.</param>
	public static void Test(Workspace workspace, string name, string? providerOption, string provider, string[] sqlTests)
	{
		Report.Section($"Template {name}");
		string solutionDir = Path.Combine(workspace.Root, $"Template-{name}");
		if (!New(workspace, solutionDir, providerOption))
			return;
		CheckReferences(workspace, solutionDir, provider);
		RunTests(solutionDir, sqlTests);
	}

	static bool New(Workspace workspace, string solutionDir, string? providerOption)
	{
		string[] arguments = ["new", "dblive", "-n", ProjectName, "-o", solutionDir, "--debug:custom-hive", workspace.TemplateHive,
			.. providerOption is null ? Array.Empty<string>() : ["--provider", providerOption]];
		var (exitCode, output) = Dotnet.Run(arguments);
		if (exitCode != 0)
			Report.Fail($"dotnet {string.Join(' ', arguments)} failed (exit code {exitCode}):{Environment.NewLine}{output}");
		return exitCode == 0;
	}

	// The template of this build creates projects on the DbLive packages of this build.
	static void CheckReferences(Workspace workspace, string solutionDir, string provider)
	{
		string[] actual = [.. Directory.GetFiles(solutionDir, "*.csproj", SearchOption.AllDirectories)
			.SelectMany(path => Regex.Matches(File.ReadAllText(path), @"<PackageReference Include=""(DbLive[^""]*)"" Version=""([^""]*)""")
				.Select(m => $"{m.Groups[1].Value} {m.Groups[2].Value}"))
			.Order(StringComparer.Ordinal)];
		string[] expected = [.. new[] { $"DbLive.{provider}", $"DbLive.Testcontainers.{provider}", "DbLive.xunit" }
			.Select(id => $"{id} {workspace.Version}")
			.Order(StringComparer.Ordinal)];
		if (!actual.SequenceEqual(expected))
			Report.Fail($"package references [{string.Join(", ", actual)}], expected [{string.Join(", ", expected)}].");
		else
			Report.Ok(string.Join(", ", actual));
	}

	// The SQL tests and the C# deployment test pass; the results are read from a .trx file.
	static void RunTests(string solutionDir, string[] sqlTests)
	{
		string resultsDir = Path.Combine(solutionDir, "TestResults");
		var (exitCode, output) = Dotnet.Run("test", Path.Combine(solutionDir, $"{ProjectName}.slnx"),
			"--logger", "trx;LogFileName=results.trx", "--results-directory", resultsDir);
		string trx = Path.Combine(resultsDir, "results.trx");
		if (!File.Exists(trx))
		{
			Report.Fail($"no test results, dotnet test failed (exit code {exitCode}):{Environment.NewLine}{output}");
			return;
		}

		XNamespace ns = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010";
		var results = XDocument.Load(trx).Descendants(ns + "UnitTestResult")
			.Select(r => (Name: (string)r.Attribute("testName")!, Outcome: (string)r.Attribute("outcome")!))
			.OrderBy(r => r.Name, StringComparer.Ordinal)
			.ToList();
		string[] expected = [.. sqlTests.Append($"{TestNamespace}.DeploymentTests.ReleaseDeploys").Order(StringComparer.Ordinal)];
		string actual = string.Join(", ", results.Select(r => $"{r.Name}: {r.Outcome}"));
		if (exitCode != 0 || !results.Select(r => r.Name).SequenceEqual(expected) || results.Any(r => r.Outcome != "Passed"))
			Report.Fail($"tests [{actual}], expected [{string.Join(", ", expected)}] to pass (exit code {exitCode}):{Environment.NewLine}{output}");
		else
			Report.Ok($"{results.Count} tests passed: {string.Join(", ", results.Select(r => r.Name))}");
	}
}
