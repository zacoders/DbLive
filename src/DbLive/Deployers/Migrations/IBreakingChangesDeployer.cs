namespace EasyFlow.Deployers.Migrations;

public interface IBreakingChangesDeployer
{
	void DeployBreakingChanges(DeployParameters parameters);
}