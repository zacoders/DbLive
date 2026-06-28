namespace DbLive.Deployers;

internal class DeployLockRunner(
	ILogger logger,
	IDeployLock deployLock,
	IDbLiveDbConnection connection,
	ISettingsAccessor settingsAccessor
) : IDeployLockRunner
{
	private readonly ILogger _logger = logger.ForContext(typeof(DeployLockRunner));

	public async Task ExecuteWithLockAsync(Func<Task> action, CancellationToken ct = default)
	{
		DbLiveSettings projectSettings = await settingsAccessor.GetProjectSettingsAsync().ConfigureAwait(false);

		if (!projectSettings.DeployLockEnabled)
		{
			await action().ConfigureAwait(false);
			return;
		}

		string resourceName = DeployLockResource.Build(connection, projectSettings);
		_logger.Information("Acquiring deploy lock for resource {DeployLockResource}.", resourceName);

		IDeployLockHandle lockHandle = await deployLock
			.AcquireAsync(resourceName, ct)
			.ConfigureAwait(false);

		try
		{
			await action().ConfigureAwait(false);
			await lockHandle.CommitAsync().ConfigureAwait(false);
		}
		finally
		{
			await lockHandle.DisposeAsync().ConfigureAwait(false);
		}
	}
}
