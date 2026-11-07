namespace DbLive.Deployers.Testing;

[ExcludeFromCodeCoverage]
public class UnitTestsFailedException(int failedCount, int totalCount, IReadOnlyList<string> failedTests)
	: Exception($"Unit test run failed. Failed test count {failedCount} of {totalCount}.")
{
	public int FailedCount { get; private set; } = failedCount;
	public int TotalCount { get; private set; } = totalCount;
	public IReadOnlyList<string> FailedTests { get; private set; } = failedTests;
}
