namespace EasyFlow;

public class EasyFlow : IEasyFlow
{
	private static readonly ILogger Logger = Log.ForContext(typeof(EasyFlow));

	private readonly IEasyFlowDA _da;
	private readonly IEasyFlowDeployer _deployer;
	private readonly IEasyFlowProject _project;
	private readonly IEasyFlowPaths _paths;
	private EasyFlowSettings _projectSettings = new();

	public EasyFlow(IEasyFlowProject easyFlowProject, IEasyFlowDA easyFlowDA, IEasyFlowDeployer easyFlowDeployer, IEasyFlowPaths paths)
	{
		_project = easyFlowProject;
		_da = easyFlowDA;
		_deployer = easyFlowDeployer;
		_paths = paths;
	}

	public void DeployProject(string proejctPath, string sqlConnectionString, EasyFlowDeployParameters parameters)
	{
		if (parameters.CreateDbIfNotExists)
			_deployer.CreateDB(sqlConnectionString, true);

		// Self deploy. Deploying EasyFlow to the database
		DeployProjectInternal("self", _paths.GetPathToEasyFlowSelfProject(), sqlConnectionString, EasyFlowDeployParameters.Default);

		// Deploy actuall project
		DeployProjectInternal("project", proejctPath, sqlConnectionString, parameters);
	}

	private void DeployProjectInternal(string domain, string proejctPath, string sqlConnectionString, EasyFlowDeployParameters parameters)
	{
		_project.Load(proejctPath);
		_projectSettings = _project.GetSettings();

		IOrderedEnumerable<Migration> migrationsToApply = GetMigrationsToApply(domain, sqlConnectionString, parameters);

		IEasyFlowSqlConnection cnn = _deployer.OpenConnection(sqlConnectionString);

		if (_projectSettings.TransactionWrapLevel == TransactionWrapLevel.Deployment)
			cnn.BeginTransaction(_projectSettings.TransactionIsolationLevel);

		DeployMigrations(domain, migrationsToApply, cnn);

		if (parameters.DeployCode)
			DeployCode(domain, cnn, parameters);

		if (_projectSettings.TransactionWrapLevel == TransactionWrapLevel.Deployment)
			cnn.CommitTransaction();
	}

	private void DeployCode(string domain, IEasyFlowSqlConnection cnn, EasyFlowDeployParameters parameters)
	{
		Logger.Information("DeployCode.");

		//TODO: collect errors for each CodeItem, use thread safe collection. pass collection to the task.
		//List<Errors!>
		List<Task> tasks = new();
		var codeItems = _project.GetCodeItems();
		foreach (IEnumerable<CodeItem> codeItemsBatch in codeItems.Batch(parameters.NumberOfThreadsForCodeDeploy))
		{
			var task = Task.Run(() => DeployCodeBathc(domain, cnn, codeItemsBatch.ToList()));
			tasks.Add(task);
		}
		Task.WhenAll(tasks).Wait();
	}

	private void DeployCodeBathc(string domain, IEasyFlowSqlConnection cnn, List<CodeItem> codeItems)
	{
		foreach (var codeItem in codeItems)
		{
			Logger.Information("Deploy code file: {filePath}", codeItem.FileUri.GetLastSegment());
			string sql = File.ReadAllText(codeItem.FileUri.LocalPath);
			cnn.ExecuteNonQuery(sql);
			//TODO: save execution result to the database?
		}
	}

	private void DeployMigrations(string domain, IOrderedEnumerable<Migration> migrationsToApply, IEasyFlowSqlConnection cnn)
	{
		foreach (var migration in migrationsToApply)
		{
			DeployMigration(domain, migration, cnn);
		}
	}

	public IOrderedEnumerable<Migration> GetMigrationsToApply(string domain, string sqlConnectionString, EasyFlowDeployParameters parameters)
	{
		int appliedVersion = 0;
		IReadOnlyCollection<MigrationDto> appliedMigrations = Array.Empty<MigrationDto>();

		if (_da.EasyFlowInstalled(sqlConnectionString))
		{
			appliedMigrations = _da.GetMigrations(domain, sqlConnectionString);
			appliedVersion = appliedMigrations.Count == 0 ? 0 : appliedMigrations.Max(m => m.MigrationVersion);
		}

		var migrationsToApply = _project.GetMigrations()
			.Where(m => m.Version <= (parameters.MaxVersionToDeploy ?? int.MaxValue))
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
