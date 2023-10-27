namespace EasyFlow;

public record DeployParameters
{
	public static DeployParameters Default => new();

	public static DeployParameters Breaking => new()
	{
		CreateDbIfNotExists = false,
		DeployBreaking = true,
		DeployCode = false,
		DeployMigrations = false,
		RunTests = false
	};

	/// <summary>
	/// The migration version to deploy. 
	/// If version is specified, Code (SPs/Function) cannot be deployed. Tests cannot be run. 
	/// Since there is no gurantee that Code and Tests are compatible with the deployed version.
	/// NULL value -> latest version.
	/// </summary>
	public int? MaxVersionToDeploy { get; init; }

	public bool DeployCode { get; init; } = true;

	public bool DeployMigrations { get; set; } = true;

	public bool RunTests { get; set; } = true;

	public bool DeployBreaking { get; set; } = false;

	public int NumberOfThreadsForCodeDeploy { get; init; } = 10;
	public int NumberOfThreadsForTestsRun { get; init; } = 10;

	public bool CreateDbIfNotExists { get; init; } = true;
}