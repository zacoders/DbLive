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
				services.AddSingleton<IProjectPath>(new ProjectPath("c:/db/scripts", "c:/db/vs-scripts"));
			});

		IDbLiveProject project1 = builder.CreateProject();
		IDbLiveProject project2 = builder.CreateProject();

		Assert.Same(project1, project2);
	}

	[Fact]
	public void ConfigureServices_AfterFirstResolve_RebuildsProviderWithNewConfiguration()
	{
		using DbLiveBuilder builder = new DbLiveBuilder()
			.ConfigureServices(services =>
			{
				services.AddSingleton<IProjectPath>(new ProjectPath("c:/db/scripts", "c:/db/vs-scripts"));
			});

		IDbLiveProject project1 = builder.CreateProject();

		builder.ConfigureServices(services =>
		{
			services.AddSingleton<IProjectPath>(new ProjectPath("c:/db/changed", "c:/db/changed-vs"));
		});

		IDbLiveProject project2 = builder.CreateProject();

		Assert.NotSame(project1, project2);
		Assert.Equal("c:/db/changed-vs", project2.GetVisualStudioProjectPath());
	}

	[Fact]
	public void CreateMethods_ThrowObjectDisposed_WhenBuilderDisposed()
	{
		DbLiveBuilder builder = new DbLiveBuilder()
			.ConfigureServices(services =>
			{
				services.AddSingleton<IProjectPath>(new ProjectPath("c:/db/scripts", "c:/db/vs-scripts"));
			});

		_ = builder.CreateProject();
		builder.Dispose();

		Assert.Throws<ObjectDisposedException>(() => builder.CreateProject());
		Assert.Throws<ObjectDisposedException>(() =>
			builder.ConfigureServices(services => services.AddSingleton<IProjectPath>(new ProjectPath("a", "b"))));
	}
}
