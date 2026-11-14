using DbLive.xunit;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute.ExceptionExtensions;
using System.Reflection;

namespace DbLive.Tests.SqlTest;

public class DbLiveTestFixtureTests
{
	[Fact]
	public async Task DisposeAsync_DropsTheDatabase_AndDisposesTheBuilder()
	{
		IDbLiveDA da = Substitute.For<IDbLiveDA>();
		FixtureBuilder fixtureBuilder = new(da);
		DbLiveTestFixture fixture = new(fixtureBuilder, dropDatabaseOnComplete: true);
		await fixture.InitializeAsync();

		await fixture.DisposeAsync();

		await da.Received(1).DropDbAsync();
		Assert.Throws<ObjectDisposedException>(() => fixtureBuilder.Builder!.CreateTester());
	}

	[Fact]
	public async Task Tester_is_the_tester_of_the_builder_after_InitializeAsync_and_throws_before()
	{
		IDbLiveTester tester = Substitute.For<IDbLiveTester>();
		DbLiveTestFixture fixture = new(new FixtureBuilder(Substitute.For<IDbLiveDA>(), tester), dropDatabaseOnComplete: false);

		InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => fixture.Tester);
		Assert.StartsWith("The fixture is not initialized", ex.Message);

		await fixture.InitializeAsync();

		Assert.Same(tester, fixture.Tester);
		await fixture.DisposeAsync();
	}

	[Fact]
	public async Task DisposeAsync_DropFails_TheBuilderIsDisposed()
	{
		IDbLiveDA da = Substitute.For<IDbLiveDA>();
		da.DropDbAsync().ThrowsAsync(new InvalidOperationException("drop failed"));
		FixtureBuilder fixtureBuilder = new(da);
		DbLiveTestFixture fixture = new(fixtureBuilder, dropDatabaseOnComplete: true);
		await fixture.InitializeAsync();

		await Assert.ThrowsAsync<InvalidOperationException>(fixture.DisposeAsync);

		Assert.Throws<ObjectDisposedException>(() => fixtureBuilder.Builder!.CreateTester());
	}

	private sealed class FixtureBuilder(IDbLiveDA da, IDbLiveTester? tester = null) : IDbLiveFixtureBuilder
	{
		public DbLiveBuilder? Builder { get; private set; }

		public Task<DbLiveBuilder> GetBuilderAsync()
		{
			Builder = new DbLiveBuilder().ConfigureServices(services => services
				.AddSingleton(Substitute.For<IDbLive>())
				.AddSingleton(da)
				.AddSingleton(tester ?? Substitute.For<IDbLiveTester>()));
			return Task.FromResult(Builder);
		}

		public Assembly GetProjectAssembly() => typeof(FixtureBuilder).Assembly;
	}
}
