namespace EasyFlow;

public class EasyFlow : IEasyFlow
{
	private static readonly ILogger Logger = Log.ForContext(typeof(EasyFlow));

	private readonly IEasyFlowDA _DA;
	private readonly IEasyFlowDeployer _deployer;
	private readonly IEasyFlowProject _project;
	private EasyFlowSettings _projectSettings = new EasyFlowSettings();

	public EasyFlow(IEasyFlowProject easyFlowProject, IEasyFlowDA easyFlowDA, IEasyFlowDeployer easyFlowDeployer)
	{
		_project = easyFlowProject;
		_DA = easyFlowDA;
		_deployer = easyFlowDeployer;
	}

	public void DeployProject(string proejctPath, string sqlConnectionString, int maxVersion)
	{
		// Self deploy. Deploying EasyFlow to the database
		string easyFlowSqlSource = Path.Combine(AppContext.BaseDirectory, "EasyFlowSql");
		DeployProjectInternal("self", easyFlowSqlSource, sqlConnectionString, int.MaxValue);

		// Deploy actuall project
		DeployProjectInternal("project", proejctPath, sqlConnectionString, maxVersion);
	}

	private void DeployProjectInternal(string domain, string proejctPath, string sqlConnectionString, int maxVersion)
	{
		_project.Load(proejctPath);
		_projectSettings = _project.GetSettings();

		IOrderedEnumerable<Migration> migrationsToApply = GetMigrationsToApply(domain, sqlConnectionString, maxVersion);

		IEasyFlowSqlConnection cnn = _deployer.OpenConnection(sqlConnectionString);

		if (_projectSettings.TransactionWrapLevel == TransactionWrapLevel.Deployment)
			cnn.BeginTransaction(_projectSettings.TransactionIsolationLevel);

		foreach (var migration in migrationsToApply)
		{
			DeployMigration(domain, migration, cnn);
		}

		if (_projectSettings.TransactionWrapLevel == TransactionWrapLevel.Deployment)
			cnn.CommitTransaction();
	}

	public IOrderedEnumerable<Migration> GetMigrationsToApply(string domain, string sqlConnectionString, int maxVersion)
	{
		int appliedVersion = 0;
		IReadOnlyCollection<MigrationDto> appliedMigrations = Array.Empty<MigrationDto>();

		if (_DA.EasyFlowInstalled(sqlConnectionString))
		{
			appliedMigrations = _DA.GetMigrations(domain, sqlConnectionString);
			appliedVersion = appliedMigrations.Count == 0 ? 0 : appliedMigrations.Max(m => m.MigrationVersion);
		}

		var migrationsToApply = _project.GetProjectMigrations()
			.Where(m => m.Version <= maxVersion)
			.Where(m =>
				   appliedVersion == 0
				|| m.Version > appliedVersion
				// additional check for the migrations with the same version but different name 
				|| (m.Version == appliedVersion
					 && !appliedMigrations.Any(am => am.MigrationVersion == appliedVersion && am.MigrationName == m.Name))
			)
			.OrderBy(m => m.Version)
			.ThenBy(m => m.Name);
		return migrationsToApply;
	}

	private void DeployMigration(string domain, Migration migration, IEasyFlowSqlConnection cnn)
	{
		Logger.Information(migration.PathUri.GetLastSegment());
		var tasks = _project.GetMigrationTasks(migration.PathUri.LocalPath);

		DateTime migrationStartedUtc = DateTime.UtcNow;

		if (_projectSettings.TransactionWrapLevel == TransactionWrapLevel.Migration)
			cnn.BeginTransaction(_projectSettings.TransactionIsolationLevel);

		foreach (MigrationTask task in tasks.OrderBy(t => t.MigrationType))
		{
			if (_projectSettings.TransactionWrapLevel == TransactionWrapLevel.Task)
				cnn.BeginTransaction(_projectSettings.TransactionIsolationLevel);

			Logger.Information("Migration {migrationType}", task.MigrationType);
			if (new[] { MigrationType.Migration, MigrationType.Data }.Contains(task.MigrationType))
			{
				string sql = File.ReadAllText(task.FileUri.LocalPath);
				cnn.ExecuteNonQuery(sql);
			}
			else
			{
				Logger.Information("Migration {migrationType} skipped.", task.MigrationType);
			}

			if (_projectSettings.TransactionWrapLevel == TransactionWrapLevel.Task)
				cnn.CommitTransaction();
		}

		DateTime migrationCompletedUtc = DateTime.UtcNow;

		cnn.MigrationCompleted(domain, migration.Version, migration.Name, migrationStartedUtc, migrationCompletedUtc);

		if (_projectSettings.TransactionWrapLevel == TransactionWrapLevel.Migration)
			cnn.CommitTransaction();
	}
}
