using DbLive.Adapter;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics.CodeAnalysis;

namespace DbLive.PostgreSQL.Tests;

/// <summary>
/// On SQL Server the relative path is a clustered key of at most 900 bytes, and the project load rejects a path longer than 450 or 444
/// characters. PostgreSQL stores such paths: its limit, a b-tree entry of about 2700 bytes, is not checked.
/// </summary>
[SuppressMessage("Usage", "xUnit1041:Fixture arguments to test classes must have fixture sources", Justification = "AssemblyFixture will be properly supported in xUnit v3. waiting.")]
public class RelativePathLengthTests(PostgreSqlFixture fixture, ITestOutputHelper output)
	: PostgreSqlDbTests(fixture, output)
{
	private const int PathLength = 1000;

	[Fact]
	public async Task Paths_longer_than_SQL_Server_stores_are_deployed()
	{
		using TempProject project = new TempProject()
			.AddFile(ScriptPaths.OfLength("Code", "f1.sql", PathLength), "create or replace function f1() returns int language sql as $$ select 1 $$;")
			.AddFile(ScriptPaths.OfLength("Code", "f0.breaking.sql", PathLength), "drop function if exists f0();")
			.AddFile(ScriptPaths.OfLength("BeforeDeploy", "before.sql", PathLength), "select 1;")
			.AddFile(ScriptPaths.OfLength("AfterDeploy", "after.sql", PathLength), "select 1;");
		await using DbLiveBuilder builder = CreateBuilder(project);
		IDbLiveDA da = builder.GetRequiredService<IDbLiveDA>();

		Assert.Null(da.MaxCodePathLength);
		Assert.Null(da.MaxFolderItemPathLength);

		await builder.CreateDeployer().DeployAsync(DeployParameters.Default);
		// the code file is unchanged: it is marked verified by its path.
		await builder.CreateDeployer().DeployAsync(DeployParameters.Default);
		await builder.CreateDeployer().DeployAsync(DeployParameters.Breaking);

		Assert.Equal(
			[$"{PathLength}:breaking:applied", $"{PathLength}:code:applied"],
			await da.ListAsync("select concat(length(relative_path), ':', item_type, ':', status) from dblive.code")
		);
		Assert.Equal(["code"], await da.ListAsync("select item_type from dblive.code where verified_utc is not null"));
		Assert.Equal(
			[$"{PathLength}:AfterDeploy", $"{PathLength}:BeforeDeploy"],
			await da.ListAsync("select concat(length(relative_path), ':', folder_type) from dblive.folder_items")
		);
	}
}
