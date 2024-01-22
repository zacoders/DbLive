using EasyFlow.Adapter;

namespace EasyFlow;

public class EasyFlow(
		IEasyFlowProject _project,
		IEasyFlowDA _da,
		CodeDeployer _codeDeployer,
		IEasyFlowPaths _paths,
		BreakingChangesDeployer _breakingChangesDeployer,
		MigrationsDeployer _migrationsDeployer,
		IUnitTestsRunner _unitTestsRunner,
		EasyFlowBuilder _builder,
		ILogger logger
	) : IEasyFlow
{
	private readonly ILogger _logger = logger.ForContext(typeof(EasyFlow));

	private static readonly TimeSpan _defaultTimeout = TimeSpan.FromDays(1);

	private EasyFlowSettings _projectSettings = new();

	public void Deploy(DeployParameters parameters)
	{
		_logger.Information("Starting deployment.");

		parameters.Check();

		if (parameters.CreateDbIfNotExists)
			_da.CreateDB(true);

		// Self deploy. Deploying EasyFlow to the database
		SelfDeployProjectInternal();

		// Deploy actuall project
		DeployProjectInternal(false, parameters);
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

		_projectSettings = _project.GetSettings();

		Transactions.ExecuteWithinTransaction(
			_projectSettings.TransactionWrapLevel == TransactionWrapLevel.Deployment,
			_projectSettings.TransactionIsolationLevel,
			_defaultTimeout,
			() =>
			{
				_migrationsDeployer.DeployMigrations(isSelfDeploy, parameters);

				_codeDeployer.DeployCode(isSelfDeploy, parameters);

				_breakingChangesDeployer.DeployBreakingChanges(parameters);
			}
		);

		_unitTestsRunner.RunAllTests(parameters, _projectSettings);

		_logger.Information("Project deploy completed.");
	}
}
