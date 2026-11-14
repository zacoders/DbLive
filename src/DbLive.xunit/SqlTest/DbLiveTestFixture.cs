using DbLive.Adapter;
using DbLive.Project;
using DbLive.Testing;
using System.Reflection;
using Xunit;

namespace DbLive.xunit;

public class DbLiveTestFixture(
		IDbLiveFixtureBuilder fixtureBuilder,
		bool dropDatabaseOnComplete
	)
	: IAsyncLifetime
{
	private IDbLive? _deployer;
	private DbLiveBuilder? _builder;
	private IDbLiveTester? _tester;

	/// <summary>
	/// The tester of the database that <see cref="InitializeAsync"/> deployed. xUnit initializes the fixture before the tests of the class.
	/// </summary>
	/// <exception cref="InvalidOperationException">Read before <see cref="InitializeAsync"/>.</exception>
	public IDbLiveTester Tester => _tester
		?? throw new InvalidOperationException("The fixture is not initialized: xUnit calls InitializeAsync before the tests of the class that has it as IClassFixture.");

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

		_tester = _builder.CreateTester();

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
				IDbLiveDA da = _builder.GetRequiredService<IDbLiveDA>();
				await da.DropDbAsync().ConfigureAwait(false);
			}
		}
		finally
		{
			await _builder.DisposeAsync().ConfigureAwait(false);
		}
	}
}
