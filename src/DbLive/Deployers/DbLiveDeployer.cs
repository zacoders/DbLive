
namespace DbLive.Deployers;

public class DbLiveDeployer(
		ICodeDeployer _codeDeployer,
		IBreakingChangesDeployer _breakingChangesDeployer,
		IMigrationsDeployer _migrationsDeployer,
		IFolderDeployer _folderDeployer,
		IUnitTestsRunner _unitTestsRunner,
		ILogger _logger,
		ISettingsAccessor _projectSettings,
		ITransactionRunner _transactionRunner,
		IDowngradeDeployer _downgradeDeployer
	) : IDbLiveDeployer
{
	private readonly ILogger _logger = _logger.ForContext(typeof(DbLiveDeployer));

	public async Task DeployAsync(DeployParameters parameters)
	{
		_logger.Information("Starting project deploy.");

		DbLiveSettings projectSettings = await _projectSettings.GetProjectSettingsAsync();

		await _transactionRunner.ExecuteWithinTransactionAsync(
			projectSettings.TransactionWrapLevel == TransactionWrapLevel.Deployment,
			projectSettings.TransactionIsolationLevel,
			projectSettings.DeploymentTimeout,
			async () =>
			{
				await _downgradeDeployer.DeployAsync(parameters);

				await _folderDeployer.DeployAsync(ProjectFolder.BeforeDeploy, parameters);

				await _migrationsDeployer.DeployAsync(parameters);

				await _codeDeployer.DeployAsync(parameters);

				await _breakingChangesDeployer.DeployAsync(parameters);

				await _folderDeployer.DeployAsync(ProjectFolder.AfterDeploy, parameters);
			}
		);

		await _unitTestsRunner.RunAllTestsAsync(parameters);

		_logger.Information("Project deploy completed.");
	}
}
