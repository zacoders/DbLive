namespace DbLive.Deployers.Migrations;

public interface IBreakingChangesDeployer
{
	Task DeployAsync(DeployParameters parameters);
}