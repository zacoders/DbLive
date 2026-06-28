namespace DbLive.Deployers.Migrations;

public interface IMigrationChecksumValidator
{
	Task ValidateAsync(DeployParameters parameters);
}
