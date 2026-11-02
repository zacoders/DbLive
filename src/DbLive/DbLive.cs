using DbLive.Deployers;
using DbLive.SelfDeployer;

namespace DbLive;

public class DbLive(
		IDbLiveDA _da,
		ILogger _logger,
		IDbLiveDeployer _deployer,
		IDbLiveSelfDeployer _selfDeployer
	) : IDbLive
{
	private readonly ILogger _logger = _logger.ForContext(typeof(DbLive));

	public async Task DeployAsync(DeployParameters parameters)
	{
		_logger.Information("Starting deployment.");

		if (parameters.RecreateDatabase)
		{
			await _da.DropDbAsync().ConfigureAwait(false);
			await _da.CreateDBAsync().ConfigureAwait(false);
		}

		if (parameters.CreateDbIfNotExists)
		{
			await _da.CreateDBAsync(true).ConfigureAwait(false);
		}
		else
		{
			// a clear error for a wrong database name, instead of the connection error of the provider.
			await _da.CheckDatabaseExistsAsync().ConfigureAwait(false);
		}

		// Self deploy. Deploying DbLive to the database
		await _selfDeployer.DeployAsync().ConfigureAwait(false);

		// Deploy actual project
		await _deployer.DeployAsync(parameters).ConfigureAwait(false);
	}
}
