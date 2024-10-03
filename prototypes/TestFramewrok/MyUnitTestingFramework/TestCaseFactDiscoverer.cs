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
		yield return new CustomTestCase(
			DiagnosticMessageSink,
			testMethod,
			@"C:\Data\Code\Personal\EasySqlFlow\prototypes\TestFramewrok\TestProject1\test1.sql"
		);

 		yield return new CustomTestCase(
			DiagnosticMessageSink,
			testMethod,
			@"C:\Data\Code\Personal\EasySqlFlow\prototypes\TestFramewrok\TestProject1\test2.sql"
		);

		yield return new CustomTestCase(
			DiagnosticMessageSink,
			testMethod,
			@"C:\Data\Code\Personal\EasySqlFlow\prototypes\TestFramewrok\TestProject1\test3.sql"
		);
	}

}
