
namespace DbLive.Common
{
	public interface ITransactionRunner
	{
		void ExecuteWithinTransaction(bool needTransaction, TranIsolationLevel isolationLevel, TimeSpan timeout, Action action);
	}
}