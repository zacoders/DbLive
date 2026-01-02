namespace DbLive.Deployers;

public class DbLiveDeployer(
		ICodeDeployer _codeDeployer,
		IBreakingChangesDeployer _breakingChangesDeployer,
		IMigrationsDeployer _migrationsDeployer,
		IFolderDeployer _folderDeployer,
		IUnitTestsRunner _unitTestsRunner,
		ILogger _logger,
		ISettingsAccessor _projectSettings,
		ITransactionRunner _transactionRunner
	) : IDbLiveDeployer
{
	private readonly ILogger _logger = _logger.ForContext(typeof(DbLiveDeployer));

	public void Deploy(DeployParameters parameters)
	{
		_logger.Information("Starting project deploy.");

		DbLiveSettings projectSettings = _projectSettings.ProjectSettings;

		_transactionRunner.ExecuteWithinTransaction(
			projectSettings.TransactionWrapLevel == TransactionWrapLevel.Deployment,
			projectSettings.TransactionIsolationLevel,
			projectSettings.DeploymentTimeout,
			() =>
			{
				_folderDeployer.DeployFolder(ProjectFolder.BeforeDeploy, parameters);

				_migrationsDeployer.Deploy(parameters);

				_codeDeployer.DeployCode(parameters);

				_breakingChangesDeployer.DeployBreakingChanges(parameters);

				_folderDeployer.DeployFolder(ProjectFolder.AfterDeploy, parameters);
			}
		);

		_unitTestsRunner.RunAllTests(parameters);

		_logger.Information("Project deploy completed.");
	}
}
