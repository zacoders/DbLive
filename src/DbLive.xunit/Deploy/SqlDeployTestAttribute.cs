using Xunit;
using Xunit.Sdk;

namespace DbLive.xunit.Deploy;

[XunitTestCaseDiscoverer("DbLive.xunit.Deploy.SqlDeployTestDiscoverer", "DbLive.xunit")]
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class SqlDeployTestAttribute : FactAttribute
{
}
