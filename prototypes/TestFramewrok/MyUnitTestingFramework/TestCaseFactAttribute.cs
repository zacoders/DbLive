using Xunit.Sdk;
using Xunit;

namespace MyUnitTestingFramework;

/// <summary>
/// Attribute that is applied to a method to indicate that it is a fact that should be run
/// by the test runner. It can also be extended to support a customized definition of a
/// test method.
/// </summary>

[XunitTestCaseDiscoverer("MyUnitTestingFramework.TestCaseFactDiscoverer", "MyUnitTestingFramework")]
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public class TestCaseFactAttribute : FactAttribute
{
	public TestCaseFactAttribute()
	{
		//DisplayName = "Test123";
	}
}
