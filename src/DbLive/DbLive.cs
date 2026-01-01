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

	public void Deploy(DeployParameters parameters)
	{
		_logger.Information("Starting deployment.");

		if (parameters.CreateDbIfNotExists)
			_da.CreateDB(true);

		// Self deploy. Deploying DbLive to the database
		_selfDeployer.Deploy();

		// Deploy actual project
		_deployer.Deploy(parameters);
	}
}
