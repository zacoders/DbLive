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
			_da.DropDB();
			_da.CreateDB();
		}

		if (parameters.CreateDbIfNotExists)
			_da.CreateDB(true);

		// Self deploy. Deploying DbLive to the database
		await _selfDeployer.DeployAsync();

		// Deploy actual project
		await _deployer.DeployAsync(parameters);
	}
}
