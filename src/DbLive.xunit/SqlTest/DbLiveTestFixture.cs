using DbLive.Adapter;
using DbLive.Project;
using DbLive.Testing;
using System.Reflection;
using Xunit;

namespace DbLive.xunit;

/// <summary>
/// The fixture of the <c>[SqlFact]</c> tests of a class: deploys the project once into a new database before the tests of the class,
/// and drops the database after them. Derive a class with a public parameterless constructor from it, and name that class in
/// <see cref="SqlFactAttribute.TestFixture"/> and in <c>IClassFixture&lt;T&gt;</c>.
/// </summary>
/// <example>
/// <code>
/// public class MyDbTestFixture()
///     : DbLiveTestFixture(new MsSqlContainerFixtureBuilder(typeof(MyDbMarker).Assembly), dropDatabaseOnComplete: true);
/// </code>
/// </example>
/// <param name="fixtureBuilder">Gives the builder of a new database and the SQL project assembly, from which discovery reads the tests.</param>
/// <param name="dropDatabaseOnComplete">Drops the database after the tests of the class.</param>
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

	/// <summary>
	/// Gets a new database from the fixture builder and deploys the project to it: the migrations, the code and the breaking phase,
	/// without the SQL tests. xUnit calls it before the tests of the class.
	/// </summary>
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

	/// <summary>
	/// Drops the database when <c>dropDatabaseOnComplete</c> is true, and disposes the builder. xUnit calls it after the tests of the class.
	/// </summary>
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
