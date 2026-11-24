using DbLive.Deployers;
using DbLive.SelfDeployer;

namespace DbLive;

internal class DbLive(
		IDbLiveDA _da,
		ILogger _logger,
		IProjectLoader _projectLoader,
		IDbLiveDeployer _deployer,
		IDbLiveSelfDeployer _selfDeployer,
		IProjectIdValidator _projectIdValidator
	) : IDbLive
{
	private readonly ILogger _logger = _logger.ForContext(typeof(DbLive));

	public async Task DeployAsync(DeployParameters parameters)
	{
		_logger.Information("Starting deployment.");

		// Pooling=False (SQL Server) or Enlist=False: every deployment would fail, under an error of the provider that does not name it.
		_da.CheckConnectionString();

		// before the server: an error of the project leaves the server as it was. The self-migrations below cannot be undone, and
		// after them an older DbLive may no longer deploy the database; a database created here would stay empty.
		ProjectSnapshot project = await _projectLoader.LoadAsync().ConfigureAwait(false);

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

		// one deployment of the database at a time: a second one would read the same applied versions and run the new migrations again.
		// Once the database exists, before the first read of the dblive tables; held to the end, also when the deployment fails.
		IAsyncDisposable deploymentLock = await _da.AcquireDeploymentLockAsync().ConfigureAwait(false);

		try
		{
			// before the self-deployment: its self-migrations change the dblive tables, also those of the database of another project.
			await _projectIdValidator.ValidateAsync().ConfigureAwait(false);

			// Self deploy. Deploying DbLive to the database
			await _selfDeployer.DeployAsync().ConfigureAwait(false);

			// after the self-deployment, which creates the dblive tables of a new database.
			await _projectIdValidator.BindAsync().ConfigureAwait(false);

			// Deploy actual project
			await _deployer.DeployAsync(project, parameters).ConfigureAwait(false);

			// for the connections of this process: the SQL tests after the deployment (DbLive.xunit), an application that deploys at its startup.
			await _da.OnDeployedAsync().ConfigureAwait(false);
		}
		finally
		{
			await ReleaseDeploymentLockAsync(deploymentLock).ConfigureAwait(false);
		}
	}

	private async Task ReleaseDeploymentLockAsync(IAsyncDisposable deploymentLock)
	{
		try
		{
			await deploymentLock.DisposeAsync().ConfigureAwait(false);
		}
		catch (Exception e)
		{
			// not thrown: it would replace the error of the deployment, and fail a deployment that did its work. The connection of the lock
			// is broken. When the deployer crashed or the server ended the session, the lock ended with the session. When a device on the way
			// (a NAT gateway, a firewall) dropped the connection without telling the server, the session keeps the lock until the TCP
			// keep-alive of the server ends it, and the next deployments fail with DeploymentInProgressException until then.
			_logger.Warning(
				e,
				"The deployment lock was not released: its connection is broken. If the server did not end its session, it holds the lock "
				+ "until it notices the broken connection, and every deployment of the database fails with DeploymentInProgressException "
				+ "until then: see \"One deployment at a time\" in the DbLive docs."
			);
		}
	}
}
