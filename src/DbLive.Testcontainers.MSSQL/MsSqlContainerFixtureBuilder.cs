using System.Reflection;
using Testcontainers.MsSql;

namespace DbLive.MSSQL;

/// <summary>
/// Gives every test fixture a new database with a random name (<c>DbLive--&lt;guid&gt;</c>) on SQL Server in Docker.
/// One container per image per test run: the first fixture starts it, the others wait for that start and share the
/// container. Testcontainers removes the container when the test run ends.
/// </summary>
/// <param name="project">The SQL project assembly.</param>
/// <param name="image">The SQL Server image.</param>
public sealed class MsSqlContainerFixtureBuilder(Assembly project, string image)
	: IDbLiveFixtureBuilder
{
	// the updates of SQL Server 2025. Internal, so that the image is not in the public API files: a new major version of
	// SQL Server here is a breaking change, which only a major DbLive release makes (docs/versioning.md).
	internal const string DefaultImage = "mcr.microsoft.com/mssql/server:2025-latest";

	/// <summary>
	/// With the default image, SQL Server 2025 (<c>mcr.microsoft.com/mssql/server:2025-latest</c>).
	/// </summary>
	/// <param name="project">The SQL project assembly.</param>
	public MsSqlContainerFixtureBuilder(Assembly project)
		: this(project, DefaultImage)
	{
	}

	// the start of each image: its connection string, or the error every fixture gets when the start failed.
	private static readonly Dictionary<string, Task<string>> _servers = [];

	public async Task<DbLiveBuilder> GetBuilderAsync()
	{
		string serverConnectionString = await GetServerAsync(image).ConfigureAwait(false);

		return await new MsSqlFixtureBuilder(project, serverConnectionString).GetBuilderAsync().ConfigureAwait(false);
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
		MsSqlContainer container = new MsSqlBuilder(image).Build();
		await container.StartAsync().ConfigureAwait(false);
		return container.GetConnectionString();
	}
}
