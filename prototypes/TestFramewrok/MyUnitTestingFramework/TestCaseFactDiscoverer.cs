using Xunit.Abstractions;
using Xunit.Sdk;

namespace MyUnitTestingFramework;

public class TestCaseFactDiscoverer : IXunitTestCaseDiscoverer
{
	private readonly IMessageSink _diagnosticMessageSink;

	public IMessageSink DiagnosticMessageSink => _diagnosticMessageSink;

	public TestCaseFactDiscoverer(IMessageSink diagnosticMessageSink)
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
		string rootPath = attr.GetNamedArgument<string>("RootPath");
				
		foreach (string file in Directory.EnumerateFiles(rootPath, "*.sql", SearchOption.AllDirectories))
		{
			yield return new CustomTestCase(
				DiagnosticMessageSink,
				testMethod,
				file
			);
		}

		//yield return new CustomTestCase(
		//	DiagnosticMessageSink,
		//	testMethod,
		//	@"C:\Data\Code\Personal\EasySqlFlow\prototypes\TestFramewrok\TestProject1\test3.sql"
		//);
	}

}
