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
		DeployFixture fixture = new(new FixtureBuilder(dbLive), dropDatabaseOnComplete: false);
		await fixture.InitializeAsync();

		await fixture.DeployAsync(Substitute.For<ITestOutputHelper>(), deployBreaking: true, undoTestMode);

		Received.InOrder(() =>
		{
			dbLive.DeployAsync(Arg.Is<DeployParameters>(p =>
				p.DeployMigrations && p.DeployCode && p.DeployBreaking && p.RunTests && p.UndoTestDeployment == undoTestMode));

			// as in production: a run of its own, without migrations and code.
			dbLive.DeployAsync(DeployParameters.Breaking);
		});
		await dbLive.Received(2).DeployAsync(Arg.Any<DeployParameters>());
	}

	[Fact]
	public async Task Without_breaking_one_deployment()
	{
		IDbLive dbLive = Substitute.For<IDbLive>();
		DeployFixture fixture = new(new FixtureBuilder(dbLive), dropDatabaseOnComplete: false);
		await fixture.InitializeAsync();

		await fixture.DeployAsync(Substitute.For<ITestOutputHelper>(), deployBreaking: false, UndoTestMode.None);

		await dbLive.Received(1).DeployAsync(Arg.Is<DeployParameters>(p => !p.DeployBreaking && p.DeployCode && p.RunTests));
		await dbLive.Received(1).DeployAsync(Arg.Any<DeployParameters>());
	}

	private sealed class FixtureBuilder(IDbLive dbLive) : IDbLiveFixtureBuilder
	{
		public Task<DbLiveBuilder> GetBuilderAsync() =>
			Task.FromResult(new DbLiveBuilder().ConfigureServices(services => services.AddSingleton(dbLive)));

		public Assembly GetProjectAssembly() => typeof(FixtureBuilder).Assembly;
	}
}
