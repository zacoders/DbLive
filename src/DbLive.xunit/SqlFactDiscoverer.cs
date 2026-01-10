using DbLive.Common;
using DbLive.Project;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace DbLive.xunit;

public class SqlFactDiscoverer : IXunitTestCaseDiscoverer
{
	public IMessageSink DiagnosticMessageSink { get; }

	public SqlFactDiscoverer(IMessageSink diagnosticMessageSink)
	{
		DiagnosticMessageSink = diagnosticMessageSink ?? throw new ArgumentNullException(nameof(diagnosticMessageSink));
	}

	public IEnumerable<IXunitTestCase> Discover(
		ITestFrameworkDiscoveryOptions discoveryOptions,
		ITestMethod testMethod,
		IAttributeInfo factAttribute
	)
	{
		var attr = (ReflectionAttributeInfo)factAttribute;
		string assemblyName = attr.GetNamedArgument<string>(nameof(SqlFactAttribute.SqlAssemblyName));
		string projectPath = Path.GetFullPath(assemblyName);

		IDbLiveProject project = new DbLiveBuilder()
			.SetProjectPath(projectPath)
			.CreateProject();


		string root = project
			.GetVisualStudioProjectPathAsync()
			.GetAwaiter()
			.GetResult();

		IReadOnlyCollection<TestItem> tests = project
			.GetTestsAsync()
			.GetAwaiter()
			.GetResult();

		foreach (Project.TestItem testItem in tests)
		{
			yield return new SqlXunitTestCase(
				DiagnosticMessageSink,
				testMethod,
				Path.Combine(root, testItem.FileData.RelativePath),
				testItem.FileData.RelativePath
			);
		}
	}
}
