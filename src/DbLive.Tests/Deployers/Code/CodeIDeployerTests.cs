namespace DbLive.Tests.Deployers.Code;

public class CodeDeployerTests
{
	[Fact]
	public async Task DeployCode_NoCodeGroups()
	{
		MockSet mockSet = new();

		CodeDeployer deployer = mockSet.CreateUsingMocks<CodeDeployer>();

		await deployer.DeployAsync(DeployParameters.Default);
	}

	[Fact]
	public async Task DeployCode_SkipDeployment()
	{
		MockSet mockSet = new();

		CodeDeployer deployer = mockSet.CreateUsingMocks<CodeDeployer>();

		await deployer.DeployAsync(new DeployParameters { DeployCode = false });
	}

	[Fact]
	public async Task DeployCode()
	{
		MockSet mockSet = new();

		mockSet.DbLiveProject.GetCodeGroupsAsync().Returns([
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
		]);

		mockSet.CodeItemDeployer.DeployAsync(Arg.Any<CodeItem>()).Returns(CodeItemDeployResult.Success());

		CodeDeployer deployer = mockSet.CreateUsingMocks<CodeDeployer>();

		await deployer.DeployAsync(DeployParameters.Default);

		await mockSet.CodeItemDeployer.Received(5).DeployAsync(Arg.Any<CodeItem>());
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

		mockSet.DbLiveProject.GetCodeGroupsAsync().Returns([
			new CodeGroup
			{
				Path = "Code2",
				CodeItems = [GetCodeItem("item11"), GetCodeItem("item22")]
			}
		]);

		mockSet.CodeItemDeployer.DeployAsync(Arg.Any<CodeItem>())
			.Returns(CodeItemDeployResult.Failed(new Exception("Dummy1")));

		CodeDeployer deployer = mockSet.CreateUsingMocks<CodeDeployer>();

		await Assert.ThrowsAsync<CodeDeploymentAggregateException>(
			async () => await deployer.DeployAsync(DeployParameters.Default).ConfigureAwait(false)
		);

		await mockSet.CodeItemDeployer.Received().DeployAsync(Arg.Any<CodeItem>());
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

		mockSet.DbLiveProject.GetCodeGroupsAsync().Returns([
			new CodeGroup
			{
				Path = "Code",
				CodeItems = [dependentItem, mainItem]
			}
		]);

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

		await deployer.DeployAsync(DeployParameters.Default);

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

		mockSet.DbLiveProject.GetCodeGroupsAsync().Returns([
			new CodeGroup
			{
				Path = "Code",
				CodeItems = [failingItem]
			}
		]);

		mockSet.CodeItemDeployer
			.DeployAsync(Arg.Is<CodeItem>(i => i.Name == failingItem.Name))
			.Returns(CodeItemDeployResult.Failed(new Exception("Dummy")));

		CodeDeployer deployer = mockSet.CreateUsingMocks<CodeDeployer>();

		await Assert.ThrowsAsync<CodeDeploymentAggregateException>(
			async () => await deployer.DeployAsync(DeployParameters.Default).ConfigureAwait(false)
		);

		await mockSet.CodeItemDeployer.Received(3)
			.DeployAsync(Arg.Is<CodeItem>(i => i.Name == failingItem.Name));
	}

	private static CodeItem GetCodeItem(string name)
	{
		string relativePath = $"/some_path/{name}";
		return new CodeItem
		{
			Name = name,
			FileData = new FileData
			{
				Content = $"-- code item {name}",
				RelativePath = relativePath,
				FilePath = "c:/data" + relativePath
			}
		};
	}
}