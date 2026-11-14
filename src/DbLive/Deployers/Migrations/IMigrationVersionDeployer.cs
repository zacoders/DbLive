namespace DbLive.Deployers.Migrations;

internal interface IMigrationVersionDeployer
{
	/// <summary>
	/// Deploys the migration script of the version by its own settings, in its own transaction.
	/// </summary>
	Task DeployAsync(Migration migration);
}
