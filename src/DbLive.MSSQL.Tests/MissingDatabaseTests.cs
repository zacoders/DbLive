using Microsoft.Data.SqlClient;
using System.Diagnostics.CodeAnalysis;

namespace DbLive.MSSQL.Tests;


/// <summary>
/// SQL Server answers "Cannot open database ... The login failed." (error 4060) both for a database that does not exist
/// and for a login without access to an existing one. DbLive names the first case and leaves the second to SQL Server.
/// </summary>
[SuppressMessage("Usage", "xUnit1041:Fixture arguments to test classes must have fixture sources", Justification = "AssemblyFixture will be properly supported in xUnit v3. waiting.")]
public class MissingDatabaseTests(SqlServerIntegrationFixture fixture, ITestOutputHelper output)
	: SqlServerDbTests(fixture, output, createDb: false)
{
	[Fact]
	public async Task Deployment_to_a_database_that_does_not_exist_fails_with_its_name()
	{
		using TempProject project = new TempProject()
			.AddFile("Migrations/001.migration.sql", "create table dbo.t1 ( id int not null );");

		DatabaseNotFoundException ex = await Assert.ThrowsAsync<DatabaseNotFoundException>(
			() => CreateBuilder(project).CreateDeployer().DeployAsync(DeployParameters.Default)
		);

		Assert.Equal(
			$"Database '{DbName}' does not exist on server '{new SqlConnectionStringBuilder(DbCnnString).DataSource}'. Check the database name in the connection string. A deployment creates the database only with DeployParameters.CreateDbIfNotExists = true.",
			ex.Message
		);
		Assert.Equal(4060, Assert.IsType<SqlException>(ex.InnerException).Number);
		Assert.False(await DbExistsAsync());
	}

	[Fact]
	public async Task Tests_against_a_database_that_does_not_exist_fail_with_its_name()
	{
		using TempProject project = new TempProject()
			.AddFile("Tests/t1.test.sql", "select 'empty' as assert;");

		DatabaseNotFoundException ex = await Assert.ThrowsAsync<DatabaseNotFoundException>(
			CreateBuilder(project).CreateTester().RunAllTestsAsync
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
			// pooled: DbLive does not support Pooling=False on SQL Server (ConnectionStringNotSupported). Its connection of the login
			// to master stays in the pool, so DropLoginAsync kills it.
			SqlConnectionStringBuilder loginCnnString = new(DbCnnString) { UserID = login, Password = password };

			using TempProject project = new TempProject()
				.AddFile("Migrations/001.migration.sql", "create table dbo.t1 ( id int not null );");

			SqlException ex = await Assert.ThrowsAsync<SqlException>(
				() => CreateLoginBuilder(project, loginCnnString.ConnectionString).CreateDeployer().DeployAsync(DeployParameters.Default)
			);

			Assert.Equal(4060, ex.Number);
		}
		finally
		{
			await DropLoginAsync(login);
		}
	}

	private async Task ExecuteOnMasterAsync(string sql)
	{
		using SqlConnection cnn = new(Fixture.MasterDbConnectionString);
		await cnn.OpenAsync();
		using SqlCommand cmd = cnn.CreateCommand();
		cmd.CommandText = sql;
		_ = await cmd.ExecuteNonQueryAsync();
	}

	// the database of the test with the connection string of another login.
	private DbLiveBuilder CreateLoginBuilder(TempProject project, string loginCnnString)
	{
		return new DbLiveBuilder()
			.LogToXUnitOutput(Output)
			.SqlServer()
			.SetDbConnection(loginCnnString)
			.SetScriptsFolder(project.Path);
	}
}
