
namespace DbLive.Tests.Deployers.Code;

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
			RelativePath = relativePath,
			Status = CodeItemStatus.Applied,
			CreatedUtc = new DateTime(2023, 1, 1, 1,1,1),
			ErrorMessage = null,
			VerifiedUtc = new DateTime(2023, 1, 2)
		};

		mockSet.DbLiveDA.FindCodeItem(relativePath).Returns(codeItemDtoExists ? codeItemDto : null);
		mockSet.DbLiveDA.ExecuteNonQuery(content, Arg.Any<TranIsolationLevel>(), Arg.Any<TimeSpan>());
		mockSet.DbLiveDA.SaveCodeItem(Arg.Any<CodeItemDto>());

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

		Assert.True(res.IsSuccess);
	}

	[Fact]
	public void DeployCodeItem_RedeployCodeItem_Success()
	{
		var arrange = CommonArrange(codeItemDtoExists: true);

		var res = arrange.deployer.DeployCodeItem(false, arrange.codeItem);

		Assert.True(res.IsSuccess);
	}

	[Fact]
	public void DeployCodeItem_Success()
	{
		var arrange = CommonArrange(codeItemDtoExists: false);

		var res = arrange.deployer.DeployCodeItem(false, arrange.codeItem);

		Assert.True(res.IsSuccess);
	}

	[Fact]
	public void DeployCodeItem_DifferentHash()
	{
		var arrange = CommonArrange(codeItemDtoExists: true);

		arrange.codeItemDto!.ContentHash = 99999999;

		var res = arrange.deployer.DeployCodeItem(false, arrange.codeItem);

		Assert.True(res.IsSuccess);
	}
}