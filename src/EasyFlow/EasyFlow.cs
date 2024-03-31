using EasyFlow.Adapter;
using EasyFlow.Deployers.Code;
using EasyFlow.Deployers.Migrations;
using EasyFlow.Deployers.Tests;

namespace EasyFlow;

public class EasyFlow(
		IEasyFlowDA _da,
		CodeDeployer _codeDeployer,
		IEasyFlowPaths _paths,
		BreakingChangesDeployer _breakingChangesDeployer,
		MigrationsDeployer _migrationsDeployer,
		FolderDeployer _folderDeployer,
		IUnitTestsRunner _unitTestsRunner,
		EasyFlowBuilder _builder,
		ILogger logger,
		ISettingsAccessor _projectSettings,
		ITransactionRunner _transactionRunner
	) : IEasyFlow
{
	private readonly ILogger _logger = logger.ForContext(typeof(EasyFlow));

	public void Deploy(DeployParameters parameters)
	{
		_logger.Information("Starting deployment.");

		parameters.Check();

		if (parameters.CreateDbIfNotExists)
			_da.CreateDB(true);

		// Self deploy. Deploying EasyFlow to the database
		SelfDeployProjectInternal();

		// Deploy actual project
		DeployProjectInternal(false, parameters);
	}

	public void DropDatabase(bool skipIfNotExists = true)
	{
		_da.DropDB(skipIfNotExists);
	}

	private void SelfDeployProjectInternal()
	{
		_logger.Information("Starting self deploy.");

		var selfDeployer = (EasyFlow)_builder.CloneBuilder()
			.SetProjectPath(_paths.GetPathToEasyFlowSelfProject())
			.CreateDeployer();

		selfDeployer.DeployProjectInternal(true, DeployParameters.Default);

		_logger.Information("Self deploy completed.");
	}

	private void DeployProjectInternal(bool isSelfDeploy, DeployParameters parameters)
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
