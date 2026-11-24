namespace DbLive;

/// <summary>
/// A script ran longer than its timeout: <c>CodeItemTimeout</c>, <c>MigrationTimeout</c> or the <c>Timeout</c> of a migration,
/// <c>BeforeDeployFolderTimeout</c>, <c>AfterDeployFolderTimeout</c>, <c>UnitTestItemTimeout</c>. The timeout is the timeout of the whole
/// file, on both providers. The error of the provider is the inner exception: its timeout on SQL Server, the cancel of the command on
/// PostgreSQL (<see cref="OperationCanceledException"/>); there is none when on SQL Server the time of the file ran out between two
/// <c>GO</c> batches.
/// <para>
/// Or a transaction of DbLive ran longer than its timeout: the deployment (<c>DeploymentTimeout</c>), a migration or breaking script in
/// its own transaction, a test (twice <c>UnitTestItemTimeout</c>), the self-deployment of DbLive. The message says which, and the inner
/// exception is the error of the step that failed after the end of the transaction.
/// </para>
/// </summary>
[Serializable]
[ExcludeFromCodeCoverage]
public sealed class ExecutionTimeoutException : DbLiveSqlException
{
	/// <summary>
	/// The timeout that ran out: of the script, or of the transaction.
	/// </summary>
	public TimeSpan Timeout { get; }

	internal ExecutionTimeoutException(TimeSpan timeout, Exception? innerException = null)
		: this("The script", timeout, null, innerException)
	{
	}

	/// <param name="subject">What ran out of its time, for example "The deployment".</param>
	/// <param name="timeout">The timeout that ran out.</param>
	/// <param name="setting">The setting that gives the timeout, or null when no setting does.</param>
	/// <param name="innerException">The error of the provider or of the step that failed.</param>
	internal ExecutionTimeoutException(string subject, TimeSpan timeout, string? setting, Exception? innerException)
		: base(
			$"Execution Timeout Expired. {subject} ran longer than its timeout of {timeout:c}{(setting is null ? "" : $" ({setting})")}.",
			"",
			innerException
		)
	{
		Timeout = timeout;
	}
}
