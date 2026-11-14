namespace DbLive.Deployers;

internal class DbLiveDeployer(
		ICodeDeployer _codeDeployer,
		IBreakingChangesDeployer _breakingChangesDeployer,
		IMigrationsDeployer _migrationsDeployer,
		IFolderDeployer _folderDeployer,
		ILogger _logger,
		ISettingsAccessor _projectSettings,
		ITransactionRunner _transactionRunner,
		IAppliedMigrationsValidator _appliedMigrationsValidator,
		IMigrationsSaver _migrationsSaver,
		IMigrationChecksumValidator _migrationChecksumValidator,
		IScriptSource _scriptSource
	) : IDbLiveDeployer
{
	private readonly ILogger _logger = _logger.ForContext(typeof(DbLiveDeployer));

	public async Task DeployAsync(ProjectSnapshot project, DeployParameters parameters)
	{
		_logger.Information("Starting project deploy from {Location}.", _scriptSource.Location);

		DbLiveSettings projectSettings = await _projectSettings.GetProjectSettingsAsync().ConfigureAwait(false);

		await _transactionRunner.ExecuteWithinTransactionAsync(
			projectSettings.TransactionWrapLevel == TransactionWrapLevel.Deployment,
			projectSettings.TransactionIsolationLevel,
			projectSettings.DeploymentTimeout,
			async () =>
			{
				// an older build fails here, before any script of the project runs and before any row of it is written.
				// After the self-deployment: the check reads the dblive tables in the format of this DbLive.
				await _appliedMigrationsValidator.ValidateAsync(project).ConfigureAwait(false);

				await _migrationChecksumValidator.ValidateAsync(project, parameters).ConfigureAwait(false);

				// saving migrations to the log table before actual deployment.
				await _migrationsSaver.SaveAsync(project).ConfigureAwait(false);

				await _folderDeployer.DeployAsync(project, ProjectFolder.BeforeDeploy, parameters).ConfigureAwait(false);

				await _migrationsDeployer.DeployAsync(project, parameters).ConfigureAwait(false);

				await _codeDeployer.DeployAsync(project, parameters).ConfigureAwait(false);

				await _breakingChangesDeployer.DeployAsync(project, parameters).ConfigureAwait(false);

				await _folderDeployer.DeployAsync(project, ProjectFolder.AfterDeploy, parameters).ConfigureAwait(false);
			}
		).ConfigureAwait(false);

		_logger.Information("Project deploy completed.");
	}
}
