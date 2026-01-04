namespace DbLive;

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

	internal bool RecreateDatabase { get; set; } = false;

	public bool DeployCode { get; set; } = true;

	public bool DeployMigrations { get; set; } = true;

	public bool RunTests { get; set; } = true;

	public bool DeployBreaking { get; set; } = false;

	public bool CreateDbIfNotExists { get; set; } = true;

	/// <summary>
	/// This should be enabled only for testing purposes and should not be used for production.
	/// This will deploy the database changes with undo and changes again to test undo scripts.
	/// This is not what production deployment should do.
	/// </summary>
	public UndoTestMode UndoTestDeployment { get; set; } = UndoTestMode.None;

	/// <summary>
	/// If true, allows downgrading the database to an earlier version.
	/// It will apply UNDO up to the current version.
	/// </summary>
	public bool AllowDatabaseDowngrade { get; set; } = false;
}