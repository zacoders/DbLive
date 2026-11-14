namespace DbLive;

[ExcludeFromCodeCoverage]
public sealed record DeployParameters
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

	internal bool RecreateDatabase { get; init; } = false;

	public bool DeployCode { get; init; } = true;

	public bool DeployMigrations { get; init; } = true;

	public bool DeployBreaking { get; init; } = false;

	/// <summary>
	/// Creates the database of the connection string if it does not exist.
	/// Off by default: a typo in the database name of a production connection string must fail, not create an empty database.
	/// </summary>
	public bool CreateDbIfNotExists { get; init; } = false;

	/// <summary>
	/// Updates stored checksums for applied migrations whose files no longer match.
	/// Use only after confirming the database schema already matches the project files.
	/// </summary>
	public bool RepairMigrationChecksums { get; init; } = false;
}