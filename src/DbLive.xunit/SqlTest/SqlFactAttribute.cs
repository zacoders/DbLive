using DbLive.xunit.SqlTest;
using Xunit;
using Xunit.Sdk;

namespace DbLive.xunit;

/// <summary>
/// Attribute that is applied to a method to indicate that it is a fact that should be run
/// by the test runner: one test case per SQL test of the project.
/// </summary>

[XunitTestCaseDiscoverer($"DbLive.xunit.SqlTest.{nameof(SqlFactDiscoverer)}", "DbLive.xunit")]
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class SqlFactAttribute : FactAttribute
{
	public required Type TestFixture { get; set; }
}
