using DbLive.Adapter;
using Microsoft.Data.SqlClient;
using System.Diagnostics.CodeAnalysis;

namespace DbLive.MSSQL.Tests;

/// <summary>
/// Without the check, Pooling=False failed the self-deployment with "Implicit distributed transactions have not been enabled", and
/// Enlist=False failed every script in a transaction with DbLiveTransactionEndedException (both verified).
/// </summary>
[SuppressMessage("Usage", "xUnit1041:Fixture arguments to test classes must have fixture sources", Justification = "AssemblyFixture will be properly supported in xUnit v3. waiting.")]
public class ConnectionStringSettingsTests(SqlServerIntegrationFixture fixture, ITestOutputHelper output)
	: SqlServerDbTests(fixture, output)
{
	[Fact]
	public async Task Deployment_without_pooling_fails_before_it_connects()
	{
		using TempProject project = CreateProject();

		NotSupportedException ex = await Assert.ThrowsAsync<NotSupportedException>(
			() => CreateBuilder(project, new SqlConnectionStringBuilder(DbCnnString) { Pooling = false }).CreateDeployer().DeployAsync(DeployParameters.Default)
		);

		Assert.Equal(ConnectionStringNotSupported.Pooling().Message, ex.Message);
		Assert.Equal(0, await CountTablesAsync());
	}

	[Fact]
	public async Task Deployment_without_enlist_fails_before_it_connects()
	{
		using TempProject project = CreateProject();

		NotSupportedException ex = await Assert.ThrowsAsync<NotSupportedException>(
			() => CreateBuilder(project, new SqlConnectionStringBuilder(DbCnnString) { Enlist = false }).CreateDeployer().DeployAsync(DeployParameters.Default)
		);

		Assert.Equal(ConnectionStringNotSupported.Enlist().Message, ex.Message);
		Assert.Equal(0, await CountTablesAsync());
	}

	[Fact]
	public async Task Tests_without_pooling_fail_before_they_connect()
	{
		using TempProject project = CreateProject();

		NotSupportedException ex = await Assert.ThrowsAsync<NotSupportedException>(
			CreateBuilder(project, new SqlConnectionStringBuilder(DbCnnString) { Pooling = false }).CreateTester().RunAllTestsAsync
		);

		Assert.Equal(ConnectionStringNotSupported.Pooling().Message, ex.Message);
	}

	private static TempProject CreateProject() => new TempProject()
		.AddFile("Migrations/001.migration.sql", "create table dbo.t1 ( id int not null );")
		.AddFile("Tests/t1.test.sql", "select * from dbo.t1; select 'empty' as assert;");

	private DbLiveBuilder CreateBuilder(TempProject project, SqlConnectionStringBuilder cnnString)
	{
		return new DbLiveBuilder()
			.LogToXUnitOutput(Output)
			.SqlServer()
			.SetDbConnection(cnnString.ConnectionString)
			.SetScriptsFolder(project.Path);
	}

	private async Task<int> CountTablesAsync()
	{
		await using SqlConnection cnn = new(DbCnnString);
		await cnn.OpenAsync();
		await using SqlCommand cmd = new("select count(*) from sys.tables", cnn);
		return (int)(await cmd.ExecuteScalarAsync())!;
	}
}
