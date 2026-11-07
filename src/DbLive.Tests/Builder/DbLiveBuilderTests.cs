using Microsoft.Extensions.DependencyInjection;

namespace DbLive.Tests.Builder;

public class DbLiveBuilderTests
{
	[Fact]
	public void CreateProject_ReturnsSameSingleton_FromSingleBuilderProvider()
	{
		using DbLiveBuilder builder = new DbLiveBuilder()
			.ConfigureServices(services =>
			{
				services.AddSingleton<IScriptSource>(new InMemoryScriptSource());
			});

		IDbLiveProject project1 = builder.CreateProject();
		IDbLiveProject project2 = builder.CreateProject();

		Assert.Same(project1, project2);
	}

	[Fact]
	public async Task ConfigureServices_AfterFirstResolve_RebuildsProviderWithNewConfiguration()
	{
		using DbLiveBuilder builder = new DbLiveBuilder()
			.ConfigureServices(services =>
			{
				services.AddSingleton<IScriptSource>(new InMemoryScriptSource());
			});

		IDbLiveProject project1 = builder.CreateProject();

		builder.ConfigureServices(services =>
		{
			services.AddSingleton<IScriptSource>(new InMemoryScriptSource().AddFile("Migrations/001.migration.sql"));
		});

		IDbLiveProject project2 = builder.CreateProject();

		Assert.NotSame(project1, project2);
		Assert.Single(await project2.GetMigrationsAsync());
	}

	[Fact]
	public void CreateMethods_ThrowObjectDisposed_WhenBuilderDisposed()
	{
		DbLiveBuilder builder = new DbLiveBuilder()
			.ConfigureServices(services =>
			{
				services.AddSingleton<IScriptSource>(new InMemoryScriptSource());
			});

		_ = builder.CreateProject();
		builder.Dispose();

		Assert.Throws<ObjectDisposedException>(() => builder.CreateProject());
		Assert.Throws<ObjectDisposedException>(() =>
			builder.ConfigureServices(services => services.AddSingleton<IScriptSource>(new InMemoryScriptSource())));
	}
}
