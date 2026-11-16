namespace DbLive;

/// <summary>
/// A script ran longer than its timeout: <c>CodeItemTimeout</c>, <c>MigrationTimeout</c> or the <c>Timeout</c> of a migration,
/// <c>BeforeDeployFolderTimeout</c>, <c>AfterDeployFolderTimeout</c>, <c>UnitTestItemTimeout</c>. The timeout is the timeout of the whole
/// file, on both providers. The timeout error of the provider is the inner exception; there is none when on SQL Server the time of the
/// file ran out between two <c>GO</c> batches.
/// </summary>
[Serializable]
[ExcludeFromCodeCoverage]
public sealed class ExecutionTimeoutException : DbLiveSqlException
{
	/// <summary>
	/// The timeout of the script.
	/// </summary>
	public TimeSpan Timeout { get; }

	internal ExecutionTimeoutException(TimeSpan timeout, Exception? innerException = null)
		: base($"Execution Timeout Expired. The script ran longer than its timeout of {timeout:c}.", "", innerException)
	{
		Timeout = timeout;
	}
}
