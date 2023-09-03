using Polly;
using Polly.Retry;
using System.Transactions;

namespace EasyFlow;

public class EasyFlow : IEasyFlow
{
	private static readonly ILogger Logger = Log.ForContext(typeof(EasyFlow));

	private readonly IEasyFlowDA _da;
	private readonly IEasyFlowDeployer _deployer;
	private readonly IEasyFlowProject _project;
	private readonly IEasyFlowPaths _paths;
	private static readonly TimeSpan _defaultTimeout = TimeSpan.FromDays(1);

	private EasyFlowSettings _projectSettings = new();

	private readonly RetryPolicy _codeItemRetryPolicy =
		Policy.Handle<Exception>()
			  .WaitAndRetry(
					10,
					retryAttempt => TimeSpan.FromSeconds(retryAttempt * retryAttempt)
			  );

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

		ExecuteWithTransaction(
			_projectSettings.TransactionWrapLevel == TransactionWrapLevel.Deployment,
			_projectSettings.TransactionIsolationLevel,
			() =>
			{
				DeployMigrations(domain, migrationsToApply, sqlConnectionString);

				if (parameters.DeployCode)
					DeployCode(domain, sqlConnectionString, parameters);
			}
		);
	}

	private static void ExecuteWithTransaction(bool needTransaction, TranIsolationLevel isolationLevel, Action action)
	{
		if (!needTransaction)
		{
			action();
			return;
		}

		using TransactionScope _transactionScope = TransactionScopeManager.Create(isolationLevel, _defaultTimeout);
		action();
		_transactionScope.Complete();
	}

	private void DeployCode(string domain, string sqlConnectionString, EasyFlowDeployParameters parameters)
	{
		Logger.Information("DeployCode.");

		var codeItems = _project.GetCodeItems();
		var parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = parameters.NumberOfThreadsForCodeDeploy };

		Parallel.ForEach(codeItems, parallelOptions, codeItem =>
		{
			DeployCodeItem(sqlConnectionString, codeItem);
		});
	}

	private void DeployCodeItem(string sqlConnectionString, CodeItem codeItem)
	{
		try
		{
			_codeItemRetryPolicy.Execute(() =>
			{
				Logger.Information("Deploy code file: {filePath}", codeItem.FileUri.GetLastSegment());
				string sql = File.ReadAllText(codeItem.FileUri.LocalPath);
				IEasyFlowSqlConnection cnn = _deployer.OpenConnection(sqlConnectionString);
				cnn.ExecuteNonQuery(sql);
				cnn.Close();
			});
		}
		catch (Exception ex)
		{
			Logger.Error(ex, "Deploy code file error. File path: {filePath}", codeItem.FileUri.LocalPath);
		}
	}

	private void DeployMigrations(string domain, IOrderedEnumerable<Migration> migrationsToApply, string sqlConnectionString)
	{
		foreach (var migration in migrationsToApply)
		{
			DeployMigration(domain, migration, sqlConnectionString);
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

	private void DeployMigration(string domain, Migration migration, string sqlConnectionString)
	{
		Logger.Information(migration.PathUri.GetLastSegment());
		var tasks = _project.GetMigrationTasks(migration.PathUri.LocalPath);

		DateTime migrationStartedUtc = DateTime.UtcNow;

		ExecuteWithTransaction(
			_projectSettings.TransactionWrapLevel == TransactionWrapLevel.Migration,
			_projectSettings.TransactionIsolationLevel,
			() =>
			{
				foreach (MigrationTask task in tasks.OrderBy(t => t.MigrationType))
				{
					ExecuteWithTransaction(
						_projectSettings.TransactionWrapLevel == TransactionWrapLevel.Task,
						_projectSettings.TransactionIsolationLevel,
						() =>
						{
							Logger.Information("Migration {migrationType}", task.MigrationType);
							if (new[] { MigrationType.Migration, MigrationType.Data }.Contains(task.MigrationType))
							{
								string sql = File.ReadAllText(task.FileUri.LocalPath);
								IEasyFlowSqlConnection cnn = _deployer.OpenConnection(sqlConnectionString);
								cnn.ExecuteNonQuery(sql);
								cnn.Close();
							}
							else
							{
								Logger.Information("Migration {migrationType} skipped.", task.MigrationType);
							}
						}
					);
				}

				DateTime migrationCompletedUtc = DateTime.UtcNow;

				IEasyFlowSqlConnection cnn = _deployer.OpenConnection(sqlConnectionString);
				cnn.MigrationCompleted(domain, migration.Version, migration.Name, migrationStartedUtc, migrationCompletedUtc);
				cnn.Close();
			}
		);
	}
}
