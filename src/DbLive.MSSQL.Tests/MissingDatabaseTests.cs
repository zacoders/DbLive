using DbLive.Adapter;
using Microsoft.Data.SqlClient;
using System.Diagnostics.CodeAnalysis;
using Xunit.Extensions.AssemblyFixture;

namespace DbLive.MSSQL.Tests;


/// <summary>
/// SQL Server answers "Cannot open database ... The login failed." (error 4060) both for a database that does not exist
/// and for a login without access to an existing one. DbLive names the first case and leaves the second to SQL Server.
/// </summary>
[SuppressMessage("Usage", "xUnit1041:Fixture arguments to test classes must have fixture sources", Justification = "AssemblyFixture will be properly supported in xUnit v3. waiting.")]
public class MissingDatabaseTests(SqlServerIntegrationFixture _fixture, ITestOutputHelper _output)
	: IAssemblyFixture<SqlServerIntegrationFixture>
{
	private readonly string _dbCnnString = _fixture.MasterDbConnectionString.SetRandomMsSqlDatabaseName();

	private string DbName => new SqlConnectionStringBuilder(_dbCnnString).InitialCatalog;

	[Fact]
	public async Task Deployment_to_a_database_that_does_not_exist_fails_with_its_name()
	{
		using TempProject project = new TempProject()
			.AddFile("Migrations/001.migration.sql", "create table dbo.t1 ( id int not null );");

		DatabaseNotFoundException ex = await Assert.ThrowsAsync<DatabaseNotFoundException>(
			() => CreateBuilder(project, _dbCnnString).CreateDeployer().DeployAsync(DeployParameters.Default)
		);

		Assert.Equal(
			$"Database '{DbName}' does not exist on server '{new SqlConnectionStringBuilder(_dbCnnString).DataSource}'. Check the database name in the connection string. A deployment creates the database only with DeployParameters.CreateDbIfNotExists = true.",
			ex.Message
		);
		Assert.Equal(4060, Assert.IsType<SqlException>(ex.InnerException).Number);
		Assert.False(await DatabaseExistsAsync());
	}

	[Fact]
	public async Task Tests_against_a_database_that_does_not_exist_fail_with_its_name()
	{
		using TempProject project = new TempProject()
			.AddFile("Tests/t1.test.sql", "select 'empty' as assert;");

		DatabaseNotFoundException ex = await Assert.ThrowsAsync<DatabaseNotFoundException>(
			CreateBuilder(project, _dbCnnString).CreateTester().RunAllTestsAsync
		);

		Assert.Equal(DbName, ex.DatabaseName);
	}

	[Fact]
	public async Task Login_without_access_to_an_existing_database_gets_the_error_of_SQL_Server()
	{
		string login = $"dblive-no-access-{Guid.NewGuid():N}";
		const string password = "No-access-1234";

		// the login can connect to master and sees the database there, but has no user in the database.
		await ExecuteOnMasterAsync($"create database [{DbName}]; create login [{login}] with password = '{password}', check_policy = off;");

		try
		{
			// a pooled connection of the login to master would block 'drop login'. SqlConnection.ClearAllPools() is not an option:
			// it also clears the pools of tests running in parallel, and their open transactions get promoted to distributed ones.
			// DbLive builds its connection to master from this connection string, so it is not pooled either.
			SqlConnectionStringBuilder loginCnnString = new(_dbCnnString) { UserID = login, Password = password, Pooling = false };

			using TempProject project = new TempProject()
				.AddFile("Migrations/001.migration.sql", "create table dbo.t1 ( id int not null );");

			SqlException ex = await Assert.ThrowsAsync<SqlException>(
				() => CreateBuilder(project, loginCnnString.ConnectionString).CreateDeployer().DeployAsync(DeployParameters.Default)
			);

			Assert.Equal(4060, ex.Number);
		}
		finally
		{
			await ExecuteOnMasterAsync($"drop login [{login}];");
		}
	}

	private async Task<bool> DatabaseExistsAsync()
	{
		using SqlConnection cnn = new(_fixture.MasterDbConnectionString);
		await cnn.OpenAsync();
		using SqlCommand cmd = cnn.CreateCommand();
		cmd.CommandText = "select count(*) from sys.databases where name = @name";
		_ = cmd.Parameters.AddWithValue("name", DbName);
		return (int)(await cmd.ExecuteScalarAsync())! == 1;
	}

	private async Task ExecuteOnMasterAsync(string sql)
	{
		using SqlConnection cnn = new(_fixture.MasterDbConnectionString);
		await cnn.OpenAsync();
		using SqlCommand cmd = cnn.CreateCommand();
		cmd.CommandText = sql;
		_ = await cmd.ExecuteNonQueryAsync();
	}

	private DbLiveBuilder CreateBuilder(TempProject project, string cnnString)
	{
		return new DbLiveBuilder()
			.LogToXUnitOutput(_output)
			.SqlServer()
			.SetDbConnection(cnnString)
			.SetScriptsFolder(project.Path);
	}
}
