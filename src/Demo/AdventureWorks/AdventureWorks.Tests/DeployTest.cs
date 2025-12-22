using DbLive;
using DbLive.Common;
using DbLive.MSSQL;
using DbLive.xunit;
using Testcontainers.MsSql;
using Xunit.Abstractions;

namespace AdventureWorks.Tests;


public class DeployTest(ITestOutputHelper output)
{
	public const string DockerImage = "mcr.microsoft.com/mssql/server:2022-latest";
	private readonly MsSqlContainer _dockerContainer = new MsSqlBuilder().WithImage(DockerImage).Build();

	[Fact]
	public async Task DeployAsync()
	{
		await _dockerContainer.StartAsync();
		string masterDbCnnString = _dockerContainer.GetConnectionString();
		string dbCnnString = masterDbCnnString.SetRandomDatabaseName();

		Deploy(dbCnnString);
	}

	[Fact(Skip = "For local run only.")]
	public async Task DeployToLocalSqlServerAsync()
	{
		string dbCnnString = "Server=localhost;Database=AdventureWorksEasyFlow;Trusted_Connection=True;";
		Deploy(dbCnnString);
	}

	private void Deploy(string dbCnnString)
	{
		string projectPath = Path.GetFullPath(MyEasyFlowTestingMSSQLFixture.SqlProjectName);

		IEasyFlowBuilder builder = new EasyFlowBuilder()
			.LogToXUnitOutput(output)
			.SqlServer()
			.SetDbConnection(dbCnnString)
			.SetProjectPath(projectPath);

		var deployer = builder.CreateDeployer();

		deployer.Deploy(
			new DeployParameters
			{
				CreateDbIfNotExists = true,
				DeployBreaking = false,
				DeployCode = true,
				DeployMigrations = true,
				RunTests = false
			}
		);
	}
}