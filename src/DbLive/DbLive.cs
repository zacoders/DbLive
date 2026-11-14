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

		// first: an error of the project leaves the server as it was. The self-migrations below cannot be undone, and after them
		// an older DbLive may no longer deploy the database; a database created here would stay empty.
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

		// before the self-deployment: its self-migrations change the dblive tables, also those of the database of another project.
		await _projectIdValidator.ValidateAsync().ConfigureAwait(false);

		// Self deploy. Deploying DbLive to the database
		await _selfDeployer.DeployAsync().ConfigureAwait(false);

		// after the self-deployment, which creates the dblive tables of a new database.
		await _projectIdValidator.BindAsync().ConfigureAwait(false);

		// Deploy actual project
		await _deployer.DeployAsync(project, parameters).ConfigureAwait(false);
	}
}
