namespace DbLive.Tests.Deployers.Code;

public class CodeItemDeployerTests
{
	[Fact]
	public async Task Deploy_CodeItem_Success()
	{
		// Arrange
		MockSet mockSet = new();

		CodeItem codeItem = GetCodeItem();

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

	[Fact]
	public async Task DeployCodeItem_Keeps_The_Real_Casing_Of_The_Path()
	{
		// Arrange
		MockSet mockSet = new();

		// the database stores the path in lower case, the deployer passes and logs the path of the file.
		CodeItem codeItem = GetCodeItem(relativePath: "Code/dbo/GetUser.sql");

		CodeItemDeployer deployer = mockSet.CreateUsingMocks<CodeItemDeployer>();

		// Act
		CodeItemDeployResult res = await deployer.DeployAsync(codeItem);

		// Assert
		Assert.True(res.IsSuccess);
		await mockSet.DbLiveDA.Received().SaveCodeItemAsync(Arg.Is<CodeItemDto>(i => i.RelativePath == "Code/dbo/GetUser.sql"));
		mockSet.Logger.Received().Information("Deploying code file: {filePath}", "Code/dbo/GetUser.sql");
	}

	private static CodeItem GetCodeItem(string content = "select * from table", string relativePath = "/path-to/some-code-item.sql")
	{
		CodeItem codeItem = new()
		{
			Name = "some-code-item",
			FileData = new FileData
			{
				Content = content,
				RelativePath = relativePath
			}
		};

		return codeItem;
	}
}
