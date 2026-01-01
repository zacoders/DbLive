using DotNet.Testcontainers.Containers;
using Testcontainers.PostgreSql;

namespace DbLive.PostgreSQL.Tests;


public class PostgreSqlFixture : IAsyncLifetime
{
	private static readonly PostgreSqlContainer _dockerContainer
		= new PostgreSqlBuilder()
			.WithImage("postgres:latest")
			.WithName("DbLive.PostgreSQL.Tests")
			//.WithReuse(true)
			.Build();

	//private static readonly string TestDbNamePrefix = "dblive--";

	public string PostgresDBConnectionString =>
		_postgresDbConnectionString ?? throw new Exception("Connection string is not found or container is not initialized yet.");

	private string? _postgresDbConnectionString;

	public async Task InitializeAsync()
	{
		if (_dockerContainer.State != TestcontainersStates.Running)
		{
			await _dockerContainer.StartAsync();
		}
		_postgresDbConnectionString = _dockerContainer.GetConnectionString();
	}

	public async Task DisposeAsync()
	{
		await _dockerContainer.DisposeAsync().AsTask();
	}
}