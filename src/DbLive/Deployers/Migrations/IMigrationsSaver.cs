namespace DbLive.Deployers.Migrations;

internal interface IMigrationsSaver
{
	Task SaveAsync(ProjectSnapshot project);
}
