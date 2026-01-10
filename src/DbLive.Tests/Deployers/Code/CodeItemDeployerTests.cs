namespace DbLive.Tests.Deployers.Code;

public class CodeItemDeployerTests
{
	[Fact]
	public async Task DeployCodeItem_Redeploy_CodeItem_Success()
	{
		// Arrange
		MockSet mockSet = new();

		CodeItem codeItem = GetCodeItem();

		CodeItemDto codeItemDto = new()
		{
			ContentHash = codeItem.FileData.ContentHash, // same hash
			AppliedUtc = new DateTime(2023, 1, 1),
			ExecutionTimeMs = 5,
			RelativePath = codeItem.FileData.RelativePath,
			Status = CodeItemStatus.Applied,
			CreatedUtc = new DateTime(2023, 1, 1, 1, 1, 1),
			ErrorMessage = null,
			VerifiedUtc = new DateTime(2023, 1, 2)
		};

		mockSet.DbLiveDA.FindCodeItemAsync(codeItem.FileData.RelativePath).Returns(codeItemDto);

		CodeItemDeployer deployer = mockSet.CreateUsingMocks<CodeItemDeployer>();

		// Act
		CodeItemDeployResult res = await deployer.DeployAsync(codeItem);

		// Assert
		Assert.True(res.IsSuccess);

		await mockSet.DbLiveDA.DidNotReceiveWithAnyArgs().ExecuteNonQueryAsync(Arg.Any<string>(), Arg.Any<TranIsolationLevel>(), Arg.Any<TimeSpan>());
		await mockSet.DbLiveDA.DidNotReceiveWithAnyArgs().SaveCodeItemAsync(Arg.Any<CodeItemDto>());
	}

	[Fact]
	public async Task DeployCodeItem_DifferentHash()
	{
		// Arrange
		MockSet mockSet = new();

		CodeItem codeItem = GetCodeItem();

		int differentHash = codeItem.FileData.ContentHash + 123;

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

		mockSet.DbLiveDA.FindCodeItemAsync(codeItem.FileData.RelativePath).Returns(codeItemDto);

		CodeItemDeployer deployer = mockSet.CreateUsingMocks<CodeItemDeployer>();

		// Act
		CodeItemDeployResult res = await deployer.DeployAsync(codeItem);

		// Assert
		Assert.True(res.IsSuccess);
		await mockSet.DbLiveDA.Received().ExecuteNonQueryAsync(Arg.Any<string>(), Arg.Any<TranIsolationLevel>(), Arg.Any<TimeSpan>());
		await mockSet.DbLiveDA.Received().SaveCodeItemAsync(Arg.Is<CodeItemDto>(i => i.Status == CodeItemStatus.Applied));
	}


	[Fact]
	public async Task Deploy_New_CodeItem_Success()
	{
		// Arrange
		MockSet mockSet = new();

		CodeItem codeItem = GetCodeItem();

		mockSet.DbLiveDA.FindCodeItemAsync(codeItem.FileData.RelativePath).ReturnsNull();

		CodeItemDeployer deployer = mockSet.CreateUsingMocks<CodeItemDeployer>();

		// Act
		CodeItemDeployResult res = await deployer.DeployAsync(codeItem);

		// Assert
		Assert.True(res.IsSuccess);
		await mockSet.DbLiveDA.Received().ExecuteNonQueryAsync(Arg.Any<string>(), Arg.Any<TranIsolationLevel>(), Arg.Any<TimeSpan>());
		await mockSet.DbLiveDA.Received().SaveCodeItemAsync(Arg.Is<CodeItemDto>(i => i.Status == CodeItemStatus.Applied));
	}

	[Fact]
	public async Task DeployCodeItem_Execute_Throws_Exception()
	{
		// Arrange
		MockSet mockSet = new();

		CodeItem codeItem = GetCodeItem();

		mockSet.DbLiveDA.FindCodeItemAsync(codeItem.FileData.RelativePath).ReturnsNull();

		CodeItemDeployer deployer = mockSet.CreateUsingMocks<CodeItemDeployer>();

		mockSet.DbLiveDA
			.When(x => x.ExecuteNonQueryAsync(Arg.Any<string>(), Arg.Any<TranIsolationLevel>(), Arg.Any<TimeSpan>()))
			.Do(x => throw new Exception("some exception"));

		// Act
		CodeItemDeployResult res = await deployer.DeployAsync(codeItem);

		// Assert
		Assert.False(res.IsSuccess);
		await mockSet.DbLiveDA.Received().ExecuteNonQueryAsync(Arg.Any<string>(), Arg.Any<TranIsolationLevel>(), Arg.Any<TimeSpan>());
		await mockSet.DbLiveDA.Received().SaveCodeItemAsync(Arg.Is<CodeItemDto>(i => i.Status == CodeItemStatus.Error));
	}

	private static CodeItem GetCodeItem()
	{
		string relativePath = "/path-to/some-code-item.sql";
		string content = "select * from table";

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