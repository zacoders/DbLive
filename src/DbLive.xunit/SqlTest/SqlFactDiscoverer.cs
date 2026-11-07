using DbLive.Project;
using System.Reflection;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace DbLive.xunit.SqlTest;

public class SqlFactDiscoverer(IMessageSink diagnosticMessageSink) : IXunitTestCaseDiscoverer
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

		if (!typeof(DbLiveTestFixture).IsAssignableFrom(fixtureType))
		{
			string attributeName = nameof(SqlFactAttribute).Replace("Attribute", "");
			string paramName = nameof(SqlFactAttribute.TestFixture);
			string spaces = new(' ', paramName.Length + attributeName.Length);

			string errorMessage = $$"""
				Invalid type specified for {{paramName}} in {{attributeName}} attribute.

				The type must inherit from: {{nameof(DbLiveTestFixture)}}.
				Actual type: {{fixtureType.FullName}}.

				Example:

				class MyTestFixture : {{nameof(DbLiveTestFixture)}}
				{
					...
				}
				{{spaces}}            🡳🡳🡳
				[{{attributeName}}({{paramName}} = typeof(MyTestFixture))]
				public async Task Sql(string testRelativePath)
				{
					...
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

		var fixture = (DbLiveTestFixture)Activator.CreateInstance(fixtureType)!;
		var project = fixture.GetProject();

#pragma warning disable VSTHRD002 // Avoid problematic synchronous waits

		IReadOnlyCollection<TestItem> tests = project
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
