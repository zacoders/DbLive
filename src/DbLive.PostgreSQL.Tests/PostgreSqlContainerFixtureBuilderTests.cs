using DbLive.Adapter;
using Npgsql;
using System.Reflection;

namespace DbLive.PostgreSQL.Tests;

/// <summary>
/// Starts a container of its own with the default image, next to the one of <see cref="PostgreSqlFixture"/>.
/// </summary>
public class PostgreSqlContainerFixtureBuilderTests
{
	[Fact]
	public async Task Fixtures_that_start_at_the_same_time_share_one_container_and_get_their_own_databases()
	{
		// a builder needs an assembly with scripts; the project is not deployed here.
		Assembly project = Assembly.Load("Demo.PostgreSQL.Chinook");

		// as fixtures that xUnit initializes in parallel: every fixture has a builder of its own.
		DbLiveBuilder[] builders = await Task.WhenAll(Enumerable.Range(0, 4)
			.Select(_ => Task.Run(() => new PostgreSqlContainerFixtureBuilder(project).GetBuilderAsync())));

		NpgsqlConnectionStringBuilder[] cnns = [.. builders
			.Select(b => new NpgsqlConnectionStringBuilder(b.GetRequiredService<IDbLiveDbConnection>().ConnectionString))];
		// every container gets a port of its own.
		_ = Assert.Single(cnns.Select(c => $"{c.Host}:{c.Port}").Distinct());
		Assert.Equal(4, cnns.Select(c => c.Database).Distinct().Count());

		foreach (DbLiveBuilder builder in builders)
		{
			IDbLiveInternalDA da = builder.GetRequiredService<IDbLiveInternalDA>();
			await da.CreateDBAsync(skipIfExists: false);
			await da.DropDbAsync(skipIfNotExists: false);
			await builder.DisposeAsync();
		}
	}
}
