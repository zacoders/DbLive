using DbLive.Adapter;
using System.Diagnostics.CodeAnalysis;

namespace DbLive.PostgreSQL.Tests;

/// <summary>
/// A database name may have any character. <c>CREATE DATABASE</c>, <c>revoke connect</c> and <c>drop database</c> take it in double
/// quotes, with a <c>"</c> doubled.
/// </summary>
[SuppressMessage("Usage", "xUnit1041:Fixture arguments to test classes must have fixture sources", Justification = "AssemblyFixture will be properly supported in xUnit v3. waiting.")]
public class QuotedDatabaseNameTests(PostgreSqlFixture fixture, ITestOutputHelper output)
	: PostgreSqlDbTests(fixture, output, createDb: false, dbName: $"dblive\"'-{Guid.NewGuid()}")
{
	[Fact]
	public async Task Deployment_creates_and_drop_drops_a_database_with_a_double_quote_in_its_name()
	{
		using TempProject project = new TempProject()
			.AddFile("Migrations/001.migration.sql", "create table t1 (id int not null);");

		await using DbLiveBuilder builder = CreateBuilder(project);
		await builder.CreateDeployer().DeployAsync(DeployParameters.Default with { CreateDbIfNotExists = true });

		Assert.True(await DbExistsAsync());

		await builder.GetRequiredService<IDbLiveDA>().DropDbAsync(skipIfNotExists: false);

		Assert.False(await DbExistsAsync());
	}
}
