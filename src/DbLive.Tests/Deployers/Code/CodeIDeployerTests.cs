
namespace DbLive.Tests.Deployers.Code;

public class CodeDeployerTests
{
	[Fact]
	public void DeployCode_NoCodeGroups()
	{
		MockSet mockSet = new();

		var deployer = mockSet.CreateUsingMocks<CodeDeployer>();

		deployer.Deploy(DeployParameters.Default);
	}

	[Fact]
	public void DeployCode_SkipDeployment()
	{
		MockSet mockSet = new();

		var deployer = mockSet.CreateUsingMocks<CodeDeployer>();

		deployer.Deploy(new DeployParameters { DeployCode = false });
	}

	[Fact]
	public void DeployCode()
	{
		MockSet mockSet = new();

		mockSet.DbLiveProject.GetCodeGroups().Returns([
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

		mockSet.CodeItemDeployer.Deploy(Arg.Any<CodeItem>()).Returns(CodeItemDeployResult.Success());

		var deployer = mockSet.CreateUsingMocks<CodeDeployer>();

		deployer.Deploy(DeployParameters.Default);

		mockSet.CodeItemDeployer.Received(5).Deploy(Arg.Any<CodeItem>());
	}


	[Fact]
	public void DeployCode_FailedCodeItems()
	{
		MockSet mockSet = new();

		mockSet.DbLiveProject.GetCodeGroups().Returns([
			new CodeGroup
			{
				Path = "Code2",
				CodeItems = [GetCodeItem("item11"), GetCodeItem("item22")]
			}
		]);

		mockSet.CodeItemDeployer.Deploy(Arg.Any<CodeItem>())
			.Returns(CodeItemDeployResult.Failed(new Exception("Dummy1")));

		var deployer = mockSet.CreateUsingMocks<CodeDeployer>();

		Assert.Throws<CodeDeploymentAggregateException>(
			() => deployer.Deploy(DeployParameters.Default)
		);

		mockSet.CodeItemDeployer.Received().Deploy(Arg.Any<CodeItem>());
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