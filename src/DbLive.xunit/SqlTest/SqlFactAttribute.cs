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
	/// <summary>
	/// The fixture class of the test class: derived from <see cref="DbLiveTestFixture"/>, with a public parameterless constructor.
	/// Discovery creates it to read the SQL tests from the project assembly of its fixture builder.
	/// </summary>
	public required Type TestFixture { get; set; }
}
