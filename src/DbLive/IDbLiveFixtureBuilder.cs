using System.Reflection;

namespace DbLive;

/// <summary>
/// Gives a test fixture the database to deploy the project to. Ready-made builders: <c>MsSqlFixtureBuilder</c> and
/// <c>PostgreSqlFixtureBuilder</c> for a server you run, <c>MsSqlContainerFixtureBuilder</c> and
/// <c>PostgreSqlContainerFixtureBuilder</c> for a server in Docker.
/// </summary>
public interface IDbLiveFixtureBuilder
{
	/// <summary>
	/// A new builder with the provider, the connection string of a new database and the project. Called once per fixture;
	/// the fixture disposes the builder at the end, so never return the same builder twice.
	/// </summary>
	Task<DbLiveBuilder> GetBuilderAsync();

	/// <summary>
	/// The SQL project assembly; test discovery reads the SQL tests from it.
	/// </summary>
	Assembly GetProjectAssembly();
}
