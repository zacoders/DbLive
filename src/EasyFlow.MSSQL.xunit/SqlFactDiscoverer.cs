using EasyFlow.Common;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace EasyFlow.MSSQL.xunit;

public class SqlFactDiscoverer : IXunitTestCaseDiscoverer
{
	private readonly IMessageSink _diagnosticMessageSink;

	public IMessageSink DiagnosticMessageSink => _diagnosticMessageSink;

	public SqlFactDiscoverer(IMessageSink diagnosticMessageSink)
	{
		_diagnosticMessageSink = diagnosticMessageSink ?? throw new ArgumentNullException(nameof(diagnosticMessageSink));
	}

	public IEnumerable<IXunitTestCase> Discover(
		ITestFrameworkDiscoveryOptions discoveryOptions,
		ITestMethod testMethod,
		IAttributeInfo factAttribute
	)
	{
		//IEnumerable<IAttributeInfo> allAttributes = testMethod.Method.GetCustomAttributes(typeof(TestCaseFactAttribute));
		//var attr = (ReflectionAttributeInfo)allAttributes.Single();

		var attr = (ReflectionAttributeInfo)factAttribute;
		string assemblyName = attr.GetNamedArgument<string>(nameof(SqlFactAttribute.SqlAssemblyName));
		string projectPath = Path.GetFullPath(assemblyName);

		var project = new EasyFlowBuilder()
			.SetProjectPath(projectPath)
			.CreateProject();

		string root = project.GetVisualStudioProjectPath();
		foreach (Project.TestItem testItem in project.GetTests())
		{
			//Add(testItem.FileData.RelativePath); // adding tests to TheoryData base class.
			yield return new SqlXunitTestCase(
				DiagnosticMessageSink,
				testMethod,
				Path.Combine(root, testItem.FileData.RelativePath),
				testItem.FileData.RelativePath
			);
		}

		//foreach (string file in Directory.EnumerateFiles(rootPath, "*.sql", SearchOption.AllDirectories))
		//{
		//	yield return new SqlXunitTestCase(
		//		DiagnosticMessageSink,
		//		testMethod,
		//		file
		//	);
		//}

		//yield return new CustomTestCase(
		//	DiagnosticMessageSink,
		//	testMethod,
		//	@"C:\Data\Code\Personal\EasySqlFlow\prototypes\TestFramewrok\TestProject1\test3.sql"
		//);
	}

}
