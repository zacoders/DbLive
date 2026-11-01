namespace DbLive.Deployers.Code;

public interface ICodeBreakingDeployer
{
	/// <summary>
	/// Runs every breaking script of the Code folder, in the order of their relative paths, and records each run in <c>dblive.code</c>.
	/// </summary>
	/// <exception cref="CodeDeploymentException">A script failed; the scripts after it are not run.</exception>
	Task DeployAsync();
}
