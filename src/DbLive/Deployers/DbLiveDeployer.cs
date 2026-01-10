
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

		DbLiveSettings projectSettings = await _projectSettings.GetProjectSettingsAsync().ConfigureAwait(false);

		await _transactionRunner.ExecuteWithinTransactionAsync(
			projectSettings.TransactionWrapLevel == TransactionWrapLevel.Deployment,
			projectSettings.TransactionIsolationLevel,
			projectSettings.DeploymentTimeout,
			async () =>
			{
				await _downgradeDeployer.DeployAsync(parameters).ConfigureAwait(false);

				await _folderDeployer.DeployAsync(ProjectFolder.BeforeDeploy, parameters).ConfigureAwait(false);

				await _migrationsDeployer.DeployAsync(parameters).ConfigureAwait(false);

				await _codeDeployer.DeployAsync(parameters).ConfigureAwait(false);

				await _breakingChangesDeployer.DeployAsync(parameters).ConfigureAwait(false);

				await _folderDeployer.DeployAsync(ProjectFolder.AfterDeploy, parameters).ConfigureAwait(false);
			}
		).ConfigureAwait(false);

		await _unitTestsRunner.RunAllTestsAsync(parameters).ConfigureAwait(false);

		_logger.Information("Project deploy completed.");
	}
}
