
namespace DbLive.Common;

internal interface ITransactionRunner
{
	/// <summary>
	/// Runs <paramref name="action"/> in a <see cref="TransactionScope"/> when <paramref name="needTransaction"/> is true, otherwise
	/// without one.
	/// </summary>
	/// <exception cref="ExecutionTimeoutException">The transaction ran longer than <paramref name="timeout"/>, and a step in it failed;
	/// the error of the step is the inner exception.</exception>
	Task ExecuteWithinTransactionAsync(
		bool needTransaction,
		TranIsolationLevel isolationLevel,
		TransactionTimeout timeout,
		Func<Task> action
	);
}
