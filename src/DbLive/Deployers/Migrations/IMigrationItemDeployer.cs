namespace DbLive.Deployers.Migrations;

internal interface IMigrationItemDeployer
{
	/// <summary>
	/// Deploys one migration or breaking script as one unit: in its own transaction
	/// (unless the project wraps the whole deployment), with the item settings merged over the project settings.
	/// </summary>
	Task DeployAsync(long migrationVersion, MigrationItem migrationItem, MigrationItemSettings itemSettings);
}
