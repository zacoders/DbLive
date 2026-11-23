namespace DbLive.Testing;

/// <summary>
/// SQL tests failed in <see cref="IDbLiveTester.RunAllTestsAsync"/>. All tests ran. The message names the first ten failed tests by
/// relative path, <see cref="FailedTests"/> has all of them; why each one failed is in the log.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class UnitTestsFailedException : Exception
{
	/// <summary>
	/// The number of failed tests the message names; the rest are counted.
	/// </summary>
	internal const int MaxNamedTests = 10;

	internal UnitTestsFailedException(int failedCount, int totalCount, IReadOnlyList<string> failedTests)
		: base(
			$"Unit test run failed. Failed test count {failedCount} of {totalCount}: "
			+ string.Join(", ", failedTests.Take(MaxNamedTests).Select(p => $"'{p}'"))
			+ (failedTests.Count > MaxNamedTests ? $" and {failedTests.Count - MaxNamedTests} more" : "")
			+ "."
		)
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
