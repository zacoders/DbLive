using DbLive.Adapter;
using System.Diagnostics.CodeAnalysis;

namespace DbLive.MSSQL.Tests;

/// <summary>
/// A database name may have any character. <c>create database</c> and <c>drop database</c> take it in brackets, with a <c>]</c> doubled.
/// </summary>
[SuppressMessage("Usage", "xUnit1041:Fixture arguments to test classes must have fixture sources", Justification = "AssemblyFixture will be properly supported in xUnit v3. waiting.")]
public class QuotedDatabaseNameTests(SqlServerIntegrationFixture fixture, ITestOutputHelper output)
	: SqlServerDbTests(fixture, output, createDb: false, dbName: $"DbLive]['-{Guid.NewGuid()}")
{
	[Fact]
	public async Task Deployment_creates_and_drop_drops_a_database_with_a_closing_bracket_in_its_name()
	{
		using TempProject project = new TempProject()
			.AddFile("Migrations/001.migration.sql", "create table dbo.t1 ( id int not null );");

		await using DbLiveBuilder builder = CreateBuilder(project);
		await builder.CreateDeployer().DeployAsync(DeployParameters.Default with { CreateDbIfNotExists = true });

		Assert.True(await DbExistsAsync());

		await builder.GetRequiredService<IDbLiveDA>().DropDbAsync(skipIfNotExists: false);

		Assert.False(await DbExistsAsync());
	}
}
