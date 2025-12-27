namespace DbLive;

public class DbLive(
		IDbLiveDA _da,
		ILogger _logger,
		IDbLiveInternal _DbLiveInternal
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
		_DbLiveInternal.SelfDeployProjectInternal();

		// Deploy actual project
		_DbLiveInternal.DeployProjectInternal(false, parameters);
	}

	public void DropDatabase(bool skipIfNotExists = true)
	{
		_da.DropDB(skipIfNotExists);
	}
}
