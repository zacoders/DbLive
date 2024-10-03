using Xunit.Sdk;
using Xunit;

namespace MyUnitTestingFramework;

/// <summary>
/// Attribute that is applied to a method to indicate that it is a fact that should be run
/// by the test runner. It can also be extended to support a customized definition of a
/// test method.
/// </summary>

[XunitTestCaseDiscoverer("MyUnitTestingFramework.TestCaseFactDiscoverer", "MyUnitTestingFramework")]
public class TestCaseFactAttribute : FactAttribute
{
	public TestCaseFactAttribute()
	{ }
}

////[MyXunitTestCaseDiscoverer(typeof(TestCaseFactDiscoverer))]
//[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
//public class MyFactAttribute : Attribute, IFactAttribute
//{
//	/// <inheritdoc/>
//	public string? DisplayName { get; set; }

//	/// <inheritdoc/>
//	public bool Explicit { get; set; }

//	/// <inheritdoc/>
//	public string? Skip { get; set; }

//	/// <inheritdoc/>
//	public Type? SkipType { get; set; }

//	/// <inheritdoc/>
//	public string? SkipUnless { get; set; }

//	/// <inheritdoc/>
//	public string? SkipWhen { get; set; }

//	/// <inheritdoc/>
//	public int Timeout { get; set; }
//}
