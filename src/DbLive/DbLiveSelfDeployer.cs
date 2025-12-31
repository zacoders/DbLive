
namespace DbLive;

public class DbLiveSelfDeployer(
		ILogger _logger,
		ISettingsAccessor _projectSettings,
		IDbLiveBuilder _builder,
		IDbLivePaths _paths
		//IDbLiveDbConnection connection
	) : IDbLiveSelfDeployer
{
	private readonly ILogger _logger = _logger.ForContext(typeof(DbLiveSelfDeployer));

	public void Deploy()
	{
		DbLiveSettings projectSettings = _projectSettings.ProjectSettings;

		if (projectSettings.LogSelfDeploy)
		{
			_logger.Information("Starting self deploy.");
		}

		IDbLiveBuilder builder = _builder
			.CloneBuilder()
			.SetProjectPath(_paths.GetPathToDbLiveSelfProject());

		if (!projectSettings.LogSelfDeploy)
		{
			builder = builder.WithNoLogs();
		}

		// todo: refactor, this prevents mock and unit test
		IDbLiveInternalDeployer deployer = ((DbLiveBuilder)builder).CreateInternalDeployer();

		deployer.Deploy(
			true,
			new DeployParameters()
			{
				CreateDbIfNotExists = false,
				DeployBreaking = false,
				DeployCode = true,
				DeployMigrations = true,
				RunTests = false
			}
		);

		if (projectSettings.LogSelfDeploy)
		{
			_logger.Information("Self deploy completed.");
		}
	}
}
