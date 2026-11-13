namespace DbLive.Project;

internal interface IProjectLoader
{
	/// <summary>
	/// Reads and checks the whole project a deployment deploys, whatever its <see cref="DeployParameters"/>: <c>settings.json</c>
	/// and its transaction settings, the checks of all files, the migrations with their settings files, the code, the breaking
	/// scripts of the Code folder, the BeforeDeploy and AfterDeploy folders. Touches no database, so an error of the project
	/// stops the deployment before it changes anything on the server.
	/// </summary>
	Task<ProjectSnapshot> LoadAsync();
}
