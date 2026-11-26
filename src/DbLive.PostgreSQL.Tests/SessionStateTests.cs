using DbLive.Adapter;
using DbLive.Testing;
using System.Diagnostics.CodeAnalysis;

namespace DbLive.PostgreSQL.Tests;

/// <summary>
/// A PostgreSQL transaction belongs to one session: Npgsql gives every connection opened in one <c>TransactionScope</c> the session of
/// the transaction, and resets it only when the connection goes back to the pool after the transaction. So the scripts of one
/// transaction share their session state (temporary tables, <c>SET</c>), unlike on SQL Server, where each script gets a reset session.
/// </summary>
[SuppressMessage("Usage", "xUnit1041:Fixture arguments to test classes must have fixture sources", Justification = "AssemblyFixture will be properly supported in xUnit v3. waiting.")]
public class SessionStateTests(PostgreSqlFixture fixture, ITestOutputHelper output)
	: PostgreSqlDbTests(fixture, output)
{
	[Fact]
	public async Task Temporary_table_and_setting_of_setup_test_sql_reach_the_test()
	{
		using TempProject project = new TempProject()
			.AddFile("Migrations/001.migration.sql", "create table t ( x int );")
			.AddFile("Tests/Session/setup.test.sql", """
				create temporary table setup_rows as select 1 as x;
				set application_name = 'from setup';
				""")
			.AddFile("Tests/Session/session.sql", """
				select x, current_setting('application_name') as app from setup_rows;
				select 'rows' as assert;
				select 1 as x, 'from setup' as app;
				""");

		DbLiveBuilder builder = CreateBuilder(project);
		await builder.CreateDeployer().DeployAsync(DeployParameters.Default);

		TestRunResult result = await builder.CreateTester().RunTestAsync(Output.WriteLine, "Tests/Session/session.sql");

		Assert.True(result.IsSuccess, result.ErrorMessage);
	}

	[Theory]
	// the deployment transaction is one session: the setting of the migration stays for the code file after it.
	[InlineData("Deployment", "create schema s; set search_path = s;", "s")]
	// 'set local' lasts to the end of the transaction, which is the deployment.
	[InlineData("Deployment", "create schema s; set local search_path = s;", "s")]
	// a script that restores the setting at its end.
	[InlineData("Deployment", "create schema s; set search_path = s; create table t ( x int ); reset search_path;", "public")]
	// a transaction of its own: the session is reset when the connection goes back to the pool after it.
	[InlineData("Migration", "create schema s; set search_path = s;", "public")]
	public async Task Session_setting_of_a_migration_reaches_the_code_files_only_in_the_deployment_transaction(
		string transactionWrapLevel,
		string migration,
		string schemaOfView
	)
	{
		using TempProject project = new TempProject()
			.AddFile("settings.json", $$"""{ "TransactionWrapLevel": "{{transactionWrapLevel}}" }""")
			.AddFile("Migrations/001.migration.sql", migration)
			.AddFile("Code/v.sql", "create or replace view v as select 1 as x;");

		DbLiveBuilder builder = CreateBuilder(project);
		await builder.CreateDeployer().DeployAsync(DeployParameters.Default);

		IDbLiveDA da = builder.GetRequiredService<IDbLiveDA>();
		Assert.Equal([schemaOfView], await da.ListAsync("select table_schema::text from information_schema.views where table_name = 'v'"));
	}
}
