using EasyFlow.Deployers.Code;

namespace EasyFlow.Tests.Deployers.Code;

public class CodeDeployerTests
{
    [Fact]
    public void DeployCode_NoCodeGroups()
    {
        var mockSet = new MockSet();

        CodeDeployer deployer = new(mockSet.Logger, mockSet.EasyFlowProject, mockSet.CodeItemDeployer);

        deployer.DeployCode(true, DeployParameters.Default);
    }

    [Fact]
    public void DeployCode_SkipDeployment()
    {
        var mockSet = new MockSet();

        CodeDeployer deployer = new(mockSet.Logger, mockSet.EasyFlowProject, mockSet.CodeItemDeployer);

        deployer.DeployCode(true, new DeployParameters { DeployCode = false });
    }

    [Fact]
    public void DeployCode()
    {
        var mockSet = new MockSet();

        mockSet.EasyFlowProject.GetCodeGroups().Returns([
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

        mockSet.CodeItemDeployer.DeployCodeItem(Arg.Any<bool>(), Arg.Any<CodeItem>()).Returns(true);

        CodeDeployer deployer = new(mockSet.Logger, mockSet.EasyFlowProject, mockSet.CodeItemDeployer);

        deployer.DeployCode(true, DeployParameters.Default);

        mockSet.CodeItemDeployer.Received(5).DeployCodeItem(Arg.Any<bool>(), Arg.Any<CodeItem>());
    }


    [Fact]
    public void DeployCode_FailedCodeItems()
    {
        var mockSet = new MockSet();

        mockSet.EasyFlowProject.GetCodeGroups().Returns([
            new CodeGroup
            {
                Path = "Code2",
                CodeItems = [GetCodeItem("item11"), GetCodeItem("item22")]
            }
        ]);

        mockSet.CodeItemDeployer.DeployCodeItem(Arg.Any<bool>(), Arg.Any<CodeItem>()).Returns(false);

        CodeDeployer deployer = new(mockSet.Logger, mockSet.EasyFlowProject, mockSet.CodeItemDeployer);

        Assert.Throws<CodeDeploymentException>(
            () => deployer.DeployCode(true, DeployParameters.Default)
        );

        mockSet.CodeItemDeployer.Received(2).DeployCodeItem(Arg.Any<bool>(), Arg.Any<CodeItem>());
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