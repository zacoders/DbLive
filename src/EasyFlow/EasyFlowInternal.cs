using EasyFlow.Deployers.Code;
using EasyFlow.Deployers.Migrations;
using EasyFlow.Deployers.Testing;

namespace EasyFlow;

public class EasyFlowInternal(
		ICodeDeployer _codeDeployer,
		IBreakingChangesDeployer _breakingChangesDeployer,
		IMigrationsDeployer _migrationsDeployer,
		IFolderDeployer _folderDeployer,
		IUnitTestsRunner _unitTestsRunner,
		ILogger logger,
		ISettingsAccessor _projectSettings,
		ITransactionRunner _transactionRunner,
		ISelfDeployer _selfDeployer
	) : IEasyFlowInternal
{
	private readonly ILogger _logger = logger.ForContext(typeof(EasyFlowInternal));

	public void SelfDeployProjectInternal()
	{
		_logger.Information("Starting self deploy.");

		IEasyFlowInternal easyFlowSelfDeployer = _selfDeployer.CreateEasyFlowSelf();
		easyFlowSelfDeployer.DeployProjectInternal(true, DeployParameters.Default);

		_logger.Information("Self deploy completed.");
	}

	public void DeployProjectInternal(bool isSelfDeploy, DeployParameters parameters)
	{
		_logger.Information("Starting project deploy.");

		EasyFlowSettings projectSettings = _projectSettings.ProjectSettings;

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
