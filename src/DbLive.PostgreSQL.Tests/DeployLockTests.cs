using DbLive.Adapter;
using DbLive.Common.Settings;
using DbLive.Deployers;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics.CodeAnalysis;
using Xunit.Extensions.AssemblyFixture;

namespace DbLive.PostgreSQL.Tests;

[SuppressMessage("Usage", "xUnit1041:Fixture arguments to test classes must have fixture sources", Justification = "AssemblyFixture will be properly supported in xUnit v3. waiting.")]
public class DeployLockTests : IntegrationTestsBase, IAssemblyFixture<PostgreSqlFixture>, IAsyncLifetime
{
	private readonly IDbLiveDA _da;
	private readonly IDeployLock _deployLock;
	private readonly string _resourceName;

	public DeployLockTests(PostgreSqlFixture fixture, ITestOutputHelper output) : base(output)
	{
		Container.InitializePostgreSQL();
		Container.InitializeDbLive();

		var cnn = new DbLiveDbConnection(fixture.PostgresDBConnectionString);
		_ = Container.AddSingleton<IDbLiveDbConnection>(cnn);

		_da = GetService<IDbLiveDA>();
		_deployLock = GetService<IDeployLock>();
		_resourceName = DeployLockResource.Build(cnn, new DbLiveSettings());
	}

	public async Task InitializeAsync()
	{
		await _da.CreateDBAsync();
	}

	public Task DisposeAsync() => Task.CompletedTask;

	[Fact]
	public async Task Second_acquire_waits_until_first_releases()
	{
		await using IDeployLockHandle firstHandle = await _deployLock.AcquireAsync(_resourceName);

		bool secondAcquired = false;
		Task secondTask = Task.Run(async () =>
		{
			await using IDeployLockHandle secondHandle = await _deployLock.AcquireAsync(_resourceName);
			secondAcquired = true;
			await secondHandle.CommitAsync();
		});

		await Task.Delay(300);
		Assert.False(secondAcquired);

		await firstHandle.CommitAsync();
		await secondTask.WaitAsync(TimeSpan.FromSeconds(30));

		Assert.True(secondAcquired);
	}
}
