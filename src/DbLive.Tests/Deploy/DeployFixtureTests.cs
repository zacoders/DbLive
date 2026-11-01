using DbLive.xunit;
using DbLive.xunit.Deploy;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using Xunit.Abstractions;

namespace DbLive.Tests.Deploy;

public class DeployFixtureTests
{
	[Theory]
	[InlineData(UndoTestMode.None)]
	[InlineData(UndoTestMode.MigrationUndoMigration)]
	[InlineData(UndoTestMode.MigrationBreakingUndoMigration)]
	public async Task With_breaking_a_separate_breaking_deployment_follows(UndoTestMode undoTestMode)
	{
		IDbLive dbLive = Substitute.For<IDbLive>();
		IDbLiveDA da = Substitute.For<IDbLiveDA>();
		da.GetSchemaSnapshotAsync().Returns(_ => new SchemaSnapshot([]));
		DeployFixture fixture = new(new FixtureBuilder(dbLive, da), dropDatabaseOnComplete: false);
		await fixture.InitializeAsync();

		await fixture.DeployAsync(Substitute.For<ITestOutputHelper>(), deployBreaking: true, undoTestMode);

		Received.InOrder(() =>
		{
			dbLive.DeployAsync(Arg.Is<DeployParameters>(p =>
				p.DeployMigrations && p.DeployCode && p.DeployBreaking && p.RunTests && p.UndoTestDeployment == undoTestMode));

			// as in production: a run of its own, without migrations and code, which must not change the schema.
			da.GetSchemaSnapshotAsync();
			dbLive.DeployAsync(DeployParameters.Breaking);
			da.GetSchemaSnapshotAsync();
		});
		await dbLive.Received(2).DeployAsync(Arg.Any<DeployParameters>());
	}

	[Fact]
	public async Task Second_breaking_deployment_that_changes_the_schema_fails()
	{
		IDbLive dbLive = Substitute.For<IDbLive>();
		IDbLiveDA da = Substitute.For<IDbLiveDA>();
		SchemaObject phone = new(SchemaObjectType.Column, "dbo.Person.Phone", "varchar(20) null", "dbo.Person");
		da.GetSchemaSnapshotAsync().Returns(new SchemaSnapshot([phone]), new SchemaSnapshot([]));
		DeployFixture fixture = new(new FixtureBuilder(dbLive, da), dropDatabaseOnComplete: false);
		await fixture.InitializeAsync();

		SchemaChangedException ex = await Assert.ThrowsAsync<SchemaChangedException>(
			() => fixture.DeployAsync(Substitute.For<ITestOutputHelper>(), deployBreaking: true, UndoTestMode.None)
		);

		Assert.Equal(SchemaCheck.BreakingSecondDeployment, ex.Check);
		Assert.StartsWith("Deployment test: a second breaking deployment changed the schema.", ex.Message);
		Assert.EndsWith("  missing column dbo.Person.Phone: varchar(20) null", ex.Message);
	}

	[Fact]
	public async Task Without_breaking_one_deployment()
	{
		IDbLive dbLive = Substitute.For<IDbLive>();
		IDbLiveDA da = Substitute.For<IDbLiveDA>();
		DeployFixture fixture = new(new FixtureBuilder(dbLive, da), dropDatabaseOnComplete: false);
		await fixture.InitializeAsync();

		await fixture.DeployAsync(Substitute.For<ITestOutputHelper>(), deployBreaking: false, UndoTestMode.None);

		await dbLive.Received(1).DeployAsync(Arg.Is<DeployParameters>(p => !p.DeployBreaking && p.DeployCode && p.RunTests));
		await dbLive.Received(1).DeployAsync(Arg.Any<DeployParameters>());
		await da.DidNotReceive().GetSchemaSnapshotAsync();
	}

	private sealed class FixtureBuilder(IDbLive dbLive, IDbLiveDA da) : IDbLiveFixtureBuilder
	{
		public Task<DbLiveBuilder> GetBuilderAsync() =>
			Task.FromResult(new DbLiveBuilder().ConfigureServices(services => services.AddSingleton(dbLive).AddSingleton(da)));

		public Assembly GetProjectAssembly() => typeof(FixtureBuilder).Assembly;
	}
}
