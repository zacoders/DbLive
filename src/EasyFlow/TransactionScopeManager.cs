namespace EasyFlow;

public static class TransactionScopeManager
{
	//private static readonly TransactionOptions _defaultTransactionOptions = new ()
	//{
	//	IsolationLevel = IsolationLevel.ReadCommitted,
	//	Timeout = TransactionManager.DefaultTimeout
	//};

	///// <summary>
	///// Creates TransactionScopes with default options:
	/////  * IsolationLevel = IsolationLevel.ReadCommitted
	/////  * Timeout = DefaultTimeout(1 minute)
	/////  * TransactionScopeOption: Required(A transaction is required by the
	/////    scope. It uses an ambient transaction if one already exists.Otherwise, it creates
	/////    a new transaction before entering the scope.This is the default value.)
	/////  * AsyncFlowOption: Enabled(Specifies that transaction flow across thread continuations is enabled.)
	///// </summary>
	//public static TransactionScope Create()
	//{
	//	return new TransactionScope(TransactionScopeOption.Required, _defaultTransactionOptions, TransactionScopeAsyncFlowOption.Enabled);
	//}

	public static TransactionScope Create(TranIsolationLevel isolationLevel, TimeSpan timeOut)
	{
		// TransactionManager.ImplicitDistributedTransactions = false;
		TransactionOptions _options = new()
		{
			IsolationLevel = isolationLevel.ToSystemTransaction(),
			Timeout = timeOut
		};
		return new TransactionScope(TransactionScopeOption.Required, _options, TransactionScopeAsyncFlowOption.Enabled);
	}
}