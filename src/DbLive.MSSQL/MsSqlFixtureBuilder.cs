using System.Reflection;

namespace DbLive.MSSQL;

/// <summary>
/// Gives every test fixture a new database with a random name (<c>DbLive--&lt;guid&gt;</c>) on a SQL Server you run.
/// </summary>
/// <param name="project">The SQL project assembly.</param>
/// <param name="connectionString">A connection string to the server. The database name in it is replaced.</param>
public sealed class MsSqlFixtureBuilder(Assembly project, string connectionString) : IDbLiveFixtureBuilder
{
	public Task<DbLiveBuilder> GetBuilderAsync() =>
		Task.FromResult(new DbLiveBuilder()
			.SqlServer()
			.SetDbConnection(connectionString.SetRandomMsSqlDatabaseName())
			.SetProject(project));

	public Assembly GetProjectAssembly() => project;
}
