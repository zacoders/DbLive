using EasyFlow.Project;

namespace EasyFlow;

public class EasyFlow : IEasyFlow
{
	private static readonly ILogger Logger = Log.ForContext(typeof(EasyFlow));

	private readonly IEasyFlowDA _da;
	private readonly IEasyFlowProject _project;
	private readonly IEasyFlowPaths _paths;
	private static readonly TimeSpan _defaultTimeout = TimeSpan.FromDays(1);
	private readonly IFileSystem _fileSystem;

	private EasyFlowSettings _projectSettings = new();

	private readonly RetryPolicy _codeItemRetryPolicy =
		Policy.Handle<Exception>()
			  .WaitAndRetry(
					10,
					retryAttempt => TimeSpan.FromSeconds(retryAttempt * retryAttempt)
			  );

	public EasyFlow(IEasyFlowProject easyFlowProject, IEasyFlowDA easyFlowDA, IEasyFlowPaths paths, IFileSystem fileSystem)
	{
		_project = easyFlowProject;
		_da = easyFlowDA;
		_paths = paths;
		_fileSystem = fileSystem;
	}

	public void DeployProject(string proejctPath, string sqlConnectionString, DeployParameters parameters)
	{
		parameters.Check();

		if (parameters.CreateDbIfNotExists)
			_da.CreateDB(sqlConnectionString, true);

		// Self deploy. Deploying EasyFlow to the database
		DeployProjectInternal(true, _paths.GetPathToEasyFlowSelfProject(), sqlConnectionString, DeployParameters.Default);

		// Deploy actuall project
		DeployProjectInternal(false, proejctPath, sqlConnectionString, parameters);
	}

	private void DeployProjectInternal(bool isSelfDeploy, string proejctPath, string sqlConnectionString, DeployParameters parameters)
	{
		_project.Load(proejctPath);
		_projectSettings = _project.GetSettings();

		IOrderedEnumerable<Migration> migrationsToApply = GetMigrationsToApply(isSelfDeploy, sqlConnectionString, parameters);

		ExecuteWithinTransaction(
			_projectSettings.TransactionWrapLevel == TransactionWrapLevel.Deployment,
			_projectSettings.TransactionIsolationLevel,
			() =>
			{
				DeployMigrations(isSelfDeploy, sqlConnectionString, parameters, migrationsToApply);

				DeployCode(isSelfDeploy, sqlConnectionString, parameters);

				DeployBreakingChanges(isSelfDeploy, sqlConnectionString, parameters);
			}
		);

		RunTests(sqlConnectionString, parameters, _projectSettings);
	}

	private void RunTests(string sqlConnectionString, DeployParameters parameters, EasyFlowSettings settings)
	{
		if (!parameters.RunTests)
		{
			return;
		}

		Logger.Information("Running Tests.");

		var tests = _project.GetTests();

		TestRunResult result = new();

		var parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = parameters.NumberOfThreadsForTestsRun };

		Parallel.ForEach(tests, parallelOptions, test =>
		{
			bool isSuccess = RunTest(test, sqlConnectionString, settings);
			if (isSuccess) { result.IncremenPassed(); } else { result.IncremenFailed(); }
		});

		Logger.Information("Tests Run Result> Passed: {PassedCount}, Failed: {FailedCount}.",
			result.PassedCount, result.FailedCount);
	}

	private bool RunTest(TestItem test, string sqlConnectionString, EasyFlowSettings settings)
	{
		try
		{
			using TransactionScope _transactionScope = TransactionScopeManager.Create(settings.TestsTransactionIsolationLevel, _defaultTimeout);

			_da.ExecuteNonQuery(sqlConnectionString, test.FileData.Content);

			_transactionScope.Dispose(); //canceling transaction

			Logger.Information("PASSED Test: {filePath}", test.Name);
			return true;
		}
		catch (Exception ex)
		{
			Logger.Error(ex, "FAILED Test: {filePath}. Error Message: {errorMessage}", test.Name, ex.Message);
			return false;
		}
	}

	private void DeployBreakingChanges(bool isSelfDeploy, string sqlConnectionString, DeployParameters parameters)
	{
		if (!parameters.DeployBreaking)
		{
			return;
		}

		throw new NotImplementedException();
	}

	private void DeployMigrations(bool isSelfDeploy, string sqlConnectionString, DeployParameters parameters, IOrderedEnumerable<Migration> migrationsToApply)
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

	private static void ExecuteWithinTransaction(bool needTransaction, TranIsolationLevel isolationLevel, Action action)
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

	private void DeployCode(bool isSelfDeploy, string sqlConnectionString, DeployParameters parameters)
	{
		if (!parameters.DeployCode)
		{
			return;
		}

		Logger.Information("Deploying Code.");

		var codeItems = _project.GetCodeItems();
		var parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = parameters.NumberOfThreadsForCodeDeploy };

		Parallel.ForEach(codeItems, parallelOptions, codeItem =>
		{
			DeployCodeItem(isSelfDeploy, sqlConnectionString, codeItem);
		});

		Logger.Information("Code deploy completed.");
	}

	private void DeployCodeItem(bool isSelfDeploy, string sqlConnectionString, CodeItem codeItem)
	{
		//TODO: add unit tests for thsi code!
		try
		{			
			Logger.Information("Deploy code file: {filePath}", codeItem.FileData.FilePath.GetLastSegment());

			bool isApplied = false;
			if (!isSelfDeploy)
			{
				//todo: just return item and hash, and show proper error. it cannot be applied, hash can be changed!
				isApplied = _da.IsCodeItemApplied(sqlConnectionString, codeItem.FileData.RelativePath, codeItem.FileData.MD5Hash);
			}

			if (isApplied)
			{
				_da.MarkCodeAsVerified(sqlConnectionString, codeItem.FileData.RelativePath, DateTime.UtcNow);
				return;
			}

			DateTime migrationStartedUtc = DateTime.UtcNow;
			_codeItemRetryPolicy.Execute(() =>
			{
				_da.ExecuteNonQuery(sqlConnectionString, codeItem.FileData.Content);
				//todo: maybe count number of attempts to deploy the code item?
			});
			DateTime migrationCompletedUtc = DateTime.UtcNow;

			if (!isSelfDeploy)
			{
				_da.MarkCodeAsApplied(sqlConnectionString, codeItem.FileData.RelativePath, codeItem.FileData.MD5Hash, migrationCompletedUtc, (int)(migrationCompletedUtc - migrationStartedUtc).TotalMilliseconds);
			}
		}
		catch (Exception ex)
		{
			Logger.Error(ex, "Deploy code file error. File path: {filePath}", codeItem.FileData.FilePath);
			throw;
		}
	}

	public IOrderedEnumerable<Migration> GetMigrationsToApply(bool isSelfDeploy, string sqlConnectionString, DeployParameters parameters)
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
				appliedVersion = appliedMigrations.Count == 0 ? 0 : appliedMigrations.Max(m => m.Version);
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
			.Where(m => !appliedMigrations.Any(am => am.Version == m.Version && am.Name == m.Name))
			.OrderBy(m => m.Version)
			.ThenBy(m => m.Name);
	}

	private void DeployMigration(bool isSelfDeploy, Migration migration, string sqlConnectionString)
	{
		Logger.Information(migration.FolderPath.GetLastSegment());
		var migrationItems = _project.GetMigrationItems(migration.FolderPath);

		if (migrationItems.Count == 0) return;

		DateTime migrationStartedUtc = DateTime.UtcNow;

		ExecuteWithinTransaction(
			_projectSettings.TransactionWrapLevel == TransactionWrapLevel.Migration,
			_projectSettings.TransactionIsolationLevel,
			() =>
			{
				foreach (MigrationItem migrationItem in migrationItems.OrderBy(t => t.MigrationType))
				{
					DeployMigrationItem(sqlConnectionString, migrationItem);
				}

				DateTime migrationCompletedUtc = DateTime.UtcNow;

				if (isSelfDeploy)
				{
					_da.SetEasyFlowVersion(sqlConnectionString, migration.Version, migrationCompletedUtc);
				}
				else
				{
					int durationMs = (int)(migrationCompletedUtc - migrationStartedUtc).TotalMilliseconds;
					_da.MarkMigrationAsApplied(sqlConnectionString, migration.Version, migration.Name, migrationCompletedUtc, durationMs);
				}
			}
		);
	}

	private void DeployMigrationItem(string sqlConnectionString, MigrationItem migrationItem)
	{
		ExecuteWithinTransaction(
			_projectSettings.TransactionWrapLevel == TransactionWrapLevel.Task,
			_projectSettings.TransactionIsolationLevel,
			() =>
			{
				Logger.Information("Migration {migrationType}", migrationItem.MigrationType);
				if (migrationItem.MigrationType.In(MigrationType.Migration, MigrationType.Data))
				{
					_da.ExecuteNonQuery(sqlConnectionString, migrationItem.FileData.Content);
					return;
				}
				Logger.Information("Migration {migrationType} skipped.", migrationItem.MigrationType);
			}
		);
	}
}
