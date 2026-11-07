
namespace DbLive.Deployers.Code;

public class CodeItemDeployResult
{
	public bool IsSuccess { get; init; }
	public Exception? Exception { get; init; }

	/// <summary>
	/// False when a retry of the failed file cannot succeed: the file failed in a transaction that the error aborted
	/// (<see cref="IDbLiveDA.ErrorAbortsTransaction"/>). The deployment then fails with this error at once, not with the error of a retry.
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
