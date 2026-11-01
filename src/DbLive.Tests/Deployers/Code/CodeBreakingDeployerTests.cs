namespace DbLive.Tests.Deployers.Code;

public class CodeBreakingDeployerTests
{
	[Fact]
	public async Task Runs_every_script_in_order_and_records_each_run()
	{
		MockSet mockSet = new();

		mockSet.SettingsAccessor.GetProjectSettingsAsync().Returns(new DbLiveSettings
		{
			TransactionIsolationLevel = TranIsolationLevel.RepeatableRead,
			CodeItemTimeout = TimeSpan.FromMinutes(2)
		});

		GenericItem item1 = GetItem("Code/dbo/GetOrder.breaking.sql", "drop proc if exists dbo.GetOrder_v1");
		GenericItem item2 = GetItem("Code/dbo/GetUser.b.sql", "drop proc if exists dbo.GetUser_v1");

		mockSet.DbLiveProject.GetCodeBreakingItemsAsync().Returns([item1, item2]);

		DateTime startedUtc = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
		DateTime appliedUtc = startedUtc.AddMilliseconds(15);
		mockSet.TimeProvider.UtcNow().Returns(startedUtc, appliedUtc, startedUtc, appliedUtc);

		CodeBreakingDeployer deployer = mockSet.CreateUsingMocks<CodeBreakingDeployer>();

		await deployer.DeployAsync();

		Received.InOrder(() =>
		{
			mockSet.DbLiveDA.ExecuteNonQueryAsync("drop proc if exists dbo.GetOrder_v1", TranIsolationLevel.RepeatableRead, TimeSpan.FromMinutes(2));
			mockSet.DbLiveDA.SaveCodeItemAsync(Arg.Is<CodeItemDto>(d => d.RelativePath == "Code/dbo/GetOrder.breaking.sql"));
			mockSet.DbLiveDA.ExecuteNonQueryAsync("drop proc if exists dbo.GetUser_v1", TranIsolationLevel.RepeatableRead, TimeSpan.FromMinutes(2));
			mockSet.DbLiveDA.SaveCodeItemAsync(Arg.Is<CodeItemDto>(d => d.RelativePath == "Code/dbo/GetUser.b.sql"));
		});

		await mockSet.DbLiveDA.Received(1).SaveCodeItemAsync(Arg.Is<CodeItemDto>(d =>
			d.RelativePath == "Code/dbo/GetUser.b.sql"
			&& d.ItemType == CodeItemType.Breaking
			&& d.Status == CodeItemStatus.Applied
			&& d.ContentHash == item2.FileData.ContentHash
			&& d.AppliedUtc == appliedUtc
			&& d.ExecutionTimeMs == 15
			&& d.VerifiedUtc == null
			&& d.ErrorMessage == ""
		));

		// each script is its own command; no transaction of its own.
		await mockSet.TransactionRunner.DidNotReceiveWithAnyArgs()
			.ExecuteWithinTransactionAsync(default, default, default, default!);
	}

	[Fact]
	public async Task Runs_on_every_deployment_without_looking_at_the_previous_run()
	{
		MockSet mockSet = new();

		mockSet.DbLiveProject.GetCodeBreakingItemsAsync().Returns([GetItem("Code/GetUser.breaking.sql", "drop proc if exists dbo.GetUser_v1")]);

		CodeBreakingDeployer deployer = mockSet.CreateUsingMocks<CodeBreakingDeployer>();

		await deployer.DeployAsync();
		await deployer.DeployAsync();

		await mockSet.DbLiveDA.Received(2).ExecuteNonQueryAsync("drop proc if exists dbo.GetUser_v1", Arg.Any<TranIsolationLevel>(), Arg.Any<TimeSpan?>());
		await mockSet.DbLiveDA.DidNotReceiveWithAnyArgs().FindCodeItemAsync(default!);
	}

	[Fact]
	public async Task Failed_script_is_recorded_with_its_error_and_stops_the_deployment()
	{
		MockSet mockSet = new();

		mockSet.DbLiveProject.GetCodeBreakingItemsAsync().Returns([
			GetItem("Code/a.breaking.sql", "drop proc dbo.a_v1"),
			GetItem("Code/b.breaking.sql", "drop proc if exists dbo.b_v1")
		]);

		mockSet.DbLiveDA
			.ExecuteNonQueryAsync("drop proc dbo.a_v1", Arg.Any<TranIsolationLevel>(), Arg.Any<TimeSpan?>())
			.Returns(Task.FromException(new DbLiveSqlException("Cannot drop the procedure 'dbo.a_v1'.")));

		CodeBreakingDeployer deployer = mockSet.CreateUsingMocks<CodeBreakingDeployer>();

		CodeDeploymentException ex = await Assert.ThrowsAsync<CodeDeploymentException>(deployer.DeployAsync);

		Assert.Contains("Code/a.breaking.sql", ex.Message);
		_ = Assert.IsType<DbLiveSqlException>(ex.InnerException);

		await mockSet.DbLiveDA.Received(1).SaveCodeItemAsync(Arg.Is<CodeItemDto>(d =>
			d.RelativePath == "Code/a.breaking.sql"
			&& d.ItemType == CodeItemType.Breaking
			&& d.Status == CodeItemStatus.Error
			&& d.ErrorMessage!.Contains("Cannot drop the procedure 'dbo.a_v1'.")
		));

		await mockSet.DbLiveDA.DidNotReceive().ExecuteNonQueryAsync("drop proc if exists dbo.b_v1", Arg.Any<TranIsolationLevel>(), Arg.Any<TimeSpan?>());
	}

	[Fact]
	public async Task No_scripts_nothing_runs()
	{
		MockSet mockSet = new();

		mockSet.DbLiveProject.GetCodeBreakingItemsAsync().Returns([]);

		CodeBreakingDeployer deployer = mockSet.CreateUsingMocks<CodeBreakingDeployer>();

		await deployer.DeployAsync();

		await mockSet.DbLiveDA.DidNotReceiveWithAnyArgs().ExecuteNonQueryAsync(default!, default, default);
		await mockSet.DbLiveDA.DidNotReceiveWithAnyArgs().SaveCodeItemAsync(default!);
	}

	private static GenericItem GetItem(string relativePath, string content) => new()
	{
		Name = Path.GetFileName(relativePath),
		FileData = new FileData { RelativePath = relativePath, Content = content }
	};
}
