using DbLive.Adapter;
using DbLive.Project.Exceptions;
using Microsoft.Data.SqlClient;
using System.Diagnostics.CodeAnalysis;
using Xunit.Extensions.AssemblyFixture;

namespace DbLive.MSSQL.Tests;


/// <summary>
/// The project is read and checked before anything touches the server: an error of the project creates no database and runs
/// no self-migration, so an older DbLive can still deploy the database.
/// </summary>
[SuppressMessage("Usage", "xUnit1041:Fixture arguments to test classes must have fixture sources", Justification = "AssemblyFixture will be properly supported in xUnit v3. waiting.")]
public class InvalidProjectTests(SqlServerIntegrationFixture _fixture, ITestOutputHelper _output)
	: IAssemblyFixture<SqlServerIntegrationFixture>
{
	private readonly string _dbCnnString = _fixture.MasterDbConnectionString.SetRandomMsSqlDatabaseName();

	private string DbName => new SqlConnectionStringBuilder(_dbCnnString).InitialCatalog;

	[Fact]
	public async Task Undo_script_fails_the_deployment_before_the_self_deployment()
	{
		// a new database, not deployed by this DbLive: a database it deployed has no pending self-migration.
		using TempProject project = CreateProjectWithUndoScript();
		DbLiveBuilder builder = CreateBuilder(project);
		IDbLiveDA da = builder.CreateDbLiveDA();
		await da.CreateDBAsync();

		UndoNotSupportedException ex = await Assert.ThrowsAsync<UndoNotSupportedException>(
			() => builder.CreateDeployer().DeployAsync(DeployParameters.Default)
		);

		Assert.Equal("Migrations/001.undo.sql", ex.RelativePath);
		Assert.Equal(0, await ScalarAsync(da, "select count(*) from sys.schemas where name = 'dblive'"));
		Assert.Equal(0, await ScalarAsync(da, "select count(*) from sys.tables"));
	}

	[Fact]
	public async Task Undo_script_fails_the_deployment_before_the_database_is_created()
	{
		using TempProject project = CreateProjectWithUndoScript();

		_ = await Assert.ThrowsAsync<UndoNotSupportedException>(
			() => CreateBuilder(project).CreateDeployer().DeployAsync(DeployParameters.Default with { CreateDbIfNotExists = true })
		);

		Assert.False(await DatabaseExistsAsync());
	}

	private static TempProject CreateProjectWithUndoScript() => new TempProject()
		.AddFile("Migrations/001.migration.sql", "create table dbo.t1 ( id int not null );")
		// left in the project after the upgrade to DbLive 2.0, which has no undo scripts.
		.AddFile("Migrations/001.undo.sql", "drop table dbo.t1;");

	private async Task<bool> DatabaseExistsAsync()
	{
		using SqlConnection cnn = new(_fixture.MasterDbConnectionString);
		await cnn.OpenAsync();
		using SqlCommand cmd = cnn.CreateCommand();
		cmd.CommandText = "select count(*) from sys.databases where name = @name";
		_ = cmd.Parameters.AddWithValue("name", DbName);
		return (int)(await cmd.ExecuteScalarAsync())! == 1;
	}

	private static async Task<long> ScalarAsync(IDbLiveDA da, string query)
	{
		List<SqlResult> results = await da.ExecuteQueryMultipleAsync(query);
		return Convert.ToInt64(results.Single().Rows.Single()[0]);
	}

	private DbLiveBuilder CreateBuilder(TempProject project)
	{
		return new DbLiveBuilder()
			.LogToXUnitOutput(_output)
			.SqlServer()
			.SetDbConnection(_dbCnnString)
			.SetScriptsFolder(project.Path);
	}
}
