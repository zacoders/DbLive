namespace DbLive.Testing;

/// <summary>
/// SQL tests failed in <see cref="IDbLiveTester.RunAllTestsAsync"/>. All tests ran; their results are in the log and in
/// <c>dblive.unit_test_run</c>.
/// </summary>
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

	/// <summary>
	/// The number of failed tests.
	/// </summary>
	public int FailedCount { get; private set; }

	/// <summary>
	/// The number of tests that ran.
	/// </summary>
	public int TotalCount { get; private set; }

	/// <summary>
	/// The relative paths of the failed test files, sorted.
	/// </summary>
	public IReadOnlyList<string> FailedTests { get; private set; }
}
