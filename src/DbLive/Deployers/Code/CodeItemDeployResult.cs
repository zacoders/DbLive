
namespace DbLive.Deployers.Code;

public class CodeItemDeployResult
{
	public bool IsSuccess { get; init; }
	public Exception? Exception { get; init; }

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
