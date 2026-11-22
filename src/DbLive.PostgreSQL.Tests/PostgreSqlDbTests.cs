using DbLive.Adapter;
using Npgsql;
using Xunit.Extensions.AssemblyFixture;

namespace DbLive.PostgreSQL.Tests;

/// <summary>
/// A test class whose tests deploy a database of their own: xUnit creates the class for each test, so each test gets a new random
/// database name. The database is created before the test, unless <c>createDb</c> is false: the deployment of the test creates it, or the
/// test checks that none is created. It is dropped after the test, also a failed one. The drop ends the sessions of the database, so the
/// pools of its connection strings keep no connection to the server to the end of the run (0096-103).
/// </summary>
public abstract class PostgreSqlDbTests(PostgreSqlFixture fixture, ITestOutputHelper output, bool createDb = true)
	: IAssemblyFixture<PostgreSqlFixture>, IAsyncLifetime
{
	protected PostgreSqlFixture Fixture { get; } = fixture;

	protected ITestOutputHelper Output { get; } = output;

	protected string DbCnnString { get; } = fixture.PostgresDBConnectionString.SetRandomPostgreSqlDatabaseName();

	protected string DbName => new NpgsqlConnectionStringBuilder(DbCnnString).Database!;

	public virtual async Task InitializeAsync()
	{
		if (!createDb) return;

		// DbLive deploys to an existing database without creating it.
		await using DbLiveBuilder builder = CreateBuilder();
		await builder.GetRequiredService<IDbLiveDA>().CreateDBAsync();
	}

	public virtual async Task DisposeAsync()
	{
		await using DbLiveBuilder builder = CreateBuilder();
		await builder.GetRequiredService<IDbLiveDA>().DropDbAsync();
	}

	/// <summary>A builder of the database of the test, with the log in the output of the test and without a project.</summary>
	protected DbLiveBuilder CreateBuilder()
	{
		return new DbLiveBuilder()
			.ConfigureServices(s => s.AddXUnitLogger(Output))
			.PostgreSQL()
			.SetDbConnection(DbCnnString);
	}

	protected DbLiveBuilder CreateBuilder(TempProject project) => CreateBuilder().SetScriptsFolder(project.Path);

	protected async Task<bool> DbExistsAsync()
	{
		await using NpgsqlConnection cnn = new(Fixture.PostgresDBConnectionString);
		await cnn.OpenAsync();
		await using NpgsqlCommand cmd = new("select exists(select 1 from pg_database where datname = @name)", cnn);
		_ = cmd.Parameters.AddWithValue("name", DbName);
		return (bool)(await cmd.ExecuteScalarAsync())!;
	}
}
