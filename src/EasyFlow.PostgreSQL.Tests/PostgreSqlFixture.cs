using Testcontainers.PostgreSql;

namespace EasyFlow.PostgreSQL.Tests;


public class PostgreSqlFixture : IAsyncLifetime
{
	private readonly PostgreSqlContainer _postgreSqlContainer = new PostgreSqlBuilder().WithImage("postgres:latest").Build();

	private static readonly string TestDbNamePrefix = "EasyFlow--";

	public string PostgresDBConnectionString =>
		_postgresDbConnectionString ?? throw new Exception("Connection string is not found or container is not initialized yet.");

	private string? _postgresDbConnectionString;

	protected static string GetRanomDbName() => $"{TestDbNamePrefix}{Guid.NewGuid()}";

	public async Task InitializeAsync()
	{
		await _postgreSqlContainer.StartAsync();
		_postgresDbConnectionString = new TestConfig().GetPostgreSqlConnectionString() ?? _postgreSqlContainer.GetConnectionString();
	}

	public async Task DisposeAsync()
	{
		await _postgreSqlContainer.DisposeAsync().AsTask();
	}
}