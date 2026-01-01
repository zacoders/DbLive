
namespace DbLive.Deployers.Code;

public interface ICodeItemDeployer
{
	/// <summary>
	/// Deploys code item.
	/// </summary>
	/// <param name="codeItem"></param>
	/// <returns>Returns false if there was any error during deployment.</returns>
	CodeItemDeployResult DeployCodeItem(CodeItem codeItem);
}
