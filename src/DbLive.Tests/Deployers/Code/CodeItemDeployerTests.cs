
namespace DbLive.Tests.Deployers.Code;

public class CodeItemDeployerTests
{
	[Fact]
	public void DeployCodeItem_Redeploy_CodeItem_SelfDeploy_Success()
	{
		// Arrange
		MockSet mockSet = new();

		CodeItem codeItem = GetCodeItem();

		CodeItemDto codeItemDto = new()
		{
			ContentHash = codeItem.FileData.Crc32Hash, // same hash
			AppliedUtc = new DateTime(2023, 1, 1),
			ExecutionTimeMs = 5,
			RelativePath = codeItem.FileData.RelativePath,
			Status = CodeItemStatus.Applied,
			CreatedUtc = new DateTime(2023, 1, 1, 1, 1, 1),
			ErrorMessage = null,
			VerifiedUtc = new DateTime(2023, 1, 2)
		};

		mockSet.DbLiveDA.FindCodeItem(codeItem.FileData.RelativePath).Returns(codeItemDto);

		var deployer = mockSet.CreateUsingMocks<CodeItemDeployer>();

		// Act
		var res = deployer.DeployCodeItem(true, codeItem);

		// Assert
		Assert.True(res.IsSuccess);

		mockSet.DbLiveDA.Received().ExecuteNonQuery(Arg.Any<string>(), Arg.Any<TranIsolationLevel>(), Arg.Any<TimeSpan>());
		mockSet.DbLiveDA.DidNotReceiveWithAnyArgs().SaveCodeItem(Arg.Any<CodeItemDto>());
	}

	[Fact]
	public void DeployCodeItem_Redeploy_CodeItem_Success()
	{
		// Arrange
		MockSet mockSet = new();

		CodeItem codeItem = GetCodeItem();

		CodeItemDto codeItemDto = new()
		{
			ContentHash = codeItem.FileData.Crc32Hash, // same hash
			AppliedUtc = new DateTime(2023, 1, 1),
			ExecutionTimeMs = 5,
			RelativePath = codeItem.FileData.RelativePath,
			Status = CodeItemStatus.Applied,
			CreatedUtc = new DateTime(2023, 1, 1, 1, 1, 1),
			ErrorMessage = null,
			VerifiedUtc = new DateTime(2023, 1, 2)
		};

		mockSet.DbLiveDA.FindCodeItem(codeItem.FileData.RelativePath).Returns(codeItemDto);

		var deployer = mockSet.CreateUsingMocks<CodeItemDeployer>();

		// Act
		var res = deployer.DeployCodeItem(false, codeItem);

		// Assert
		Assert.True(res.IsSuccess);

		mockSet.DbLiveDA.DidNotReceiveWithAnyArgs().ExecuteNonQuery(Arg.Any<string>(), Arg.Any<TranIsolationLevel>(), Arg.Any<TimeSpan>());
		mockSet.DbLiveDA.DidNotReceiveWithAnyArgs().SaveCodeItem(Arg.Any<CodeItemDto>());
	}

	[Fact]
	public void DeployCodeItem_DifferentHash()
	{
		// Arrange
		MockSet mockSet = new();

		CodeItem codeItem = GetCodeItem();

		int differentHash = codeItem.FileData.Crc32Hash + 123;

		CodeItemDto codeItemDto = new()
		{
			ContentHash = differentHash,
			AppliedUtc = new DateTime(2023, 1, 1),
			ExecutionTimeMs = 5,
			RelativePath = codeItem.FileData.RelativePath,
			Status = CodeItemStatus.Applied,
			CreatedUtc = new DateTime(2023, 1, 1, 1, 1, 1),
			ErrorMessage = null,
			VerifiedUtc = new DateTime(2023, 1, 2)
		};

		mockSet.DbLiveDA.FindCodeItem(codeItem.FileData.RelativePath).Returns(codeItemDto);
		
		var deployer = mockSet.CreateUsingMocks<CodeItemDeployer>();

		// Act
		var res = deployer.DeployCodeItem(false, codeItem);

		// Assert
		Assert.True(res.IsSuccess);
		mockSet.DbLiveDA.Received().ExecuteNonQuery(Arg.Any<string>(), Arg.Any<TranIsolationLevel>(), Arg.Any<TimeSpan>());
		mockSet.DbLiveDA.Received().SaveCodeItem(Arg.Is<CodeItemDto>(i => i.Status == CodeItemStatus.Applied));
	}


	[Fact]
	public void Deploy_New_CodeItem_Success()
	{
		// Arrange
		MockSet mockSet = new();

		CodeItem codeItem = GetCodeItem();

		mockSet.DbLiveDA.FindCodeItem(codeItem.FileData.RelativePath).ReturnsNull();

		var deployer = mockSet.CreateUsingMocks<CodeItemDeployer>();

		// Act
		var res = deployer.DeployCodeItem(false, codeItem);

		// Assert
		Assert.True(res.IsSuccess);
		mockSet.DbLiveDA.Received().ExecuteNonQuery(Arg.Any<string>(), Arg.Any<TranIsolationLevel>(), Arg.Any<TimeSpan>());
		mockSet.DbLiveDA.Received().SaveCodeItem(Arg.Is<CodeItemDto>(i => i.Status == CodeItemStatus.Applied));
	}

	[Fact]
	public void DeployCodeItem_Execute_Throws_Exception()
	{
		// Arrange
		MockSet mockSet = new();

		CodeItem codeItem = GetCodeItem();

		mockSet.DbLiveDA.FindCodeItem(codeItem.FileData.RelativePath).ReturnsNull();
		
		var deployer = mockSet.CreateUsingMocks<CodeItemDeployer>();

		mockSet.DbLiveDA
			.When(x => x.ExecuteNonQuery(Arg.Any<string>(), Arg.Any<TranIsolationLevel>(), Arg.Any<TimeSpan>()))
			.Do(x => throw new Exception("some exception"));

		// Act
		var res = deployer.DeployCodeItem(false, codeItem);

		// Assert
		Assert.False(res.IsSuccess);
		mockSet.DbLiveDA.Received().ExecuteNonQuery(Arg.Any<string>(), Arg.Any<TranIsolationLevel>(), Arg.Any<TimeSpan>());
		mockSet.DbLiveDA.Received().SaveCodeItem(Arg.Is<CodeItemDto>(i => i.Status == CodeItemStatus.Error));
	}

	private static CodeItem GetCodeItem()
	{
		string relativePath = "/path-to/some-code-item.sql";
		string content = "select * from table";
		int hashCode = content.Crc32HashCode();

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

		return codeItem;
	}
}