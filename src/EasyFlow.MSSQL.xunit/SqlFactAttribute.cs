using Xunit.Sdk;
using Xunit;

namespace EasyFlow.MSSQL.xunit;

/// <summary>
/// Attribute that is applied to a method to indicate that it is a fact that should be run
/// by the test runner. It can also be extended to support a customized definition of a
/// test method.
/// </summary>

[XunitTestCaseDiscoverer("EasyFlow.MSSQL.xunit.SqlFactDiscoverer", "EasyFlow.MSSQL.xunit")]
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public class SqlFactAttribute : FactAttribute
{
	public required string SqlAssemblyName { get; set; }
}
