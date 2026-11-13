namespace DbLive.Deployers.Migrations;

internal interface IMigrationChecksumValidator
{
	Task ValidateAsync(ProjectSnapshot project, DeployParameters parameters);
}
