using DbLive.Adapter;
using Microsoft.Data.SqlClient;
using Xunit.Extensions.AssemblyFixture;

namespace DbLive.MSSQL.Tests;

/// <summary>
/// A test class whose tests deploy a database of their own: xUnit creates the class for each test, so each test gets a new random
/// database name. The database is created before the test, unless <c>createDb</c> is false: the deployment of the test creates it, or the
/// test checks that none is created. It is dropped after the test, also a failed one, and the sessions still open in it end.
/// </summary>
/// <param name="dbName">A fixed name instead of a random one.</param>
/// <param name="keepDb">Keeps the database after the test. With <c>.WithReuse(true)</c> in <see cref="SqlServerIntegrationFixture"/>
/// the container stays after the run, and the next run deploys to the database of the previous one; without it the fixture removes
/// the container, and the database with it.</param>
public abstract class SqlServerDbTests(
	SqlServerIntegrationFixture fixture,
	ITestOutputHelper output,
	bool createDb = true,
	string? dbName = null,
	bool keepDb = false
) : IAssemblyFixture<SqlServerIntegrationFixture>, IAsyncLifetime
{
	protected SqlServerIntegrationFixture Fixture { get; } = fixture;

	protected ITestOutputHelper Output { get; } = output;

	protected string DbCnnString { get; } = dbName is null
		? fixture.MasterDbConnectionString.SetRandomMsSqlDatabaseName()
		: fixture.MasterDbConnectionString.SetMsSqlDatabaseName(dbName);

	protected string DbName => new SqlConnectionStringBuilder(DbCnnString).InitialCatalog;

	public virtual async Task InitializeAsync()
	{
		if (!createDb) return;

		// DbLive deploys to an existing database without creating it.
		await using DbLiveBuilder builder = CreateBuilder();
		await builder.GetRequiredService<IDbLiveDA>().CreateDBAsync();
	}

	public virtual async Task DisposeAsync()
	{
		if (keepDb) return;

		await using DbLiveBuilder builder = CreateBuilder();
		await builder.GetRequiredService<IDbLiveDA>().DropDbAsync();
	}

	/// <summary>A builder of the database of the test, with the log in the output of the test and without a project.</summary>
	protected DbLiveBuilder CreateBuilder()
	{
		return new DbLiveBuilder()
			.LogToXUnitOutput(Output)
			.SqlServer()
			.SetDbConnection(DbCnnString);
	}

	protected DbLiveBuilder CreateBuilder(TempProject project) => CreateBuilder().SetScriptsFolder(project.Path);

	protected async Task<bool> DbExistsAsync()
	{
		await using SqlConnection cnn = new(Fixture.MasterDbConnectionString);
		await cnn.OpenAsync();
		await using SqlCommand cmd = new("select count(*) from sys.databases where name = @name", cnn);
		_ = cmd.Parameters.AddWithValue("name", DbName);
		return (int)(await cmd.ExecuteScalarAsync())! == 1;
	}
}
