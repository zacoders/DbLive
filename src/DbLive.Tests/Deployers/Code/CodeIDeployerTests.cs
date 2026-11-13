namespace DbLive.Tests.Deployers.Code;

public class CodeDeployerTests
{
	[Fact]
	public async Task DeployCode_NoCodeGroups()
	{
		MockSet mockSet = new();

		CodeDeployer deployer = mockSet.CreateUsingMocks<CodeDeployer>();

		await deployer.DeployAsync(ProjectSnapshots.Empty, DeployParameters.Default);
	}

	[Fact]
	public async Task DeployCode_SkipDeployment()
	{
		MockSet mockSet = new();

		CodeDeployer deployer = mockSet.CreateUsingMocks<CodeDeployer>();

		await deployer.DeployAsync(ProjectSnapshots.Empty, new DeployParameters { DeployCode = false });

		await mockSet.DbLiveDA.DidNotReceiveWithAnyArgs().MarkMissingCodeAsRemovedAsync(default!, default);
	}

	[Fact]
	public async Task DeployCode_Marks_Rows_Of_Files_Missing_From_The_Project_As_Removed()
	{
		MockSet mockSet = new();

		ProjectSnapshot project = ProjectSnapshots.Empty with
		{
			CodeGroups = [
				new CodeGroup { Path = "Code/schemas", CodeItems = [GetCodeItem("dbo.sql")] },
				new CodeGroup { Path = "Code", CodeItems = [GetCodeItem("GetUser.sql"), GetCodeItem("GetOrder.sql")] }
			],
			// a breaking script of the Code folder is a file of the project too: its row is not marked.
			CodeBreakingItems = [
				new GenericItem
				{
					Name = "GetUser.breaking.sql",
					FileData = new FileData { RelativePath = "Code/GetUser.breaking.sql", Content = "drop proc if exists dbo.GetUser_v1" }
				}
			]
		};

		mockSet.CodeItemDeployer.DeployAsync(Arg.Any<CodeItem>()).Returns(CodeItemDeployResult.Success());

		DateTime now = new(2026, 11, 1, 10, 0, 0, DateTimeKind.Utc);
		mockSet.TimeProvider.UtcNow().Returns(now);

		mockSet.DbLiveDA.MarkMissingCodeAsRemovedAsync(Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<DateTime>())
			.Returns(["code/getuser-v1.sql"]);

		CodeDeployer deployer = mockSet.CreateUsingMocks<CodeDeployer>();

		await deployer.DeployAsync(project, DeployParameters.Default);

		await mockSet.DbLiveDA.Received(1).MarkMissingCodeAsRemovedAsync(
			Arg.Is<IReadOnlyCollection<string>>(paths => paths.SequenceEqual(new[]
			{
				"/some_path/dbo.sql",
				"/some_path/GetUser.sql",
				"/some_path/GetOrder.sql",
				"Code/GetUser.breaking.sql"
			})),
			now
		);

		mockSet.Logger.Received(1).Information(Arg.Any<string>(), "code/getuser-v1.sql");
	}

	[Fact]
	public async Task DeployCode_Without_Code_Files_Marks_All_Rows_As_Removed()
	{
		MockSet mockSet = new();

		ProjectSnapshot project = ProjectSnapshots.Empty;

		CodeDeployer deployer = mockSet.CreateUsingMocks<CodeDeployer>();

		await deployer.DeployAsync(project, DeployParameters.Default);

		await mockSet.DbLiveDA.Received(1).MarkMissingCodeAsRemovedAsync(
			Arg.Is<IReadOnlyCollection<string>>(paths => paths.Count == 0),
			Arg.Any<DateTime>()
		);
	}

	[Fact]
	public async Task DeployCode()
	{
		MockSet mockSet = new();

		ProjectSnapshot project = ProjectSnapshots.Empty with
		{
			CodeGroups = [
				new CodeGroup
				{
					Path = "Code1",
					CodeItems = [GetCodeItem("item1"), GetCodeItem("item2"), GetCodeItem("item3")]
				},
				new CodeGroup
				{
					Path = "Code2",
					CodeItems = [GetCodeItem("item11"), GetCodeItem("item22")]
				}
			]
		};

		mockSet.CodeItemDeployer.DeployAsync(Arg.Any<CodeItem>()).Returns(CodeItemDeployResult.Success());

		CodeDeployer deployer = mockSet.CreateUsingMocks<CodeDeployer>();

		await deployer.DeployAsync(project, DeployParameters.Default);

		await mockSet.CodeItemDeployer.Received(5).DeployAsync(Arg.Any<CodeItem>());
		await mockSet.DbLiveDA.DidNotReceiveWithAnyArgs().MarkCodeAsVerifiedAsync(default!, default);
	}

	[Fact]
	public async Task DeployCode_Skips_Deployed_Files_And_Verifies_Them_With_One_Command()
	{
		MockSet mockSet = new();

		CodeItem applied = GetCodeItem("dbo.sql");
		CodeItem crlf = GetCodeItem("GetUser.sql", "select 1 as a\nunion all\nselect 2;\n");
		CodeItem changed = GetCodeItem("GetOrder.sql");
		CodeItem removed = GetCodeItem("Restored.sql");
		CodeItem failed = GetCodeItem("Failed.sql");
		CodeItem added = GetCodeItem("New.sql");

		ProjectSnapshot project = ProjectSnapshots.Empty with
		{
			CodeGroups = [
				new CodeGroup { Path = "Code/schemas", CodeItems = [applied] },
				new CodeGroup { Path = "Code", CodeItems = [crlf, changed, removed, failed, added] }
			]
		};

		// the database stores the paths in lower case.
		mockSet.DbLiveDA.GetCodeItemsAsync().Returns([
			GetCodeRow(applied, CodeItemStatus.Applied, applied.FileData.ContentHash),
			// deployed from a CRLF checkout by DbLive 1.x, which hashed the raw text: only the current hash counts, so it runs once more.
			GetCodeRow(crlf, CodeItemStatus.Applied, LegacyContentHash.Of("select 1 as a\r\nunion all\r\nselect 2;\r\n")),
			GetCodeRow(changed, CodeItemStatus.Applied, changed.FileData.ContentHash + 123),
			// the file was deleted by a release, and a later release brought it back (git revert) with the same content.
			GetCodeRow(removed, CodeItemStatus.Removed, removed.FileData.ContentHash),
			GetCodeRow(failed, CodeItemStatus.Error, failed.FileData.ContentHash)
		]);

		mockSet.CodeItemDeployer.DeployAsync(Arg.Any<CodeItem>()).Returns(CodeItemDeployResult.Success());

		DateTime now = new(2026, 11, 7, 10, 0, 0, DateTimeKind.Utc);
		mockSet.TimeProvider.UtcNow().Returns(now);

		CodeDeployer deployer = mockSet.CreateUsingMocks<CodeDeployer>();

		await deployer.DeployAsync(project, DeployParameters.Default);

		await mockSet.CodeItemDeployer.DidNotReceive().DeployAsync(applied);
		await mockSet.CodeItemDeployer.Received(1).DeployAsync(crlf);
		await mockSet.CodeItemDeployer.Received(1).DeployAsync(changed);
		await mockSet.CodeItemDeployer.Received(1).DeployAsync(removed);
		await mockSet.CodeItemDeployer.Received(1).DeployAsync(failed);
		await mockSet.CodeItemDeployer.Received(1).DeployAsync(added);

		await mockSet.DbLiveDA.Received(1).MarkCodeAsVerifiedAsync(
			Arg.Is<IReadOnlyCollection<string>>(paths => paths.SequenceEqual(new[] { "/some_path/dbo.sql" })),
			now
		);

		mockSet.Logger.Received(1).Information("Deploying code file: {filePath} [hash match]", "/some_path/dbo.sql");
	}

	[Fact]
	public async Task DeployCode_SerialWhenInAmbientTransaction()
	{
		MockSet mockSet = new();
		mockSet.SettingsAccessor.GetProjectSettingsAsync().Returns(new DbLiveSettings
		{
			NumberOfThreadsForCodeDeploy = 10
		});

		List<CodeItem> codeItems =
		[
			GetCodeItem("item1"),
			GetCodeItem("item2"),
			GetCodeItem("item3"),
			GetCodeItem("item4"),
			GetCodeItem("item5")
		];

		ProjectSnapshot project = ProjectSnapshots.Empty with
		{
			CodeGroups = [
				new CodeGroup { Path = "Code", CodeItems = codeItems }
			]
		};

		int concurrent = 0;
		int maxConcurrent = 0;
		object sync = new();

		mockSet.CodeItemDeployer.DeployAsync(Arg.Any<CodeItem>()).Returns(async _ =>
		{
			lock (sync)
			{
				concurrent++;
				maxConcurrent = Math.Max(maxConcurrent, concurrent);
			}

			await Task.Delay(50).ConfigureAwait(false);

			lock (sync)
			{
				concurrent--;
			}

			return CodeItemDeployResult.Success();
		});

		CodeDeployer deployer = mockSet.CreateUsingMocks<CodeDeployer>();

		using TransactionScope scope = TransactionScopeManager.Create();
		await deployer.DeployAsync(project, DeployParameters.Default);
		scope.Complete();

		Assert.Equal(1, maxConcurrent);
		mockSet.Logger.Received(1).Information(
			"Parallel code deploy disabled because deployment runs inside a transaction."
		);
	}


	[Fact]
	public async Task DeployCode_FailedCodeItems()
	{
		MockSet mockSet = new();
		mockSet.SettingsAccessor.GetProjectSettingsAsync().Returns(new DbLiveSettings
		{
			NumberOfThreadsForCodeDeploy = 1,
			MaxCodeDeployRetries = 3
		});

		ProjectSnapshot project = ProjectSnapshots.Empty with
		{
			CodeGroups = [
				new CodeGroup
				{
					Path = "Code2",
					CodeItems = [GetCodeItem("item11"), GetCodeItem("item22")]
				}
			]
		};

		mockSet.CodeItemDeployer.DeployAsync(Arg.Any<CodeItem>())
			.Returns(CodeItemDeployResult.Failed(new Exception("Dummy1")));

		CodeDeployer deployer = mockSet.CreateUsingMocks<CodeDeployer>();

		await Assert.ThrowsAsync<CodeDeploymentAggregateException>(
			async () => await deployer.DeployAsync(project, DeployParameters.Default).ConfigureAwait(false)
		);

		await mockSet.CodeItemDeployer.Received().DeployAsync(Arg.Any<CodeItem>());

		// rows of missing files are marked only by a code deployment that completed.
		await mockSet.DbLiveDA.DidNotReceiveWithAnyArgs().MarkMissingCodeAsRemovedAsync(default!, default);
	}

	[Fact]
	public async Task DeployCode_DependencyRetry_UsesQueueOrder()
	{
		MockSet mockSet = new();
		mockSet.SettingsAccessor.GetProjectSettingsAsync().Returns(new DbLiveSettings
		{
			NumberOfThreadsForCodeDeploy = 1,
			MaxCodeDeployRetries = 3
		});

		CodeItem dependentItem = GetCodeItem("dependent");
		CodeItem mainItem = GetCodeItem("main");

		ProjectSnapshot project = ProjectSnapshots.Empty with
		{
			CodeGroups = [
				new CodeGroup
				{
					Path = "Code",
					CodeItems = [dependentItem, mainItem]
				}
			]
		};

		List<string> attempts = [];

		mockSet.CodeItemDeployer
			.DeployAsync(Arg.Any<CodeItem>())
			.Returns(callInfo =>
			{
				CodeItem item = callInfo.Arg<CodeItem>();
				attempts.Add(item.Name);

				int attemptForItem = attempts.Count(i => i == item.Name);
				if (item.Name == dependentItem.Name && attemptForItem == 1)
				{
					return CodeItemDeployResult.Failed(new Exception("Dependency is missing"));
				}

				return CodeItemDeployResult.Success();
			});

		CodeDeployer deployer = mockSet.CreateUsingMocks<CodeDeployer>();

		await deployer.DeployAsync(project, DeployParameters.Default);

		Assert.Equal([dependentItem.Name, mainItem.Name, dependentItem.Name], attempts);
	}

	[Fact]
	public async Task DeployCode_StopsAfterConfiguredMaxRetries_ForSingleItem()
	{
		MockSet mockSet = new();
		mockSet.SettingsAccessor.GetProjectSettingsAsync().Returns(new DbLiveSettings
		{
			NumberOfThreadsForCodeDeploy = 1,
			MaxCodeDeployRetries = 3
		});

		CodeItem failingItem = GetCodeItem("always-failing");

		ProjectSnapshot project = ProjectSnapshots.Empty with
		{
			CodeGroups = [
				new CodeGroup
				{
					Path = "Code",
					CodeItems = [failingItem]
				}
			]
		};

		mockSet.CodeItemDeployer
			.DeployAsync(Arg.Is<CodeItem>(i => i.Name == failingItem.Name))
			.Returns(CodeItemDeployResult.Failed(new Exception("Dummy")));

		CodeDeployer deployer = mockSet.CreateUsingMocks<CodeDeployer>();

		await Assert.ThrowsAsync<CodeDeploymentAggregateException>(
			async () => await deployer.DeployAsync(project, DeployParameters.Default).ConfigureAwait(false)
		);

		await mockSet.CodeItemDeployer.Received(3)
			.DeployAsync(Arg.Is<CodeItem>(i => i.Name == failingItem.Name));
	}

	[Fact]
	public async Task DeployCode_Does_Not_Retry_An_Item_That_Cannot_Be_Retried()
	{
		MockSet mockSet = new();
		mockSet.SettingsAccessor.GetProjectSettingsAsync().Returns(new DbLiveSettings
		{
			NumberOfThreadsForCodeDeploy = 1,
			MaxCodeDeployRetries = 3
		});

		CodeItem failingItem = GetCodeItem("aborts-the-transaction");
		CodeItem otherItem = GetCodeItem("other");

		ProjectSnapshot project = ProjectSnapshots.Empty with
		{
			CodeGroups = [
				new CodeGroup { Path = "Code", CodeItems = [failingItem, otherItem] }
			]
		};

		CodeDeploymentException error = new("Deploy code file error.", new Exception("syntax error"));
		mockSet.CodeItemDeployer
			.DeployAsync(failingItem)
			.Returns(new CodeItemDeployResult { IsSuccess = false, CanRetry = false, Exception = error });

		CodeDeployer deployer = mockSet.CreateUsingMocks<CodeDeployer>();

		CodeDeploymentAggregateException ex = await Assert.ThrowsAsync<CodeDeploymentAggregateException>(
			() => deployer.DeployAsync(project, DeployParameters.Default)
		);

		// the error of the file, not of a retry in the aborted transaction; the files after it do not run.
		Assert.Same(error, Assert.Single(ex.InnerExceptions));
		await mockSet.CodeItemDeployer.Received(1).DeployAsync(failingItem);
		await mockSet.CodeItemDeployer.DidNotReceive().DeployAsync(otherItem);
	}

	private static CodeItem GetCodeItem(string name, string? content = null)
	{
		string relativePath = $"/some_path/{name}";
		return new CodeItem
		{
			Name = name,
			FileData = new FileData
			{
				Content = content ?? $"-- code item {name}",
				RelativePath = relativePath
			}
		};
	}

	private static CodeItemDto GetCodeRow(CodeItem codeItem, CodeItemStatus status, long contentHash) => new()
	{
		RelativePath = codeItem.FileData.RelativePath.ToLowerInvariant(),
		ItemType = CodeItemType.Code,
		ContentHash = contentHash,
		Status = status
	};
}