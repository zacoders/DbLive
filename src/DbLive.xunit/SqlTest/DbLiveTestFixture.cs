using DbLive.Adapter;
using DbLive.Project;
using DbLive.Testing;
using System.Reflection;
using Xunit;

namespace DbLive.xunit.SqlTest;

public class DbLiveTestFixture(
		IDbLiveFixtureBuilder fixtureBuilder,
		bool dropDatabaseOnComplete
	)
	: IAsyncLifetime
{
	private IDbLive? _deployer;
	private DbLiveBuilder? _builder;

	public IDbLiveTester? Tester { get; private set; }

	/// <summary>
	/// The SQL tests of the project, for discovery. The builder lives only for this read.
	/// </summary>
	internal async Task<IReadOnlyCollection<TestItem>> GetTestsAsync()
	{
		using DbLiveBuilder builder = new();

		return await builder
			.SetProject(GetProjectAssembly())
			.CreateProject()
			.GetTestsAsync()
			.ConfigureAwait(false);
	}

	internal Assembly GetProjectAssembly() => fixtureBuilder.GetProjectAssembly();

	public async Task InitializeAsync()
	{
		_builder = await fixtureBuilder.GetBuilderAsync().ConfigureAwait(false);

		_deployer = _builder.CreateDeployer();

		Tester = _builder.CreateTester();

		await _deployer!.DeployAsync(new DeployParameters
		{
			CreateDbIfNotExists = true,
			DeployBreaking = true,
			DeployCode = true,
			DeployMigrations = true
		}).ConfigureAwait(false);
	}

	public async Task DisposeAsync()
	{
		if (_builder is null)
		{
			return;
		}

		try
		{
			if (dropDatabaseOnComplete)
			{
				IDbLiveDA da = _builder.CreateDbLiveDA();
				await da.DropDbAsync().ConfigureAwait(false);
			}
		}
		finally
		{
			await _builder.DisposeAsync().ConfigureAwait(false);
		}
	}
}
