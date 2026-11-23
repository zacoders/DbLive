using DbLive.Adapter;
using Microsoft.Data.SqlClient;
using System.Diagnostics.CodeAnalysis;

namespace DbLive.MSSQL.Tests;

/// <summary>
/// The relative path is the clustered key of <c>dblive.code</c> and, after <c>folder_type</c>, of <c>dblive.folder_items</c>, and a
/// clustered key holds at most 900 bytes: <see cref="IDbLiveDA.MaxCodePathLength"/> and <see cref="IDbLiveDA.MaxFolderItemPathLength"/>
/// are the longest paths they store. The project load rejects a longer path (<see cref="InvalidProjectTests"/>).
/// </summary>
[SuppressMessage("Usage", "xUnit1041:Fixture arguments to test classes must have fixture sources", Justification = "AssemblyFixture will be properly supported in xUnit v3. waiting.")]
public class RelativePathLengthTests(SqlServerIntegrationFixture fixture, ITestOutputHelper output)
	: SqlServerDbTests(fixture, output)
{
	// the numbers of the docs.
	private const int MaxCodePathLength = 450;
	private const int MaxFolderItemPathLength = 444;

	[Fact]
	public async Task Paths_of_the_max_lengths_are_stored_and_one_character_more_is_not()
	{
		using TempProject project = new TempProject()
			.AddFile(ScriptPaths.OfLength("Code", "p1.sql", MaxCodePathLength), "create or alter procedure dbo.p1 as select 1;")
			.AddFile(ScriptPaths.OfLength("Code", "p0.breaking.sql", MaxCodePathLength), "drop procedure if exists dbo.p0;")
			.AddFile(ScriptPaths.OfLength("BeforeDeploy", "before.sql", MaxFolderItemPathLength), "select 1;")
			.AddFile(ScriptPaths.OfLength("AfterDeploy", "after.sql", MaxFolderItemPathLength), "select 1;");
		await using DbLiveBuilder builder = CreateBuilder(project);
		IDbLiveDA da = builder.GetRequiredService<IDbLiveDA>();

		Assert.Equal(MaxCodePathLength, da.MaxCodePathLength);
		Assert.Equal(MaxFolderItemPathLength, da.MaxFolderItemPathLength);

		await builder.CreateDeployer().DeployAsync(DeployParameters.Default);
		// the code file is unchanged: it is marked verified by its path (#paths of MsSqlDA).
		await builder.CreateDeployer().DeployAsync(DeployParameters.Default);
		await builder.CreateDeployer().DeployAsync(DeployParameters.Breaking);

		Assert.Equal(
			[$"{MaxCodePathLength}:breaking:applied", $"{MaxCodePathLength}:code:applied"],
			await da.ListAsync("select concat(len(relative_path), ':', item_type, ':', status) from dblive.code")
		);
		Assert.Equal(["code"], await da.ListAsync("select item_type from dblive.code where verified_utc is not null"));
		Assert.Equal(
			[$"{MaxFolderItemPathLength}:AfterDeploy", $"{MaxFolderItemPathLength}:BeforeDeploy"],
			await da.ListAsync("select concat(len(relative_path), ':', folder_type) from dblive.folder_items")
		);

		DbLiveSqlException codeEx = await Assert.ThrowsAsync<DbLiveSqlException>(() => da.ExecuteSqlAsync($"""
			insert into dblive.code ( relative_path, item_type, status, execution_time_ms, created_utc, content_hash )
			values ( replicate(N'a', {MaxCodePathLength + 1}), 'code', 'applied', 1, sysutcdatetime(), 1 );
			"""));
		Assert.Equal(1946, Assert.IsType<SqlException>(codeEx.InnerException).Number);

		// the longest folder_type.
		DbLiveSqlException folderEx = await Assert.ThrowsAsync<DbLiveSqlException>(() => da.ExecuteSqlAsync($"""
			insert into dblive.folder_items ( folder_type, relative_path, content_hash, created_utc, started_utc, completed_utc, execution_time_ms )
			values ( 'BeforeDeploy', replicate(N'a', {MaxFolderItemPathLength + 1}), 1, sysutcdatetime(), sysutcdatetime(), sysutcdatetime(), 1 );
			"""));
		Assert.Equal(1946, Assert.IsType<SqlException>(folderEx.InnerException).Number);
	}
}
