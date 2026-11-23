using DbLive.Adapter;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using System.Diagnostics.CodeAnalysis;

namespace DbLive.PostgreSQL.Tests;


/// <summary>
/// The project is read and checked before anything touches the server: an error of the project creates no database and runs
/// no self-migration, so an older DbLive can still deploy the database.
/// </summary>
[SuppressMessage("Usage", "xUnit1041:Fixture arguments to test classes must have fixture sources", Justification = "AssemblyFixture will be properly supported in xUnit v3. waiting.")]
public class InvalidProjectTests(PostgreSqlFixture fixture, ITestOutputHelper output)
	: PostgreSqlDbTests(fixture, output, createDb: false)
{
	[Fact]
	public async Task Undo_script_fails_the_deployment_before_the_self_deployment()
	{
		// a new database, not deployed by this DbLive: a database it deployed has no pending self-migration.
		using TempProject project = CreateProjectWithUndoScript();
		DbLiveBuilder builder = CreateBuilder(project);
		IDbLiveDA da = builder.GetRequiredService<IDbLiveDA>();
		await da.CreateDBAsync();

		UndoNotSupportedException ex = await Assert.ThrowsAsync<UndoNotSupportedException>(
			() => builder.CreateDeployer().DeployAsync(DeployParameters.Default)
		);

		Assert.Equal("Migrations/001.undo.sql", ex.RelativePath);
		Assert.Equal(0, await da.ScalarAsync("select count(*) from pg_namespace where nspname = 'dblive'"));
		Assert.Equal(0, await da.ScalarAsync("select count(*) from pg_class where relname = 't1'"));
	}

	[Fact]
	public async Task Undo_script_fails_the_deployment_before_the_database_is_created()
	{
		using TempProject project = CreateProjectWithUndoScript();

		_ = await Assert.ThrowsAsync<UndoNotSupportedException>(
			() => CreateBuilder(project).CreateDeployer().DeployAsync(DeployParameters.Default with { CreateDbIfNotExists = true })
		);

		Assert.False(await DbExistsAsync());
	}

	[Fact]
	public async Task Project_id_longer_than_128_characters_fails_the_deployment_before_the_database_is_created()
	{
		// dblive.version.project_id is varchar(128).
		using TempProject project = new TempProject()
			.AddFile("settings.json", $$"""{ "ProjectId": "{{new string('a', 129)}}" }""")
			.AddFile("Migrations/001.migration.sql", "create table t1 (id int not null);");

		InvalidProjectSettingsException ex = await Assert.ThrowsAsync<InvalidProjectSettingsException>(
			() => CreateBuilder(project).CreateDeployer().DeployAsync(DeployParameters.Default with { CreateDbIfNotExists = true })
		);

		Assert.Contains("$.ProjectId", ex.Message);
		Assert.False(await DbExistsAsync());
	}

	private static TempProject CreateProjectWithUndoScript() => new TempProject()
		.AddFile("Migrations/001.migration.sql", "create table t1 (id int not null);")
		// left in the project after the upgrade to DbLive 2.0, which has no undo scripts.
		.AddFile("Migrations/001.undo.sql", "drop table t1;");
}
