namespace DbLive.Deployers.Migrations;

public interface IAppliedMigrationsValidator
{
	/// <summary>
	/// Checks that every migration version applied to the database is in the project.
	/// </summary>
	/// <exception cref="DatabaseAheadOfProjectException"/>
	Task ValidateAsync();
}
