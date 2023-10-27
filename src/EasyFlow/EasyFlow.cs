using EasyFlow.Exceptions;

namespace EasyFlow;

public class EasyFlow : IEasyFlow
{
	private static readonly ILogger Logger = Log.ForContext(typeof(EasyFlow));

	private readonly IEasyFlowDA _da;
	private readonly IEasyFlowProject _project;
	private readonly IEasyFlowPaths _paths;
	private readonly CodeDeployer _codeDeployer;
	private readonly BreakingChangesDeployer _breakingChangesDeployer;
	private readonly MigrationsDeployer _migrationsDeployer;
	private readonly UnitTestsRunner _unitTestsRunner;

	private static readonly TimeSpan _defaultTimeout = TimeSpan.FromDays(1);

	private EasyFlowSettings _projectSettings = new();


	public EasyFlow(
		IEasyFlowProject easyFlowProject,
		IEasyFlowDA easyFlowDA,
		IEasyFlowPaths paths,
		CodeDeployer codeDeployer,
		BreakingChangesDeployer breakingChangesDeployer,
		MigrationsDeployer migrationsDeployer,
		UnitTestsRunner unitTestsRunner)
	{
		_project = easyFlowProject;
		_da = easyFlowDA;
		_paths = paths;
		_codeDeployer = codeDeployer;
		_breakingChangesDeployer = breakingChangesDeployer;
		_migrationsDeployer = migrationsDeployer;
		_unitTestsRunner = unitTestsRunner;
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

		_unitTestsRunner.RunTests(sqlConnectionString, parameters, _projectSettings);
	}
}
