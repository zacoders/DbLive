using DbLive.Common;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace EasyFlow.xunit;

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

		var project = new EasyFlowBuilder()
			.SetProjectPath(projectPath)
			.CreateProject();

		string root = project.GetVisualStudioProjectPath();
		foreach (Project.TestItem testItem in project.GetTests())
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
