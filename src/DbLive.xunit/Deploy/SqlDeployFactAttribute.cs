using Xunit;
using Xunit.Sdk;

namespace DbLive.xunit.Deploy;

[XunitTestCaseDiscoverer($"DbLive.xunit.Deploy.{nameof(SqlDeployFactDiscoverer)}", "DbLive.xunit")]
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class SqlDeployFactAttribute : FactAttribute
{
}
