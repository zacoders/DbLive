namespace EasyFlow;

[ExcludeFromCodeCoverage]
public record DeployParameters
{
	public static DeployParameters Default => new()
	{
		CreateDbIfNotExists = true,
		DeployBreaking = false,
		DeployCode = true,
		DeployMigrations = true,
		RunTests = true
	};

	public static DeployParameters Breaking => new()
	{
		CreateDbIfNotExists = false,
		DeployBreaking = true,
		DeployCode = false,
		DeployMigrations = false,
		RunTests = false
	};

	public static DeployParameters BreakingAndTests => new()
	{
		CreateDbIfNotExists = false,
		DeployBreaking = true,
		DeployCode = false,
		DeployMigrations = false,
		RunTests = true
	};

	/// <summary>
	/// The migration version to deploy. 
	/// If version is specified, Code (SPs/Function) cannot be deployed. Tests cannot be run. 
	/// Since there is no gurantee that Code and Tests are compatible with the deployed version.
	/// NULL value -> latest version.
	/// </summary>
	public int? MaxVersionToDeploy { get; set; }

	public bool DeployCode { get; set; } = true;

	public bool DeployMigrations { get; set; } = true;

	public bool RunTests { get; set; } = true;

	public bool DeployBreaking { get; set; } = false;

	public int NumberOfThreadsForCodeDeploy { get; set; } = 10;
	public int NumberOfThreadsForTestsRun { get; set; } = 10;

	public bool CreateDbIfNotExists { get; set; } = true;
}