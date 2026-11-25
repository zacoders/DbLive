
namespace DbLive.Deployers.Code;

internal class CodeItemDeployResult
{
	public bool IsSuccess { get; init; }
	public Exception? Exception { get; init; }

	/// <summary>
	/// False when the file failed in a transaction that its error ended (<see cref="IDbLiveDA.IsTransactionUsableAsync"/>):
	/// no later statement may run; and when the file ended the transaction it ran in with COMMIT or ROLLBACK
	/// (<see cref="DbLiveTransactionEndedException"/>) or left a transaction open (<see cref="DbLiveTransactionLeftOpenException"/>),
	/// which it does on every run; and when the file failed inside a transaction it began, which the data access rolled back with the
	/// deployment transaction (<see cref="DbLiveSqlException.RolledBackWithTransactionLeftOpen"/>). The deployment then fails with this
	/// error at once, not with the error of a retry.
	/// </summary>
	public bool CanRetry { get; init; } = true;

	public static CodeItemDeployResult Success()
	{
		return new CodeItemDeployResult
		{
			IsSuccess = true
		};
	}

	public static CodeItemDeployResult Failed(Exception exception)
	{
		return new CodeItemDeployResult
		{
			IsSuccess = false,
			Exception = exception
		};
	}
}
