using DbLive;
using DbLive.Common;
using DbLive.Deployers.Testing;
using DbLive.MSSQL;
using DbLive.xunit;
using DotNet.Testcontainers.Containers;
using Testcontainers.MsSql;
using Xunit;
using Xunit.Abstractions;

namespace DemoMSSQL.Tests;


public class MyDbLiveTestingMSSQLFixture()
	: DbLiveTestingFixture(dropDatabaseOnComplete: true)
{
	public const string SqlProjectName = "DemoMSSQL";
	public const string DockerImage = "mcr.microsoft.com/mssql/server:2025-latest";

	private static readonly MsSqlContainer _dockerContainer
		= new MsSqlBuilder()
				.WithImage(DockerImage)
				.WithName("DbLive.DemoMSSQL")
				//.WithReuse(true)
				.Build();

	public async override Task<DbLiveBuilder> GetBuilderAsync()
	{
		if (_dockerContainer.State != TestcontainersStates.Running)
		{
			await _dockerContainer.StartAsync();
		}

		string masterDbCnnString = _dockerContainer.GetConnectionString();
		string dbCnnString = masterDbCnnString.SetRandomDatabaseName();

		// or just local sql server
		//string dbCnnString = "Server=localhost;Database=master;Trusted_Connection=True;".SetRandomDatabaseName();

		return new DbLiveBuilder()
			.SqlServer()
			.SetDbConnection(dbCnnString)
			.SetProjectPath(Path.GetFullPath(SqlProjectName));
	}
}

public class DBTests(ITestOutputHelper _output, MyDbLiveTestingMSSQLFixture _fixture)
	: IClassFixture<MyDbLiveTestingMSSQLFixture>
{
	// TODO: if there are a lot of tests they will be in the same place
	// it will be good to separate them by folders, it can be done by adding filter and creating multiple test methods.
	[SqlFact(SqlAssemblyName = MyDbLiveTestingMSSQLFixture.SqlProjectName)]
	public void Sql(string testFileRelativePath)
	{
		TestRunResult result = _fixture.Tester!.RunTest(_output.WriteLine, testFileRelativePath);
		Assert.True(result.IsSuccess, result.ErrorMessage);
	}
}


public class DeploymentTests(ITestOutputHelper _output, MyDbLiveTestingMSSQLFixture fixture)
	: IClassFixture<MyDbLiveTestingMSSQLFixture>
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
		builder.LogToXUnitOutput(_output);
		IDbLive deployer = builder.CreateDeployer();

		deployer.Deploy(new DeployParameters
		{
			CreateDbIfNotExists = true,
			DeployBreaking = breaking,
			DeployCode = true,
			DeployMigrations = true,
			RunTests = true,
			UndoTestDeployment = undoTestingMode
		});
	}
}
