using EasyFlow.Adapter;
using EasyFlow.Deployers.Code;

namespace EasyFlow.Tests.Deployers.Code;

public class CodeItemDeployerTests
{
    record Arrange(
        CodeItemDeployer deployer,
        string cnnString,
        CodeItem codeItem,
        CodeItemDto? codeItemDto
    );

    private static Arrange CommonArrange(bool codeItemDtoExists)
    {
        MockSet mockSet = new();

        string cnnString = "some cnn string";
        string relativePath = "/path-to/some-code-item.sql";
        string content = "select * from table";
        int hashCode = content.Crc32HashCode();
        CodeItemDto codeItemDto = new()
        {
            ContentHash = hashCode,
            AppliedUtc = new DateTime(2023, 1, 1),
            ExecutionTimeMs = 5,
            RelativePath = relativePath
        };

        mockSet.EasyFlowDA.FindCodeItem(relativePath).Returns(codeItemDtoExists ? codeItemDto : null);
        mockSet.EasyFlowDA.ExecuteNonQuery(content);
        mockSet.EasyFlowDA.MarkCodeAsApplied(relativePath, hashCode, DateTime.UtcNow, 5);

		var deploy = mockSet.CreateUsingMocks<CodeItemDeployer>();

        CodeItem codeItem = new()
        {
            Name = "some-code-item",
            FileData = new FileData
            {
                Content = content,
                RelativePath = relativePath,
                FilePath = "c:/data" + relativePath
            }
        };

        return new Arrange(deploy, cnnString, codeItem, codeItemDto);
    }

    [Fact]
    public void DeployCodeItem_RedeployCodeItem_SelfDeploy_Success()
    {
        var arrange = CommonArrange(codeItemDtoExists: true);

        var res = arrange.deployer.DeployCodeItem(true, arrange.codeItem);

        Assert.True(res);
    }

    [Fact]
    public void DeployCodeItem_RedeployCodeItem_Success()
    {
        var arrange = CommonArrange(codeItemDtoExists: true);

        var res = arrange.deployer.DeployCodeItem(false, arrange.codeItem);

        Assert.True(res);
    }

    [Fact]
    public void DeployCodeItem_Success()
    {
        var arrange = CommonArrange(codeItemDtoExists: false);

        var res = arrange.deployer.DeployCodeItem(false, arrange.codeItem);

        Assert.True(res);
    }

    [Fact]
    public void DeployCodeItem_WrongHash()
    {
        var arrange = CommonArrange(codeItemDtoExists: true);

        arrange.codeItemDto!.ContentHash = 99999999; // wrong hash.

        var res = arrange.deployer.DeployCodeItem(false, arrange.codeItem);

        Assert.False(res);
    }

    [Fact]
    public void DeployCodeItem_RetryTest()
    {
        MockSet mockSet = new();

        bool isThrown = false;
        mockSet.EasyFlowDA
            .When(x => x.ExecuteNonQuery(Arg.Any<string>()))
            .Do(x =>
            {
                if (isThrown == false)
                {
                    isThrown = true;
                    throw new Exception();
                }
            });

        var deploy = mockSet.CreateUsingMocks<CodeItemDeployer>();

		CodeItem codeItem = new()
        {
            Name = "some-code-item",
            FileData = new FileData
            {
                Content = "--some content",
                RelativePath = "item.sql",
                FilePath = "c:/data/item.sql"
            }
        };

        var res = deploy.DeployCodeItem(false, codeItem);

        Assert.True(res, "Should be deployed from the second retry attempt.");

        mockSet.EasyFlowDA.Received(2).ExecuteNonQuery(Arg.Any<string>());
    }
}