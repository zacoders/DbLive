
namespace EasyFlow.Deployers.Code;

public interface ICodeItemDeployer
{
	/// <summary>
	/// Deploys code item.
	/// </summary>
	/// <param name="isSelfDeploy"></param>
	/// <param name="codeItem"></param>
	/// <returns>Returns false if there was any error during deployment.</returns>
	CodeItemDeployResult DeployCodeItem(bool isSelfDeploy, CodeItem codeItem);
}
