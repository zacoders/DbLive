namespace DbLive.Testing;

[ExcludeFromCodeCoverage]
public sealed class UnitTestsFailedException : Exception
{
	internal UnitTestsFailedException(int failedCount, int totalCount, IReadOnlyList<string> failedTests)
		: base($"Unit test run failed. Failed test count {failedCount} of {totalCount}.")
	{
		FailedCount = failedCount;
		TotalCount = totalCount;
		FailedTests = failedTests;
	}

	public int FailedCount { get; private set; }
	public int TotalCount { get; private set; }
	public IReadOnlyList<string> FailedTests { get; private set; }
}
