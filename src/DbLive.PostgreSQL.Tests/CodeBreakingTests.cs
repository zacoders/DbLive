using DbLive.Adapter;
using DbLive.Deployers.Code;
using DbLive.Deployers.Migrations;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics.CodeAnalysis;
using Xunit.Extensions.AssemblyFixture;

namespace DbLive.PostgreSQL.Tests;


/// <summary>
/// The contract of a function changes: its file creates get_user_v2 instead of get_user_v1, and a breaking script of the Code folder
/// drops get_user_v1 in the breaking phase. Each test deploys the builds of consecutive releases from one folder.
/// </summary>
[SuppressMessage("Usage", "xUnit1041:Fixture arguments to test classes must have fixture sources", Justification = "AssemblyFixture will be properly supported in xUnit v3. waiting.")]
public class CodeBreakingTests(PostgreSqlFixture _fixture, ITestOutputHelper _output)
	: IAssemblyFixture<PostgreSqlFixture>, IAsyncLifetime
{
	private const string FunctionV1 = "create or replace function get_user_v1() returns int language sql as $$ select 1 $$;";
	private const string FunctionV2 = "create or replace function get_user_v2(p_user_id int) returns int language sql as $$ select p_user_id $$;";
	private const string DropV1 = "drop function if exists get_user_v1();";

	private readonly string _dbCnnString = _fixture.PostgresDBConnectionString.SetRandomPostgreSqlDatabaseName();

	// DbLive deploys to an existing database without creating it.
	public Task InitializeAsync() => new DbLiveBuilder().PostgreSQL().SetDbConnection(_dbCnnString).CreateDbLiveDA().CreateDBAsync();

	public Task DisposeAsync() => Task.CompletedTask;

	[Fact]
	public async Task Code_breaking_script_drops_the_old_version_and_a_revert_restores_it()
	{
		// build N
		using TempProject project = new TempProject()
			.AddFile("Code/public/get_user.sql", FunctionV1);

		DbLiveBuilder builder = CreateBuilder(project);
		IDbLiveDA da = builder.CreateDbLiveDA();

		await builder.CreateDeployer().DeployAsync(DeployParameters.Default);

		// build N+1: the file creates the next version, the breaking script next to it drops the old one.
		_ = project
			.AddFile("Code/public/get_user.sql", FunctionV2)
			.AddFile("Code/public/get_user.breaking.sql", DropV1);
		builder = CreateBuilder(project);

		await builder.CreateDeployer().DeployAsync(DeployParameters.Default);

		// both versions, for the old and the new application; the breaking script is not deployed as code.
		Assert.Equal(["get_user_v1", "get_user_v2"], await FunctionsAsync(da));
		Assert.Equal(["code/public/get_user.sql:code:applied"], await CodeRowsAsync(da));

		await builder.CreateDeployer().DeployAsync(DeployParameters.Breaking);

		Assert.Equal(["get_user_v2"], await FunctionsAsync(da));
		Assert.Equal(["code/public/get_user.breaking.sql:breaking:applied", "code/public/get_user.sql:code:applied"], await CodeRowsAsync(da));

		// a normal deployment of the same build: the breaking script is a file of the project, its row is not marked removed.
		await builder.CreateDeployer().DeployAsync(DeployParameters.Default);

		Assert.Equal(["code/public/get_user.breaking.sql:breaking:applied", "code/public/get_user.sql:code:applied"], await CodeRowsAsync(da));

		// build N+2 reverts the code of build N+1 (git revert): the code file has the hash of N again, so get_user_v1 is back. get_user_v2 stays.
		_ = project
			.DeleteFiles("get_user.breaking.sql")
			.AddFile("Code/public/get_user.sql", FunctionV1);
		builder = CreateBuilder(project);

		await builder.CreateDeployer().DeployAsync(DeployParameters.Default);

		Assert.Equal(["get_user_v1", "get_user_v2"], await FunctionsAsync(da));
		Assert.Equal(["code/public/get_user.breaking.sql:breaking:removed", "code/public/get_user.sql:code:applied"], await CodeRowsAsync(da));

		// build N+3 brings the code of N+1 back: the breaking deployment drops get_user_v1 again.
		_ = project
			.AddFile("Code/public/get_user.sql", FunctionV2)
			.AddFile("Code/public/get_user.breaking.sql", DropV1);
		builder = CreateBuilder(project);

		await builder.CreateDeployer().DeployAsync(DeployParameters.Default);

		Assert.Equal(["get_user_v1", "get_user_v2"], await FunctionsAsync(da));

		await builder.CreateDeployer().DeployAsync(DeployParameters.Breaking);

		Assert.Equal(["get_user_v2"], await FunctionsAsync(da));
		Assert.Equal(["code/public/get_user.breaking.sql:breaking:applied", "code/public/get_user.sql:code:applied"], await CodeRowsAsync(da));
	}

	[Fact]
	public async Task Old_version_in_its_own_deleted_file_is_restored_by_a_revert()
	{
		// build N: the version in a file of its own.
		using TempProject project = new TempProject()
			.AddFile("Code/public/get_user_v1.sql", FunctionV1);

		DbLiveBuilder builder = CreateBuilder(project);
		IDbLiveDA da = builder.CreateDbLiveDA();

		await builder.CreateDeployer().DeployAsync(DeployParameters.Default);

		// build N+1: the file of the old version is deleted, a breaking script drops what it created.
		_ = project
			.DeleteFiles("get_user_v1.sql")
			.AddFile("Code/public/get_user_v2.sql", FunctionV2)
			.AddFile("Code/public/get_user_v1.breaking.sql", DropV1);
		builder = CreateBuilder(project);

		await builder.CreateDeployer().DeployAsync(DeployParameters.Default);
		await builder.CreateDeployer().DeployAsync(DeployParameters.Breaking);

		Assert.Equal(["get_user_v2"], await FunctionsAsync(da));
		Assert.Equal(
			["code/public/get_user_v1.breaking.sql:breaking:applied", "code/public/get_user_v1.sql:code:removed", "code/public/get_user_v2.sql:code:applied"],
			await CodeRowsAsync(da)
		);

		// build N+2 reverts the code of build N+1 (git revert): the file is back with the content it was applied with. Its row is not applied, so it is executed.
		_ = project
			.DeleteFiles("get_user_v2.sql")
			.DeleteFiles("get_user_v1.breaking.sql")
			.AddFile("Code/public/get_user_v1.sql", FunctionV1);
		builder = CreateBuilder(project);

		await builder.CreateDeployer().DeployAsync(DeployParameters.Default);

		Assert.Equal(["get_user_v1", "get_user_v2"], await FunctionsAsync(da));
		Assert.Equal(
			["code/public/get_user_v1.breaking.sql:breaking:removed", "code/public/get_user_v1.sql:code:applied", "code/public/get_user_v2.sql:code:removed"],
			await CodeRowsAsync(da)
		);
	}

	[Fact]
	public async Task Row_of_a_file_with_special_characters_in_its_path_is_not_marked_removed()
	{
		// the project paths reach the database as an array, and lower() of the database compares them with the stored paths.
		using TempProject project = new TempProject()
			.AddFile("Code/public/R&D Отчёт.sql", FunctionV1);

		DbLiveBuilder builder = CreateBuilder(project);
		IDbLiveDA da = builder.CreateDbLiveDA();

		await builder.CreateDeployer().DeployAsync(DeployParameters.Default);
		await builder.CreateDeployer().DeployAsync(DeployParameters.Default);

		Assert.Equal(["code/public/r&d отчёт.sql:code:applied"], await CodeRowsAsync(da));
	}

	[Fact]
	public async Task Breaking_deployment_before_the_normal_deployment_of_the_build_fails()
	{
		using TempProject project = new TempProject()
			.AddFile("Code/public/get_user.sql", FunctionV1);

		DbLiveBuilder builder = CreateBuilder(project);
		IDbLiveDA da = builder.CreateDbLiveDA();

		await builder.CreateDeployer().DeployAsync(DeployParameters.Default);

		// the breaking stage of build N+1 runs while application N still calls get_user_v1.
		_ = project
			.AddFile("Code/public/get_user.sql", FunctionV2)
			.AddFile("Code/public/get_user.breaking.sql", DropV1);
		builder = CreateBuilder(project);

		CodeNotDeployedException ex = await Assert.ThrowsAsync<CodeNotDeployedException>(
			() => builder.CreateDeployer().DeployAsync(DeployParameters.Breaking)
		);

		Assert.Equal(1, ex.Count);
		Assert.Equal(["Code/public/get_user.sql"], ex.RelativePaths);

		Assert.Equal(["get_user_v1"], await FunctionsAsync(da));
		Assert.Equal(["code/public/get_user.sql:code:applied"], await CodeRowsAsync(da));
	}

	[Fact]
	public async Task Migration_breaking_script_does_not_run_after_a_failed_code_deployment()
	{
		using TempProject project = new TempProject()
			.AddFile("Migrations/001.migration.sql", "create table users ( user_id int not null, phone varchar(32) null );")
			.AddFile("Migrations/001.breaking.sql", "alter table users drop column if exists phone;")
			.AddFile("Code/public/get_user.sql", "create or replace function get_user_v2() returns int language sql as $$ selec 1 $$;");

		DbLiveBuilder builder = CreateBuilder(project);
		IDbLiveDA da = builder.CreateDbLiveDA();

		// the migration is applied and committed, then the code fails.
		_ = await Assert.ThrowsAsync<CodeDeploymentAggregateException>(
			() => builder.CreateDeployer().DeployAsync(DeployParameters.Default)
		);

		_ = await Assert.ThrowsAsync<CodeNotDeployedException>(
			() => builder.CreateDeployer().DeployAsync(DeployParameters.Breaking)
		);

		Assert.Equal(1, await ScalarAsync(da, "select count(*) from information_schema.columns where table_name = 'users' and column_name = 'phone'"));
	}

	[Theory]
	[InlineData("Migration", "code/public/get_user.breaking.sql:breaking:error")]
	// in a deployment transaction no error row is written, and the deployment is rolled back.
	[InlineData("Deployment", null)]
	public async Task Failed_code_breaking_script_fails_with_its_own_error(string transactionWrapLevel, string? breakingRow)
	{
		using TempProject project = new TempProject()
			.AddFile("settings.json", $$"""{ "TransactionWrapLevel": "{{transactionWrapLevel}}" }""")
			.AddFile("Code/public/get_user.sql", FunctionV2)
			// without 'if exists': get_user_v1 never existed in this database.
			.AddFile("Code/public/get_user.breaking.sql", "drop function get_user_v1();");

		DbLiveBuilder builder = CreateBuilder(project);
		IDbLiveDA da = builder.CreateDbLiveDA();

		await builder.CreateDeployer().DeployAsync(DeployParameters.Default);

		CodeDeploymentException ex = await Assert.ThrowsAsync<CodeDeploymentException>(
			() => builder.CreateDeployer().DeployAsync(DeployParameters.Breaking)
		);

		Assert.Equal("Deploy code breaking script error. File path: Code/public/get_user.breaking.sql", ex.Message);
		Assert.Contains("get_user_v1() does not exist", Assert.IsType<DbLiveSqlException>(ex.InnerException).Message);

		Assert.Equal(
			[.. new[] { breakingRow, "code/public/get_user.sql:code:applied" }.OfType<string>()],
			await CodeRowsAsync(da)
		);
	}

	/// <summary>
	/// A builder reads the project once: a new build of the folder takes a new builder, like a new run of a release pipeline.
	/// </summary>
	private DbLiveBuilder CreateBuilder(TempProject project)
	{
		return new DbLiveBuilder()
			.ConfigureServices(s => s.AddXUnitLogger(_output))
			.PostgreSQL()
			.SetDbConnection(_dbCnnString)
			.SetScriptsFolder(project.Path);
	}

	private static async Task<List<string>> FunctionsAsync(IDbLiveDA da)
	{
		List<SqlResult> results = await da.ExecuteQueryMultipleAsync(
			"select proname::text from pg_proc where pronamespace = 'public'::regnamespace and proname like 'get_user%'");
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
