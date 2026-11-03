using DbLive.Adapter;
using Microsoft.Data.SqlClient;
using System.Reflection;

namespace DbLive.MSSQL.Tests;

/// <summary>
/// Starts a container of its own with the default image, next to the one of <see cref="SqlServerIntegrationFixture"/>.
/// </summary>
public class MsSqlContainerFixtureBuilderTests
{
	[Fact]
	public async Task Fixtures_that_start_at_the_same_time_share_one_container_and_get_their_own_databases()
	{
		Assembly project = Assembly.Load("DemoMSSQLEmpty");

		// as fixtures that xUnit initializes in parallel: every fixture has a builder of its own.
		DbLiveBuilder[] builders = await Task.WhenAll(Enumerable.Range(0, 4)
			.Select(_ => Task.Run(() => new MsSqlContainerFixtureBuilder(project).GetBuilderAsync())));

		SqlConnectionStringBuilder[] cnns = [.. builders
			.Select(b => new SqlConnectionStringBuilder(b.GetRequiredService<IDbLiveDbConnection>().ConnectionString))];
		// every container gets a port of its own.
		_ = Assert.Single(cnns.Select(c => c.DataSource).Distinct());
		Assert.Equal(4, cnns.Select(c => c.InitialCatalog).Distinct().Count());

		foreach (DbLiveBuilder builder in builders)
		{
			IDbLiveDA da = builder.CreateDbLiveDA();
			await da.CreateDBAsync(skipIfExists: false);
			await da.DropDbAsync(skipIfNotExists: false);
			await builder.DisposeAsync();
		}
	}
}
