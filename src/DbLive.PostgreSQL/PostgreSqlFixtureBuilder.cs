using System.Reflection;

namespace DbLive.PostgreSQL;

/// <summary>
/// Gives every test fixture a new database with a random name (<c>dblive--&lt;guid&gt;</c>) on a PostgreSQL server you run.
/// </summary>
/// <param name="project">The SQL project assembly.</param>
/// <param name="connectionString">A connection string to the server. The database name in it is replaced.</param>
public sealed class PostgreSqlFixtureBuilder(Assembly project, string connectionString) : IDbLiveFixtureBuilder
{
	public Task<DbLiveBuilder> GetBuilderAsync() =>
		Task.FromResult(new DbLiveBuilder()
			.PostgreSQL()
			.SetDbConnection(connectionString.SetRandomPostgreSqlDatabaseName())
			.SetProject(project));

	public Assembly GetProjectAssembly() => project;
}
