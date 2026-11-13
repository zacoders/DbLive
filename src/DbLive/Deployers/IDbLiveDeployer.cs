namespace DbLive.Deployers;

internal interface IDbLiveDeployer
{
	/// <summary>
	/// Deploys the project loaded by <see cref="IProjectLoader"/>, after the self-deployment. Reads no project file.
	/// </summary>
	Task DeployAsync(ProjectSnapshot project, DeployParameters parameters);
}
