namespace EasyFlow.Tests;

public class CodeDeployerTest
{
	record Arrange(
		CodeDeployer deploy,
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
		int hashCode = Hasher.Crc32HashCode(content);
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

		CodeDeployer deploy = new(mockSet.Logger, mockSet.EasyFlowProject, mockSet.EasyFlowDA, mockSet.TimeProvider);
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

		var res = arrange.deploy.DeployCodeItem(true, arrange.codeItem);

		Assert.True(res);
	}

	[Fact]
	public void DeployCodeItem_RedeployCodeItem_Success()
	{
		var arrange = CommonArrange(codeItemDtoExists: true);

		var res = arrange.deploy.DeployCodeItem(false, arrange.codeItem);

		Assert.True(res);
	}

	[Fact]
	public void DeployCodeItem_Success()
	{
		var arrange = CommonArrange(codeItemDtoExists: false);

		var res = arrange.deploy.DeployCodeItem(false, arrange.codeItem);

		Assert.True(res);
	}

	[Fact]
	public void DeployCodeItem_WrongHash()
	{
		var arrange = CommonArrange(codeItemDtoExists: true);

		arrange.codeItemDto!.ContentHash = 99999999; // wrong hash.

		var res = arrange.deploy.DeployCodeItem(false, arrange.codeItem);

		Assert.False(res);
	}
}