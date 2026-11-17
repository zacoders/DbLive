using DbLive.Adapter;
using DbLive.Testing;
using System.Diagnostics.CodeAnalysis;
using Xunit.Extensions.AssemblyFixture;

namespace DbLive.PostgreSQL.Tests;

// exploratory, 0096-87: not to be kept.
[SuppressMessage("Usage", "xUnit1041:Fixture arguments to test classes must have fixture sources", Justification = "AssemblyFixture will be properly supported in xUnit v3. waiting.")]
public class ScriptEndsTransactionExploreTests(PostgreSqlFixture _fixture, ITestOutputHelper _output)
	: IAssemblyFixture<PostgreSqlFixture>, IAsyncLifetime
{
	private readonly string _dbCnnString = _fixture.PostgresDBConnectionString.SetRandomPostgreSqlDatabaseName();

	public Task InitializeAsync() => new DbLiveBuilder().PostgreSQL().SetDbConnection(_dbCnnString).GetRequiredService<IDbLiveDA>().CreateDBAsync();

	public Task DisposeAsync() => Task.CompletedTask;

	[Theory]
	[InlineData("Migration", "migration", "rollback")]
	[InlineData("Migration", "migration", "commit")]
	[InlineData("Deployment", "migration", "rollback")]
	[InlineData("Deployment", "code", "rollback")]
	[InlineData("Migration", "code", "rollback")]
	[InlineData("Migration", "code", "commit")]
	public async Task Explore(string level, string where, string end)
	{
		using TempProject project = new TempProject()
			.AddFile("settings.json", $$"""{ "TransactionWrapLevel": "{{level}}" }""")
			.AddFile("Migrations/001.migration.sql", "create table t ( id int not null primary key );");

		await CreateBuilder(project).CreateDeployer().DeployAsync(DeployParameters.Default);

		string script = $"insert into t ( id ) values ( 1 ); {end}; insert into t ( id ) values ( 2 );";
		_ = where switch
		{
			"migration" => project
				.AddFile("Migrations/002.migration.sql", script)
				.AddFile("Migrations/003.migration.sql", "insert into t ( id ) values ( 3 );"),
			"code" => project
				.AddFile("Code/a.sql", "create or replace view v1 as select 1 as x; " + script + " create or replace view v2 as select 2 as x;"),
			_ => throw new ArgumentOutOfRangeException(nameof(where))
		};

		for (int run = 1; run <= 2; run++)
		{
			DbLiveBuilder builder = CreateBuilder(project);
			_output.WriteLine($"===== {level}, {where}, {end}: deployment {run} =====");
			try
			{
				await builder.CreateDeployer().DeployAsync(DeployParameters.Default);
				_output.WriteLine("RESULT: no exception");
			}
			catch (Exception ex)
			{
				for (Exception? e = ex; e is not null; e = e.InnerException)
				{
					_output.WriteLine($"RESULT: {e.GetType().FullName}: {e.Message}");
				}
			}

			IDbLiveDA da = builder.GetRequiredService<IDbLiveDA>();
			foreach (string row in await ListAsync(da, "select concat(version, ' ', item_type, ' ', status) from dblive.migration"))
			{
				_output.WriteLine($"dblive.migration: {row}");
			}
			foreach (string row in await ListAsync(da, "select id::text from t"))
			{
				_output.WriteLine($"t: {row}");
			}
			foreach (string row in await ListAsync(da, "select concat(relative_path, ' ', status) from dblive.code union all select concat('view ', table_name) from information_schema.views where table_schema = 'public'"))
			{
				_output.WriteLine($"other: {row}");
			}
		}
	}

	// 0096-76 checks the transaction after a test with IsTransactionUsableAsync: 'select 1' on PostgreSQL.
	[Theory]
	[InlineData("commit")]
	[InlineData("rollback")]
	public async Task Explore_test(string end)
	{
		using TempProject project = new TempProject()
			.AddFile("Migrations/001.migration.sql", "create table t ( id int not null primary key );")
			.AddFile("Tests/end.sql", $"insert into t ( id ) values ( 100 ); {end}; insert into t ( id ) values ( 200 ); select 1 as x; select 'rows' as assert; select 1 as x;");

		DbLiveBuilder builder = CreateBuilder(project);
		await builder.CreateDeployer().DeployAsync(DeployParameters.Default);

		_output.WriteLine($"===== test, {end} =====");
		try
		{
			TestRunResult result = await builder.CreateTester().RunTestAsync(_output.WriteLine, "Tests/end.sql");
			_output.WriteLine($"RESULT: IsSuccess {result.IsSuccess}, {result.ErrorMessage}");
		}
		catch (Exception ex)
		{
			for (Exception? e = ex; e is not null; e = e.InnerException)
			{
				_output.WriteLine($"RESULT: {e.GetType().FullName}: {e.Message}");
			}
		}

		foreach (string row in await ListAsync(builder.GetRequiredService<IDbLiveDA>(), "select id::text from t"))
		{
			_output.WriteLine($"t: {row}");
		}
	}

	private DbLiveBuilder CreateBuilder(TempProject project)
	{
		return new DbLiveBuilder()
			.PostgreSQL()
			.SetDbConnection(_dbCnnString)
			.SetScriptsFolder(project.Path);
	}

	private static async Task<List<string>> ListAsync(IDbLiveDA da, string query)
	{
		List<SqlResult> results = await da.QuerySqlAsync(query);
		return [.. results.Single().Rows.Select(r => Convert.ToString(r[0])!).Order(StringComparer.Ordinal)];
	}
}
