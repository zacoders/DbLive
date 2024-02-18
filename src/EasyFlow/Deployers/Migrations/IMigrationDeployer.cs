namespace EasyFlow.Deployers.Migrations;

public interface IMigrationDeployer
{
	void DeployMigration(bool isSelfDeploy, Migration migration);
}