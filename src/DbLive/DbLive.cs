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
			await _da.DropDBAsync();
			await _da.CreateDBAsync();
		}

		if (parameters.CreateDbIfNotExists)
			await _da.CreateDBAsync(true);

		// Self deploy. Deploying DbLive to the database
		await _selfDeployer.DeployAsync();

		// Deploy actual project
		await _deployer.DeployAsync(parameters);
	}
}
