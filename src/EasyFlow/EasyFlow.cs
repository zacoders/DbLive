namespace EasyFlow;

public class EasyFlow(
		IEasyFlowProject _project,
		IEasyFlowDA _da,
		IEasyFlowPaths _paths,
		CodeDeployer _codeDeployer,
		BreakingChangesDeployer _breakingChangesDeployer,
		MigrationsDeployer _migrationsDeployer,
		IUnitTestsRunner _unitTestsRunner
	) : IEasyFlow
{
	//private static readonly ILogger Logger = _logger.ForContext(typeof(EasyFlow));

	private static readonly TimeSpan _defaultTimeout = TimeSpan.FromDays(1);

	private EasyFlowSettings _projectSettings = new();

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

		Transactions.ExecuteWithinTransaction(
			_projectSettings.TransactionWrapLevel == TransactionWrapLevel.Deployment,
			_projectSettings.TransactionIsolationLevel,
			_defaultTimeout,
			() =>
			{
				_migrationsDeployer.DeployMigrations(isSelfDeploy, sqlConnectionString, parameters);

				_codeDeployer.DeployCode(isSelfDeploy, sqlConnectionString, parameters);

				_breakingChangesDeployer.DeployBreakingChanges(sqlConnectionString, parameters);
			}
		);

		_unitTestsRunner.RunAllTests(sqlConnectionString, parameters, _projectSettings);
	}
}
