
namespace DbLive.Deployers.Migrations;

public interface IDowngradeDeployer
{
	Task DeployAsync(DeployParameters parameters);
}
