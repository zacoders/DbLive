namespace EasyFlow.Common;

public class TransactionRunner : ITransactionRunner
{
	public void ExecuteWithinTransaction(bool needTransaction, TranIsolationLevel isolationLevel, TimeSpan timeout, Action action)
	{
		if (!needTransaction)
		{
			action();
			return;
		}

		using TransactionScope _transactionScope = TransactionScopeManager.Create(isolationLevel, timeout);
		action();
		_transactionScope.Complete();
	}
}
