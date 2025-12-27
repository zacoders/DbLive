using DbLive.Common.Settings;
using DbLive.Deployers.Code;
using DbLive.Deployers.Folder;
using DbLive.Deployers.Migrations;
using DbLive.Deployers.Testing;

namespace DbLive;

public class DbLiveInternal(
		ICodeDeployer _codeDeployer,
		IBreakingChangesDeployer _breakingChangesDeployer,
		IMigrationsDeployer _migrationsDeployer,
		IFolderDeployer _folderDeployer,
		IUnitTestsRunner _unitTestsRunner,
		ILogger _logger,
		ISettingsAccessor _projectSettings,
		ITransactionRunner _transactionRunner,
		IDbLiveInternalManager _selfDeployer
	) : IDbLiveInternal
{
	private readonly ILogger _logger = _logger.ForContext(typeof(DbLiveInternal));

	public void SelfDeployProjectInternal()
	{
		_logger.Information("Starting self deploy.");

		IDbLiveInternal DbLiveSelfDeployer = _selfDeployer.CreateDbLiveInternal();
		DbLiveSelfDeployer.DeployProjectInternal(true, DeployParameters.Default);

		_logger.Information("Self deploy completed.");
	}

	public void DeployProjectInternal(bool isSelfDeploy, DeployParameters parameters)
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

				_migrationsDeployer.DeployMigrations(isSelfDeploy, parameters);

				_codeDeployer.DeployCode(isSelfDeploy, parameters);

				_breakingChangesDeployer.DeployBreakingChanges(parameters);

				_folderDeployer.DeployFolder(ProjectFolder.AfterDeploy, parameters);
			}
		);

		_unitTestsRunner.RunAllTests(parameters);

		_logger.Information("Project deploy completed.");
	}
}
