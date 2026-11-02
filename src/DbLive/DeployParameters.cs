namespace DbLive;

[ExcludeFromCodeCoverage]
public record DeployParameters
{
	public static DeployParameters Default => new()
	{
		CreateDbIfNotExists = false,
		DeployBreaking = false,
		DeployCode = true,
		DeployMigrations = true
	};

	public static DeployParameters Breaking => new()
	{
		CreateDbIfNotExists = false,
		DeployBreaking = true,
		DeployCode = false,
		DeployMigrations = false
	};

	internal bool RecreateDatabase { get; set; } = false;

	public bool DeployCode { get; set; } = true;

	public bool DeployMigrations { get; set; } = true;

	public bool DeployBreaking { get; set; } = false;

	/// <summary>
	/// Creates the database of the connection string if it does not exist.
	/// Off by default: a typo in the database name of a production connection string must fail, not create an empty database.
	/// </summary>
	public bool CreateDbIfNotExists { get; set; } = false;

	/// <summary>
	/// This should be enabled only for testing purposes and should not be used for production.
	/// This will deploy the database changes with undo and changes again to test undo scripts.
	/// This is not what production deployment should do.
	/// </summary>
	internal UndoTestMode UndoTestDeployment { get; set; } = UndoTestMode.None;

	/// <summary>
	/// If true, allows downgrading the database to an earlier version.
	/// It will apply UNDO up to the current version.
	/// </summary>
	public bool AllowDatabaseDowngrade { get; set; } = false;

	/// <summary>
	/// Updates stored checksums for applied migrations whose files no longer match.
	/// Use only after confirming the database schema already matches the project files.
	/// </summary>
	public bool RepairMigrationChecksums { get; set; } = false;
}