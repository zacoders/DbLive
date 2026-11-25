namespace DbLive;

/// <summary>
/// Deploys the project to the database. Created by <see cref="DbLiveBuilder.CreateDeployer"/>.
/// </summary>
public interface IDbLive
{
	/// <summary>
	/// Deploys the project: the migrations, the code and the breaking phase, as far as <paramref name="parameters"/> turn them on,
	/// with the BeforeDeploy and AfterDeploy scripts around them. Does not run the SQL tests.
	/// <para>
	/// The project files are read and checked first, before anything touches the server: an error in them (a wrong file name,
	/// an invalid <c>settings.json</c>, a file that is not UTF-8) stops the deployment with nothing changed. One deployment
	/// of a database runs at a time.
	/// </para>
	/// </summary>
	/// <param name="parameters">What to deploy: <see cref="DeployParameters.Default"/> for the release, then
	/// <see cref="DeployParameters.Breaking"/> for its breaking changes once no old application instance runs.</param>
	/// <exception cref="NotSupportedException">The connection string sets <c>Pooling=False</c> on SQL Server or <c>Enlist=False</c>.
	/// Checked before connecting.</exception>
	/// <exception cref="InvalidProjectSettingsException"><c>settings.json</c> is invalid. Thrown when the project is loaded, like the
	/// errors of the project files below: before anything touches the server.</exception>
	/// <exception cref="NotSupportedTransactionIsolationLevelException">The provider does not support an isolation level of
	/// <c>settings.json</c> or of an item settings file (<c>Snapshot</c> on PostgreSQL).</exception>
	/// <exception cref="InvalidMigrationItemSettingsException">An item settings file is invalid.</exception>
	/// <exception cref="FilesOutsideProjectFoldersException">Files of the scripts folder are outside the project folders.</exception>
	/// <exception cref="FilesInSeveralProjectFoldersException">Files are in more than one project folder.</exception>
	/// <exception cref="ScriptPathsDifferOnlyByCaseException">Two file paths differ only by case.</exception>
	/// <exception cref="ScriptPathTooLongException">SQL Server: a path is too long for the <c>dblive</c> tables.</exception>
	/// <exception cref="FileNotUtf8Exception">A file of the project is not UTF-8.</exception>
	/// <exception cref="InvalidMigrationVersionException">A migration file has an invalid version.</exception>
	/// <exception cref="DuplicateMigrationItemException">Two files are the same item of a migration version.</exception>
	/// <exception cref="MigrationItemMustExistsException">A migration version has no migration script.</exception>
	/// <exception cref="MigrationSettingsWithoutScriptException">An item settings file has no script.</exception>
	/// <exception cref="ObsoleteMigrationSettingsFileException">A migration settings file has the old name.</exception>
	/// <exception cref="UnknownMigrationSettingsException">An unexpected <c>.json</c> file is in the Migrations folder.</exception>
	/// <exception cref="UndoNotSupportedException">The Migrations folder has an undo script.</exception>
	/// <exception cref="BreakingScriptMatchesTestPatternException">A breaking script of the Code folder matches the test file patterns.</exception>
	/// <exception cref="DatabaseNotFoundException">The database does not exist, and <see cref="DeployParameters.CreateDbIfNotExists"/> is off.</exception>
	/// <exception cref="DeploymentInProgressException">Another deployment of the database is running.</exception>
	/// <exception cref="ProjectIdMismatchException">The database belongs to another project.</exception>
	/// <exception cref="DatabaseRequiresNewerDbLiveException">A newer DbLive has deployed the database.</exception>
	/// <exception cref="DatabaseAheadOfProjectException">The database has applied migrations that the project does not have.</exception>
	/// <exception cref="MigrationChecksumMismatchException">An applied migration or breaking script was edited.</exception>
	/// <exception cref="MigrationDeploymentException">A migration or breaking script of a migration failed.</exception>
	/// <exception cref="CodeDeploymentAggregateException">Code files failed.</exception>
	/// <exception cref="CodeDeploymentException">A breaking script of the Code folder failed.</exception>
	/// <exception cref="FolderDeploymentException">A BeforeDeploy or AfterDeploy script failed.</exception>
	/// <exception cref="CodeNotDeployedException">The breaking phase started before the normal deployment of the same build.</exception>
	/// <exception cref="ExecutionTimeoutException">The deployment transaction ran longer than <c>DeploymentTimeout</c>, or the
	/// self-deployment of DbLive ran longer than its timeout. The timeout of a script is the inner exception of the error that names its file.</exception>
	Task DeployAsync(DeployParameters parameters);
}
