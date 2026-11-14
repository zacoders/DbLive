using Microsoft.Data.SqlClient;
using System.Diagnostics.CodeAnalysis;
using Xunit.Extensions.AssemblyFixture;

namespace DbLive.MSSQL.Tests;


/// <summary>
/// A deployment login that is not db_owner: db_ddladmin, db_datareader and db_datawriter are enough for what DbLive does
/// itself, also when the schema dblive belongs to dbo (an administrator deployed the database first, or DbLive 1.5 did).
/// </summary>
[SuppressMessage("Usage", "xUnit1041:Fixture arguments to test classes must have fixture sources", Justification = "AssemblyFixture will be properly supported in xUnit v3. waiting.")]
public class DeploymentLoginPermissionsTests(SqlServerIntegrationFixture _fixture, ITestOutputHelper _output)
	: IAssemblyFixture<SqlServerIntegrationFixture>
{
	private const string Password = "Deploy-login-1234";

	private readonly string _dbCnnString = _fixture.MasterDbConnectionString.SetRandomMsSqlDatabaseName();

	private string DbName => new SqlConnectionStringBuilder(_dbCnnString).InitialCatalog;

	[Fact]
	public async Task Login_with_ddladmin_datareader_datawriter_deploys_a_database_deployed_by_dbo()
	{
		using TempProject project = new TempProject()
			.AddFile("Migrations/001.migration.sql", "create table dbo.t1 ( id int not null );")
			.AddFile("Code/v1.sql", "create or alter view dbo.v1 as select 1 as one;")
			.AddFile("Code/v2.sql", "create or alter view dbo.v2 as select 2 as two;");

		// the first deployment by an administrator: the schema dblive and its objects belong to dbo.
		await CreateBuilder(project, _dbCnnString).CreateDeployer().DeployAsync(DeployParameters.Default with { CreateDbIfNotExists = true });

		string login = await CreateLoginAsync();
		try
		{
			// v1 is skipped by its hash, v2 is removed from the project, v3 is new.
			_ = project
				.DeleteFiles("v2.sql")
				.AddFile("Migrations/002.migration.sql", "create table dbo.t2 ( id int not null, old_column int null );")
				.AddFile("Migrations/002.breaking.sql", "alter table dbo.t2 drop column old_column;")
				.AddFile("Code/v3.sql", "create or alter view dbo.v3 as select 3 as three;")
				.AddFile("Code/drop-v0.breaking.sql", "drop view if exists dbo.v0;")
				.AddFile("BeforeDeploy/01.sql", "select 1 as one;")
				.AddFile("AfterDeploy/01.sql", "select 1 as one;");

			DbLiveBuilder builder = CreateBuilder(project, LoginCnnString(login));
			await builder.CreateDeployer().DeployAsync(DeployParameters.Default);
			await builder.CreateDeployer().DeployAsync(DeployParameters.Breaking);
		}
		finally
		{
			await DropLoginAsync(login);
		}

		Assert.Equal(
			[
				"code/drop-v0.breaking.sql applied not verified",
				"code/v1.sql applied verified",
				"code/v2.sql removed verified",
				"code/v3.sql applied not verified"
			],
			await QueryAsync("""
				select concat(relative_path, ' ', status, ' ', iif(verified_utc is null, 'not verified', 'verified'))
				from dblive.code
				order by relative_path
				""")
		);
	}

	[Fact]
	public async Task Login_with_ddladmin_datareader_datawriter_deploys_an_empty_database()
	{
		using TempProject project = new TempProject()
			.AddFile("Migrations/001.migration.sql", "create table dbo.t1 ( id int not null );")
			.AddFile("Code/v1.sql", "create or alter view dbo.v1 as select 1 as one;");

		await ExecuteAsync(_fixture.MasterDbConnectionString, $"create database [{DbName}];");

		string login = await CreateLoginAsync();
		try
		{
			await CreateBuilder(project, LoginCnnString(login)).CreateDeployer().DeployAsync(DeployParameters.Default);

			// the second deployment skips the code file by its hash.
			await CreateBuilder(project, LoginCnnString(login)).CreateDeployer().DeployAsync(DeployParameters.Default);
		}
		finally
		{
			await DropLoginAsync(login);
		}

		// the login created the schema dblive, so it owns it.
		Assert.Equal(["code/v1.sql applied verified"], await QueryAsync("""
			select concat(relative_path, ' ', status, ' ', iif(verified_utc is null, 'not verified', 'verified'))
			from dblive.code
			"""));
	}

	private async Task<string> CreateLoginAsync()
	{
		string login = $"dblive-deploy-{Guid.NewGuid():N}";
		await ExecuteAsync(_fixture.MasterDbConnectionString, $"create login [{login}] with password = '{Password}', check_policy = off;");
		await ExecuteAsync(_dbCnnString, $"""
			create user [{login}] for login [{login}];
			alter role db_ddladmin add member [{login}];
			alter role db_datareader add member [{login}];
			alter role db_datawriter add member [{login}];
			""");
		return login;
	}

	// pooled: without pooling every connection in a TransactionScope of DbLive is a new one, and the transaction is promoted
	// to a distributed one. The pools of the login are never used again, so DropLoginAsync kills their connections.
	private string LoginCnnString(string login) =>
		new SqlConnectionStringBuilder(_dbCnnString) { UserID = login, Password = Password }.ConnectionString;

	// an open connection of the login blocks 'drop login'. SqlConnection.ClearAllPools() would also clear the pools
	// of tests running in parallel (see MissingDatabaseTests).
	private async Task DropLoginAsync(string login)
	{
		await ExecuteAsync(_fixture.MasterDbConnectionString, $"""
			declare @sql nvarchar(max) = (
				select string_agg(concat('kill ', session_id, ';'), ' ')
				from sys.dm_exec_sessions
				where login_name = '{login}'
			);
			exec sys.sp_executesql @sql;
			drop login [{login}];
			""");
	}

	private static async Task ExecuteAsync(string cnnString, string sql)
	{
		using SqlConnection cnn = new(cnnString);
		await cnn.OpenAsync();
		using SqlCommand cmd = cnn.CreateCommand();
		cmd.CommandText = sql;
		_ = await cmd.ExecuteNonQueryAsync();
	}

	private async Task<List<string>> QueryAsync(string sql)
	{
		using SqlConnection cnn = new(_dbCnnString);
		await cnn.OpenAsync();
		using SqlCommand cmd = cnn.CreateCommand();
		cmd.CommandText = sql;
		using SqlDataReader reader = await cmd.ExecuteReaderAsync();
		List<string> rows = [];
		while (await reader.ReadAsync())
		{
			rows.Add(reader.GetString(0));
		}
		return rows;
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
