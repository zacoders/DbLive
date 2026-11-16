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
			NumberOfThreadsForCodeDeploy = 1
		});

		CodeItem item1 = GetCodeItem("item11");
		CodeItem item2 = GetCodeItem("item22");

		ProjectSnapshot project = ProjectSnapshots.Empty with
		{
			CodeGroups = [
				new CodeGroup
				{
					Path = "Code2",
					CodeItems = [item1, item2]
				}
			]
		};

		Exception error1 = new("Dummy1");
		Exception error2 = new("Dummy2");
		mockSet.CodeItemDeployer.DeployAsync(item1).Returns(CodeItemDeployResult.Failed(error1));
		mockSet.CodeItemDeployer.DeployAsync(item2).Returns(CodeItemDeployResult.Failed(error2));

		CodeDeployer deployer = mockSet.CreateUsingMocks<CodeDeployer>();

		CodeDeploymentAggregateException ex = await Assert.ThrowsAsync<CodeDeploymentAggregateException>(
			async () => await deployer.DeployAsync(project, DeployParameters.Default).ConfigureAwait(false)
		);

		// the first round deployed no file, so a second round would fail the same way: it does not run.
		await mockSet.CodeItemDeployer.Received(1).DeployAsync(item1);
		await mockSet.CodeItemDeployer.Received(1).DeployAsync(item2);
		Assert.Equal([error1, error2], ex.InnerExceptions);

		// rows of missing files are marked only by a code deployment that completed.
		await mockSet.DbLiveDA.DidNotReceiveWithAnyArgs().MarkMissingCodeAsRemovedAsync(default!, default);
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task DeployCode_DependencyRetry_UsesQueueOrder(bool inDeploymentTransaction)
	{
		MockSet mockSet = new();
		mockSet.SettingsAccessor.GetProjectSettingsAsync().Returns(new DbLiveSettings
		{
			// in the deployment transaction the files are deployed by one worker anyway.
			NumberOfThreadsForCodeDeploy = inDeploymentTransaction ? 10 : 1
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

		using (TransactionScope? scope = inDeploymentTransaction ? TransactionScopeManager.Create() : null)
		{
			await deployer.DeployAsync(project, DeployParameters.Default);
			scope?.Complete();
		}

		Assert.Equal([dependentItem.Name, mainItem.Name, dependentItem.Name], attempts);
	}

	[Fact]
	public async Task DeployCode_Retries_A_File_After_Its_Dependency_That_Runs_On_Another_Worker()
	{
		MockSet mockSet = new();
		mockSet.SettingsAccessor.GetProjectSettingsAsync().Returns(new DbLiveSettings
		{
			NumberOfThreadsForCodeDeploy = 2
		});

		CodeItem slowBase = GetCodeItem("a_base.sql");
		CodeItem dependent = GetCodeItem("b_dependent.sql");

		ProjectSnapshot project = ProjectSnapshots.Empty with
		{
			CodeGroups = [new CodeGroup { Path = "Code", CodeItems = [slowBase, dependent] }]
		};

		TaskCompletionSource dependentFailed = new(TaskCreationOptions.RunContinuationsAsynchronously);
		bool baseDeployed = false;

		// the base completes only after the dependent failed on the other worker, and a while later.
		mockSet.CodeItemDeployer.DeployAsync(slowBase).Returns(async _ =>
		{
			await dependentFailed.Task.WaitAsync(TimeSpan.FromSeconds(10)).ConfigureAwait(false);
			await Task.Delay(100).ConfigureAwait(false);
			Volatile.Write(ref baseDeployed, true);
			return CodeItemDeployResult.Success();
		});

		int dependentAttempts = 0;
		mockSet.CodeItemDeployer.DeployAsync(dependent).Returns(_ =>
		{
			Interlocked.Increment(ref dependentAttempts);

			if (Volatile.Read(ref baseDeployed))
			{
				return CodeItemDeployResult.Success();
			}

			dependentFailed.TrySetResult();
			return CodeItemDeployResult.Failed(new Exception("Invalid object name 'dbo.v_base'."));
		});

		CodeDeployer deployer = mockSet.CreateUsingMocks<CodeDeployer>();

		await deployer.DeployAsync(project, DeployParameters.Default);

		// not retried while the base was running: the second attempt waited for the round to end.
		Assert.Equal(2, dependentAttempts);
		await mockSet.CodeItemDeployer.Received(1).DeployAsync(slowBase);
	}

	[Fact]
	public async Task DeployCode_Deploys_A_Chain_Of_Dependencies_In_The_Reverse_Order()
	{
		MockSet mockSet = new();
		mockSet.SettingsAccessor.GetProjectSettingsAsync().Returns(new DbLiveSettings
		{
			NumberOfThreadsForCodeDeploy = 1
		});

		// view7 uses view6, ..., view2 uses view1: a round deploys one file.
		List<CodeItem> chain = [.. Enumerable.Range(1, 7).Reverse().Select(i => GetCodeItem($"view{i}"))];

		ProjectSnapshot project = ProjectSnapshots.Empty with
		{
			CodeGroups = [new CodeGroup { Path = "Code", CodeItems = chain }]
		};

		HashSet<string> deployed = [];
		mockSet.CodeItemDeployer.DeployAsync(Arg.Any<CodeItem>()).Returns(callInfo =>
		{
			CodeItem item = callInfo.Arg<CodeItem>();
			int number = int.Parse(item.Name["view".Length..]);

			if (number > 1 && !deployed.Contains($"view{number - 1}"))
			{
				return CodeItemDeployResult.Failed(new Exception($"Invalid object name 'view{number - 1}'."));
			}

			deployed.Add(item.Name);
			return CodeItemDeployResult.Success();
		});

		CodeDeployer deployer = mockSet.CreateUsingMocks<CodeDeployer>();

		await deployer.DeployAsync(project, DeployParameters.Default);

		Assert.Equal(7, deployed.Count);
		// view7 waits for six files, one per round: seven attempts, more than the five a file had before.
		await mockSet.CodeItemDeployer.Received(7).DeployAsync(chain[0]);
	}

	[Fact]
	public async Task DeployCode_Stops_When_A_Round_Deploys_No_File()
	{
		MockSet mockSet = new();
		mockSet.SettingsAccessor.GetProjectSettingsAsync().Returns(new DbLiveSettings
		{
			NumberOfThreadsForCodeDeploy = 1
		});

		CodeItem failingItem = GetCodeItem("always-failing");
		CodeItem otherItem = GetCodeItem("other");

		ProjectSnapshot project = ProjectSnapshots.Empty with
		{
			CodeGroups = [
				new CodeGroup
				{
					Path = "Code",
					CodeItems = [failingItem, otherItem]
				}
			]
		};

		Exception error = new("Dummy");
		mockSet.CodeItemDeployer.DeployAsync(failingItem).Returns(CodeItemDeployResult.Failed(error));
		mockSet.CodeItemDeployer.DeployAsync(otherItem).Returns(CodeItemDeployResult.Success());

		CodeDeployer deployer = mockSet.CreateUsingMocks<CodeDeployer>();

		CodeDeploymentAggregateException ex = await Assert.ThrowsAsync<CodeDeploymentAggregateException>(
			async () => await deployer.DeployAsync(project, DeployParameters.Default).ConfigureAwait(false)
		);

		// the first round deployed the other file, so the failed one got a second round, alone, and failed again.
		await mockSet.CodeItemDeployer.Received(2).DeployAsync(failingItem);
		await mockSet.CodeItemDeployer.Received(1).DeployAsync(otherItem);
		Assert.Same(error, Assert.Single(ex.InnerExceptions));
		mockSet.Logger.Received(1).Error(error, "Deployment failed for {FilePath} after {Attempts} attempts", "/some_path/always-failing", 2);
	}

	[Fact]
	public async Task DeployCode_Does_Not_Retry_An_Item_That_Cannot_Be_Retried()
	{
		MockSet mockSet = new();
		mockSet.SettingsAccessor.GetProjectSettingsAsync().Returns(new DbLiveSettings
		{
			NumberOfThreadsForCodeDeploy = 1
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

	[Fact]
	public async Task DeployCode_Fails_With_The_Error_Of_The_File_When_The_Transaction_Check_Throws()
	{
		// on SQL Server the check opens a connection, and it cannot enlist when the error broke the connection of the transaction.
		MockSet mockSet = new();
		mockSet.DbLiveDA.IsTransactionUsableAsync()
			.Returns(Task.FromException<bool>(new TransactionException("The operation is not valid for the state of the transaction.")));

		DbLiveSqlException scriptError = new("A severe error occurred on the current command.");
		mockSet.DbLiveDA
			.ExecuteCodeItemAsync(Arg.Any<string>(), Arg.Any<TranIsolationLevel>(), Arg.Any<TimeSpan?>())
			.Returns(Task.FromException(scriptError));

		ProjectSnapshot project = ProjectSnapshots.Empty with
		{
			CodeGroups = [new CodeGroup { Path = "Code", CodeItems = [GetCodeItem("GetUser.sql")] }]
		};

		// the deployer of a file is the real one, not the mock.
		CodeDeployer deployer = new(
			mockSet.Logger,
			mockSet.CreateUsingMocks<CodeItemDeployer>(),
			mockSet.SettingsAccessor,
			mockSet.DbLiveDA,
			mockSet.TimeProvider
		);

		CodeDeploymentAggregateException ex;
		using (TransactionScope _ = TransactionScopeManager.Create())
		{
			ex = await Assert.ThrowsAsync<CodeDeploymentAggregateException>(
				() => deployer.DeployAsync(project, DeployParameters.Default)
			);
		}

		CodeDeploymentException codeEx = Assert.IsType<CodeDeploymentException>(Assert.Single(ex.InnerExceptions));
		Assert.Equal("Deploy code file error. File path: /some_path/GetUser.sql. The error ended the deployment transaction, so the file is not retried.", codeEx.Message);
		Assert.Same(scriptError, codeEx.InnerException);
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