using DbLive.Project;
using System.Reflection;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace DbLive.xunit.SqlTest;

internal class SqlFactDiscoverer(IMessageSink diagnosticMessageSink) : IXunitTestCaseDiscoverer
{
	public IMessageSink DiagnosticMessageSink { get; } = diagnosticMessageSink ?? throw new ArgumentNullException(nameof(diagnosticMessageSink));

	public IEnumerable<IXunitTestCase> Discover(
		ITestFrameworkDiscoveryOptions discoveryOptions,
		ITestMethod testMethod,
		IAttributeInfo factAttribute
	)
	{
		var attr = (ReflectionAttributeInfo)factAttribute;

		Type fixtureType = attr.GetNamedArgument<Type>(
			nameof(SqlFactAttribute.TestFixture)
		);

		// Activator.CreateInstance and the cast below need all three: without the check an abstract type or one without
		// a public parameterless constructor throws MissingMethodException, "Exception during discovery" without the fix.
		string? problem =
			!typeof(DbLiveTestFixture).IsAssignableFrom(fixtureType) ? $"does not derive from {nameof(DbLiveTestFixture)}"
			: fixtureType.IsAbstract ? "is abstract"
			: fixtureType.GetConstructor(Type.EmptyTypes) is null ? "has no public parameterless constructor"
			: null;

		if (problem is not null)
		{
			string attributeName = nameof(SqlFactAttribute).Replace("Attribute", "");
			string paramName = nameof(SqlFactAttribute.TestFixture);

			string errorMessage = $$"""
				Invalid type specified for {{paramName}} in {{attributeName}} attribute: {{fixtureType.FullName}} {{problem}}.

				The type must be a public class that derives from {{nameof(DbLiveTestFixture)}}, is not abstract and has a public parameterless constructor.

				Example:

				public class MyTestFixture()
					: {{nameof(DbLiveTestFixture)}}(new MsSqlContainerFixtureBuilder(typeof(MyDbMarker).Assembly), dropDatabaseOnComplete: true);

				public class SqlTests(ITestOutputHelper output, MyTestFixture fixture)
					: IClassFixture<MyTestFixture>
				{
					[{{attributeName}}({{paramName}} = typeof(MyTestFixture))]
					public async Task Sql(string testFileRelativePath)
					{
						...
					}
				}
				""";
			yield return new ExecutionErrorTestCase(
				DiagnosticMessageSink,
				discoveryOptions.MethodDisplayOrDefault(),
				discoveryOptions.MethodDisplayOptionsOrDefault(),
				testMethod,
				errorMessage
			);
			yield break;
		}

		// an exception here (an error of the project) is not caught: xUnit turns it into a failing test,
		// "Exception during discovery", with the exception.
		var fixture = (DbLiveTestFixture)Activator.CreateInstance(fixtureType)!;

#pragma warning disable VSTHRD002 // Avoid problematic synchronous waits

		IReadOnlyCollection<TestItem> tests = fixture
			.GetTestsAsync()
			.GetAwaiter()
			.GetResult();

#pragma warning restore VSTHRD002 // Avoid problematic synchronous waits

		// null without the ProjectDir metadata: the tests run, but "Go to source" does not open the file.
		string? sourceScriptsPath = GetSourceScriptsPath(fixture.GetProjectAssembly());

		foreach (Project.TestItem testItem in tests)
		{
			yield return new SqlXunitTestCase(
				DiagnosticMessageSink,
				testMethod,
				// the relative path uses '/', the full path for the IDE uses the OS separator.
				sourceScriptsPath is null ? null : Path.GetFullPath(Path.Combine(sourceScriptsPath, testItem.FileData.RelativePath)),
				testItem.FileData.RelativePath
			);
		}
	}

	/// <summary>
	/// The scripts folder in the source project, from the assembly metadata that the DbLive package adds:
	/// <c>ProjectDir</c> and <c>DbLiveScriptsFolder</c> (<c>Scripts</c> without it). Null without <c>ProjectDir</c>.
	/// </summary>
	internal static string? GetSourceScriptsPath(Assembly projectAssembly)
	{
		AssemblyMetadataAttribute[] metadata = [.. projectAssembly.GetCustomAttributes<AssemblyMetadataAttribute>()];

		return GetSourceScriptsPath(
			projectDir: metadata.FirstOrDefault(a => a.Key == "ProjectDir")?.Value,
			scriptsFolder: metadata.FirstOrDefault(a => a.Key == "DbLiveScriptsFolder")?.Value
		);
	}

	/// <param name="projectDir">The folder of the SQL project; null or empty when the assembly has no <c>ProjectDir</c> metadata.</param>
	/// <param name="scriptsFolder">Relative to the project, written in the .csproj with '\' or '/'.</param>
	internal static string? GetSourceScriptsPath(string? projectDir, string? scriptsFolder)
	{
		if (string.IsNullOrEmpty(projectDir))
		{
			return null;
		}

		string folder = string.IsNullOrEmpty(scriptsFolder)
			? "Scripts"
			: scriptsFolder.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);

		return Path.Combine(projectDir, folder);
	}
}
