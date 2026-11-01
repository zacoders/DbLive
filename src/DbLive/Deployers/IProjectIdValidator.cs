namespace DbLive.Deployers;

public interface IProjectIdValidator
{
	/// <summary>
	/// Before a deployment: binds the ProjectId of the settings to a database that has none yet.
	/// </summary>
	Task ValidateAsync();

	/// <summary>
	/// Before the SQL tests: only compares. A database without a ProjectId was not deployed by this project.
	/// </summary>
	Task ValidateBeforeTestsAsync();
}
