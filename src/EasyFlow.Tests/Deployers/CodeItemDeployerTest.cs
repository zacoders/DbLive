using EasyFlow.Adapter;
using EasyFlow.Deployers.Code;

namespace EasyFlow.Tests.Deployers;

public class CodeItemDeployerTest
{
	record Arrange(
		CodeItemDeployer deployer,
		string cnnString,
		CodeItem codeItem,
		CodeItemDto? codeItemDto
	);

	private static Arrange CommonArrange(bool codeItemDtoExists)
	{
		var mockSet = new MockSet();

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

		CodeItemDeployer deploy = new(mockSet.Logger, mockSet.EasyFlowDA, mockSet.TimeProvider);
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
}