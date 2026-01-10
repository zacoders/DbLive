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
			async () => await deployer.DeployAsync(DeployParameters.Default)
		);

		await mockSet.CodeItemDeployer.Received().DeployAsync(Arg.Any<CodeItem>());
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