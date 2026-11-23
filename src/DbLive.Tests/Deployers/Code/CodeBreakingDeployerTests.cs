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
		));

		// each script is its own command; no transaction of its own.
		await mockSet.TransactionRunner.DidNotReceiveWithAnyArgs()
			.ExecuteWithinTransactionAsync(default, default, default!, default!);
	}

	[Fact]
	public async Task Runs_on_every_deployment_without_looking_at_the_previous_run()
	{
		MockSet mockSet = new();

		ProjectSnapshot project = ProjectSnapshots.Empty with { CodeBreakingItems = [GetItem("Code/GetUser.breaking.sql", "drop proc if exists dbo.GetUser_v1")] };

		CodeBreakingDeployer deployer = mockSet.CreateUsingMocks<CodeBreakingDeployer>();

		await deployer.DeployAsync(project);
		await deployer.DeployAsync(project);

		await mockSet.DbLiveDA.Received(2).ExecuteNonQueryAsync("drop proc if exists dbo.GetUser_v1", Arg.Any<TranIsolationLevel>(), Arg.Any<TimeSpan>());
		await mockSet.DbLiveDA.DidNotReceive().GetCodeItemsAsync();
	}

	[Fact]
	public async Task Failed_script_writes_no_row_logs_its_sql_error_and_stops_the_deployment()
	{
		MockSet mockSet = new();

		ProjectSnapshot project = ProjectSnapshots.Empty with
		{
			CodeBreakingItems = [
				GetItem("Code/a.breaking.sql", "drop proc dbo.a_v1"),
				GetItem("Code/b.breaking.sql", "drop proc if exists dbo.b_v1")
			]
		};

		DbLiveSqlException scriptError = new(
			"Cannot drop the procedure 'dbo.a_v1'.",
			"SQL Server error:\nMsg 3701, Level 11, State 5, Line 1\nCannot drop the procedure 'dbo.a_v1'.\n",
			null
		);
		mockSet.DbLiveDA
			.ExecuteNonQueryAsync("drop proc dbo.a_v1", Arg.Any<TranIsolationLevel>(), Arg.Any<TimeSpan>())
			.Returns(Task.FromException(scriptError));

		CodeBreakingDeployer deployer = mockSet.CreateUsingMocks<CodeBreakingDeployer>();

		CodeDeploymentException ex = await Assert.ThrowsAsync<CodeDeploymentException>(() => deployer.DeployAsync(project));

		// SqlError is a property of the inner exception: without it in the message, the log would have the message of the server only.
		Assert.Equal(
			$"Deploy code breaking script error. File path: Code/a.breaking.sql{Environment.NewLine}"
			+ "SQL Server error:\nMsg 3701, Level 11, State 5, Line 1\nCannot drop the procedure 'dbo.a_v1'.",
			ex.Message
		);
		Assert.Same(scriptError, ex.InnerException);
		mockSet.Logger.Received(1).Error(ex, "Deploy code breaking script error. File path: {FilePath}", "Code/a.breaking.sql");

		// the row of the script stays its last success.
		await mockSet.DbLiveDA.DidNotReceiveWithAnyArgs().SaveCodeItemAsync(default!);
		await mockSet.DbLiveDA.DidNotReceive().ExecuteNonQueryAsync("drop proc if exists dbo.b_v1", Arg.Any<TranIsolationLevel>(), Arg.Any<TimeSpan>());
	}

	[Fact]
	public async Task Failed_script_in_a_transaction_fails_with_its_own_error()
	{
		MockSet mockSet = new();

		ProjectSnapshot project = ProjectSnapshots.Empty with { CodeBreakingItems = [GetItem("Code/a.breaking.sql", "drop proc dbo.a_v1")] };

		DbLiveSqlException scriptError = new("Cannot drop the procedure 'dbo.a_v1'.");
		mockSet.DbLiveDA
			.ExecuteNonQueryAsync("drop proc dbo.a_v1", Arg.Any<TranIsolationLevel>(), Arg.Any<TimeSpan>())
			.Returns(Task.FromException(scriptError));

		CodeBreakingDeployer deployer = mockSet.CreateUsingMocks<CodeBreakingDeployer>();

		CodeDeploymentException ex;
		using (TransactionScope _ = TransactionScopeManager.Create())
		{
			ex = await Assert.ThrowsAsync<CodeDeploymentException>(() => deployer.DeployAsync(project));
		}

		Assert.Equal(
			$"Deploy code breaking script error. File path: Code/a.breaking.sql{Environment.NewLine}Cannot drop the procedure 'dbo.a_v1'.",
			ex.Message
		);
		Assert.Same(scriptError, ex.InnerException);
		await mockSet.DbLiveDA.DidNotReceiveWithAnyArgs().SaveCodeItemAsync(default!);
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
