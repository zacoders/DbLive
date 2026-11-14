using DbLive.Testing;
using DbLive.xunit;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute.ExceptionExtensions;
using System.Reflection;
using Xunit.Abstractions;

namespace DbLive.Tests.Deploy;

public class DeploymentTestTests
{
	[Fact]
	public async Task Deploys_as_production_release_then_breaking_twice()
	{
		IDbLive dbLive = Substitute.For<IDbLive>();
		IDbLiveDA da = Substitute.For<IDbLiveDA>();
		IDbLiveTester tester = Substitute.For<IDbLiveTester>();
		da.GetSchemaSnapshotAsync().Returns(_ => new SchemaSnapshot([]));
		DeploymentTest test = new(new FixtureBuilder(dbLive, da, tester), dropDatabaseOnComplete: false);

		await test.DeployAsync(Substitute.For<ITestOutputHelper>());

		Received.InOrder(() =>
		{
			// the release, without breaking changes, then the SQL tests.
			dbLive.DeployAsync(Arg.Is<DeployParameters>(p =>
				p.CreateDbIfNotExists && p.DeployMigrations && p.DeployCode && !p.DeployBreaking));
			tester.RunAllTestsAsync();

			// the breaking phase as a run of its own, with the SQL tests after it.
			dbLive.DeployAsync(DeployParameters.Breaking);
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
		DeploymentTest test = new(new FixtureBuilder(dbLive, da, Substitute.For<IDbLiveTester>()), dropDatabaseOnComplete: false);

		SchemaChangedException ex = await Assert.ThrowsAsync<SchemaChangedException>(
			() => test.DeployAsync(Substitute.For<ITestOutputHelper>())
		);

		Assert.StartsWith("Deployment test: a second breaking deployment changed the schema.", ex.Message);
		Assert.EndsWith("  missing column dbo.Person.Phone: varchar(20) null", ex.Message);
	}

	[Theory]
	[InlineData(true)]
	[InlineData(false)]
	public async Task DeployAsync_drops_the_database_when_asked_and_disposes_the_builder(bool dropDatabaseOnComplete)
	{
		IDbLiveDA da = Substitute.For<IDbLiveDA>();
		da.GetSchemaSnapshotAsync().Returns(_ => new SchemaSnapshot([]));
		FixtureBuilder fixtureBuilder = new(Substitute.For<IDbLive>(), da, Substitute.For<IDbLiveTester>());
		DeploymentTest test = new(fixtureBuilder, dropDatabaseOnComplete);

		await test.DeployAsync(Substitute.For<ITestOutputHelper>());

		await da.Received(dropDatabaseOnComplete ? 1 : 0).DropDbAsync();
		Assert.Throws<ObjectDisposedException>(() => fixtureBuilder.Builder!.CreateDeployer());
	}

	[Fact]
	public async Task Failed_deployment_drops_the_database_and_throws_its_own_error_when_the_drop_fails()
	{
		IDbLive dbLive = Substitute.For<IDbLive>();
		dbLive.DeployAsync(Arg.Any<DeployParameters>()).ThrowsAsync(new InvalidOperationException("deployment failed"));
		IDbLiveDA da = Substitute.For<IDbLiveDA>();
		da.DropDbAsync().ThrowsAsync(new TimeoutException("drop failed"));
		ITestOutputHelper output = Substitute.For<ITestOutputHelper>();
		FixtureBuilder fixtureBuilder = new(dbLive, da, Substitute.For<IDbLiveTester>());
		DeploymentTest test = new(fixtureBuilder, dropDatabaseOnComplete: true);

		InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(() => test.DeployAsync(output));

		Assert.Equal("deployment failed", ex.Message);
		await da.Received(1).DropDbAsync();
		output.Received(1).WriteLine(Arg.Is<string>(line =>
			line.StartsWith("The database was not dropped after the failed deployment: System.TimeoutException: drop failed")));
		Assert.Throws<ObjectDisposedException>(() => fixtureBuilder.Builder!.CreateDeployer());
	}

	private sealed class FixtureBuilder(IDbLive dbLive, IDbLiveDA da, IDbLiveTester tester) : IDbLiveFixtureBuilder
	{
		public DbLiveBuilder? Builder { get; private set; }

		public Task<DbLiveBuilder> GetBuilderAsync()
		{
			Builder = new DbLiveBuilder().ConfigureServices(services => services.AddSingleton(dbLive).AddSingleton(da).AddSingleton(tester));
			return Task.FromResult(Builder);
		}

		public Assembly GetProjectAssembly() => typeof(FixtureBuilder).Assembly;
	}
}
