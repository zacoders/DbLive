namespace EasyFlow;

public record EasyFlowDeployParameters
{
	public static EasyFlowDeployParameters Default => new();

	/// <summary>
	/// The migration version to deploy. If version is specified code (SPs/Function) cannot be deployed.
	/// NULL value -> latest version.
	/// </summary>
	public int? MaxVersionToDeploy { get; init; }

	public bool DeployCode { get; init; } = true;
	public int NumberOfThreadsForCodeDeploy { get; init; } = 10;
	public bool CreateDbIfNotExists { get; init; } = true;
}