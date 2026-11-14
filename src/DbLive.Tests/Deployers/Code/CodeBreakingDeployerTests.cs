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

		ProjectSnapshot project = ProjectSnapshots.Empty with { CodeBreakingItems = [item1, item2] };

		DateTime startedUtc = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
		DateTime appliedUtc = startedUtc.AddMilliseconds(15);
		mockSet.TimeProvider.UtcNow().Returns(startedUtc, appliedUtc, startedUtc, appliedUtc);

		CodeBreakingDeployer deployer = mockSet.CreateUsingMocks<CodeBreakingDeployer>();

		await deployer.DeployAsync(project);

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

		ProjectSnapshot project = ProjectSnapshots.Empty with { CodeBreakingItems = [GetItem("Code/GetUser.breaking.sql", "drop proc if exists dbo.GetUser_v1")] };

		CodeBreakingDeployer deployer = mockSet.CreateUsingMocks<CodeBreakingDeployer>();

		await deployer.DeployAsync(project);
		await deployer.DeployAsync(project);

		await mockSet.DbLiveDA.Received(2).ExecuteNonQueryAsync("drop proc if exists dbo.GetUser_v1", Arg.Any<TranIsolationLevel>(), Arg.Any<TimeSpan?>());
		await mockSet.DbLiveDA.DidNotReceive().GetCodeItemsAsync();
	}

	[Fact]
	public async Task Failed_script_is_recorded_with_its_error_and_stops_the_deployment()
	{
		MockSet mockSet = new();

		ProjectSnapshot project = ProjectSnapshots.Empty with
		{
			CodeBreakingItems = [
				GetItem("Code/a.breaking.sql", "drop proc dbo.a_v1"),
				GetItem("Code/b.breaking.sql", "drop proc if exists dbo.b_v1")
			]
		};

		mockSet.DbLiveDA
			.ExecuteNonQueryAsync("drop proc dbo.a_v1", Arg.Any<TranIsolationLevel>(), Arg.Any<TimeSpan?>())
			.Returns(Task.FromException(new DbLiveSqlException("Cannot drop the procedure 'dbo.a_v1'.")));

		CodeBreakingDeployer deployer = mockSet.CreateUsingMocks<CodeBreakingDeployer>();

		CodeDeploymentException ex = await Assert.ThrowsAsync<CodeDeploymentException>(() => deployer.DeployAsync(project));

		Assert.Contains("Code/a.breaking.sql", ex.Message);
		_ = Assert.IsType<DbLiveSqlException>(ex.InnerException);

		await mockSet.DbLiveDA.Received(1).SaveCodeItemAsync(Arg.Is<CodeItemDto>(d =>
			d.RelativePath == "Code/a.breaking.sql"
			&& d.ItemType == CodeItemType.Breaking
			&& d.Status == CodeItemStatus.Error
			&& d.ErrorMessage == "Cannot drop the procedure 'dbo.a_v1'."
		));

		await mockSet.DbLiveDA.DidNotReceive().ExecuteNonQueryAsync("drop proc if exists dbo.b_v1", Arg.Any<TranIsolationLevel>(), Arg.Any<TimeSpan?>());
	}

	[Fact]
	public async Task Failed_script_in_a_transaction_fails_with_its_own_error_and_saves_no_error_row()
	{
		MockSet mockSet = new();

		ProjectSnapshot project = ProjectSnapshots.Empty with { CodeBreakingItems = [GetItem("Code/a.breaking.sql", "drop proc dbo.a_v1")] };

		DbLiveSqlException scriptError = new("Cannot drop the procedure 'dbo.a_v1'.");
		mockSet.DbLiveDA
			.ExecuteNonQueryAsync("drop proc dbo.a_v1", Arg.Any<TranIsolationLevel>(), Arg.Any<TimeSpan?>())
			.Returns(Task.FromException(scriptError));

		CodeBreakingDeployer deployer = mockSet.CreateUsingMocks<CodeBreakingDeployer>();

		CodeDeploymentException ex;
		using (TransactionScope _ = TransactionScopeManager.Create())
		{
			ex = await Assert.ThrowsAsync<CodeDeploymentException>(() => deployer.DeployAsync(project));
		}

		Assert.Equal("Deploy code breaking script error. File path: Code/a.breaking.sql", ex.Message);
		Assert.Same(scriptError, ex.InnerException);
		await mockSet.DbLiveDA.DidNotReceiveWithAnyArgs().SaveCodeItemAsync(default!);
	}

	[Fact]
	public async Task Failed_script_fails_with_the_error_of_its_error_row_when_the_row_cannot_be_saved()
	{
		MockSet mockSet = new();

		ProjectSnapshot project = ProjectSnapshots.Empty with { CodeBreakingItems = [GetItem("Code/a.breaking.sql", "drop proc dbo.a_v1")] };

		mockSet.DbLiveDA
			.ExecuteNonQueryAsync("drop proc dbo.a_v1", Arg.Any<TranIsolationLevel>(), Arg.Any<TimeSpan?>())
			.Returns(Task.FromException(new DbLiveSqlException("Cannot drop the procedure 'dbo.a_v1'.")));

		DbLiveSqlException saveError = new("Invalid column name 'error'.");
		mockSet.DbLiveDA
			.SaveCodeItemAsync(Arg.Any<CodeItemDto>())
			.Returns(Task.FromException(saveError));

		CodeBreakingDeployer deployer = mockSet.CreateUsingMocks<CodeBreakingDeployer>();

		DbLiveSqlException ex = await Assert.ThrowsAsync<DbLiveSqlException>(() => deployer.DeployAsync(project));

		Assert.Same(saveError, ex);
	}

	[Fact]
	public async Task No_scripts_nothing_runs()
	{
		MockSet mockSet = new();

		ProjectSnapshot project = ProjectSnapshots.Empty;

		CodeBreakingDeployer deployer = mockSet.CreateUsingMocks<CodeBreakingDeployer>();

		await deployer.DeployAsync(project);

		await mockSet.DbLiveDA.DidNotReceiveWithAnyArgs().ExecuteNonQueryAsync(default!, default, default);
		await mockSet.DbLiveDA.DidNotReceiveWithAnyArgs().SaveCodeItemAsync(default!);
	}

	private static GenericItem GetItem(string relativePath, string content) => new()
	{
		Name = Path.GetFileName(relativePath),
		FileData = new FileData { RelativePath = relativePath, Content = content }
	};
}
