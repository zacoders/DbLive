using DbLive.Testing;
using DbLive.xunit.Deploy;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using Xunit.Abstractions;

namespace DbLive.Tests.Deploy;

public class DeploymentTestFixtureTests
{
	[Fact]
	public async Task Deploys_as_production_release_then_breaking_twice()
	{
		IDbLive dbLive = Substitute.For<IDbLive>();
		IDbLiveDA da = Substitute.For<IDbLiveDA>();
		IDbLiveTester tester = Substitute.For<IDbLiveTester>();
		da.GetSchemaSnapshotAsync().Returns(_ => new SchemaSnapshot([]));
		DeploymentTestFixture fixture = new(new FixtureBuilder(dbLive, da, tester), dropDatabaseOnComplete: false);
		await fixture.InitializeAsync();

		await fixture.DeployAsync(Substitute.For<ITestOutputHelper>());

		Received.InOrder(() =>
		{
			// the release, without breaking changes, then the SQL tests.
			dbLive.DeployAsync(Arg.Is<DeployParameters>(p =>
				p.CreateDbIfNotExists && p.DeployMigrations && p.DeployCode && !p.DeployBreaking && !p.CheckBreakingSecondRun));
			tester.RunAllTestsAsync();

			// the breaking phase as a run of its own, every migration breaking script twice, with the SQL tests after it.
			dbLive.DeployAsync(DeployParameters.Breaking with { CheckBreakingSecondRun = true });
			tester.RunAllTestsAsync();

			// the second breaking run must not change the schema.
			da.GetSchemaSnapshotAsync();
			dbLive.DeployAsync(DeployParameters.Breaking);
			da.GetSchemaSnapshotAsync();
		});
		await dbLive.Received(3).DeployAsync(Arg.Any<DeployParameters>());
		await tester.Received(2).RunAllTestsAsync();
	}

	[Fact]
	public async Task Second_breaking_deployment_that_changes_the_schema_fails()
	{
		IDbLive dbLive = Substitute.For<IDbLive>();
		IDbLiveDA da = Substitute.For<IDbLiveDA>();
		SchemaObject phone = new(SchemaObjectType.Column, "dbo.Person.Phone", "varchar(20) null", "dbo.Person");
		da.GetSchemaSnapshotAsync().Returns(new SchemaSnapshot([phone]), new SchemaSnapshot([]));
		DeploymentTestFixture fixture = new(new FixtureBuilder(dbLive, da, Substitute.For<IDbLiveTester>()), dropDatabaseOnComplete: false);
		await fixture.InitializeAsync();

		SchemaChangedException ex = await Assert.ThrowsAsync<SchemaChangedException>(
			() => fixture.DeployAsync(Substitute.For<ITestOutputHelper>())
		);

		Assert.Equal(SchemaCheck.BreakingSecondDeployment, ex.Check);
		Assert.StartsWith("Deployment test: a second breaking deployment changed the schema.", ex.Message);
		Assert.EndsWith("  missing column dbo.Person.Phone: varchar(20) null", ex.Message);
	}

	private sealed class FixtureBuilder(IDbLive dbLive, IDbLiveDA da, IDbLiveTester tester) : IDbLiveFixtureBuilder
	{
		public Task<DbLiveBuilder> GetBuilderAsync() =>
			Task.FromResult(new DbLiveBuilder().ConfigureServices(services => services.AddSingleton(dbLive).AddSingleton(da).AddSingleton(tester)));

		public Assembly GetProjectAssembly() => typeof(FixtureBuilder).Assembly;
	}
}
