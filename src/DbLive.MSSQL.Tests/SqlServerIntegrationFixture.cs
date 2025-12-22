using Testcontainers.MsSql;
namespace DbLive.MSSQL.Tests;

public class SqlServerIntegrationFixture : IAsyncLifetime
{
	private readonly MsSqlContainer _msSqlContainer = new MsSqlBuilder().WithImage("mcr.microsoft.com/mssql/server:2022-latest").Build();

	public string MasterDbConnectionString =>
		_masterDbConnectionString ?? throw new Exception("Connection string is not found or container is not initialized yet.");

	private string? _masterDbConnectionString;

	public async Task InitializeAsync()
	{
		string? configuredConnectionString = new TestConfig().GetSqlServerConnectionString();

		if (string.IsNullOrWhiteSpace(configuredConnectionString))
		{
			await _msSqlContainer.StartAsync();
			_masterDbConnectionString = _msSqlContainer.GetConnectionString();
		}
		else
		{
			_masterDbConnectionString = configuredConnectionString;
		}
	}

	public async Task DisposeAsync()
	{
		await _msSqlContainer.DisposeAsync().AsTask();
	}
}