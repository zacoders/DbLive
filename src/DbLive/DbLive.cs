namespace DbLive;

public class DbLive(
		IDbLiveDA _da,
		ILogger _logger,
		IDbLiveInternalDeployer _internalDeployer,
		IDbLiveSelfDeployer _selfDeployer
	) : IDbLive
{
	private readonly ILogger _logger = _logger.ForContext(typeof(DbLive));

	public void Deploy(DeployParameters parameters)
	{
		_logger.Information("Starting deployment.");

		parameters.Check();

		if (parameters.CreateDbIfNotExists)
			_da.CreateDB(true);

		// Self deploy. Deploying DbLive to the database
		_selfDeployer.Deploy();

		// Deploy actual project
		_internalDeployer.Deploy(false, parameters);
	}

	public void DropDatabase(bool skipIfNotExists = true)
	{
		// todo: I think this is not a good idea to have drop database method.
		_da.DropDB(skipIfNotExists);
	}
}
