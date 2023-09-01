using System.Transactions;

namespace EasyFlow;
//
// Summary:
//     Creates TransactionScopes with default options which which are most suitable
//     for sql server. * IsolationLevel = IsolationLevel.ReadCommitted * Timeout = DefaultTimeout
//     (1 minute) * TransactionScopeOption: Required (A transaction is required by the
//     scope. It uses an ambient transaction if one already exists. Otherwise, it creates
//     a new transaction before entering the scope. This is the default value.) * AsyncFlowOption:
//     Enabled (Specifies that transaction flow across thread continuations is enabled.)
public static class TransactionScopeManager
{
	private static readonly TransactionOptions _defaultTransactionOptions = new TransactionOptions
	{
		IsolationLevel = IsolationLevel.ReadCommitted,
		Timeout = TransactionManager.DefaultTimeout
	};

	//
	// Summary:
	//     Creates TransactionScope with default params. TransactionScope will have the
	//     following params: * IsolationLevel = IsolationLevel.ReadCommitted * Timeout =
	//     DefaultTimeout (1 minute) * TransactionScopeOption: Required (A transaction is
	//     required by the scope. It uses an ambient transaction if one already exists.
	//     Otherwise, it creates a new transaction before entering the scope. This is the
	//     default value.) * AsyncFlowOption: Enabled (Specifies that transaction flow across
	//     thread continuations is enabled.)
	//
	// Returns:
	//     Transaction scope.
	public static TransactionScope Create()
	{
		return new TransactionScope(TransactionScopeOption.Required, _defaultTransactionOptions, TransactionScopeAsyncFlowOption.Enabled);
	}

	//
	// Summary:
	//     Creates TransactionScope with transaction options specified. TransactionScope
	//     will have the following params: * Timeout = DefaultTimeout (1 minute) * TransactionScopeOption:
	//     Required (A transaction is required by the scope. It uses an ambient transaction
	//     if one already exists. Otherwise, it creates a new transaction before entering
	//     the scope. This is the default value.) * AsyncFlowOption: Enabled (Specifies
	//     that transaction flow across thread continuations is enabled.)
	//
	// Parameters:
	//   transactionOptions:
	//     Contains additional information that specifies transaction behavior.
	//
	// Returns:
	//     Transaction scope.
	public static TransactionScope Create(TransactionOptions transactionOptions)
	{
		TransactionManager.ImplicitDistributedTransactions = false;
		return new TransactionScope(TransactionScopeOption.Required, transactionOptions, TransactionScopeAsyncFlowOption.Enabled);
	}

	//
	// Summary:
	//     Creates TransactionScope with transaction and async flow options specified. TransactionScope
	//     will have the following params: * TransactionScopeOption: Required (A transaction
	//     is required by the scope. It uses an ambient transaction if one already exists.
	//     Otherwise, it creates a new transaction before entering the scope. This is the
	//     default value.)
	//
	// Parameters:
	//   transactionOptions:
	//
	//   asyncFlowOption:
	//
	// Returns:
	//     Transaction scope.
	public static TransactionScope Create(TransactionOptions transactionOptions, TransactionScopeAsyncFlowOption asyncFlowOption)
	{
		return new TransactionScope(TransactionScopeOption.Required, transactionOptions, asyncFlowOption);
	}
}