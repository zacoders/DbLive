using DbLive.Adapter;
using System.Diagnostics.CodeAnalysis;

namespace DbLive.MSSQL.Tests;

/// <summary>
/// Code files are deployed in parallel, and a file that fails is retried in the next round, after every file of its round finished.
/// A file is then not retried while the file it needs still runs on another worker.
/// </summary>
[SuppressMessage("Usage", "xUnit1041:Fixture arguments to test classes must have fixture sources", Justification = "AssemblyFixture will be properly supported in xUnit v3. waiting.")]
public class CodeRetryRoundsTests(SqlServerIntegrationFixture fixture, ITestOutputHelper output)
	: SqlServerDbTests(fixture, output)
{
	[Fact]
	public async Task Code_file_is_retried_after_its_slow_dependency_on_another_worker()
	{
		// the delay stands for a file that waits for a schema lock or is large. Before the rounds, the dependent file used up its
		// attempts within milliseconds while the base was running, and the deployment failed.
		using TempProject project = new TempProject()
			.AddFile("Migrations/001.migration.sql", "create table dbo.T ( Id int );")
			.AddFile("Code/a_base.sql", "waitfor delay '00:00:01';\ngo\ncreate or alter view dbo.v_base as select Id from dbo.T;")
			.AddFile("Code/b_dependent.sql", "create or alter view dbo.v_dependent as select Id from dbo.v_base;");

		DbLiveBuilder builder = CreateBuilder(project);

		await builder.CreateDeployer().DeployAsync(DeployParameters.Default);

		IDbLiveDA da = builder.GetRequiredService<IDbLiveDA>();
		Assert.Equal(["v_base", "v_dependent"], await da.ListAsync("select name from sys.views"));
		Assert.Equal(
			["code/a_base.sql:applied", "code/b_dependent.sql:applied"],
			await da.ListAsync("select concat(relative_path, ':', status) from dblive.code")
		);
	}
}
