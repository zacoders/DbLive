namespace DbLive.Common;

internal class TransactionRunner : ITransactionRunner
{
	public async Task ExecuteWithinTransactionAsync(
		bool needTransaction,
		TranIsolationLevel isolationLevel,
		TransactionTimeout timeout,
		Func<Task> action
	)
	{
		if (!needTransaction)
		{
			await action().ConfigureAwait(false);
			return;
		}

		long started = Stopwatch.GetTimestamp();
		using TransactionScope transactionScope = TransactionScopeManager.Create(isolationLevel, timeout.Value);
		// a clone of its own: the scope disposes its transaction, and the check below reads it also after the Dispose() of the scope.
		using Transaction transaction = Transaction.Current!.Clone();

		try
		{
			await action().ConfigureAwait(false);
			transactionScope.Complete();
			// inside the try: a transaction that ended after the action throws TransactionAbortedException here.
			transactionScope.Dispose();
		}
		catch (Exception ex) when (timeout.IsCauseOf(transaction, started, ex))
		{
			throw timeout.Expired(ex);
		}
	}
}
