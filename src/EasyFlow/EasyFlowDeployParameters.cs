namespace EasyFlow;

public class EasyFlowDeployParameters
{
	public static EasyFlowDeployParameters Default => new();

	/// <summary>
	/// The migration version to deploy. If version is specified code (SPs/Function) cannot be deployed.
	/// NULL value -> latest version.
	/// </summary>
	public int? MaxVersionToDeploy { get; set; }

	public bool DeployCode { get; internal set; } = true;
	public int NumberOfThreadsForCodeDeploy { get; internal set; } = 10;
}