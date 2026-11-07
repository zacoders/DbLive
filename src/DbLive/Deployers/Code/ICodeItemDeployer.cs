
namespace DbLive.Deployers.Code;

public interface ICodeItemDeployer
{
	/// <summary>
	/// Executes the code item and saves its row. A file already deployed is skipped by <see cref="CodeDeployer"/> before.
	/// </summary>
	/// <param name="codeItem"></param>
	/// <returns>Returns false if there was any error during deployment.</returns>
	Task<CodeItemDeployResult> DeployAsync(CodeItem codeItem);
}
