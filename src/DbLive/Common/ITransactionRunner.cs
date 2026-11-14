
namespace DbLive.Common;

internal interface ITransactionRunner
{
	Task ExecuteWithinTransactionAsync(
		bool needTransaction,
		TranIsolationLevel isolationLevel,
		TimeSpan timeout,
		Func<Task> action
	);
}