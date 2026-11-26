using DbLive.Adapter;
using Npgsql;
using System.Diagnostics.CodeAnalysis;

namespace DbLive.PostgreSQL.Tests;

/// <summary>
/// Without the check, Enlist=false failed every script in a transaction with DbLiveTransactionEndedException (verified). Pooling=false
/// works on PostgreSQL: Npgsql keeps the connection of a transaction for the next connection opened in it.
/// </summary>
[SuppressMessage("Usage", "xUnit1041:Fixture arguments to test classes must have fixture sources", Justification = "AssemblyFixture will be properly supported in xUnit v3. waiting.")]
public class ConnectionStringSettingsTests(PostgreSqlFixture fixture, ITestOutputHelper output)
	: PostgreSqlDbTests(fixture, output)
{
	[Fact]
	public async Task Deployment_without_enlist_fails_before_it_connects()
	{
		using TempProject project = CreateProject();

		NotSupportedException ex = await Assert.ThrowsAsync<NotSupportedException>(
			() => CreateBuilder(project, new NpgsqlConnectionStringBuilder(DbCnnString) { Enlist = false }).CreateDeployer().DeployAsync(DeployParameters.Default)
		);

		Assert.Equal(ConnectionStringNotSupported.Enlist().Message, ex.Message);
		Assert.Equal(0, await CountTablesAsync());
	}

	[Fact]
	public async Task Tests_without_enlist_fail_before_they_connect()
	{
		using TempProject project = CreateProject();

		NotSupportedException ex = await Assert.ThrowsAsync<NotSupportedException>(
			CreateBuilder(project, new NpgsqlConnectionStringBuilder(DbCnnString) { Enlist = false }).CreateTester().RunAllTestsAsync
		);

		Assert.Equal(ConnectionStringNotSupported.Enlist().Message, ex.Message);
	}

	[Fact]
	public async Task Deployment_and_tests_without_pooling_succeed()
	{
		using TempProject project = CreateProject();
		await using DbLiveBuilder builder = CreateBuilder(project, new NpgsqlConnectionStringBuilder(DbCnnString) { Pooling = false });

		await builder.CreateDeployer().DeployAsync(DeployParameters.Default);
		await builder.CreateTester().RunAllTestsAsync();

		Assert.True(await CountTablesAsync() > 0);
	}

	private static TempProject CreateProject() => new TempProject()
		.AddFile("Migrations/001.migration.sql", "create table t1 ( id int not null );")
		.AddFile("Tests/t1.test.sql", "select * from t1; select 'empty' as assert;");

	private DbLiveBuilder CreateBuilder(TempProject project, NpgsqlConnectionStringBuilder cnnString)
	{
		return new DbLiveBuilder()
			.ConfigureServices(s => s.AddXUnitLogger(Output))
			.PostgreSQL()
			.SetDbConnection(cnnString.ConnectionString)
			.SetScriptsFolder(project.Path);
	}

	private async Task<long> CountTablesAsync()
	{
		await using NpgsqlConnection cnn = new(DbCnnString);
		await cnn.OpenAsync();
		await using NpgsqlCommand cmd = new("select count(*) from pg_tables where schemaname not in ('pg_catalog', 'information_schema')", cnn);
		return (long)(await cmd.ExecuteScalarAsync())!;
	}
}
