using Polly;
using Polly.Retry;
using System.Transactions;

namespace EasyFlow;

public class EasyFlow : IEasyFlow
{
	private static readonly ILogger Logger = Log.ForContext(typeof(EasyFlow));

	private readonly IEasyFlowDA _da;
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

	public EasyFlow(IEasyFlowProject easyFlowProject, IEasyFlowDA easyFlowDA, IEasyFlowPaths paths)
	{
		_project = easyFlowProject;
		_da = easyFlowDA;
		_paths = paths;
	}

	public void DeployProject(string proejctPath, string sqlConnectionString, EasyFlowDeployParameters parameters)
	{
		if (parameters.CreateDbIfNotExists)
			_da.CreateDB(sqlConnectionString, true);

		// Self deploy. Deploying EasyFlow to the database
		DeployProjectInternal(true, _paths.GetPathToEasyFlowSelfProject(), sqlConnectionString, EasyFlowDeployParameters.Default);

		// Deploy actuall project
		DeployProjectInternal(false, proejctPath, sqlConnectionString, parameters);
	}

	private void DeployProjectInternal(bool isSelfDeploy, string proejctPath, string sqlConnectionString, EasyFlowDeployParameters parameters)
	{
		_project.Load(proejctPath);
		_projectSettings = _project.GetSettings();

		IOrderedEnumerable<Migration> migrationsToApply = GetMigrationsToApply(isSelfDeploy, sqlConnectionString, parameters);

		ExecuteWithTransaction(
			_projectSettings.TransactionWrapLevel == TransactionWrapLevel.Deployment,
			_projectSettings.TransactionIsolationLevel,
			() =>
			{
				DeployMigrations(isSelfDeploy, sqlConnectionString, parameters, migrationsToApply);

				DeployCode(isSelfDeploy, sqlConnectionString, parameters);

				DeployBreakingChanges(isSelfDeploy, sqlConnectionString, parameters);
			}
		);

		RunTests(isSelfDeploy, sqlConnectionString, parameters);
	}

	private void RunTests(bool isSelfDeploy, string sqlConnectionString, EasyFlowDeployParameters parameters)
	{
		throw new NotImplementedException();
	}

	private void DeployBreakingChanges(bool isSelfDeploy, string sqlConnectionString, EasyFlowDeployParameters parameters)
	{
		if (!parameters.DeployBreaking)
		{
			return;
		}

		throw new NotImplementedException();
	}

	private void DeployMigrations(bool isSelfDeploy, string sqlConnectionString, EasyFlowDeployParameters parameters, IOrderedEnumerable<Migration> migrationsToApply)
	{
		if (!parameters.DeployMigrations)
		{
			return;
		}

		foreach (var migration in migrationsToApply)
		{
			DeployMigration(isSelfDeploy, migration, sqlConnectionString);
		}
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

	private void DeployCode(bool isSelfDeploy, string sqlConnectionString, EasyFlowDeployParameters parameters)
	{
		if (!parameters.DeployCode)
		{
			return;
		}

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
				Logger.Information("Deploy code file: {filePath}", codeItem.FilePath.GetLastSegment());
				string sql = File.ReadAllText(codeItem.FilePath);
				_da.ExecuteNonQuery(sqlConnectionString, sql);
			});
		}
		catch (Exception ex)
		{
			Logger.Error(ex, "Deploy code file error. File path: {filePath}", codeItem.FilePath);
		}
	}

	public IOrderedEnumerable<Migration> GetMigrationsToApply(bool isSelfDeploy, string sqlConnectionString, EasyFlowDeployParameters parameters)
	{
		int appliedVersion = 0;
		IReadOnlyCollection<MigrationDto> appliedMigrations = Array.Empty<MigrationDto>();

		if (_da.EasyFlowInstalled(sqlConnectionString))
		{
			if (isSelfDeploy)
			{
				appliedVersion = _da.GetEasyFlowVersion(sqlConnectionString);
			}
			else
			{
				appliedMigrations = _da.GetMigrations(sqlConnectionString);
				appliedVersion = appliedMigrations.Count == 0 ? 0 : appliedMigrations.Max(m => m.MigrationVersion);
			}
		}

		if (isSelfDeploy)
		{
			return _project.GetMigrations()
				.Where(m => m.Version <= (parameters.MaxVersionToDeploy ?? int.MaxValue))
				.Where(m => m.Version > appliedVersion)
				.OrderBy(m => m.Version)
				.ThenBy(m => m.Name);
		}

		return _project.GetMigrations()
			.Where(m => m.Version <= (parameters.MaxVersionToDeploy ?? int.MaxValue))
			.Where(m => !appliedMigrations.Any(am => am.MigrationVersion == m.Version && am.MigrationName == m.Name))
			.OrderBy(m => m.Version)
			.ThenBy(m => m.Name);
	}

	private void DeployMigration(bool isSelfDeploy, Migration migration, string sqlConnectionString)
	{
		Logger.Information(migration.Path.GetLastSegment());
		var tasks = _project.GetMigrationTasks(migration.Path);

		if (tasks.Count == 0) return;

		DateTime migrationStartedUtc = DateTime.UtcNow;

		ExecuteWithTransaction(
			_projectSettings.TransactionWrapLevel == TransactionWrapLevel.Migration,
			_projectSettings.TransactionIsolationLevel,
			() =>
			{
				foreach (MigrationTask task in tasks.OrderBy(t => t.MigrationType))
				{
					DeployMigrationTask(sqlConnectionString, task);
				}

				DateTime migrationCompletedUtc = DateTime.UtcNow;

				if (isSelfDeploy)
				{
					_da.SetEasyFlowVersion(sqlConnectionString, migration.Version, migrationCompletedUtc);
				}
				else
				{
					_da.MigrationCompleted(sqlConnectionString, migration.Version, migration.Name, migrationStartedUtc, migrationCompletedUtc);
				}
			}
		);
	}

	private void DeployMigrationTask(string sqlConnectionString, MigrationTask task)
	{
		ExecuteWithTransaction(
			_projectSettings.TransactionWrapLevel == TransactionWrapLevel.Task,
			_projectSettings.TransactionIsolationLevel,
			() =>
			{
				Logger.Information("Migration {migrationType}", task.MigrationType);
				if (task.MigrationType.In(MigrationType.Migration, MigrationType.Data))
				{
					string sql = File.ReadAllText(task.FilePath);
					_da.ExecuteNonQuery(sqlConnectionString, sql);
					return;
				}
				Logger.Information("Migration {migrationType} skipped.", task.MigrationType);
			}
		);
	}
}
