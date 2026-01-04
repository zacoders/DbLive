
namespace DbLive.Deployers.Migrations;

public interface IDowngradeDeployer
{
	void Deploy(DeployParameters parameters);
}
