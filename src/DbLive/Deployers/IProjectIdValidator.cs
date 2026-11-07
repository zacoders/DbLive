namespace DbLive.Deployers;

public interface IProjectIdValidator
{
	/// <summary>
	/// Before a deployment, before the self-deployment changes the <c>dblive</c> tables: fails when the database belongs to another
	/// project. Changes nothing. A database without the DbLive tables or without a ProjectId passes: <see cref="BindAsync"/> stores it.
	/// </summary>
	/// <exception cref="ProjectIdMismatchException"/>
	Task ValidateAsync();

	/// <summary>
	/// After the self-deployment, which creates the <c>dblive</c> tables of a new database: binds the ProjectId of the settings
	/// to a database that has none yet.
	/// </summary>
	/// <exception cref="ProjectIdMismatchException"/>
	Task BindAsync();

	/// <summary>
	/// Before the SQL tests: only compares. A database without a ProjectId was not deployed by this project.
	/// </summary>
	/// <exception cref="ProjectIdMismatchException"/>
	Task ValidateBeforeTestsAsync();
}
