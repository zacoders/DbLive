using System.Reflection;
using Testcontainers.MsSql;

namespace DbLive.MSSQL;

/// <summary>
/// Gives every test fixture a new database with a random name (<c>DbLive--&lt;guid&gt;</c>) on SQL Server in Docker.
/// One container per image per test process: the first fixture starts it, the others wait for that start and share the
/// container. <c>dotnet test</c> runs each test project in a process of its own, with a container of its own. Testcontainers
/// removes the container when the test process ends, unless its resource reaper is turned off (<c>TESTCONTAINERS_RYUK_DISABLED=true</c>,
/// set on some CI runners): then nothing removes it.
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

	/// <summary>
	/// A new builder for a new database in the container of the image. The first call starts the container; concurrent calls wait
	/// for that start. A failed start, such as Docker not running or an image not found, is not retried: every call of the
	/// test process gets its exception.
	/// </summary>
	public async Task<DbLiveBuilder> GetBuilderAsync()
	{
		string serverConnectionString = await GetServerAsync(image).ConfigureAwait(false);

		return await new MsSqlFixtureBuilder(project, serverConnectionString).GetBuilderAsync().ConfigureAwait(false);
	}

	/// <inheritdoc/>
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
