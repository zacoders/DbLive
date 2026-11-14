using System.Reflection;
using Testcontainers.PostgreSql;

namespace DbLive.PostgreSQL;

/// <summary>
/// Gives every test fixture a new database with a random name (<c>dblive--&lt;guid&gt;</c>) on PostgreSQL in Docker.
/// One container per image per test run: the first fixture starts it, the others wait for that start and share the
/// container. Testcontainers removes the container when the test run ends.
/// </summary>
/// <param name="project">The SQL project assembly.</param>
/// <param name="image">The PostgreSQL image.</param>
public sealed class PostgreSqlContainerFixtureBuilder(Assembly project, string image)
	: IDbLiveFixtureBuilder
{
	// a major version, not latest: a new major version of PostgreSQL does not change the image of the tests. Raising it is
	// a breaking change, which only a major DbLive release makes (docs/versioning.md); internal, so that the image is not in
	// the public API files.
	internal const string DefaultImage = "postgres:18";

	/// <summary>
	/// With the default image, PostgreSQL 18 (<c>postgres:18</c>).
	/// </summary>
	/// <param name="project">The SQL project assembly.</param>
	public PostgreSqlContainerFixtureBuilder(Assembly project)
		: this(project, DefaultImage)
	{
	}

	// the start of each image: its connection string, or the error every fixture gets when the start failed.
	private static readonly Dictionary<string, Task<string>> _servers = [];

	public async Task<DbLiveBuilder> GetBuilderAsync()
	{
		string serverConnectionString = await GetServerAsync(image).ConfigureAwait(false);

		return await new PostgreSqlFixtureBuilder(project, serverConnectionString).GetBuilderAsync().ConfigureAwait(false);
	}

	public Assembly GetProjectAssembly() => project;

	private static Task<string> GetServerAsync(string image)
	{
		lock (_servers)
		{
			if (!_servers.TryGetValue(image, out Task<string>? server))
			{
				server = StartServerAsync(image);
				_servers.Add(image, server);
			}
			return server;
		}
	}

	private static async Task<string> StartServerAsync(string image)
	{
		PostgreSqlContainer container = new PostgreSqlBuilder(image).Build();
		await container.StartAsync().ConfigureAwait(false);
		return container.GetConnectionString();
	}
}
