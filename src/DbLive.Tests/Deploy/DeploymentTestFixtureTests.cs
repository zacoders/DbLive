using DbLive.xunit;
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
		da.GetSchemaSnapshotAsync().Returns(_ => new SchemaSnapshot([]));
		DeploymentTestFixture fixture = new(new FixtureBuilder(dbLive, da), dropDatabaseOnComplete: false);
		await fixture.InitializeAsync();

		await fixture.DeployAsync(Substitute.For<ITestOutputHelper>());

		Received.InOrder(() =>
		{
			// the release with the SQL tests and the undo cycle, without breaking changes.
			dbLive.DeployAsync(Arg.Is<DeployParameters>(p =>
				p.CreateDbIfNotExists && p.DeployMigrations && p.DeployCode && !p.DeployBreaking && p.RunTests && p.UndoTestDeployment == UndoTestMode.UndoCycle));

			// the breaking phase as a run of its own, with the SQL tests after it.
			dbLive.DeployAsync(DeployParameters.BreakingAndTests);

			// the second breaking run must not change the schema.
			da.GetSchemaSnapshotAsync();
			dbLive.DeployAsync(DeployParameters.Breaking);
			da.GetSchemaSnapshotAsync();
		});
		await dbLive.Received(3).DeployAsync(Arg.Any<DeployParameters>());
	}

	[Fact]
	public async Task Second_breaking_deployment_that_changes_the_schema_fails()
	{
		IDbLive dbLive = Substitute.For<IDbLive>();
		IDbLiveDA da = Substitute.For<IDbLiveDA>();
		SchemaObject phone = new(SchemaObjectType.Column, "dbo.Person.Phone", "varchar(20) null", "dbo.Person");
		da.GetSchemaSnapshotAsync().Returns(new SchemaSnapshot([phone]), new SchemaSnapshot([]));
		DeploymentTestFixture fixture = new(new FixtureBuilder(dbLive, da), dropDatabaseOnComplete: false);
		await fixture.InitializeAsync();

		SchemaChangedException ex = await Assert.ThrowsAsync<SchemaChangedException>(
			() => fixture.DeployAsync(Substitute.For<ITestOutputHelper>())
		);

		Assert.Equal(SchemaCheck.BreakingSecondDeployment, ex.Check);
		Assert.StartsWith("Deployment test: a second breaking deployment changed the schema.", ex.Message);
		Assert.EndsWith("  missing column dbo.Person.Phone: varchar(20) null", ex.Message);
	}

	private sealed class FixtureBuilder(IDbLive dbLive, IDbLiveDA da) : IDbLiveFixtureBuilder
	{
		public Task<DbLiveBuilder> GetBuilderAsync() =>
			Task.FromResult(new DbLiveBuilder().ConfigureServices(services => services.AddSingleton(dbLive).AddSingleton(da)));

		public Assembly GetProjectAssembly() => typeof(FixtureBuilder).Assembly;
	}
}
