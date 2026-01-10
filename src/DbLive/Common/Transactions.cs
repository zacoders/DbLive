namespace DbLive.Common;

public class TransactionRunner : ITransactionRunner
{
	public async Task ExecuteWithinTransactionAsync(
		bool needTransaction,
		TranIsolationLevel isolationLevel,
		TimeSpan timeout,
		Func<Task> action
	)
	{
		if (!needTransaction)
		{
			await action().ConfigureAwait(false);
			return;
		}

		using TransactionScope _transactionScope = TransactionScopeManager.Create(isolationLevel, timeout);
		await action().ConfigureAwait(false);
		_transactionScope.Complete();
	}
}
