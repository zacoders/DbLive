using DbLive.Adapter;
using DbLive.Deployers.Code;
using DbLive.Deployers.Migrations;
using System.Diagnostics.CodeAnalysis;
using Xunit.Extensions.AssemblyFixture;

namespace DbLive.MSSQL.Tests;


/// <summary>
/// The contract of a procedure changes: its file creates GetUser_v2 instead of GetUser_v1, and a breaking script of the Code folder
/// drops GetUser_v1 in the breaking phase. Each test deploys the builds of consecutive releases from one folder.
/// </summary>
[SuppressMessage("Usage", "xUnit1041:Fixture arguments to test classes must have fixture sources", Justification = "AssemblyFixture will be properly supported in xUnit v3. waiting.")]
public class CodeBreakingTests(SqlServerIntegrationFixture _fixture, ITestOutputHelper _output)
	: IAssemblyFixture<SqlServerIntegrationFixture>, IAsyncLifetime
{
	private const string ProcV1 = "create or alter proc dbo.GetUser_v1 as select 1 as user_id;";
	private const string ProcV2 = "create or alter proc dbo.GetUser_v2 @UserId int as select @UserId as user_id;";
	private const string DropV1 = "drop proc if exists dbo.GetUser_v1;";

	private readonly string _dbCnnString = _fixture.MasterDbConnectionString.SetRandomMsSqlDatabaseName();

	// DbLive deploys to an existing database without creating it.
	public Task InitializeAsync() => new DbLiveBuilder().SqlServer().SetDbConnection(_dbCnnString).GetRequiredService<IDbLiveDA>().CreateDBAsync();

	public Task DisposeAsync() => Task.CompletedTask;

	[Fact]
	public async Task Code_breaking_script_drops_the_old_version_and_a_revert_restores_it()
	{
		// build N
		using TempProject project = new TempProject()
			.AddFile("Code/dbo/GetUser.sql", ProcV1);

		DbLiveBuilder builder = CreateBuilder(project);
		IDbLiveDA da = builder.GetRequiredService<IDbLiveDA>();

		await builder.CreateDeployer().DeployAsync(DeployParameters.Default);

		// build N+1: the file creates the next version, the breaking script next to it drops the old one.
		_ = project
			.AddFile("Code/dbo/GetUser.sql", ProcV2)
			.AddFile("Code/dbo/GetUser.breaking.sql", DropV1);
		builder = CreateBuilder(project);

		await builder.CreateDeployer().DeployAsync(DeployParameters.Default);

		// both versions, for the old and the new application; the breaking script is not deployed as code.
		Assert.Equal(["GetUser_v1", "GetUser_v2"], await ProceduresAsync(da));
		Assert.Equal(["code/dbo/getuser.sql:code:applied"], await CodeRowsAsync(da));

		await builder.CreateDeployer().DeployAsync(DeployParameters.Breaking);

		Assert.Equal(["GetUser_v2"], await ProceduresAsync(da));
		Assert.Equal(["code/dbo/getuser.breaking.sql:breaking:applied", "code/dbo/getuser.sql:code:applied"], await CodeRowsAsync(da));

		// a normal deployment of the same build: the breaking script is a file of the project, its row is not marked removed.
		await builder.CreateDeployer().DeployAsync(DeployParameters.Default);

		Assert.Equal(["code/dbo/getuser.breaking.sql:breaking:applied", "code/dbo/getuser.sql:code:applied"], await CodeRowsAsync(da));

		// build N+2 reverts the code of build N+1 (git revert): the code file has the hash of N again, so GetUser_v1 is back. GetUser_v2 stays.
		_ = project
			.DeleteFiles("GetUser.breaking.sql")
			.AddFile("Code/dbo/GetUser.sql", ProcV1);
		builder = CreateBuilder(project);

		await builder.CreateDeployer().DeployAsync(DeployParameters.Default);

		Assert.Equal(["GetUser_v1", "GetUser_v2"], await ProceduresAsync(da));
		Assert.Equal(["code/dbo/getuser.breaking.sql:breaking:removed", "code/dbo/getuser.sql:code:applied"], await CodeRowsAsync(da));

		// build N+3 brings the code of N+1 back: the breaking deployment drops GetUser_v1 again.
		_ = project
			.AddFile("Code/dbo/GetUser.sql", ProcV2)
			.AddFile("Code/dbo/GetUser.breaking.sql", DropV1);
		builder = CreateBuilder(project);

		await builder.CreateDeployer().DeployAsync(DeployParameters.Default);

		Assert.Equal(["GetUser_v1", "GetUser_v2"], await ProceduresAsync(da));

		await builder.CreateDeployer().DeployAsync(DeployParameters.Breaking);

		Assert.Equal(["GetUser_v2"], await ProceduresAsync(da));
		Assert.Equal(["code/dbo/getuser.breaking.sql:breaking:applied", "code/dbo/getuser.sql:code:applied"], await CodeRowsAsync(da));
	}

	[Fact]
	public async Task Old_version_in_its_own_deleted_file_is_restored_by_a_revert()
	{
		// build N: the version in a file of its own.
		using TempProject project = new TempProject()
			.AddFile("Code/dbo/GetUser_v1.sql", ProcV1);

		DbLiveBuilder builder = CreateBuilder(project);
		IDbLiveDA da = builder.GetRequiredService<IDbLiveDA>();

		await builder.CreateDeployer().DeployAsync(DeployParameters.Default);

		// build N+1: the file of the old version is deleted, a breaking script drops what it created.
		_ = project
			.DeleteFiles("GetUser_v1.sql")
			.AddFile("Code/dbo/GetUser_v2.sql", ProcV2)
			.AddFile("Code/dbo/GetUser_v1.breaking.sql", DropV1);
		builder = CreateBuilder(project);

		await builder.CreateDeployer().DeployAsync(DeployParameters.Default);
		await builder.CreateDeployer().DeployAsync(DeployParameters.Breaking);

		Assert.Equal(["GetUser_v2"], await ProceduresAsync(da));
		Assert.Equal(
			["code/dbo/getuser_v1.breaking.sql:breaking:applied", "code/dbo/getuser_v1.sql:code:removed", "code/dbo/getuser_v2.sql:code:applied"],
			await CodeRowsAsync(da)
		);

		// build N+2 reverts the code of build N+1 (git revert): the file is back with the content it was applied with. Its row is not applied, so it is executed.
		_ = project
			.DeleteFiles("GetUser_v2.sql")
			.DeleteFiles("GetUser_v1.breaking.sql")
			.AddFile("Code/dbo/GetUser_v1.sql", ProcV1);
		builder = CreateBuilder(project);

		await builder.CreateDeployer().DeployAsync(DeployParameters.Default);

		Assert.Equal(["GetUser_v1", "GetUser_v2"], await ProceduresAsync(da));
		Assert.Equal(
			["code/dbo/getuser_v1.breaking.sql:breaking:removed", "code/dbo/getuser_v1.sql:code:applied", "code/dbo/getuser_v2.sql:code:removed"],
			await CodeRowsAsync(da)
		);
	}

	[Fact]
	public async Task Row_of_a_file_with_special_characters_in_its_path_is_not_marked_removed()
	{
		// the project paths reach the database as xml, and lower() of the database compares them with the stored paths.
		using TempProject project = new TempProject()
			.AddFile("Code/dbo/R&D Отчёт.sql", ProcV1);

		DbLiveBuilder builder = CreateBuilder(project);
		IDbLiveDA da = builder.GetRequiredService<IDbLiveDA>();

		await builder.CreateDeployer().DeployAsync(DeployParameters.Default);
		await builder.CreateDeployer().DeployAsync(DeployParameters.Default);

		Assert.Equal(["code/dbo/r&d отчёт.sql:code:applied"], await CodeRowsAsync(da));
	}

	[Fact]
	public async Task Breaking_deployment_before_the_normal_deployment_of_the_build_fails()
	{
		using TempProject project = new TempProject()
			.AddFile("Code/dbo/GetUser.sql", ProcV1);

		DbLiveBuilder builder = CreateBuilder(project);
		IDbLiveDA da = builder.GetRequiredService<IDbLiveDA>();

		await builder.CreateDeployer().DeployAsync(DeployParameters.Default);

		// the breaking stage of build N+1 runs while application N still calls GetUser_v1.
		_ = project
			.AddFile("Code/dbo/GetUser.sql", ProcV2)
			.AddFile("Code/dbo/GetUser.breaking.sql", DropV1);
		builder = CreateBuilder(project);

		CodeNotDeployedException ex = await Assert.ThrowsAsync<CodeNotDeployedException>(
			() => builder.CreateDeployer().DeployAsync(DeployParameters.Breaking)
		);

		Assert.Equal(1, ex.Count);
		Assert.Equal(["Code/dbo/GetUser.sql"], ex.RelativePaths);

		Assert.Equal(["GetUser_v1"], await ProceduresAsync(da));
		Assert.Equal(["code/dbo/getuser.sql:code:applied"], await CodeRowsAsync(da));
	}

	[Fact]
	public async Task Migration_breaking_script_does_not_run_after_a_failed_code_deployment()
	{
		using TempProject project = new TempProject()
			.AddFile("Migrations/001.migration.sql", "create table dbo.Users ( UserId int not null, Phone varchar(32) null );")
			.AddFile("Migrations/001.breaking.sql", "alter table dbo.Users drop column if exists Phone;")
			.AddFile("Code/dbo/GetUser.sql", "create or alter proc dbo.GetUser_v2 as selec 1;");

		DbLiveBuilder builder = CreateBuilder(project);
		IDbLiveDA da = builder.GetRequiredService<IDbLiveDA>();

		// the migration is applied and committed, then the code fails.
		_ = await Assert.ThrowsAsync<CodeDeploymentAggregateException>(
			() => builder.CreateDeployer().DeployAsync(DeployParameters.Default)
		);

		_ = await Assert.ThrowsAsync<CodeNotDeployedException>(
			() => builder.CreateDeployer().DeployAsync(DeployParameters.Breaking)
		);

		Assert.Equal(1, await ScalarAsync(da, "select count(*) from sys.columns where object_id = object_id('dbo.Users') and name = 'Phone'"));
	}

	[Theory]
	[InlineData("Migration", "code/dbo/getuser.breaking.sql:breaking:error")]
	// in a deployment transaction no error row is written, and the deployment is rolled back.
	[InlineData("Deployment", null)]
	public async Task Failed_code_breaking_script_fails_with_its_own_error(string transactionWrapLevel, string? breakingRow)
	{
		using TempProject project = new TempProject()
			.AddFile("settings.json", $$"""{ "TransactionWrapLevel": "{{transactionWrapLevel}}" }""")
			.AddFile("Code/dbo/GetUser.sql", ProcV2)
			// without 'if exists': GetUser_v1 never existed in this database.
			.AddFile("Code/dbo/GetUser.breaking.sql", "set xact_abort on; drop proc dbo.GetUser_v1;");

		DbLiveBuilder builder = CreateBuilder(project);
		IDbLiveDA da = builder.GetRequiredService<IDbLiveDA>();

		await builder.CreateDeployer().DeployAsync(DeployParameters.Default);

		CodeDeploymentException ex = await Assert.ThrowsAsync<CodeDeploymentException>(
			() => builder.CreateDeployer().DeployAsync(DeployParameters.Breaking)
		);

		Assert.Equal("Deploy code breaking script error. File path: Code/dbo/GetUser.breaking.sql", ex.Message);
		Assert.Contains("Cannot drop the procedure 'dbo.GetUser_v1'", Assert.IsType<DbLiveSqlException>(ex.InnerException).Message);

		Assert.Equal(
			[.. new[] { breakingRow, "code/dbo/getuser.sql:code:applied" }.OfType<string>()],
			await CodeRowsAsync(da)
		);
	}

	[Fact]
	public async Task Failed_code_file_in_a_deployment_transaction_is_retried()
	{
		// an error keeps the deployment transaction of SQL Server usable: the view, deployed first (path order),
		// fails without the function and succeeds on its retry.
		using TempProject project = new TempProject()
			.AddFile("settings.json", """{ "TransactionWrapLevel": "Deployment" }""")
			.AddFile("Code/a_view.sql", "create or alter view dbo.UserOne as select dbo.GetOne() as one;")
			.AddFile("Code/b_function.sql", "create or alter function dbo.GetOne() returns int as begin return 1; end;");

		DbLiveBuilder builder = CreateBuilder(project);
		IDbLiveDA da = builder.GetRequiredService<IDbLiveDA>();

		await builder.CreateDeployer().DeployAsync(DeployParameters.Default);

		Assert.Equal(["code/a_view.sql:code:applied", "code/b_function.sql:code:applied"], await CodeRowsAsync(da));
		Assert.Equal(1, await ScalarAsync(da, "select one from dbo.UserOne"));
	}

	/// <summary>
	/// A builder reads the project once: a new build of the folder takes a new builder, like a new run of a release pipeline.
	/// </summary>
	private DbLiveBuilder CreateBuilder(TempProject project)
	{
		return new DbLiveBuilder()
			.LogToXUnitOutput(_output)
			.SqlServer()
			.SetDbConnection(_dbCnnString)
			.SetScriptsFolder(project.Path);
	}

	private static async Task<List<string>> ProceduresAsync(IDbLiveDA da)
	{
		List<SqlResult> results = await da.ExecuteQueryMultipleAsync("select name from sys.procedures where name like 'GetUser%'");
		return [.. results.Single().Rows.Select(r => (string)r[0]!).Order(StringComparer.Ordinal)];
	}

	private static async Task<List<string>> CodeRowsAsync(IDbLiveDA da)
	{
		List<SqlResult> results = await da.ExecuteQueryMultipleAsync("select relative_path, item_type, status from dblive.code");
		return [.. results.Single().Rows.Select(r => $"{r[0]}:{r[1]}:{r[2]}").Order(StringComparer.Ordinal)];
	}

	private static async Task<long> ScalarAsync(IDbLiveDA da, string query)
	{
		List<SqlResult> results = await da.ExecuteQueryMultipleAsync(query);
		return Convert.ToInt64(results.Single().Rows.Single()[0]);
	}
}
