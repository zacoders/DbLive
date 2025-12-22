using Testcontainers.PostgreSql;

namespace DbLive.PostgreSQL.Tests;


public class PostgreSqlFixture : IAsyncLifetime
{
	private readonly PostgreSqlContainer _postgreSqlContainer = new PostgreSqlBuilder().WithImage("postgres:latest").Build();

	private static readonly string TestDbNamePrefix = "DbLive--";

	public string PostgresDBConnectionString =>
		_postgresDbConnectionString ?? throw new Exception("Connection string is not found or container is not initialized yet.");

	private string? _postgresDbConnectionString;

	protected static string GetRanomDbName() => $"{TestDbNamePrefix}{Guid.NewGuid()}";

	public async Task InitializeAsync()
	{
		string? configuredConnectionString = new TestConfig().GetSqlServerConnectionString();

		if (string.IsNullOrWhiteSpace(configuredConnectionString))
		{
			await _postgreSqlContainer.StartAsync();
			_postgresDbConnectionString = _postgreSqlContainer.GetConnectionString();
		}
		else
		{
			_postgresDbConnectionString = configuredConnectionString;
		}
	}

	public async Task DisposeAsync()
	{
		await _postgreSqlContainer.DisposeAsync().AsTask();
	}
}