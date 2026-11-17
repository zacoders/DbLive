using Xunit;

namespace DbLive.xunit;

/// <summary>
/// Marks the deployment test of a project: a test without parameters that calls <see cref="DeploymentTest.DeployAsync"/>.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class SqlDeployFactAttribute : FactAttribute
{
}
