using DotNet.Testcontainers.Containers;
using Testcontainers.MsSql;
namespace DbLive.MSSQL.Tests;

public class SqlServerIntegrationFixture : IAsyncLifetime
{
	private static readonly MsSqlContainer _dockerContainer
		= new MsSqlBuilder("mcr.microsoft.com/mssql/server:2025-latest")
			.WithName("DbLive.MSSQL.Tests")
			//.WithReuse(true)
			.Build();

	public string MasterDbConnectionString =>
		_masterDbConnectionString ?? throw new Exception("Connection string is not found or container is not initialized yet.");

	private string? _masterDbConnectionString;

	public async Task InitializeAsync()
	{
		if (_dockerContainer.State != TestcontainersStates.Running)
		{
			await _dockerContainer.StartAsync().ConfigureAwait(false);
		}
		_masterDbConnectionString = _dockerContainer.GetConnectionString();
	}

	public async Task DisposeAsync()
	{
		await _dockerContainer.DisposeAsync().AsTask().ConfigureAwait(false);
	}
}