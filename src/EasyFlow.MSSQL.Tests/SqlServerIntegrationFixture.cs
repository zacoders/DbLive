using Testcontainers.MsSql;
namespace EasyFlow.MSSQL.Tests;

public class SqlServerIntegrationFixture : IAsyncLifetime
{
	private readonly MsSqlContainer _msSqlContainer = new MsSqlBuilder().WithImage("mcr.microsoft.com/mssql/server:2022-latest").Build();

	private static readonly string TestDbNamePrefix = "EasyFlow--";

	public string MasterDbConnectionString =>
		_masterDbConnectionString ?? throw new Exception("Connection string is not found or container is not initialized yet.");

	private string? _masterDbConnectionString;

	protected static string GetRanomDbName() => $"{TestDbNamePrefix}{Guid.NewGuid()}";

	public async Task InitializeAsync()
	{
		await _msSqlContainer.StartAsync();
		_masterDbConnectionString = new TestConfig().GetSqlServerConnectionString() ?? _msSqlContainer.GetConnectionString();
	}

	public async Task DisposeAsync()
	{
		await _msSqlContainer.DisposeAsync().AsTask();
	}
}