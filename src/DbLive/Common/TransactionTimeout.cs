namespace DbLive.Common;

/// <summary>
/// The timeout of a transaction of DbLive and what it limits, for the message of its error: for example
/// <c>new(DeploymentTimeout, "The deployment", "DeploymentTimeout")</c>.
/// </summary>
/// <param name="Value">The timeout of the <see cref="TransactionScope"/>.</param>
/// <param name="Subject">What runs in the transaction, the start of the message: "The deployment".</param>
/// <param name="Setting">The setting that gives the timeout, or null when no setting does.</param>
internal sealed record TransactionTimeout(TimeSpan Value, string Subject, string? Setting = null)
{
	/// <summary>
	/// Whether <paramref name="error"/> of a step in the scope comes from the end of the transaction by this timeout. When the timeout
	/// runs out, System.Transactions aborts the transaction on its timer thread, and the error comes from the next step in the scope, in a
	/// shape that depends on the provider and the step: <see cref="TransactionException"/> when a connection opens,
	/// <see cref="InvalidOperationException"/> of SqlClient for the next batch on the same connection, the cancel of the running command on
	/// PostgreSQL, <see cref="TransactionAbortedException"/> from <see cref="TransactionScope.Dispose"/>. Only the state of the transaction
	/// and the time tell them apart from other errors.
	/// <para>
	/// Not for an error that already is a timeout: a script that ran out of its time first stays the timeout of the script. A migration
	/// gives its script and its transaction the same timeout, and the script usually runs out first.
	/// </para>
	/// </summary>
	/// <param name="transaction">A clone of the transaction of the scope, taken right after the scope was created.</param>
	/// <param name="startedTimestamp"><see cref="Stopwatch.GetTimestamp"/> right before the scope was created.</param>
	/// <param name="error">The error of the step.</param>
	public bool IsCauseOf(Transaction transaction, long startedTimestamp, Exception error)
	{
		// 00:00:00 is no limit.
		return Value != TimeSpan.Zero
			&& transaction.TransactionInformation.Status == TransactionStatus.Aborted
			// a transaction of the application that DbLive joined can end earlier, by a timeout of its own.
			&& Stopwatch.GetElapsedTime(startedTimestamp) >= Value
			&& error.Get<ExecutionTimeoutException>() is null;
	}

	/// <summary>
	/// The timeout error, with the error of the step inside.
	/// </summary>
	public ExecutionTimeoutException Expired(Exception error) => new(Subject, Value, Setting, error);
}
