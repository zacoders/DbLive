namespace DbLive.Testing;

/// <summary>
/// The result of one SQL test, from <see cref="IDbLiveTester.RunTestAsync"/>.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class TestRunResult
{
	internal TestRunResult()
	{
	}

	/// <summary>
	/// True when the test passed: it ran without an error and its assertions held.
	/// </summary>
	public bool IsSuccess { get; internal set; }

	/// <summary>
	/// Why the test failed: the assertion that did not hold, the error of the test or of its setup file, a <c>COMMIT</c> or
	/// <c>ROLLBACK</c> that ended the transaction of the test, or on SQL Server a transaction that the test left open
	/// (<c>BEGIN TRANSACTION</c> without <c>COMMIT</c>, also in its setup file or a procedure they call). Null when it passed.
	/// </summary>
	public string? ErrorMessage { get; internal set; }
	internal DateTime StartedUtc { get; set; }
	internal DateTime CompletedUtc { get; set; }

	internal long ExecutionTimeMs { get; set; }

	/// <summary>
	/// The exception of a test that failed with an error, usually a <see cref="DbLiveSqlException"/>.
	/// Null when it passed, and when it failed without an exception: an assertion did not hold, the transaction ended, or the test left
	/// a transaction open.
	/// </summary>
	public Exception? Exception { get; internal set; }
	internal string Output { get; set; } = "";
}
