using DbLive;
using DbLive.Common;
using DbLive.Deployers.Testing;
using DbLive.PostgreSQL;
using DbLive.xunit;
using DotNet.Testcontainers.Containers;
using Testcontainers.PostgreSql;
using Xunit.Abstractions;

namespace Demo.PostgreSQL.Chinook.Tests;


public class MyPostgreSQLFixture()
	: DbLiveTestingFixture(dropDatabaseOnComplete: true)
{
	public const string SqlProjectName = "Demo.PostgreSQL.Chinook";
	public const string DockerImage = "mcr.microsoft.com/mssql/server:2025-latest";

	private static readonly PostgreSqlContainer _dockerContainer
		= new PostgreSqlBuilder("postgres:latest")
			.WithName("Demo.PostgreSQL.Chinook")
			//.WithReuse(true)
			.Build();

	public async override Task<DbLiveBuilder> GetBuilderAsync()
	{
		if (_dockerContainer.State != TestcontainersStates.Running)
		{
			await _dockerContainer.StartAsync().ConfigureAwait(false);
		}

		string masterDbCnnString = _dockerContainer.GetConnectionString();
		string dbCnnString = masterDbCnnString.SetRandomPostgreSqlDatabaseName();

		// or just local sql server
		//string dbCnnString = "...".SetRandomDatabaseName();

		return new DbLiveBuilder()
			.PostgreSQL()
			.SetDbConnection(dbCnnString)
			.SetProjectPath(Path.GetFullPath(SqlProjectName));
	}
}

public class DBTests(ITestOutputHelper _output, MyPostgreSQLFixture _fixture)
	: IClassFixture<MyPostgreSQLFixture>
{
	[SqlFact(SqlAssemblyName = MyPostgreSQLFixture.SqlProjectName)]
	public async Task Sql(string testFileRelativePath)
	{
		TestRunResult result = await _fixture.Tester!.RunTestAsync(_output.WriteLine, testFileRelativePath).ConfigureAwait(false);
		Assert.True(result.IsSuccess, result.ErrorMessage);
	}
}


public class DeploymentTests(ITestOutputHelper _output, MyPostgreSQLFixture fixture)
	: IClassFixture<MyPostgreSQLFixture>
{
	[Theory]
	[InlineData(false, UndoTestMode.None)]
	[InlineData(true, UndoTestMode.None)]
	[InlineData(false, UndoTestMode.MigrationUndoMigration)]
	[InlineData(true, UndoTestMode.MigrationUndoMigration)]
	[InlineData(false, UndoTestMode.MigrationBreakingUndoMigration)]
	[InlineData(true, UndoTestMode.MigrationBreakingUndoMigration)]
	public async Task Deploy(bool breaking, UndoTestMode undoTestingMode)
	{
		_output.WriteLine($"Deploying with breaking={breaking}, undoTestingMode={undoTestingMode}");

		DbLiveBuilder builder = await fixture.GetBuilderAsync();

		IDbLive deployer = builder
			.LogToXUnitOutput(_output)
			.CreateDeployer();

		await deployer.DeployAsync(new DeployParameters
		{
			CreateDbIfNotExists = true,
			DeployBreaking = breaking,
			DeployCode = true,
			DeployMigrations = true,
			RunTests = true,
			UndoTestDeployment = undoTestingMode
		});
	}


	[Fact]
	[Trait("Category", "LocalOnly")]
	public async Task DeployToLocalSqlServerAsync()
	{
		string dbCnnString = "Host=localhost;Port=5433;Database=dblive_chinook;Username=postgres;Password=123123;";
		string projectPath = Path.GetFullPath(MyPostgreSQLFixture.SqlProjectName);

		DbLiveBuilder builder = new DbLiveBuilder()
			.LogToXUnitOutput(_output)
			.PostgreSQL()
			.SetDbConnection(dbCnnString)
			.SetProjectPath(projectPath);

		IDbLive deployer = builder.CreateDeployer();

		await deployer.DeployAsync(
			new DeployParameters
			{
				CreateDbIfNotExists = true,
				DeployBreaking = false,
				DeployCode = true,
				DeployMigrations = true,
				RunTests = true
			}
		);
	}
}
