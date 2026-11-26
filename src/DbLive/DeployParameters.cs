namespace DbLive;

/// <summary>
/// What a deployment deploys. Start from a preset and adjust it with <c>with</c>:
/// <c>DeployParameters.Default with { CreateDbIfNotExists = true }</c>.
/// <para>
/// With every flag off a deployment still checks the project and the database, saves the migration items and runs the
/// BeforeDeploy and AfterDeploy scripts. No flag runs the SQL tests: a deployment never does.
/// </para>
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record DeployParameters
{
	/// <summary>
	/// The release: the migrations and the code, not the breaking phase. Old application instances can still run on the database.
	/// Each access returns a new instance.
	/// </summary>
	public static DeployParameters Default => new()
	{
		CreateDbIfNotExists = false,
		DeployBreaking = false,
		DeployCode = true,
		DeployMigrations = true
	};

	/// <summary>
	/// The breaking phase of the release only, once no old application instance runs. Deploy <see cref="Default"/> of the same build first:
	/// otherwise the breaking phase fails with <see cref="CodeNotDeployedException"/> when a code file of the build is not deployed with its
	/// current content. Only code files are checked: a failed or skipped <c>Default</c> of a build that added or changed no code file passes.
	/// Each access returns a new instance.
	/// </summary>
	public static DeployParameters Breaking => new()
	{
		CreateDbIfNotExists = false,
		DeployBreaking = true,
		DeployCode = false,
		DeployMigrations = false
	};

	internal bool RecreateDatabase { get; init; } = false;

	/// <summary>
	/// Deploys the code files not yet deployed with their current content: new, changed or moved ones; unchanged files are skipped.
	/// On by default.
	/// </summary>
	public bool DeployCode { get; init; } = true;

	/// <summary>
	/// Applies the migrations not applied to the database yet. On by default.
	/// </summary>
	public bool DeployMigrations { get; init; } = true;

	/// <summary>
	/// Runs the breaking phase: the breaking scripts of the Code folder, then the breaking scripts, not applied yet, of the applied migrations.
	/// It runs only when every code file of the build is deployed with its current content. Off by default.
	/// </summary>
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
