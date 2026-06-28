namespace DbLive.Tests.Deployers;

public class DeployLockRunnerTests
{
	[Fact]
	public async Task ExecuteWithLockAsync_skips_lock_when_disabled()
	{
		MockSet mockSet = new();
		mockSet.SettingsAccessor.GetProjectSettingsAsync().Returns(new DbLiveSettings
		{
			DeployLockEnabled = false
		});

		DeployLockRunner runner = mockSet.CreateUsingMocks<DeployLockRunner>();
		bool executed = false;

		await runner.ExecuteWithLockAsync(() =>
		{
			executed = true;
			return Task.CompletedTask;
		});

		Assert.True(executed);
		await mockSet.DeployLock.DidNotReceive().AcquireAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task ExecuteWithLockAsync_acquires_and_commits_when_enabled()
	{
		MockSet mockSet = new();
		mockSet.DbConnection.ConnectionString.Returns("Server=localhost;Database=TestDb;Trusted_Connection=True;");
		mockSet.SettingsAccessor.GetProjectSettingsAsync().Returns(new DbLiveSettings
		{
			DeployLockEnabled = true
		});

		TestDeployLockHandle handle = new();
		mockSet.DeployLock
			.AcquireAsync("DbLive:Deploy:TestDb", Arg.Any<CancellationToken>())
			.Returns(handle);

		DeployLockRunner runner = mockSet.CreateUsingMocks<DeployLockRunner>();
		bool executed = false;

		await runner.ExecuteWithLockAsync(() =>
		{
			executed = true;
			return Task.CompletedTask;
		});

		Assert.True(executed);
		Assert.True(handle.Committed);
		await mockSet.DeployLock.Received(1).AcquireAsync("DbLive:Deploy:TestDb", Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task ExecuteWithLockAsync_includes_project_id_in_resource_name()
	{
		MockSet mockSet = new();
		mockSet.DbConnection.ConnectionString.Returns("Server=localhost;Database=TestDb;Trusted_Connection=True;");
		mockSet.SettingsAccessor.GetProjectSettingsAsync().Returns(new DbLiveSettings
		{
			DeployLockEnabled = true,
			ProjectId = "my-project"
		});

		TestDeployLockHandle handle = new();
		mockSet.DeployLock
			.AcquireAsync("DbLive:Deploy:TestDb:my-project", Arg.Any<CancellationToken>())
			.Returns(handle);

		DeployLockRunner runner = mockSet.CreateUsingMocks<DeployLockRunner>();

		await runner.ExecuteWithLockAsync(() => Task.CompletedTask);

		Assert.True(handle.Committed);
	}

	[Fact]
	public async Task ExecuteWithLockAsync_rolls_back_when_action_fails()
	{
		MockSet mockSet = new();
		mockSet.DbConnection.ConnectionString.Returns("Server=localhost;Database=TestDb;Trusted_Connection=True;");
		mockSet.SettingsAccessor.GetProjectSettingsAsync().Returns(new DbLiveSettings
		{
			DeployLockEnabled = true
		});

		TestDeployLockHandle handle = new();
		mockSet.DeployLock
			.AcquireAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(handle);

		DeployLockRunner runner = mockSet.CreateUsingMocks<DeployLockRunner>();

		await Assert.ThrowsAsync<InvalidOperationException>(() =>
			runner.ExecuteWithLockAsync(() => throw new InvalidOperationException("deploy failed")));

		Assert.False(handle.Committed);
		Assert.True(handle.Disposed);
	}

	private sealed class TestDeployLockHandle : IDeployLockHandle
	{
		public bool Committed { get; private set; }
		public bool Disposed { get; private set; }

		public Task CommitAsync()
		{
			Committed = true;
			return Task.CompletedTask;
		}

		public ValueTask DisposeAsync()
		{
			Disposed = true;
			return ValueTask.CompletedTask;
		}
	}
}
